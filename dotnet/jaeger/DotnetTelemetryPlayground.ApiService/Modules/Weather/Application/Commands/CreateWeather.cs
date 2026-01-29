using DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;

// Command for creating weather forecast
public record CreateWeatherForecastCommand(WeatherForecastDto WeatherForecast) : ICommand<CreateWeatherForecastResponse>;

// Response for creating weather forecast
public record CreateWeatherForecastResponse(Guid? Id);

// Validator for CreateWeatherForecastCommand
public class CreateWeatherForecastCommandValidator : AbstractValidator<CreateWeatherForecastCommand>
{
    public CreateWeatherForecastCommandValidator(WeatherDbContext Db)
    {
        RuleFor(x => x.WeatherForecast).NotNull().WithErrorCode("NULL_OR_EMPTY")
            .WithMessage("Weather forecast cannot be null.");
        RuleFor(x => x.WeatherForecast.Date).NotEmpty().WithErrorCode("NULL_OR_EMPTY")
            .WithMessage("Weather forecast date cannot be empty.");
        RuleFor(x => x.WeatherForecast.Date).MustAsync(async (date, cancellationToken) =>
            {
                return !await Db.WeatherForecast.AnyAsync(x => x.ForDate == date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), cancellationToken);
            })
           .WithMessage("Weather forecast for this date already exists.").WithErrorCode("DUPLICATE");
        RuleFor(x => x.WeatherForecast.TemperatureC).InclusiveBetween(-30, 50);
        RuleFor(x => x.WeatherForecast.Summary)
            .NotEmpty().WithErrorCode("NULL_OR_EMPTY")
            .MaximumLength(100).WithErrorCode("INVALID");
    }
}

/// <summary>
/// Command handler for creating weather forecast.
/// </summary>
internal class CreateWeatherForecastCommandHandler(WeatherDbContext dbContext)
    : ICommandHandler<CreateWeatherForecastCommand, CreateWeatherForecastResponse>
{
    public async Task<CreateWeatherForecastResponse> Handle(CreateWeatherForecastCommand request, CancellationToken cancellationToken)
    {
        //Create a new WeatherForecast instance
        Domain.Models.WeatherForecast weatherForecast = CreateWeatherForecast(request);
        CreateWeatherForecastResponse result = new(weatherForecast.Id);

        // save the WeatherForecast to the database
        dbContext.Entry(weatherForecast).State = EntityState.Added;

        // Save the WeatherForecast
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private Domain.Models.WeatherForecast CreateWeatherForecast(CreateWeatherForecastCommand request)
    {
        // Create a new WeatherForecast instance
        return Domain.Models.WeatherForecast.Create(
            request.WeatherForecast.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.WeatherForecast.TemperatureC,
            request.WeatherForecast.Summary);
    }
}
