using System.Diagnostics;
using System.Text.Json;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Commands;
using DotPulsar.Abstractions;
using MediatR;

namespace DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Consumers;

public class UpdateWeatherForecastConsumer : PulsarBackgroundService<UpdateWeatherForecastConsumer, string>
{

    public UpdateWeatherForecastConsumer(IServiceScopeFactory serviceScopeFactory,
                                         ILogger<UpdateWeatherForecastConsumer> logger,
                                         PulsarBackgroundServiceOptions<string> options,
                                         ApiServiceMeter meter
                                        ) : base(serviceScopeFactory, logger, options, meter)
    {
    }

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
            UpdateWeatherForecastCommand command = new(weatherForecast);

            // Send the command
            UpdateWeatherForecastResponse response = await sender.Send(command, stoppingToken);

            //set activity tags
            Activity.Current?.SetTag("weather.id", response.Id);

            // log success
            logger.LogInformation("Update weather forecast message: {MessageId} with response: {Id} processed successfully.",
                                  message.MessageId,
                                  response.Id);
        } else {
            // log warning for null or invalid data
            logger.LogWarning("Received null or invalid weather forecast data.");
        }
    }

    protected override void RecordProcessingTime(int processingTimeMs)
    {
       // Record the processing time metric for weather updated events
         meter.RecordWeatherUpdated(processingTimeMs);
    }
}
