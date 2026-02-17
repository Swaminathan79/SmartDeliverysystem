using RouteService.DTOs;

namespace PackageService.Services;

public interface IRouteValidationService
{
    Task<bool> ValidateRouteExistsAsync(int routeId);
    Task<bool> IsRouteOwnedByDriverAsync(int routeId, int driverId);
    Task<DateTime?> GetRouteScheduledDateAsync(int routeId);
}

public class RouteValidationService : IRouteValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RouteValidationService> _logger;

    public RouteValidationService(
        IHttpClientFactory httpClientFactory,
        ILogger<RouteValidationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("RouteService");
        _logger = logger;
    }

    public async Task<bool> ValidateRouteExistsAsync(int routeId)
    {
        try
        {
            _logger.LogDebug("Validating route existence: RouteId {RouteId}", routeId);

            var response = await _httpClient.GetAsync($"/api/routes/{routeId}");

            var exists = response.IsSuccessStatusCode;

            _logger.LogDebug(
                "Route validation result for {RouteId}: {Exists}",
                routeId,
                exists
            );

            return exists;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Error calling RouteService to validate route {RouteId}",
                routeId
            );
            return false;
        }
    }

    public async Task<bool> IsRouteOwnedByDriverAsync(int routeId, int driverId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/routes/{routeId}");

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var route = System.Text.Json.JsonSerializer.Deserialize<RouteDto>(content,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return route?.DriverId == driverId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking route ownership");
            return false;
        }
    }

    public async Task<DateTime?> GetRouteScheduledDateAsync(int routeId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/routes/{routeId}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var route = System.Text.Json.JsonSerializer.Deserialize<RouteDto>(content,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return route?.ScheduledDate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting route scheduled date");
            return null;
        }
    }
}