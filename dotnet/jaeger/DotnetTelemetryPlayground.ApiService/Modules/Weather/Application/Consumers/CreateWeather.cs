using System.Diagnostics;
using System.Text.Json;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;
using DotPulsar.Abstractions;
using MediatR;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Consumers;

public class CreateWeatherForecastConsumer : PulsarBackgroundService<CreateWeatherForecastConsumer, string>
{
    public CreateWeatherForecastConsumer(IServiceScopeFactory serviceScopeFactory,
                                         ILogger<CreateWeatherForecastConsumer> logger,
                                         PulsarBackgroundServiceOptions<string> options,
                                         ApiServiceMeter meter
                                        ) : base(serviceScopeFactory, logger, options, meter)
    {
    }

    // Process the received message to create a weather forecast
    protected override async Task ProcessMessageAsync(IMessage<string> message, CancellationToken stoppingToken)
    {
        // Deserialize the message payload to WeatherForecastDto
        WeatherForecastDto? weatherForecast = JsonSerializer.Deserialize<WeatherForecastDto>(message.Value());

        // Call command handler or service to process the weather forecast
        if (weatherForecast != null) {
            // Get scoped mediator
            await using AsyncServiceScope scopeM = serviceScopeFactory.CreateAsyncScope();
            IMediator sender = scopeM.ServiceProvider.GetRequiredService<IMediator>();

            // Create the command
            CreateWeatherForecastCommand command = new(weatherForecast);

            // Send the command
            CreateWeatherForecastResponse response = await sender.Send(command, stoppingToken);

            //set activity tags
            Activity.Current?.SetTag("weather.id", response.Id);

            // log success
            logger.LogInformation("Create weather forecast message: {MessageId} with response: {Id} processed successfully.",
                                  message.MessageId,
                                  response.Id);
        } else {
            // log warning for null or invalid data
            logger.LogWarning("Received null or invalid weather forecast data.");
        }
    }

    protected override void RecordProcessingTime(int processingTimeMs)
    {
        // Record the processing time metric for weather created events
        meter.RecordWeatherCreated(processingTimeMs);
    }
}
