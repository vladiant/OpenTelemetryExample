# .NET OpenTelemetry Playground with HTTP and Apache Pulsar handling 

An experimental playground for exploring OpenTelemetry in .NET applications, with a focus on trace context propagation across various communication patterns—including synchronous Web API calls and asynchronous messaging via Apache Pulsar. It also demonstrates log correlation with trace context and performance metrics collection.

## Overview

This solution showcases how is possible to implement distributed tracing, structured logging, and performance monitoring in .NET applications using OpenTelemetry. Key areas of focus include:

> NOTE: The compose stack previously used Jaeger for trace visualization; it now uses the Aspire Dashboard. See **Aspire Dashboard** below for access and image details.

**Aspire Dashboard**

- **Image used**: `mcr.microsoft.com/dotnet/aspire-dashboard:13`
- **Mapped host ports**:
  - `18888` → Aspire UI
  - `18889` → OTLP gRPC receiver
  - `18890` → OTLP HTTP receiver

**Login Instructions**

The Aspire Dashboard requires a token-based login. Each time the container starts, a unique login token is generated and logged.

1. **Retrieve the login token** from the compose logs:
   ```bash
   docker-compose logs aspire | grep "Login to the dashboard"
   ```
   Output will look like:
   ```
   Login to the dashboard at http://localhost:18888/login?t=9497e738087399a7d8518d42367ce9d2
   ```

2. **Open the dashboard** in your browser using the full URL with the token (replace the token with the one from your logs):
   ```
   http://localhost:18888/login?t=9497e738087399a7d8518d42367ce9d2
   ```

3. After login, you'll see the Aspire Dashboard displaying:
   - Available resources (services, databases, etc.)
   - Traces from OpenTelemetry collectors
   - Metrics and logs

**Security Notes**

- The dashboard runs in unsecured mode by default in this compose setup (`ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`). This is suitable only for local development.
- For production environments, configure secure OTLP endpoints, enable HTTPS, and set up proper data protection for encryption keys.
- To change the Aspire image, edit `docker-compose.yml` and update the `image` value under the `aspire` service to your desired registry/tag.



- **Trace Context Propagation**: Understanding how trace context flows across service boundaries
- **Synchronous Communication**: Propagation through HTTP-based Web API calls
- **Asynchronous Communication**: Propagation through Apache Pulsar message queues
- **End-to-End Observability**: Comprehensive tracing across distributed services
- **Logging Integration**: Structured logging enriched with trace context and correlation identifiers
- **Performance Monitoring**: Capturing performance counters and custom metrics
- **Resilience in Distributed Systems**: Basic implementations of Retry, Dead-Letter Queues (DLQs), and fault-handling strategies using **Apache Pulsar**, **Polly**, and **Microsoft.Extensions.Http.Resilience**.

## Architecture

The solution consists of two services that communicate both synchronously and asynchronously, showcasing a variety of telemetry scenarios and best practices for observability in distributed .NET systems.

```mermaid
flowchart LR;
    A[ApiServiceAtFront .NET ] --HTTP/REST--> B[ApiService .NET];
    A --Pulsar--> C[Pulsar Queue]
    C <--Pulsar--> B
    B --EF Core/SQL--> D[Mongo Database]
```

*   **ApiServiceAtFront**\
    Acts as the frontend service, handling incoming requests and forwarding them to the backend via HTTP and Pulsar.

*   **ApiService**\
    Processes requests, interacts with a **MongoDB** database, and returns responses.

This architecture showcases full-stack observability and telemetry across both synchronous and asynchronous flows.

## Implementation Details

### Open Telemetry - Standardized Observability

