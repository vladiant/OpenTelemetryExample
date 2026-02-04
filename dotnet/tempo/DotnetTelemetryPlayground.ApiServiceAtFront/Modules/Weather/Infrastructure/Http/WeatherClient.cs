using DotnetTelemetryPlayground.Shared.DTOs.Weather;

namespace DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Infrastructure.Http;

internal class WeatherClient([FromKeyedServices("ApiServiceClient")] HttpClient httpClient, ILogger<WeatherClient> logger)
{
    public async Task<WeatherForecastsDto> GetWeatherForecastAsync(int? maxResults, CancellationToken cancellationToken)
    {
        // Local variable to hold the weather forecasts
        WeatherForecastsDto? res = null;

        try {
            if (maxResults.HasValue) {
                res = await httpClient.GetFromJsonAsync<WeatherForecastsDto>($"weather/?maxResults={maxResults.Value}", cancellationToken);
            } else {
                res = await httpClient.GetFromJsonAsync<WeatherForecastsDto>("weather/", cancellationToken);
            }
        } catch (Exception ex) {
            // Handle any errors that occur during initialization
            logger.LogError($"Error retrieving weather forecasts: {ex.Message}");
        }

        return res ?? throw new Exception("Failed to retrieve weather forecasts");
    }

    public async Task<WeatherForecastDto> SetWeatherForecastAsync(WeatherForecastDto data, CancellationToken cancellationToken)
    {
        WeatherForecastDto? res = null;

        // Call Server
        try {
            _ = await httpClient.PutAsJsonAsync("weather/", data, cancellationToken);
            res = data; // Assuming the server returns the updated data
        } catch (Exception ex) {
            logger.LogError($"Error updating weather forecasts: {ex.Message}");
            try {
                _ = await httpClient.PostAsJsonAsync("weather/", data, cancellationToken);
                res = data; // Assuming the server returns the updated data
            } catch (Exception innerEx) {
                logger.LogError($"Error creating weather forecasts: {innerEx.Message}");
            }
        }

        return res ?? throw new Exception("Failed to set weather forecast");
    }
}
