using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PackageService.DTOs;
using PackageService.Models;
using PackageService.Services;

namespace PackageService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PackagesController : ControllerBase
{
    private readonly IPackageService _packageService;
    private readonly ILogger<PackagesController> _logger;
    
    public PackagesController(
        IPackageService packageService,
        ILogger<PackagesController> logger)
    {
        _packageService = packageService;
        _logger = logger;
    }
    
    /// <summary>
    /// Get all packages (paginated)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<PackageDto>>> GetPackages(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        _logger.LogInformation(
            "GetPackages called by user {UserId} with role {Role}",
            userId,
            role
        );
        
        if (pageSize > 50) pageSize = 50;
        
        var packages = await _packageService.GetAllAsync(pageNumber, pageSize);
        
        return Ok(packages);
    }
    
    /// <summary>
    /// Get package by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PackageDto>> GetPackage(int id)
    {
        var package = await _packageService.GetByIdAsync(id);
        
        if (package == null)
        {
            return NotFound(new { message = $"Package with ID {id} not found" });
        }
        
        return Ok(package);
    }
    
    /// <summary>
    /// Track package by tracking number (public access)
    /// </summary>
    [AllowAnonymous]
    [HttpGet("tracking/{trackingNumber}")]
    public async Task<ActionResult<PackageDto>> TrackPackage(string trackingNumber)
    {
        _logger.LogInformation(
            "Package tracking request: {TrackingNumber}",
            trackingNumber
        );
        
        var package = await _packageService.GetByTrackingNumberAsync(trackingNumber);
        
        if (package == null)
        {
            return NotFound(new 
            { 
                message = $"Package with tracking number {trackingNumber} not found" 
            });
        }
        
        return Ok(package);
    }
    
    /// <summary>
    /// Get packages by customer ID (paginated)
    /// </summary>
    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<PaginatedResult<PackageDto>>> GetPackagesByCustomer(
        int customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (pageSize > 50) pageSize = 50;
        
        var packages = await _packageService.GetByCustomerAsync(
            customerId,
            pageNumber,
            pageSize
        );
        
        return Ok(packages);
    }
    
    /// <summary>
    /// Create new package
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    public async Task<ActionResult<PackageDto>> CreatePackage([FromBody] CreatePackageDto dto)
    {
        _logger.LogInformation("Create package request received");
        
        var package = await _packageService.CreateAsync(dto);
        
        return CreatedAtAction(
            nameof(GetPackage),
            new { id = package.Id },
            package
        );
    }
    
    /// <summary>
    /// Update package details
    /// </summary>
    [Authorize(Roles = "Admin,Manager")]
    [HttpPut("{id}")]
    public async Task<ActionResult<PackageDto>> UpdatePackage(
        int id,
        [FromBody] UpdatePackageDto dto)
    {
        _logger.LogInformation("Update package request for PackageId: {PackageId}", id);
        
        var package = await _packageService.UpdateAsync(id, dto);
        
        return Ok(package);
    }
    
    /// <summary>
    /// Delete package (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePackage(int id)
    {
        _logger.LogInformation("Delete package request for PackageId: {PackageId}", id);
        
        var result = await _packageService.DeleteAsync(id);
        
        if (!result)
        {
            return NotFound(new { message = $"Package with ID {id} not found" });
        }
        
        return NoContent();
    }
    
    /// <summary>
    /// Update package status
    /// Drivers can only update packages on their assigned routes
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateStatusDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        
        _logger.LogInformation(
            "Status update request for package {PackageId} by user {UserId} to {Status}",
            id,
            userId,
            dto.Status
        );
        
        int? driverId = null;
        if (role == "Driver")
        {
            var driverIdClaim = User.FindFirst("DriverId")?.Value;
            if (driverIdClaim == null)
            {
                return BadRequest(new { message = "Driver ID not found in token" });
            }
            driverId = int.Parse(driverIdClaim);
        }
        
        await _packageService.UpdateStatusAsync(id, dto.Status, driverId, role);
        
        return NoContent();
    }
    
    /// <summary>
    /// Search packages with filters and pagination
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<PaginatedResult<PackageDto>>> SearchPackages(
        [FromQuery] string? trackingNumber,
        [FromQuery] PackageStatus? status,
        [FromQuery] int? customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation(
            "Search packages request: TrackingNumber={TrackingNumber}, Status={Status}",
            trackingNumber,
            status
        );
        
        if (pageSize > 50) pageSize = 50;
        
        var packages = await _packageService.SearchAsync(
            trackingNumber,
            status,
            customerId,
            pageNumber,
            pageSize
        );
        
        return Ok(packages);
    }
}
