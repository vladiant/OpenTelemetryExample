using System.Diagnostics;
using System.Text.Json;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using FluentValidation;

namespace DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Application.Commands;

/// <summary>
/// Command for updating weather forecast.
/// </summary>
/// <param name="WeatherForecast">The weather forecast data transfer object.</param>
public record class UpdateWeatherForecastCommand(WeatherForecastDto WeatherForecast) : ICommand<UpdateWeatherForecastResponse>;

/// <summary>
/// Response for updating weather forecast.
/// </summary>
/// <param name="Id">The unique identifier of the updated weather forecast.</param>
public record UpdateWeatherForecastResponse(string Id);

// Validator for UpdateWeatherForecastCommand
public class UpdateWeatherForecastCommandValidator : AbstractValidator<UpdateWeatherForecastCommand>
{
    // Constructor
    public UpdateWeatherForecastCommandValidator()
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
internal class UpdateWeatherForecastCommandHandler(IPulsarClient pulsar,
                                                   ILogger<CreateWeatherForecastCommandHandler> logger
                                                  ) : ICommandHandler<UpdateWeatherForecastCommand, UpdateWeatherForecastResponse>
{
    // Handle the command
    public async Task<UpdateWeatherForecastResponse> Handle(UpdateWeatherForecastCommand request, CancellationToken cancellationToken)
    {
        // Create a producer for the topic
        await using IProducer<string> producer = pulsar.NewProducer(Schema.String) // Create a producer with string schema
                                                       .Topic(PulsarConstants.WeatherForecastUpdateTopic) // Set the topic
                                                       .Create(); // Create the producer

        // Check for state and add if exists
        MessageId messageId;
        if (!string.IsNullOrEmpty(Activity.Current?.TraceStateString)) {
            // Send the weather forecast as a JSON string
            messageId = await producer.NewMessage()
                                        .Property(PulsarConstants.TraceIdTAG, Activity.Current?.Id ?? string.Empty)
                                        .Property(PulsarConstants.TraceStateTAG, Activity.Current?.TraceStateString!)
                                        .Send(
                                            JsonSerializer.Serialize(request.WeatherForecast),
                                            cancellationToken);
        } else {
            // Send the weather forecast as a JSON string
            messageId = await producer.NewMessage()
                                        .Property(PulsarConstants.TraceIdTAG, Activity.Current?.Id ?? string.Empty)
                                        .Send(
                                            JsonSerializer.Serialize(request.WeatherForecast),
                                            cancellationToken);
        }

        // Log the message ID and traceId
        logger.LogInformation("Sent weather forecast update with MessageId: '{MessageId}'", messageId);

        // return GUID
        return new UpdateWeatherForecastResponse(messageId.ToString());
    }
}
