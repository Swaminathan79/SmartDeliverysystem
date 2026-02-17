using System.ComponentModel.DataAnnotations;

namespace PackageService.Models;

public class Package
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string TrackingNumber { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int RouteId { get; set; }

    [Required]
    public PackageStatus Status { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal WeightKg { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public enum PackageStatus
{
    Pending = 0,
    InTransit = 1,
    Delivered = 2
}