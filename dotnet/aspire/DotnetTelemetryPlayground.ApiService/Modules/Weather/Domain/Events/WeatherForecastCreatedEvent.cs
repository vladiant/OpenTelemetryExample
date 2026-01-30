namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Events;

/// <summary>
/// Event raised when a new WeatherForecast is created
/// </summary>
/// <param name="WeatherForecast">The created WeatherForecast.</param>
public record WeatherForecastCreatedEvent(Models.WeatherForecast WeatherForecast)
: IDomainEvent;
