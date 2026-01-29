using DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Models;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;

/// <summary>
/// Command for updating weather forecast.
/// </summary>
/// <param name="WeatherForecast">The weather forecast data transfer object.</param>
public record class UpdateWeatherForecastCommand(WeatherForecastDto WeatherForecast) : ICommand<UpdateWeatherForecastResponse>;

/// <summary>
/// Response for updating weather forecast.
/// </summary>
/// <param name="Id">The unique identifier of the updated weather forecast.</param>
public record UpdateWeatherForecastResponse(Guid? Id);

// Validator for UpdateWeatherForecastCommand
public class UpdateWeatherForecastCommandValidator : AbstractValidator<UpdateWeatherForecastCommand>
{
    // Constructor
    public UpdateWeatherForecastCommandValidator(WeatherDbContext Db)
    {
        RuleFor(x => x.WeatherForecast).NotNull().WithErrorCode("NULL_OR_EMPTY")
            .WithMessage("Weather forecast cannot be null.");
        RuleFor(x => x.WeatherForecast.Date).NotEmpty().WithErrorCode("NULL_OR_EMPTY")
            .WithMessage("Weather forecast date cannot be empty.");
        RuleFor(x => x.WeatherForecast.TemperatureC).InclusiveBetween(-30, 50);
        RuleFor(x => x.WeatherForecast.Summary)
            .NotEmpty().WithErrorCode("NULL_OR_EMPTY")
            .MaximumLength(100).WithErrorCode("INVALID");
    }
}

/// <summary>
/// Command handler for updating weather forecast.
/// </summary>
/// <param name="dbContext">The database context.</param>
internal class UpdateWeatherForecastCommandHandler(WeatherDbContext dbContext) : ICommandHandler<UpdateWeatherForecastCommand, UpdateWeatherForecastResponse>
{
    // Handle the command
    public async Task<UpdateWeatherForecastResponse> Handle(UpdateWeatherForecastCommand request, CancellationToken cancellationToken)
    {
        WeatherForecast? weatherForecast =
            await dbContext.WeatherForecast.FirstOrDefaultAsync(x => x.ForDate == request.WeatherForecast.Date.ToDateTime(TimeOnly.MinValue,  DateTimeKind.Utc), cancellationToken);
        if (weatherForecast == null) {
            throw new NotFoundException("Weather forecast not found.");
        }

        // Update the weather forecast & store it in DB
        weatherForecast.UpdateWeatherForecastCommand(request.WeatherForecast, weatherForecast.Id);
        await dbContext.SaveChangesAsync(cancellationToken);

        // return GUID
        return new UpdateWeatherForecastResponse(weatherForecast.Id);
    }
}
