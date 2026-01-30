using DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Models;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;

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
internal class GetWeatherCommandHandler(WeatherDbContext dbContext) : IQueryHandler<GetWeatherQuery, WeatherForecastsDto>
{
    public async Task<WeatherForecastsDto> Handle(GetWeatherQuery query, CancellationToken cancellationToken)
    {
        // locals
        IQueryable<WeatherForecast> qWF;

        // Load from database
        if (query.MaxResults.HasValue) {
            // If MaxResults is specified, limit the results
            qWF = dbContext.WeatherForecast
                            .AsNoTracking()
                            .Take(query.MaxResults.Value);
        } else {
            // Sky is the limit
            qWF = dbContext.WeatherForecast.AsNoTracking();
        }
        var forecasts = await qWF.ToListAsync(cancellationToken);

        //return forecasts;
        return new WeatherForecastsDto(
            Count: forecasts.Count,
            AllCount: await dbContext.WeatherForecast.CountAsync(cancellationToken),
            Forecasts: forecasts.Select(f =>
                new WeatherForecastDto(
                    Date: DateOnly.FromDateTime(f.ForDate),
                    TemperatureC: f.TemperatureC,
                    Summary: f.Summary
                )
            )
        );
    }
}