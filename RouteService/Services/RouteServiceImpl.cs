using RouteService.DTOs;
using RouteService.Models;
using RouteService.Repositories;
using Route = Microsoft.AspNetCore.Routing.Route;

namespace RouteService.Services;

public interface IRouteService
{
    Task<RouteDto?> GetByIdAsync(int id);
    Task<PaginatedResult<RouteDto>> GetAllAsync(int pageNumber, int pageSize);
    Task<PaginatedResult<RouteDto>> GetByDriverAsync(int driverId, int pageNumber, int pageSize);
    Task<RouteDto> CreateAsync(CreateRouteDto dto);
    Task<RouteDto> UpdateAsync(int id, UpdateRouteDto dto);
    Task<bool> DeleteAsync(int id);
    Task<RouteDto> AssignDriverAsync(int routeId, int driverId);
    Task<PaginatedResult<RouteDto>> SearchAsync(
        DateTime? date,
        string? driverName,
        int? driverId,
        int pageNumber,
        int pageSize);
}

public class RouteServiceImpl : IRouteService
{
    private readonly IRouteRepository _repository;
    private readonly ILogger<RouteServiceImpl> _logger;
    
    public RouteServiceImpl(IRouteRepository repository, ILogger<RouteServiceImpl> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task<RouteDto?> GetByIdAsync(int id)
    {
        var route = await _repository.GetByIdAsync(id);
        
        if (route == null)
        {
            _logger.LogInformation("Route not found: RouteId {RouteId}", id);
            return null;
        }
        
        return MapToDto(route);
    }
    
    public async Task<PaginatedResult<RouteDto>> GetAllAsync(int pageNumber, int pageSize)
    {
        var (routes, totalCount) = await _repository.SearchAsync(null, null, null, pageNumber, pageSize);
        
        return new PaginatedResult<RouteDto>
        {
            Data = routes.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    public async Task<PaginatedResult<RouteDto>> GetByDriverAsync(int driverId, int pageNumber, int pageSize)
    {
        _logger.LogDebug("Fetching routes for driver {DriverId}", driverId);
        
        var (routes, totalCount) = await _repository.SearchAsync(null, null, driverId, pageNumber, pageSize);
        
        return new PaginatedResult<RouteDto>
        {
            Data = routes.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    public async Task<RouteDto> CreateAsync(CreateRouteDto dto)
    {
        _logger.LogDebug(
            "CreateRouteAsync called with DriverId: {DriverId}, ScheduledDate: {ScheduledDate}",
            dto.DriverId,
            dto.ScheduledDate
        );
        
        // Validate scheduled date is not in the past
        if (dto.ScheduledDate.Date < DateTime.UtcNow.Date)
        {
            throw new ValidationException("Scheduled date cannot be in the past");
        }
        
        // Check for overlapping routes
        var hasOverlap = await _repository.HasOverlappingRoutesAsync(dto.DriverId, dto.ScheduledDate);
        
        if (hasOverlap)
        {
            _logger.LogWarning(
                "Route creation blocked: Driver {DriverId} has overlapping route on {Date}",
                dto.DriverId,
                dto.ScheduledDate.Date
            );
            throw new ValidationException("Driver already has a route scheduled for this date");
        }
        
        var route = new RouteService.Models.Route
        {
            DriverId = dto.DriverId,
            VehicleId = dto.VehicleId,
            StartLocation = dto.StartLocation,
            EndLocation = dto.EndLocation,
            EstimatedDistanceKm = dto.EstimatedDistanceKm,
            ScheduledDate = dto.ScheduledDate
        };
        
        var created = await _repository.AddAsync(route);
        
        _logger.LogInformation(
            "Route created: RouteId {RouteId}, DriverId {DriverId}, Distance {Distance}km",
            created.Id,
            created.DriverId,
            created.EstimatedDistanceKm
        );
        
        return MapToDto(created);
    }
    
    public async Task<RouteDto> UpdateAsync(int id, UpdateRouteDto dto)
    {
        var route = await _repository.GetByIdAsync(id);
        
        if (route == null)
        {
            _logger.LogWarning("Update failed: Route {RouteId} not found", id);
            throw new NotFoundException($"Route with ID {id} not found");
        }
        
        if (dto.VehicleId.HasValue)
            route.VehicleId = dto.VehicleId.Value;
        
        if (!string.IsNullOrEmpty(dto.StartLocation))
            route.StartLocation = dto.StartLocation;
        
        if (!string.IsNullOrEmpty(dto.EndLocation))
            route.EndLocation = dto.EndLocation;
        
        if (dto.EstimatedDistanceKm.HasValue)
            route.EstimatedDistanceKm = dto.EstimatedDistanceKm.Value;
        
        if (dto.ScheduledDate.HasValue)
        {
            // Check for overlapping routes with new date
            var hasOverlap = await _repository.HasOverlappingRoutesAsync(
                route.DriverId,
                dto.ScheduledDate.Value,
                route.Id
            );
            
            if (hasOverlap)
            {
                throw new ValidationException("Driver already has a route scheduled for this date");
            }
            
            route.ScheduledDate = dto.ScheduledDate.Value;
        }
        
        await _repository.UpdateAsync(route);
        
        _logger.LogInformation("Route updated: RouteId {RouteId}", id);
        
        return MapToDto(route);
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        var route = await _repository.GetByIdAsync(id);
        
        if (route == null)
        {
            return false;
        }
        
        await _repository.DeleteAsync(id);
        
        _logger.LogInformation("Route deleted: RouteId {RouteId}", id);
        
        return true;
    }
    
    public async Task<RouteDto> AssignDriverAsync(int routeId, int driverId)
    {
        _logger.LogInformation(
            "Assigning driver {DriverId} to route {RouteId}",
            driverId,
            routeId
        );
        
        var route = await _repository.GetByIdAsync(routeId);
        
        if (route == null)
        {
            throw new NotFoundException($"Route with ID {routeId} not found");
        }
        
        // Check for overlapping routes for the new driver
        var hasOverlap = await _repository.HasOverlappingRoutesAsync(
            driverId,
            route.ScheduledDate,
            routeId
        );
        
        if (hasOverlap)
        {
            throw new ValidationException("Driver already has a route scheduled for this date");
        }
        
        route.DriverId = driverId;
        await _repository.UpdateAsync(route);
        
        _logger.LogInformation(
            "Driver assigned: RouteId {RouteId}, DriverId {DriverId}",
            routeId,
            driverId
        );
        
        return MapToDto(route);
    }
    
    public async Task<PaginatedResult<RouteDto>> SearchAsync(
        DateTime? date,
        string? driverName,
        int? driverId,
        int pageNumber,
        int pageSize)
    {
        _logger.LogDebug(
            "Searching routes: Date={Date}, DriverName={DriverName}, DriverId={DriverId}",
            date,
            driverName,
            driverId
        );
        
        var (routes, totalCount) = await _repository.SearchAsync(
            date,
            driverName,
            driverId,
            pageNumber,
            pageSize
        );
        
        return new PaginatedResult<RouteDto>
        {
            Data = routes.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    private RouteDto MapToDto(RouteService.Models.Route route)
    {
        return new RouteDto
        {
            Id = route.Id,
            DriverId = route.DriverId,
            VehicleId = route.VehicleId,
            StartLocation = route.StartLocation,
            EndLocation = route.EndLocation,
            EstimatedDistanceKm = route.EstimatedDistanceKm,
            ScheduledDate = route.ScheduledDate
        };
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
