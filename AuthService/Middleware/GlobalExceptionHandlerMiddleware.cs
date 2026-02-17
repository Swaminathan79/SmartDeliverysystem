using System.Text.Json;
using AuthService.Services;

namespace AuthService.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };
        
        switch (exception)
        {
            case ValidationException validationEx:
                _logger.LogWarning(
                    validationEx,
                    "Validation error: {Message} | Path: {Path}",
                    validationEx.Message,
                    context.Request.Path
                );
                
                context.Response.StatusCode = 400;
                errorResponse.Error = "Validation Error";
                errorResponse.Message = validationEx.Message;
                errorResponse.StatusCode = 400;
                break;
            
            case UnauthorizedAccessException unauthorizedEx:
                _logger.LogWarning(
                    unauthorizedEx,
                    "Unauthorized access: {Message} | Path: {Path} | User: {User}",
                    unauthorizedEx.Message,
                    context.Request.Path,
                    context.User?.Identity?.Name ?? "Anonymous"
                );
                
                context.Response.StatusCode = 401;
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = unauthorizedEx.Message;
                errorResponse.StatusCode = 401;
                break;
            
            case NotFoundException notFoundEx:
                _logger.LogInformation(
                    notFoundEx,
                    "Resource not found: {Message} | Path: {Path}",
                    notFoundEx.Message,
                    context.Request.Path
                );
                
                context.Response.StatusCode = 404;
                errorResponse.Error = "Not Found";
                errorResponse.Message = notFoundEx.Message;
                errorResponse.StatusCode = 404;
                break;
            
            default:
                _logger.LogError(
                    exception,
                    "Unhandled exception: {Message} | Path: {Path}",
                    exception.Message,
                    context.Request.Path
                );
                
                context.Response.StatusCode = 500;
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = "An unexpected error occurred";
                errorResponse.StatusCode = 500;
                break;
        }
        
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        );
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
}
