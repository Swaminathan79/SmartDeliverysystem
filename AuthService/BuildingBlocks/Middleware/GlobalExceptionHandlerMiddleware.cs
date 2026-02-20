using AuthService.BuildingBlocks.Exception;
using AuthService.Services;
using System.Net;
using System.Text.Json;

namespace AuthService.BuildingBlocks.Middleware;

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
        
        catch (System.Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, System.Exception exception)
    {
        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method,
            TraceId = context.TraceIdentifier
        };

        switch (exception)
        {
            case AppException appEx:
                _logger.LogWarning(appEx, "Application exception");
                errorResponse.Error = "Application Error";
                errorResponse.Message = appEx.Message;
                errorResponse.StatusCode = appEx.StatusCode;
                break;

            case ValidationException validationEx:
                _logger.LogWarning(validationEx, "Validation error occurred");
                errorResponse.Error = "Validation Error";
                errorResponse.Message = validationEx.Message;
                errorResponse.StatusCode = StatusCodes.Status409Conflict;
                ;
                break;

            case UnauthorizedAccessException unauthorizedEx:
                _logger.LogWarning(unauthorizedEx, "Unauthorized access");
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = "Bearer token is missing or invalid. Please click “Authorize” and provide a valid token."; //unauthorizedEx.Message;
                errorResponse.StatusCode = StatusCodes.Status401Unauthorized;
                break;

            case NotFoundException notFoundEx:
                _logger.LogInformation(notFoundEx, "Resource not found");
                errorResponse.Error = "Not Found";
                errorResponse.Message = notFoundEx.Message;
                errorResponse.StatusCode = StatusCodes.Status404NotFound;
                break;
            case ArgumentException argumentEx:
                _logger.LogWarning(argumentEx, "Bad request");
                errorResponse.Error = "Bad Request";
                errorResponse.Message = argumentEx.Message;
                errorResponse.StatusCode = StatusCodes.Status400BadRequest;
                break;

            default:
                _logger.LogError(exception, "Unhandled exception");
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = "An unexpected error occurred";
                errorResponse.StatusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        context.Response.StatusCode = errorResponse.StatusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(
            errorResponse,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        await context.Response.WriteAsync(json);
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
    public string? TraceId { get; set; }

}
