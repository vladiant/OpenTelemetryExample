namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Events;

/// <summary>
/// Event raised when an existing WeatherForecast is updated
/// </summary>
/// <param name="WeatherForecast">The updated WeatherForecast.</param>
public record WeatherForecastUpdatedEvent(Models.WeatherForecast WeatherForecast)
: IDomainEvent;
