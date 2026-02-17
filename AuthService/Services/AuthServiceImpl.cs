using System.Text.RegularExpressions;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterDto dto);
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto);
    Task<bool> DeleteUserAsync(int id);
}

public class AuthServiceImpl : IAuthService
{
    private readonly AuthDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthServiceImpl> _logger;
    
    public AuthServiceImpl(
        AuthDbContext context,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ILogger<AuthServiceImpl> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }
    
    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        _logger.LogInformation(
            "Registration attempt for username: {Username}, email: {Email}, role: {Role}",
            dto.Username,
            dto.Email,
            dto.Role
        );
        
        // Validate password strength
        if (!IsPasswordStrong(dto.Password))
        {
            _logger.LogWarning("Weak password rejected for username: {Username}", dto.Username);
            throw new ValidationException(
                "Password must be at least 8 characters with uppercase, lowercase, number, and special character"
            );
        }
        
        // Check username uniqueness
        if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
        {
            _logger.LogWarning("Registration failed: Username {Username} already exists", dto.Username);
            throw new ValidationException("Username already exists");
        }
        
        // Check email uniqueness
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", dto.Email);
            throw new ValidationException("Email already exists");
        }
        
        // Validate DriverId for Driver role
        if (dto.Role == UserRole.Driver && !dto.DriverId.HasValue)
        {
            throw new ValidationException("DriverId is required for Driver role");
        }
        
        var passwordHash = _passwordHasher.HashPassword(dto.Password);
        
        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = passwordHash,
            Role = dto.Role,
            DriverId = dto.DriverId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation(
            "User registered successfully: UserId {UserId}, Username {Username}, Role {Role}",
            user.Id,
            user.Username,
            user.Role
        );
        
        return MapToDto(user);
    }
    
    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        _logger.LogInformation("Login attempt for username: {Username}", dto.Username);
        
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username);
        
        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Username} not found", dto.Username);
            throw new UnauthorizedAccessException("Invalid username or password");
        }
        
        // Check lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            _logger.LogWarning(
                "Login failed: User {Username} is locked out until {LockoutEnd}",
                dto.Username,
                user.LockoutEnd.Value
            );
            throw new UnauthorizedAccessException(
                $"Account is locked until {user.LockoutEnd.Value:yyyy-MM-dd HH:mm:ss} UTC"
            );
        }
        
        if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning(
                "Login failed: Invalid password for user {Username}",
                dto.Username
            );
            
            await IncrementFailedLoginAttempts(user);
            
            throw new UnauthorizedAccessException("Invalid username or password");
        }
        
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User {Username} is deactivated", dto.Username);
            throw new UnauthorizedAccessException("Account is deactivated");
        }
        
        // Reset failed login attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await _context.SaveChangesAsync();
        
        var token = _jwtService.GenerateToken(user);
        
        _logger.LogInformation(
            "Login successful for user: {Username} with role {Role}",
            user.Username,
            user.Role
        );
        
        return new LoginResponseDto
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            DriverId = user.DriverId,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };
    }
    
    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users.ToListAsync();
        return users.Select(MapToDto);
    }
    
    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        return user != null ? MapToDto(user) : null;
    }
    
    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
            throw new NotFoundException($"User with ID {id} not found");
        }
        
        if (dto.Email != null)
        {
            // Check email uniqueness
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
            {
                throw new ValidationException("Email already exists");
            }
            user.Email = dto.Email;
        }
        
        if (dto.Role.HasValue)
        {
            user.Role = dto.Role.Value;
        }
        
        if (dto.IsActive.HasValue)
        {
            user.IsActive = dto.IsActive.Value;
        }
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User updated: UserId {UserId}", id);
        
        return MapToDto(user);
    }
    
    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
        {
            return false;
        }
        
        user.IsActive = false;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("User deactivated: UserId {UserId}", id);
        
        return true;
    }
    
    private async Task IncrementFailedLoginAttempts(User user)
    {
        user.FailedLoginAttempts++;
        
        if (user.FailedLoginAttempts >= 5)
        {
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            _logger.LogWarning(
                "User {Username} locked out after {Attempts} failed login attempts",
                user.Username,
                user.FailedLoginAttempts
            );
        }
        
        await _context.SaveChangesAsync();
    }
    
    private bool IsPasswordStrong(string password)
    {
        if (password.Length < 8)
            return false;
        
        var hasUpperCase = Regex.IsMatch(password, @"[A-Z]");
        var hasLowerCase = Regex.IsMatch(password, @"[a-z]");
        var hasDigit = Regex.IsMatch(password, @"\d");
        var hasSpecialChar = Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]");
        
        return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
    }
    
    private UserDto MapToDto(User user)
    {
        return new UserDto
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            DriverId = user.DriverId,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
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
