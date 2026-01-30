using System.Diagnostics;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Polly;
using Polly.Retry;

namespace DotnetTelemetryPlayground.ApiService;

// Base class for Pulsar background services
public abstract class PulsarBackgroundService<S, T> : BackgroundService where S : BackgroundService
{
    protected readonly IServiceScopeFactory serviceScopeFactory;
    protected readonly ILogger<S> logger;
    protected readonly PulsarBackgroundServiceOptions<T> options;

    private readonly IServiceScope? producerServiceScope;
    private readonly IPulsarClient? producerPulsarClient;
    private readonly ResiliencePipeline processRetryPipeline;
    private readonly ResiliencePipeline receiveRetryPipeline;

    protected readonly ApiServiceMeter meter;
    private readonly Stopwatch stopwatch = new();


    public PulsarBackgroundService(IServiceScopeFactory serviceScopeFactory,
                                                         ILogger<S> logger,
                                                         PulsarBackgroundServiceOptions<T> options,
                                                         ApiServiceMeter meter
                                                       )
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
        this.options = options;
        this.meter = meter;

        // If with fallback add producer for DLQ
        if (options.WithFallback) {
            // Get the service provider, Pulsar
            producerServiceScope = serviceScopeFactory.CreateScope();
            producerPulsarClient = producerServiceScope.ServiceProvider.GetRequiredService<IPulsarClient>();
        }

        // Define retry pipelines
        receiveRetryPipeline = new ResiliencePipelineBuilder()
                                            .AddRetry(new RetryStrategyOptions
                                            {
                                                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                                MaxRetryAttempts = 3,
                                                Delay = TimeSpan.FromMilliseconds(500),
                                                BackoffType = DelayBackoffType.Linear
                                            })
                                            .Build();
        processRetryPipeline = new ResiliencePipelineBuilder()
                                    .AddRetry(new RetryStrategyOptions
                                    {
                                        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                        MaxRetryAttempts = 1,
                                        Delay = TimeSpan.FromMilliseconds(500),
                                        BackoffType = DelayBackoffType.Linear
                                    })
                                    .Build();
    }

    // Dispose resources
    public override void Dispose()
    {
        // Dispose resources
        producerServiceScope?.Dispose();

        // Call base dispose
        base.Dispose();
    }

    // Main execution loop for the background service with OpenTelemetry integration
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try {
            // wait for a moment to ensure Pulsar client is ready
            await Task.Delay(5000, stoppingToken);

            // Get the service provider, Pulsar
            await using AsyncServiceScope scopeG = serviceScopeFactory.CreateAsyncScope();
            await using IPulsarClient pulsar = scopeG.ServiceProvider.GetRequiredService<IPulsarClient>();

            // create consumer
            await using IConsumer<T> consumer = pulsar.NewConsumer(options.Schema)
                                                            .Topic(options.Topic)
                                                            .SubscriptionName(options.SubscriptionName)
                                                            .SubscriptionType(options.SubscriptionType)
                                                            .InitialPosition(options.InitialPosition)
                                                            .MessagePrefetchCount(options.MessagePrefetchCount)
                                                            .Create();

            // log info that consumer is created and listening
            logger.LogInformation("{Name} is listening to topic: {Topic} with subscription: {Subscription}",
                                  typeof(S).Name,
                                  options.Topic,
                                  options.SubscriptionName);

            // main loop
            while (!stoppingToken.IsCancellationRequested) {
                try {
                    // receive message
                    IMessage<T> message = await ReceiveMessageWithResilienceAsync(consumer, stoppingToken);

                    // Start measuring processing time
                    stopwatch.Restart();

                    // Create a new activity for processing the message
                    using Activity? activity = serviceScopeFactory.StartActivity(message, $"Process{typeof(S).Name}Message");

                    // For acknowledging the message 
                    try {
                        // Process the weather forecast message
                        await ProcessMessageWithResilienceAsync(message, stoppingToken);

                        // Mark the activity as successful
                        Activity.Current?.SetStatus(ActivityStatusCode.Ok);
                    } catch (Exception ex) {
                        // log error
                        logger.LogError(ex, "Error processing message: {MessageId} with payload: {Payload}",
                                        message.MessageId,
                                        message.Value());

                        // Mark the activity as error
                        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    } finally {
                        // Always acknowledge the message, even if processing failed
                        await consumer.Acknowledge(message, stoppingToken);
                    }

                    // Stop measuring processing time
                    stopwatch.Stop();
                    // Record processing time metric
                    RecordProcessingTime((int)stopwatch.ElapsedMilliseconds);
                } catch (Exception ex) {
                    // log grand error
                    logger.LogError(ex, "Error in {ServiceName} internal loop", typeof(S).Name);
                }
            }

            // Unsubscribe on stopping
            await consumer.Unsubscribe(CancellationToken.None);
        } catch (Exception ex) {
            logger.LogError(ex, "Error in {ServiceName}", typeof(S).Name);
        }
    }

    // Add retry policy for receiving messages
    private async Task<IMessage<T>> ReceiveMessageWithResilienceAsync(IConsumer<T> consumer, CancellationToken stoppingToken)
        => await receiveRetryPipeline.ExecuteAsync(consumer.Receive, stoppingToken);
    

    // Add retry and fallback policy for processing messages if enabled
    private async Task ProcessMessageWithResilienceAsync(IMessage<T> message, CancellationToken stoppingToken)
    {
        try {
            // Define a simple retry policy
            await processRetryPipeline.ExecuteAsync(async token => await ProcessMessageAsync(message, token), stoppingToken);
        } catch {
            // If there is an error and fallback is enabled, send to DLQ
            if (producerPulsarClient != null) {
                try {
                    // Create producer
                    await using IProducer<T> producer = producerPulsarClient.NewProducer(options.Schema) // Create a producer with string schema
                                                                            .Topic($"{options.Topic}-DLQ") // Set the topic
                                                                            .Create(); // Create the producer

                    // Send the message to DLQ
                    await producer.Send(message.Value(), stoppingToken);

                    // log error
                    logger.LogWarning("Sent message to DLQ: {MessageId}", message.MessageId);
                } catch (Exception ex) {
                    // log error
                    logger.LogError(ex, "Error sending message to DLQ: {MessageId}",
                                    message.MessageId);
                }

                // rethrow to mark the activity as error
                throw;
            } else {
                // rethrow to mark the activity as error
                throw;
            }
        }
    }

    // Abstract method to be implemented by derived classes for processing messages
    protected abstract Task ProcessMessageAsync(IMessage<T> message, CancellationToken stoppingToken);

    
    protected abstract void RecordProcessingTime(int processingTimeMs);
}

// Options for Pulsar background service
public record PulsarBackgroundServiceOptions<T>
{
    public required string Topic { get; init; }
    public required string SubscriptionName { get; init; }
    public SubscriptionType SubscriptionType { get; init; } = SubscriptionType.Shared;
    public SubscriptionInitialPosition InitialPosition { get; init; } = SubscriptionInitialPosition.Earliest;
    public ushort MessagePrefetchCount { get; init; } = 10;
    public ISchema<T> Schema { get; set; } = default!;
    public bool WithFallback { get; init; } = true;
}
