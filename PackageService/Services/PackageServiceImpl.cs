using PackageService.DTOs;
using PackageService.Models;
using PackageService.Repositories;
using PackageService.Validator;

namespace PackageService.Services;

public interface IPackageService
{
    Task<PackageDto?> GetByIdAsync(int id);
    Task<PackageDto?> GetByTrackingNumberAsync(string trackingNumber);
    Task<PaginatedResult<PackageDto>> GetAllAsync(int pageNumber, int pageSize);
    Task<PaginatedResult<PackageDto>> GetByCustomerAsync(int customerId, int pageNumber, int pageSize);
    Task<PackageDto> CreateAsync(CreatePackageDto dto);
    Task<PackageDto> UpdateAsync(int id, UpdatePackageDto dto);
    Task<bool> DeleteAsync(int id);
    Task<PackageDto> UpdateStatusAsync(int id, PackageStatus newStatus, int? driverId = null, string? role = null);
    Task<PaginatedResult<PackageDto>> SearchAsync(
        string? trackingNumber,
        PackageStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize);
}

public class PackageServiceImpl : IPackageService
{
    private readonly IPackageRepository _repository;
    private readonly IRouteValidationService _routeValidationService;
    private readonly ILogger<PackageServiceImpl> _logger;
    
    public PackageServiceImpl(
        IPackageRepository repository,
        IRouteValidationService routeValidationService,
        ILogger<PackageServiceImpl> logger)
    {
        _repository = repository;
        _routeValidationService = routeValidationService;
        _logger = logger;
    }
    
    public async Task<PackageDto?> GetByIdAsync(int id)
    {
        PackageServiceValidator.ValidateId(id);

        var package = await _repository.GetByIdAsync(id);
        
        if (package == null)
        {
            _logger.LogInformation("Package not found: PackageId {PackageId}", id);
            return null;
        }
        
        return MapToDto(package);
    }
    
    public async Task<PackageDto?> GetByTrackingNumberAsync(string trackingNumber)
    {
        //_logger.LogDebug("Getting package by tracking number: {TrackingNumber}", trackingNumber);

        PackageServiceValidator.ValidateTrackingNumber(trackingNumber);

        var package = await _repository.GetByTrackingNumberAsync(trackingNumber);
        
        if (package == null)
        {
            _logger.LogInformation("Package not found: TrackingNumber {TrackingNumber}", trackingNumber);
            return null;
        }
        
        return MapToDto(package);
    }
    
