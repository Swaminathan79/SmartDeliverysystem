using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }
    
    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterDto dto)
    {
        _logger.LogInformation(
            "Registration request received for username: {Username}",
            dto.Username
        );
        
        var user = await _authService.RegisterAsync(dto);
        
        return CreatedAtAction(
            nameof(GetUser),
            new { id = user.UserId },
            user
        );
    }
    
    /// <summary>
    /// Login and receive JWT token
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto dto)
    {
        _logger.LogInformation("Login request received for username: {Username}", dto.Username);
        
        var response = await _authService.LoginAsync(dto);
        
        return Ok(response);
    }
    
    /// <summary>
    /// Get all users (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        _logger.LogInformation("Get all users request");
        
        var users = await _authService.GetAllUsersAsync();
        
        return Ok(users);
    }
    
    /// <summary>
    /// Get user by ID (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("users/{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _authService.GetUserByIdAsync(id);
        
        if (user == null)
        {
            return NotFound(new { message = $"User with ID {id} not found" });
        }
        
        return Ok(user);
    }
    
    /// <summary>
    /// Update user (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("users/{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        _logger.LogInformation("Update user request for UserId: {UserId}", id);
        
        var user = await _authService.UpdateUserAsync(id, dto);
        
        return Ok(user);
    }
    
    /// <summary>
    /// Deactivate user (Admin only)
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        _logger.LogInformation("Delete user request for UserId: {UserId}", id);
        
        var result = await _authService.DeleteUserAsync(id);
        
        if (!result)
        {
            return NotFound(new { message = $"User with ID {id} not found" });
        }
        
        return NoContent();
    }
}
