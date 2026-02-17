using System.ComponentModel.DataAnnotations;

namespace RouteService.Models;

public class Route
{
    public int Id { get; set; }
    
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
    [Range(0.01, double.MaxValue)]
    public decimal EstimatedDistanceKm { get; set; }
    
    [Required]
    public DateTime ScheduledDate { get; set; }
}