[OpenTelemetry (OTel)](https://opentelemetry.io/) is a **vendor-neutral, open-source observability framework** designed to standardize the **generation, collection, and export of telemetry data** from applications.

| Aspect | Description |
| :--- | :--- |
| **Purpose** | To enable deep **observability** of software, allowing developers and operators to understand the internal state of a system by analyzing its outputs (telemetry data). |
| **Standard** | It provides a unified set of **APIs, SDKs, and a standard data format (OTLP)** for the three main signals of observability: **Traces** (C# `Activity` objects), **Metrics** (C# `Meter` objects), and **Logs**. |
| **Key Benefit** | It minimizes vendor lock-in, ensuring that instrumentation code (like C# `Activity` for tracing or `Meter` for metrics) can send data to **any compatible backend** (e.g., Jaeger, Prometheus, or commercial monitoring tools) without needing to change the application's code. |

***

This repository uses OpenTelemetry to collect, and export the following telemetry signals:

* **Logs:** Enriches application logs with trace context for better debugging.
* **Traces (Activities/Spans):** Tracks the flow of a single request across services, using the native C\# `Activity` class.
* **Metrics:** Collects quantitative measurements like request counts and latency, using the native C\# `Meter` class.

The collected and exported data is then available for storage and analysis.

### Distributed Tracing

Distributed tracing enables tracking the flow of requests and operations across services, processes, and machines. In .NET applications, this has long been supported through the `System.Diagnostics` namespace, particularly via the `Activity` and `ActivitySource` classes.

When a request travels between services, the .NET `Activity` class ensures the **trace context** is propagated correctly.

#### Key Concepts

- **Trace Context**  
  An immutable object that carries:
  - **Trace ID** — A globally unique identifier for the entire request.
  - **Span ID** — A unique identifier for the current operation within a service.

- **Propagation**  
  .NET instrumentation in some libraries automatically:
  - Serialize the trace context into outgoing requests (e.g., via HTTP headers).
  - Deserialize it on the receiving end to continue the trace seamlessly.

- **Standard Protocol**  
  The format and protocol used for trace context propagation follow the [W3C Trace Context](https://www.w3.org/TR/trace-context/) specification.

#### HTTP Trace Propagation in .NET

**Automatic Instrumentation**  
In .NET applications, the **OpenTelemetry SDK** enables automatic instrumentation for both HTTP clients and servers.

When using `HttpClient` to make requests, the current trace context—**Trace ID** and **Span ID**—is automatically injected into the HTTP headers of outgoing requests.  

In `ASP.NET Core` applications, these headers are automatically read from incoming requests, allowing the trace to continue seamlessly across service boundaries.

```mermaid
flowchart LR
    subgraph TraceContext["Trace ID: 00-4bf92..."]
        direction LR
        A[ApiServiceAtFront] -- HTTP Request --> B[ApiService]
    end
```

> **Important:**  
> Automatic instrumentation **must be explicitly configured in each service**.  

For configuration details, refer to the following source code snippet:

```csharp
// In ServiceDefaults project, Extensions class- HTTP Client tracing
public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>   // Add Logging tagging with trace context & other properties
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()  // Add ASP.NET Core metrics
                       .AddHttpClientInstrumentation() // Add HTTP client metrics
                       .AddRuntimeInstrumentation(); // Add runtime metrics (CPU, GC, etc.)
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation() // Add incoming HTTP request tracing
                    .AddHttpClientInstrumentation(); // Add outgoing HTTP request tracing
            });

        builder.AddOpenTelemetryExporters();  // Add OTLP Exporter based on configuration - export collected telemetry data to OLTP backend

        return builder;
    }
```

And this method has to be called in each service `Program.cs` file:

```csharp
//     Program.cs in ApiService and ApiServiceAtFront

// Create builder - WebApplication, HostedBuilder, etc.
var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.ConfigureOpenTelemetry();

```
  
***  
  
#### Pulsar Message Queue Tracing

**Manual Instrumentation**  
For asynchronous protocols like message queues, the propagation mechanism must be handled by the **Producer** (Sender) and **Consumer** (Receiver) logic, often requiring **manual propagation** if a specific instrumentation library for that queue system doesn't exist.

Since Pulsar does not have a widely adopted, official OpenTelemetry .NET instrumentation library for tracing, you'll likely need the manual approach.

```mermaid
flowchart LR
    subgraph TraceContext["Trace ID: 00-4bf92..."]
        direction LR
        A[ApiServiceAtFront] -- Pulsar Message --> C[Pulsar Queue]
        C --Pulsar Message --> B[ApiService]
    end
    
```

**Message Context Propagation**:
The solution implements custom trace context propagation for Apache Pulsar messages:

**Producer Side Implementation**:
```csharp

// UpdateWeather.cs in ApiServiceAtFront - Pulsar producer example

// Create a producer for the topic
await using IProducer<string> producer = pulsar.NewProducer(Schema.String) // Create a producer with string schema
                                                .Topic(PulsarConstants.WeatherForecastUpdateTopic) // Set the topic
                                                .Create(); // Create the producer


// Send the weather forecast as a JSON string
messageId = await producer.NewMessage()
                            .Property(PulsarConstants.TraceIdTAG, Activity.Current?.Id ?? string.Empty) // Inject TraceId
                            .Property(PulsarConstants.TraceStateTAG, Activity.Current?.TraceStateString ?? string.Empty) // Inject TraceState
                            .Send(
                                JsonSerializer.Serialize(request.WeatherForecast),
                                cancellationToken); // 

```

The important part is injecting the current `Activity` trace context into the message properties. Message properties are key-value pairs that can carry metadata alongside the message payload.

**Consumer Side Implementation**:
```csharp
// ActivityExtensions.cs in ApiService - Pulsar consumer example

// Extension method to create an activity from a message
    public static Activity? StartActivity<T>(this IServiceScopeFactory serviceScopeFactory, IMessage<T> message, string activityName = "ProcessingActivity")
    {
        try {
            // Get activity source for module
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ActivitySource source = scope.ServiceProvider.GetRequiredService<ActivitySource>();

            // Extract trace context from message properties
            bool parseRes = ActivityContext.TryParse(message.Properties.GetValueOrDefault(PulsarConstants.TraceIdTAG) ?? string.Empty,
                                                     message.Properties.GetValueOrDefault(PulsarConstants.TraceStateTAG),
                                                     out ActivityContext parentContext
            );

            // Start a new activity for processing the message
            Activity? result = parseRes ?
                                    source.StartActivity(activityName, ActivityKind.Consumer, parentContext) :
                                    source.StartActivity(activityName, ActivityKind.Consumer);

            // Add relevant tags to the activity
            result?.SetTag("messaging.message_id", message.MessageId.ToString());

            // return
            return result;
        } catch (Exception err) {
            // log error
            Log.Error(err, "Error starting activity from Pulsar message. MessageId: {MessageId}", message.MessageId);

            // in case of error return null
            return null;
        }
    }
```

The important part is extracting the trace context from the message properties and starting a new `Activity` with the extracted context as the parent. This ensures that the trace continues correctly across the asynchronous boundary.

### Custom Metrics Implementation

The solution demonstrates how to implement custom business metrics using .NET's native `Meter` class. The `ApiService` includes a dedicated metrics class that tracks message processing performance and counts.

#### ApiService Custom Metrics

The `ApiServiceMeter` class implements domain-specific metrics for weather forecast message processing:

```csharp
// ApiServiceMeter.cs - Custom business metrics implementation

public class ApiServiceMeter : IDisposable
{
    #region Constants and Fields

    public const string MeterName = "ApiService.Processor";

    private const string WeatherCreatedEvent = "weather_created";
    private const string WeatherUpdatedEvent = "weather_updated";

    private readonly Meter _meter;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _windowSize = TimeSpan.FromSeconds(60); // 1-minute window
    private readonly ConcurrentQueue<DateTime> _requestCreateTimestamps;
    private readonly ConcurrentQueue<DateTime> _requestUpdateTimestamps;

    private readonly Histogram<int> _weatherCreatedProcessingTime;
    private readonly Histogram<int> _weatherUpdatedProcessingTime;

    #endregion

    public ApiServiceMeter(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _requestCreateTimestamps = new ConcurrentQueue<DateTime>();
        _requestUpdateTimestamps = new ConcurrentQueue<DateTime>();

        // Clean up old timestamps every 10 seconds
        _cleanupTimer = new Timer(CleanupOldTimestamps, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        _weatherCreatedProcessingTime = _meter.CreateHistogram<int>("api_service.processor.weather_created_processing_time", "ms", "Processing time for weather created events");
        _weatherUpdatedProcessingTime = _meter.CreateHistogram<int>("api_service.processor.weather_updated_processing_time", "ms", "Processing time for weather updated events");
        _meter.CreateObservableGauge("api_service.processor.weather_created_per_second", () => CalculateRequestsPerSecond(WeatherCreatedEvent), "requests/second", "Rate of weather created events");
        _meter.CreateObservableGauge("api_service.processor.weather_updated_per_second", () => CalculateRequestsPerSecond(WeatherUpdatedEvent), "requests/second", "Rate of weather updated events");
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _meter.Dispose();
    }


    public double CalculateRequestsPerSecond(string eventType)
    {
        var cutoffTime = DateTime.UtcNow - _windowSize;
        var validRequests = 0;

        // Count valid requests based on event type
        switch (eventType) {
            case WeatherCreatedEvent:
                foreach (var timestamp in _requestCreateTimestamps) {
                    if (timestamp >= cutoffTime)
                        validRequests++;
                }
                break;
            case WeatherUpdatedEvent:
                foreach (var timestamp in _requestUpdateTimestamps) {
                    if (timestamp >= cutoffTime)
                        validRequests++;
                }
                break;
            default:
                throw new ArgumentException("Invalid event type. Must be 'created' or 'updated'.");
        }

        // return the rate per second
        return validRequests / _windowSize.TotalSeconds;
    }

    // Method to clean up old timestamps from the queues
    private void CleanupOldTimestamps(object? state)
    {
        var cutoffTime = DateTime.UtcNow - _windowSize;

        // Clean up both queues in parallel
        Task queueOne = Task.Run(() =>
        {
            while (_requestCreateTimestamps.TryPeek(out var timestamp) && timestamp < cutoffTime) {
                _requestCreateTimestamps.TryDequeue(out _);
            }
        });
        Task queueTwo = Task.Run(() =>
        {
            while (_requestUpdateTimestamps.TryPeek(out var timestamp) && timestamp < cutoffTime) {
                _requestUpdateTimestamps.TryDequeue(out _);
            }
        });

        // Wait for both queues to finish cleaning up
        Task.WaitAll(queueOne, queueTwo);
    }
    
    public void RecordWeatherCreated(int processingTimeMs)
    {
        _requestCreateTimestamps.Enqueue(DateTime.UtcNow);
        _weatherCreatedProcessingTime.Record(processingTimeMs);
    }

    public void RecordWeatherUpdated(int processingTimeMs)
    {
        _requestUpdateTimestamps.Enqueue(DateTime.UtcNow);
        _weatherUpdatedProcessingTime.Record(processingTimeMs);
    }
}
```

**Metrics Categories**:

| Metric Type | Name | Description | Unit |
| :--- | :--- | :--- | :--- |
| **ObservableGauge** | `api_service.processor.weather_created_per_second` | Rate of weather created events | requests/second |
| **Histogram** | `api_service.processor.weather_created_processing_time` | Measures processing time distribution for weather creation events | milliseconds |
| **ObservableGauge** | `api_service.processor.weather_updated_per_second` | Rate of weather updated events | requests/second |
| **Histogram** | `api_service.processor.weather_updated_processing_time` | Measures processing time distribution for weather update events | milliseconds |

#### Metrics Integration with Pulsar Background Service

The custom metrics are integrated into the `PulsarBackgroundService` base class to automatically track message processing performance:

```csharp
// PulsarBackgroundService.cs - Metrics integration example

protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // ...existing code...

    // Stop measuring processing time
    stopwatch.Stop();
    // Record processing time metric - delegates to derived class implementation
    RecordProcessingTime((int)stopwatch.ElapsedMilliseconds);
    
    // ...existing code...
}

// Abstract method implemented by derived classes to record specific metrics
protected abstract void RecordProcessingTime(int processingTimeMs);
```

#### Metrics Registration and Export

Custom metrics are registered with OpenTelemetry during service startup:

```csharp
// StartupExtensions.cs - Custom metrics registration

public static IServiceCollection AddCustomMetrics(this IServiceCollection services, IConfiguration configuration)
{
    // Register the custom meter as a singleton
    services.AddSingleton<ApiServiceMeter>();
    
    // Add the custom meter to OpenTelemetry metrics collection
    services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(ApiServiceMeter.MeterName); // Register our custom meter
            });

    return services;
}
```

The metrics are automatically exported via the configured OpenTelemetry exporters (OTLP, Azure Monitor, etc.) and can be visualized in monitoring dashboards or used for alerting.


### Resilience Implementation with Polly

The solution integrates Microsoft's resilience extensions (built on Polly) and custom resilience strategies (built on Polly) to enhance the robustness of Pulsar message processing.

#### Standard Resilience Handler Configuration

Standard Microsoft's resilience handlers are configured for HTTP clients, including:

```csharp
// In ServiceDefaults.cs - Resilient HTTP clients

public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        // Other configurations
        ...

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            ... // Other configurations
        });

        return builder;
    }

    // Which adds some default policies:
    _builder.AddRateLimiter(options.RateLimiter)
            .AddTimeout(options.TotalRequestTimeout)
            .AddRetry(options.Retry)
            .AddCircuitBreaker(options.CircuitBreaker)
            .AddTimeout(options.AttemptTimeout);
```

| Order | Strategy | Description | Defaults |
| :--- | :--- | :--- | :--- |
| 1 | Rate Limiter | Limits the maximum number of concurrent requests being sent to the dependency | Queue: 0, Permit: 1 000 |
| 2 | Timeout (TotalRequestTimeout) | Sets a maximum time limit for the entire request operation, including retries | 30 seconds |
| 3 | Retry | Automatically retries failed requests based on defined conditions (e.g., transient errors) | 3 retries, exponential backoff starting at 2 seconds |
| 4 | Circuit Breaker | Prevents further requests to a failing service after a certain number of consecutive failures | Failure ratio: 10%, Min throughput: 100, Sampling duration: 30s, Break duration: 5s |
| 5 | Timeout (AttemptTimeout) | Sets a maximum time limit for each individual attempt within the retry policy | 10 seconds |
---  

<br>  

> **Important Note**
> In your **production code**, you should customize these policies based on your specific requirements and service SLAs!  
> The default settings are for demonstration purposes only.

#### Custom Resilience Strategies
Custom resilience strategies are implemented for Pulsar message processing, including retry logic and dead-letter queue (DLQ) handling.

```csharp
// in PulsarBackgroundService.cs - Custom resilience for Pulsar message processing

...

// Define retry pipelines
// For receiving messages from Pulsar
receiveRetryPipeline = new ResiliencePipelineBuilder()
                                    .AddRetry(new RetryStrategyOptions
                                    {
                                        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                        MaxRetryAttempts = 3,
                                        Delay = TimeSpan.FromMilliseconds(500),
                                        BackoffType = DelayBackoffType.Linear
                                    })
                                    .Build();

// For processing messages - storing in DB, etc.
processRetryPipeline = new ResiliencePipelineBuilder()
                            .AddRetry(new RetryStrategyOptions
                            {
                                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                                MaxRetryAttempts = 1,
                                Delay = TimeSpan.FromMilliseconds(500),
                                BackoffType = DelayBackoffType.Linear
                            })
                            .Build();


...

// Add retry policy for receiving messages
private async Task<IMessage<T>> ReceiveMessageWithResilienceAsync(IConsumer<T> consumer, CancellationToken stoppingToken)
    => await receiveRetryPipeline.ExecuteAsync(consumer.Receive, stoppingToken);


...

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

```

1. **Retry Policy Execution**:
    - The `processRetryPipeline` is used to execute the `ProcessMessageAsync` method with a retry policy.
    - If the operation fails, it throws an exception.
2. **Error Handling**:
    - If an exception occurs during message processing, the code checks if a `producerPulsarClient` is available for fallback.
3. **Fallback to Dead-Letter Queue (DLQ)**:
   - If the fallback is enabled, a new Pulsar producer is created for the DLQ topic (`options.Topic-DLQ`).
    - The failed message is sent to the DLQ.
4. **Logging**:
    - Logs a warning when a message is successfully sent to the DLQ.
    - Logs an error if there is an issue sending the message to the DLQ.
5. **Rethrow Exception**:
   - If the fallback is not enabled or fails, the exception is rethrown to mark the activity as an error.

<br>  

> **Important Note**
> In your **production code**, you should customize these policies based on your specific requirements and service SLAs!  
> The default settings are for demonstration purposes only.

## Projects

### DotnetTelemetryPlayground.ApiServiceAtFront

**Framework**: .NET 10.0  
**Type**: Frontend Web API Service

A frontend API service that serves as the entry point for external requests and demonstrates telemetry propagation patterns.

**Features**:
- Weather forecast API endpoint (`/weather`)
- Synchronous HTTP calls to downstream services with automatic trace propagation
- Pulsar message publishing with trace context injection
- Resilient HTTP client calls to downstream services
- Full OpenTelemetry integration via ServiceDefaults
- Health check endpoints for monitoring

**Telemetry Integration**:
- End-to-end tracing for incoming requests
- HTTP client tracing with automatic context propagation
- Pulsar producer tracing with custom context injection

**Key Scenarios Demonstrated**:
- **Synchronous Calls**: HTTP calls to `ApiService` with trace propagation
- **Asynchronous Messaging**: Publishing messages to Pulsar queues with custom propagation of context
- **Error Scenarios**: Testing telemetry during failures and retries

**Major Dependencies**:
- `DotnetTelemetryPlayground.ServiceDefaults` for shared telemetry and resilience configurations
- `Polly.Core` for resilience patterns
- `Serilog.AspNetCore` for structured logging
- `FluentValidation.DependencyInjectionExtensions` for request validation

### DotnetTelemetryPlayground.ApiService

**Framework**: .NET 10.0  
**Type**: Backend Web API Service & Pulsar Consumer / Processor Service

A backend API service that demonstrates telemetry in a typical web service scenario. Currently implements:

**Features**:
- Weather forecast API endpoint (`/weather`)
- Pulsar message consumption with trace propagation
- Full OpenTelemetry integration via ServiceDefaults
- Health check endpoints for monitoring

**Telemetry Capabilities**:
- Automatic trace generation for HTTP requests
- Custom spans for business logic operations
- Metrics collection for HTTP and message processing performance
- Structured logging with correlation IDs

**Implementation Highlights**:
- Message processing with parent trace context extraction
- Resilience patterns for processing messages from Pulsar - retries, DLQs
- End-to-end visibility of request flows and message handling

**Major Dependencies**:
- `DotnetTelemetryPlayground.ServiceDefaults` for shared telemetry and resilience configurations
- `MongoDB.EntityFrameworkCore` for database interactions
- `DotPulsar` for Apache Pulsar integration
- `Polly.Core` for resilience patterns
- `Serilog.AspNetCore` for structured logging
- `FluentValidation.DependencyInjectionExtensions` for request validation

### DotnetTelemetryPlayground.ServiceDefaults

**Framework**: .NET 10.0  
**Type**: Shared Library (Aspire Shared Project)

A foundational library that provides common telemetry and observability configurations for all services. This project encapsulates:

**OpenTelemetry Configuration**:
- **Tracing**: ASP.NET Core and HTTP client instrumentation
- **Metrics**: Runtime, ASP.NET Core and HTTP client metrics
- **Logging**: OpenTelemetry logging integration with formatted messages and scopes

**Resilience & Discovery**:
- HTTP client resilience patterns (retry, circuit breaker, timeout) with telemetry
- Service discovery integration
- Standard health checks (`/health` and `/alive` endpoints)

**Exporters**:
- OTLP (OpenTelemetry Protocol) exporter support
- Configurable Azure Monitor integration
- Environment-based exporter configuration

**Dependencies**:
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` 
- `OpenTelemetry.Extensions.Hosting` 
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http` 
- `OpenTelemetry.Instrumentation.Runtime` 
- `Microsoft.Extensions.Http.Resilience` 
- `Microsoft.Extensions.ServiceDiscovery` 

### DotnetTelemetryPlayground.AppHost & Custom.Hosting and Custom.Client 

**Framework**: .NET 10.0  
**Type**: Aspire Application Host

The orchestration layer for the entire solution, built using .NET Aspire. This project:

- Manages service discovery and configuration
- Orchestrates the startup and communication between services
- Provides OpenTelemetry data local exporters & Collection for development purposes
- Provides a unified dashboard for monitoring all services
- Handles service-to-service reference management
- Service discovery and configuration management

> #### **Important Note**
> 
> - These projects are primarily intended to **host and manage other services** on a local development machine.  
> - They **do not contain any business logic**.  
> - All business logic and telemetry implementations reside in the **services described above**.  
> - In a **production environment**, these services would be **deployed independently**, and the **AppHost would not be used**.

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Visual Studio or VS Code with C# extensions
- Docker or Podman (for running Pulsar, Mongo and telemetry backends) locally on Development machine

### Running the Solution

1. **Restore dependencies**
   ```bash
   dotnet restore
   ```

2. **Run with Aspire**
   ```bash
   dotnet run --project DotnetTelemetryPlayground.AppHost\DotnetTelemetryplayground.AppHost.csproj
   ```

3. **Access the Aspire Dashboard**
   - Open your browser to the URL shown in the console
   - Monitor services, traces, metrics, and logs in real-time

### Testing Telemetry Scenarios

1. **Service-to-Service Communication - Basic API Calls**
   ```bash
   curl --location --request GET 'https://localhost:5001/weather/' \
        --header 'Content-Type: application/json' \
        --data '{
            "maxResults": 6
        }'
   ```
   - Frontend service calls backend service with trace propagation
   - Observe automatic trace generation in the Aspire dashboard
   - Observe parent-child span relationships
   - View HTTP client instrumentation metrics

2. **Async Message Patterns**
   ```bash
   curl --location 'https://localhost:5001/weather/' \
        --header 'Content-Type: application/json' \
        --data '{
            "date": "2025-12-17",
            "temperatureC": -10,
            "summary": "Cold as hell"
        }'
   ```
   and
    ```bash
    curl --location --request PUT 'https://localhost:5001/weather/' \
        --header 'Content-Type: application/json' \
        --data '{
            "date": "2025-12-17",
            "temperatureC": -22,
            "summary": "Cold as hell"
        }'
    ```
   - Messages published to Pulsar queues with trace context
   - Consumer processing with parent trace context
   - End-to-end message flow visibility
   - View PULSAR instrumentation metrics

4. **Resilience Testing**
   - Simulate service failures to trigger retry logic

## Telemetry Best Practices Demonstrated

- **Service Defaults Pattern**: Centralized telemetry configuration across all services
- **W3C Trace Context**: Standard-compliant trace propagation for HTTP
- **Custom Propagation**: Manual trace context injection for message queues
- **Structured Logging**: Correlated logs with trace and span IDs
- **Health Checks**: Service health monitoring with telemetry integration
- **Resilience Telemetry**: Comprehensive monitoring of fault tolerance patterns
- **Performance Monitoring**: Request duration, throughput, and queue depth metrics
- **Error Correlation**: Automatic error tracking across distributed operations

## Docker usage

### Build and run
```
# Build all images
docker-compose build

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### Access the Application
* Frontend API: <http://localhost:5000/weather>
* Backend API: <http://localhost:5001/weather>
* Jaeger UI: <http://localhost:16686>
* Pulsar Admin: <http://localhost:8081>
* MongoDB: <mongodb://admin:password@localhost:27017>

### Testing the Application
```
# Test the frontend service
curl --location --request GET 'http://localhost:5000/weather/' \
     --header 'Content-Type: application/json' \
     --data '{
         "maxResults": 6
     }'

# Create a weather forecast
curl --location 'http://localhost:5000/weather/' \
     --header 'Content-Type: application/json' \
     --data '{
         "date": "2025-12-17",
         "temperatureC": -10,
         "summary": "Cold as hell"
     }'

# Update a weather forecast
curl --location --request PUT 'http://localhost:5000/weather/' \
     --header 'Content-Type: application/json' \
     --data '{
         "date": "2025-12-17",
         "temperatureC": -22,
         "summary": "Even colder"
     }'
```

## NB. This project is for educational and experimentation purposes.
