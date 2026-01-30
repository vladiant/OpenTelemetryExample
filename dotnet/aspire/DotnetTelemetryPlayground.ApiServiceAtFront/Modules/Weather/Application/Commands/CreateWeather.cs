using System.Diagnostics;
using System.Text.Json;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using FluentValidation;

namespace DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Application.Commands;

// Command for creating weather forecast
public record CreateWeatherForecastCommand(WeatherForecastDto WeatherForecast) : ICommand<CreateWeatherForecastResponse>;

// Response for creating weather forecast
public record CreateWeatherForecastResponse(string Id);

// Validator for CreateWeatherForecastCommand
public class CreateWeatherForecastCommandValidator : AbstractValidator<CreateWeatherForecastCommand>
{
    public CreateWeatherForecastCommandValidator()
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
/// Command handler for creating weather forecast.
/// </summary>
internal class CreateWeatherForecastCommandHandler(IPulsarClient pulsar,
                                                    ILogger<CreateWeatherForecastCommandHandler> logger)
    : ICommandHandler<CreateWeatherForecastCommand, CreateWeatherForecastResponse>
{
    public async Task<CreateWeatherForecastResponse> Handle(CreateWeatherForecastCommand request, CancellationToken cancellationToken)
    {
        // Create a producer for the topic
        await using IProducer<string> producer = pulsar.NewProducer(Schema.String) // Create a producer with string schema
                                                    .Topic(PulsarConstants.WeatherForecastCreateTopic) // Set the topic
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
        logger.LogInformation("Sent weather forecast create with MessageId: '{MessageId}'", messageId);

        // Return a response with a new GUID (simulating the created resource ID)
        return new CreateWeatherForecastResponse(messageId.ToString());
    }
}
