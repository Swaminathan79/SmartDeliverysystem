using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RouteService.DTOs;
using RouteService.Services;
using Serilog.Core;
using System.Security.Claims;

namespace RouteService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RoutesController> _logger;
    
    public RoutesController(IRouteService routeService, ILogger<RoutesController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }
    
    /// <summary>
    /// Get all routes (Admin/Manager see all, Driver sees own routes)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<RouteDto>>> GetRoutes(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var userId = int.Parse(userIdClaim ?? "0");
        if (userId == 0)
        {
            _logger.LogInformation("userIdClaim missing → exception");
            return Unauthorized();
        }


        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        _logger.LogInformation("GetRoutes called by user {UserId} with role {Role}", userId, role);
        
        if (pageSize > 50) pageSize = 50;
        
        PaginatedResult<RouteDto> routes;
        
        if (role == "Driver")
        {
            var driverIdClaim = User.FindFirst("DriverId")?.Value;
            if (driverIdClaim == null)
            {
                return BadRequest(new { message = "Driver ID not found in token" });
            }
            
            var driverId = int.Parse(driverIdClaim);
            _logger.LogDebug("Fetching routes for driver {DriverId}", driverId);
            routes = await _routeService.GetByDriverAsync(driverId, pageNumber, pageSize);
        }
        else
        {
            _logger.LogDebug("Fetching all routes");
            routes = await _routeService.GetAllAsync(pageNumber, pageSize);
        }
        
        return Ok(routes);
    }

    //GET returns only routes assigned to authenticated driver
    [Authorize]
    /// <summary>
    /// Get route by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RouteDto>> GetRoute(int id)
    {
        var route = await _routeService.GetByIdAsync(id);
        
        if (route == null)
        {
            return NotFound(new { message = $"Route with ID {id} not found" });
        }
        
        // Check authorization for drivers
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Driver")
        {
            var driverIdClaim = User.FindFirst("DriverId")?.Value;
            if (driverIdClaim != null && route.DriverId != int.Parse(driverIdClaim))
            {
                _logger.LogWarning(
                    "Driver {DriverId} attempted to access route {RouteId} belonging to driver {RouteDriverId}",
                    driverIdClaim,
                    id,
                    route.DriverId
                );
                return StatusCode(403, new { message = "You can only view your own routes" });
            }
        }
        
        return Ok(route);
    }
    
    /// <summary>
    /// Create new route (Admin/Manager only)
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<ActionResult<RouteDto>> CreateRoute([FromBody] CreateRouteDto dto)
    {
        _logger.LogInformation("Create route request received");

        var driverExists = dto.DriverId;
        if (driverExists == 0)
            throw new ValidationException("Driver not found");

        var route = await _routeService.CreateAsync(dto);
        
        return CreatedAtAction(nameof(GetRoute), new { id = route.Id }, route);
    }
    
    /// <summary>
    /// Update route (Admin/Manager only)
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id}")]
    public async Task<ActionResult<RouteDto>> UpdateRoute(int id, [FromBody] UpdateRouteDto dto)
    {
        _logger.LogInformation("Update route request for RouteId: {RouteId}", id);
        
        var route = await _routeService.UpdateAsync(id, dto);
        
        return Ok(route);
    }
    
    /// <summary>
    /// Delete route (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoute(int id)
    {
        _logger.LogInformation("Delete route request for RouteId: {RouteId}", id);
        
        var result = await _routeService.DeleteAsync(id);
        
        if (!result)
        {
            return NotFound(new { message = $"Route with ID {id} not found" });
        }
        
        return NoContent();
    }
    
    /// <summary>
    /// Assign driver to route (Admin/Manager only)
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("{id}/assign-driver")]
    public async Task<ActionResult<RouteDto>> AssignDriver(int id, [FromBody] AssignDriverDto dto)
    {
        _logger.LogInformation(
            "Assign driver request: RouteId {RouteId}, DriverId {DriverId}",
            id,
            dto.DriverId
        );
        
        var route = await _routeService.AssignDriverAsync(id, dto.DriverId);
        
        return Ok(route);
    }
    
    /// <summary>
    /// Search routes with filters and pagination
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PaginatedResult<RouteDto>>> SearchRoutes(
        [FromQuery] DateTime? date,
        [FromQuery] string? driverName,
        [FromQuery] int? driverId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation("Search routes request");
        
        // Driver can only search their own routes
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Driver") //Roles.Driver
        {
            var driverIdClaim = User.FindFirst("DriverId")?.Value;
            if (driverIdClaim == null)
            {
                return BadRequest(new { message = "Driver ID not found in token" });
            }

            if (driverIdClaim != null)
            {
                driverId = int.Parse(driverIdClaim);
                _logger.LogDebug("Fetching routes for driver {DriverId}", driverId);

            }
        }
        
        if (pageSize > 50) pageSize = 50;
        
        var routes = await _routeService.SearchAsync(date, driverName, driverId, pageNumber, pageSize);
        
        return Ok(routes);
    }
}
