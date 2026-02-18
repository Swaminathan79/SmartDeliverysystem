using System.ComponentModel.DataAnnotations;
using AuthService.Models;

namespace AuthService.DTOs;

public class RegisterDto
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    //public int? DriverId { get; set; }
}

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
}

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? DriverId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

}

public class UpdateUserDto
{
    [EmailAddress]
    public string? Email { get; set; }
    
    public UserRole? Role { get; set; }
    
    public bool? IsActive { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
