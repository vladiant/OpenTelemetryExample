
namespace test_OTEL_client;

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

    public async Task<WeatherForecastDto> UpdateWeatherForecastAsync(WeatherForecastDto data, CancellationToken cancellationToken)
    {
        WeatherForecastDto? res = null;

        // Call Server
        try {
            _ = await httpClient.PutAsJsonAsync("weather/", data, cancellationToken);
            res = data; // Assuming the server returns the updated data
        } catch (Exception ex) {
            logger.LogError($"Error updating weather forecasts: {ex.Message}");
        }

        return res ?? throw new Exception("Failed to set weather forecast");
    }

    public async Task<WeatherForecastDto> CreateWeatherForecastAsync(WeatherForecastDto data, CancellationToken cancellationToken)
    {
        WeatherForecastDto? res = null;

        // Call Server
        try {
            _ = await httpClient.PostAsJsonAsync("weather/", data, cancellationToken);
            res = data; // Assuming the server returns the updated data
        } catch (Exception innerEx) {
            logger.LogError($"Error creating weather forecasts: {innerEx.Message}");
        }

        return res ?? throw new Exception("Failed to set weather forecast");
    }
}

/// <summary>
/// Data Transfer Object representing a Weather Forecast
/// </summary>
public record class WeatherForecastDto(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
};

/// <summary>
/// Data Transfer Object representing a collection of Weather Forecasts
/// </summary>
public record class WeatherForecastsDto(int Count, int AllCount, IEnumerable<WeatherForecastDto> Forecasts);
