using DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Infrastructure.Http;

namespace DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Application.Commands;

/// <summary>
/// Query to get weather forecasts
/// </summary>
public class GetWeatherQuery : IQuery<WeatherForecastsDto>
{
    public int? MaxResults { get; set; }
}

/// <summary>
/// Handler for <see cref="GetWeatherQuery"/>
/// </summary>
internal class GetWeatherCommandHandler(WeatherClient apiService) : IQueryHandler<GetWeatherQuery, WeatherForecastsDto>
{
    public async Task<WeatherForecastsDto> Handle(GetWeatherQuery query, CancellationToken cancellationToken)
     => await apiService.GetWeatherForecastAsync(query.MaxResults, cancellationToken) ?? new WeatherForecastsDto(0, 0, []);
}
