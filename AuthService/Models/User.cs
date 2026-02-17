using System.ComponentModel.DataAnnotations;

namespace AuthService.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    public int? DriverId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public int FailedLoginAttempts { get; set; }
    
    public DateTime? LockoutEnd { get; set; }
}

public enum UserRole
{
    Admin,
    Manager,
    Driver
}
