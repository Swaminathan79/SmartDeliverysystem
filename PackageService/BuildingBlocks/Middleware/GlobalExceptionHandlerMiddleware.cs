using PackageService.BuildingBlocks.Exception;
using PackageService.Services;
using System.Net;
using System.Text.Json;

namespace PackageService.BuildingBlocks.Middleware;

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
            _logger.LogError(ex, ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, System.Exception exception)
    {
        var errorResponse = new ErrorResponse
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
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
                _logger.LogWarning(validationEx, "Validation error");
                errorResponse.Error = "Validation Error";
                errorResponse.Message = validationEx.Message;
                errorResponse.StatusCode = 400;
                break;

            case UnauthorizedAccessException unauthorizedEx:
                _logger.LogWarning(unauthorizedEx, "Unauthorized access");
                errorResponse.Error = "Unauthorized";
                errorResponse.Message = unauthorizedEx.Message;
                errorResponse.StatusCode = 401;
                break;

            case NotFoundException notFoundEx:
                _logger.LogInformation(notFoundEx, "Resource not found");
                errorResponse.Error = "Not Found";
                errorResponse.Message = notFoundEx.Message;
                errorResponse.StatusCode = 404;
                break;

            case ArgumentException argumentEx:
                _logger.LogWarning(argumentEx, "Bad request");
                errorResponse.Error = "Bad Request";
                errorResponse.Message = argumentEx.Message;
                errorResponse.StatusCode = 400;
                break;

            default:
                _logger.LogError(exception, "Unhandled exception");
                errorResponse.Error = "Internal Server Error";
                errorResponse.Message = "An unexpected error occurred";
                errorResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
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
}
