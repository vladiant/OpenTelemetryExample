using DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Events;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Models;

public class WeatherForecast : Aggregate<Guid>
{
    public DateTime ForDate { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }

    static internal WeatherForecast Create(DateTime forDate, int temperatureC, string? summary)
    {
        WeatherForecast nWF = new WeatherForecast
        {
            ForDate = forDate,
            TemperatureC = temperatureC,
            Summary = summary,
            Id = Guid.NewGuid()
        };

        nWF.AddDomainEvent(new WeatherForecastCreatedEvent(nWF));

        return nWF;
    }

    internal void UpdateWeatherForecastCommand(WeatherForecastDto dto, Guid id)
    {
        ForDate = dto.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        TemperatureC = dto.TemperatureC;
        Summary = dto.Summary;

        WeatherForecast nWF = new WeatherForecast
        {
            ForDate = ForDate,
            TemperatureC = TemperatureC,
            Summary = Summary,
            Id = id
        };

        AddDomainEvent(new WeatherForecastUpdatedEvent(nWF));
    }
}