    public async Task<PaginatedResult<PackageDto>> GetAllAsync(int pageNumber, int pageSize)
    {
        PackageServiceValidator.ValidatePagination(pageNumber, pageSize);


        var (packages, totalCount) = await _repository.SearchAsync(
            null, null, null, pageNumber, pageSize);
        
        return new PaginatedResult<PackageDto>
        {
            Data = packages.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    public async Task<PaginatedResult<PackageDto>> GetByCustomerAsync(
        int customerId, 
        int pageNumber, 
        int pageSize)
    {
        _logger.LogInformation("Fetching packages for customer {CustomerId}", customerId);

        PackageServiceValidator.ValidatePagination(pageNumber, pageSize);

        var (packages, totalCount) = await _repository.SearchAsync(
            null, null, customerId, pageNumber, pageSize);
        
        return new PaginatedResult<PackageDto>
        {
            Data = packages.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    public async Task<PackageDto> CreateAsync(CreatePackageDto dto)
    {

        _logger.LogInformation(
            "Creating package for customer {CustomerId} on route {RouteId}",
            dto.CustomerId,
            dto.RouteId
        );

        PackageServiceValidator.ValidateCreateDto(dto);

        // Validate route exists via RouteService
        /*var routeExists = await _routeValidationService.ValidateRouteExistsAsync(dto.RouteId);
        
        if (!routeExists)
        {
            _logger.LogWarning(
                "Package creation failed: Route {RouteId} does not exist",
                dto.RouteId
            );
            throw new ValidationException($"Route with ID {dto.RouteId} does not exist");
        }
        */
        // Generate unique tracking number
        var trackingNumber = await GenerateUniqueTrackingNumber();
        
        var package = new Package
        {
            TrackingNumber = trackingNumber,
            CustomerId = dto.CustomerId,
            RouteId = dto.RouteId,
            Status = PackageStatus.Pending,
            WeightKg = dto.WeightKg,
            CreatedAt = DateTime.UtcNow,
            Description = dto.Description
        };
        
        var created = await _repository.AddAsync(package);
        
        _logger.LogInformation(
            "Package created successfully: {TrackingNumber}, PackageId {PackageId}",
            trackingNumber,
            created.Id
        );
        
        return MapToDto(created);
    }
    
    public async Task<PackageDto> UpdateAsync(int id, UpdatePackageDto dto)
    {
        PackageServiceValidator.ValidateId(id);
        PackageServiceValidator.ValidateUpdateDto(dto);


        var package = await _repository.GetByIdAsync(id);
        
        if (package == null)
        {
            _logger.LogWarning("Update failed: Package {PackageId} not found", id);
            throw new NotFoundException($"Package with ID {id} not found");
        }
        
        // Cannot update delivered packages
        if (package.Status == PackageStatus.Delivered)
        {
            throw new ValidationException("Cannot update a delivered package");
        }
        
        if (dto.RouteId.HasValue)
        {
            // Validate new route exists
            var routeExists = await _routeValidationService.ValidateRouteExistsAsync(dto.RouteId.Value);
            
            if (!routeExists)
            {
                throw new ValidationException($"Route with ID {dto.RouteId.Value} does not exist");
            }
            
            package.RouteId = dto.RouteId.Value;
        }
        
        if (dto.WeightKg.HasValue)
        {
            package.WeightKg = dto.WeightKg.Value;
        }
        
        if (!string.IsNullOrEmpty(dto.Description))
        {
            package.Description = dto.Description;
        }
        
        await _repository.UpdateAsync(package);
        
        _logger.LogInformation("Package updated: PackageId {PackageId}", id);
        
        return MapToDto(package);
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        var package = await _repository.GetByIdAsync(id);
        
        if (package == null)
        {
            return false;
        }
        
        await _repository.DeleteAsync(id);
        
        _logger.LogInformation("Package deleted: PackageId {PackageId}", id);
        
        return true;
    }
    
    public async Task<PackageDto> UpdateStatusAsync(
        int id, 
        PackageStatus newStatus, 
        int? driverId = null, 
        string? role = null)
    {
        _logger.LogInformation(
            "Status update request for package {PackageId} to {Status} by driver {DriverId}",
            id,
            newStatus,
            driverId
        );

        PackageServiceValidator.ValidateStatusUpdate(id);

        var package = await _repository.GetByIdAsync(id);
        
        if (package == null)
        {
            throw new NotFoundException($"Package with ID {id} not found");
        }
        
        // Check if driver owns the route (for Driver role)
        if (role == "Driver" && driverId.HasValue)
        {
            var routeOwnedByDriver = await _routeValidationService
                .IsRouteOwnedByDriverAsync(package.RouteId, driverId.Value);
            
            if (!routeOwnedByDriver)
            {
                _logger.LogWarning(
                    "Driver {DriverId} attempted to update package {PackageId} not on their route",
                    driverId.Value,
                    id
                );
                throw new UnauthorizedAccessException(
                    "You can only update packages on your assigned routes"
                );
            }
        }
        
        // Validate status transition
        if (!CanTransitionStatus(package.Status, newStatus))
        {
            throw new ValidationException(
                $"Cannot transition from {package.Status} to {newStatus}. " +
                "Valid transitions: Pending → InTransit → Delivered"
            );
        }
        
        // Additional validation for Delivered status
        if (newStatus == PackageStatus.Delivered)
        {
            var routeScheduledDate = await _routeValidationService
                .GetRouteScheduledDateAsync(package.RouteId);
            
            if (routeScheduledDate.HasValue && 
                routeScheduledDate.Value.Date > DateTime.UtcNow.Date)
            {
                _logger.LogWarning(
                    "Cannot mark package {PackageId} as delivered: Route date {RouteDate} is in the future",
                    id,
                    routeScheduledDate.Value
                );
                throw new ValidationException(
                    "Cannot mark package as delivered before the scheduled route date"
                );
            }
        }
        
        package.Status = newStatus;
        await _repository.UpdateAsync(package);
        
        _logger.LogInformation(
            "Package {PackageId} status updated to {Status}",
            id,
            newStatus
        );
        
        return MapToDto(package);
    }
    
    public async Task<PaginatedResult<PackageDto>> SearchAsync(
        string? trackingNumber,
        PackageStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize)
    {
        _logger.LogDebug(
            "Searching packages: TrackingNumber={TrackingNumber}, Status={Status}, CustomerId={CustomerId}",
            trackingNumber,
            status,
            customerId
        );

        PackageServiceValidator.ValidatePagination(pageNumber, pageSize);

        var (packages, totalCount) = await _repository.SearchAsync(
            trackingNumber,
            status,
            customerId,
            pageNumber,
            pageSize
        );
        
        return new PaginatedResult<PackageDto>
        {
            Data = packages.Select(MapToDto),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }
    
    private bool CanTransitionStatus(PackageStatus currentStatus, PackageStatus newStatus)
    {
        // Delivered is final state
        if (currentStatus == PackageStatus.Delivered)
        {
            return false;
        }
        
        // Valid transitions
        return (currentStatus == PackageStatus.Pending && newStatus == PackageStatus.InTransit) ||
               (currentStatus == PackageStatus.InTransit && newStatus == PackageStatus.Delivered);
    }
    
    private async Task<string> GenerateUniqueTrackingNumber()
    {
        string trackingNumber;
        bool exists;
        
        do
        {
            // Format: PKG-YYYY-NNNN
            var timestamp = DateTime.UtcNow;
            var random = new Random().Next(1000, 9999);
            trackingNumber = $"PKG-{timestamp:yyyy}-{random}";
            
            var existing = await _repository.GetByTrackingNumberAsync(trackingNumber);
            exists = existing != null;
        }
        while (exists);
        
        return trackingNumber;
    }
    
    private PackageDto MapToDto(Package package)
    {
        return new PackageDto
        {
            Id = package.Id,
            TrackingNumber = package.TrackingNumber,
            CustomerId = package.CustomerId,
            RouteId = package.RouteId,
            Status = package.Status.ToString(),
            WeightKg = package.WeightKg,
            CreatedAt = package.CreatedAt,
            Description = package.Description
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
