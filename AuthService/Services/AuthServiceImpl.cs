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

    // NEW
    Task<User> ValidateRefreshToken(string refreshToken);

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

    // ======================================================
    // REGISTER
    // ======================================================

    public async Task<UserDto> RegisterAsync(RegisterDto dto)
    {
        // DTO already validated by FluentValidation

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
        // BUSINESS RULE: username uniqueness
        if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
            throw new ValidationException("Username already exists");

        // BUSINESS RULE: email uniqueness
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            throw new ValidationException("Email already exists");

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = _passwordHasher.HashPassword(dto.Password),
            Role = dto.Role,
           // DriverId = dto.DriverId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "User registered successfully: UserId {UserId}, Username {Username}",
            user.Id,
            user.Username
        );

        return MapToDto(user);
    }

    // ======================================================
    // LOGIN
    // ======================================================

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        _logger.LogInformation("Login attempt for username: {Username}", dto.Username);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid username or password");

        // Lockout check
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            throw new UnauthorizedAccessException(
                $"Account locked until {user.LockoutEnd.Value:yyyy-MM-dd HH:mm:ss} UTC"
            );

        if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            await IncrementFailedLoginAttempts(user);
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        // Reset lockout
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
           // DriverId = user.DriverId,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };
    }

    // ======================================================
    // GET USERS
    // ======================================================

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

    // ======================================================
    // UPDATE USER
    // ======================================================

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        //moved to centralized validator
        /*if (user == null)
            throw new NotFoundException($"User with ID {id} not found");

        if (dto.Email != null)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
                throw new ValidationException("Email already exists");

            user.Email = dto.Email;
        }*/

        if (dto.Role.HasValue)
            user.Role = dto.Role.Value;

        if (dto.IsActive.HasValue)
            user.IsActive = dto.IsActive.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User updated: UserId {UserId}", id);

        return MapToDto(user);
    }

    // ======================================================
    // DELETE USER
    // ======================================================

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
            return false;

        user.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User deactivated: UserId {UserId}", id);

        return true;
    }

    public async Task<User> ValidateRefreshToken(string refreshToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("User inactive");

        return user;
    }


    // ======================================================
    // PRIVATE METHODS
    // ======================================================

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
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
           // DriverId = user.DriverId,
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
