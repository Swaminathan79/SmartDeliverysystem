using System.ComponentModel.DataAnnotations;
using PackageService.Models;

namespace PackageService.DTOs;

public class PackageDto
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int RouteId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
}

public class CreatePackageDto
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int RouteId { get; set; }

    [Required]
    [Range(0.01, 1000)]
    public decimal WeightKg { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class UpdatePackageDto
{
    public int? RouteId { get; set; }

    [Range(0.01, 1000)]
    public decimal? WeightKg { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class UpdateStatusDto
{
    [Required]
    public PackageStatus Status { get; set; }
}

public class PaginatedResult<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}