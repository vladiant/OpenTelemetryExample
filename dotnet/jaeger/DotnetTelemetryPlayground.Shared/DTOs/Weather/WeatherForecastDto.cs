namespace DotnetTelemetryPlayground.Shared.DTOs.Weather;

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