using System.ComponentModel.DataAnnotations;

namespace RouteService.DTOs;

public class RouteDto
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public string StartLocation { get; set; } = string.Empty;
    public string EndLocation { get; set; } = string.Empty;
    public decimal EstimatedDistanceKm { get; set; }
    public DateTime ScheduledDate { get; set; }
}

public class CreateRouteDto
{
    [Required]
    public int DriverId { get; set; }
    
    [Required]
    public int VehicleId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string StartLocation { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string EndLocation { get; set; } = string.Empty;
    
    [Required]
    [Range(0.01, 10000)]
    public decimal EstimatedDistanceKm { get; set; }
    
    [Required]
    public DateTime ScheduledDate { get; set; }
}

public class UpdateRouteDto
{
    public int? VehicleId { get; set; }
    
    [MaxLength(200)]
    public string? StartLocation { get; set; }
    
    [MaxLength(200)]
    public string? EndLocation { get; set; }
    
    [Range(0.01, 10000)]
    public decimal? EstimatedDistanceKm { get; set; }
    
    public DateTime? ScheduledDate { get; set; }
}

public class AssignDriverDto
{
    [Required]
    public int DriverId { get; set; }
}

public class PaginatedResult<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
