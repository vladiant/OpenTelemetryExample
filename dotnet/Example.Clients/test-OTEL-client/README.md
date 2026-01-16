# C# OpenTelemetry Client

A .NET 9 console application demonstrating OpenTelemetry implementation in C#. This project showcases distributed tracing, telemetry collection, and observability patterns using modern .NET features, including dependency injection, hosted services, and structured logging. It serves as a practical reference for implementing OpenTelemetry in .NET applications with real-world HTTP client scenarios.

## What is OpenTelemetry?

OpenTelemetry (OTEL) is an open-source observability framework that provides:
- **Distributed Tracing**: Track requests across multiple services with automatic correlation
- **Metrics Collection**: Gather performance and business metrics with built-in exporters
- **Logging Integration**: Structured logging with automatic trace correlation
- **Vendor Neutrality**: Works with any observability backend (Jaeger, Zipkin, Application Insights, etc.)

This C# client demonstrates how to implement OTEL in .NET applications to create observable, traceable, and maintainable systems.

## Features

- **OTLP gRPC Export**: Sends traces and logs to OpenTelemetry Protocol endpoints
- **Activity Source Integration**: .NET native distributed tracing with System.Diagnostics.Activity
- **HTTP Client Instrumentation**: Automatic tracing of HttpClient calls with W3C propagation
- **Dependency Injection**: Modern .NET hosting model with scoped services
- **Structured Logging**: OpenTelemetry-enhanced logging with trace correlation
- **Error Handling**: Exception tracking and activity status management
- **Concurrent Operations**: Scenarios with parallel requests and task coordination
- **Weather API Integration**: A HTTP client example with CRUD operations

## Prerequisites

- **.NET 9 SDK** - Latest LTS version with enhanced OpenTelemetry support (for local development)
- **Docker** - For containerized builds and runs
- **Visual Studio 2022** or **VS Code with C# extension** - Development environment
- **Running API Service** - Target service at `http://localhost:5000` (or other in your local setup)
- **OTLP Collector** - Telemetry endpoint at `http://localhost:19198` (or other in your local setup)

## 🔧 Installation & Setup

### Local Development

```bash
# Navigate to the project directory
cd Example.Clients/test-OTEL-client

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

### Docker

To build and run the application using Docker:

```bash
# Build the Docker image
docker build -t test-otel-client .

# Run the container
# Note: Update the endpoints if your API service and OTLP collector are not on localhost
docker run --network host test-otel-client

# Or run with custom environment variables
docker run -e API_ENDPOINT=http://host.docker.internal:5000 -e OTLP_ENDPOINT=http://host.docker.internal:19198 test-otel-client
```

The Docker setup uses a multi-stage build for optimal image size and includes all necessary dependencies.

## 🎯 Usage

### Quick Start

```bash
# Run simple sequential test
dotnet run

# Run complex concurrent test
dotnet run complex
```

## Project Structure

```
test-OTEL-client/
├── Program.cs                # Host configuration and OpenTelemetry setup
├── SimpleTest.cs             # Test scenarios with activity management
├── WeatherClient.cs          # HTTP client service with instrumentation
├── DefaultValues.cs          # Configuration constants
├── GlobalUsings.cs           # Global using statements
├── test-OTEL-client.csproj   # Project file with package references
└── README.md                 # This file
```

## Key OpenTelemetry Concepts & Code Examples

### 1. Defining OpenTelemetry in .NET

The foundation of OTEL in .NET is proper host configuration with dependency injection:

```csharp
// Program.cs - Host Builder Configuration
IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);

// Configure OpenTelemetry Logging
hostBuilder.ConfigureLogging(logging =>
{
    logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
            ResourceBuilder.CreateDefault()
                          .AddService(DefaultValues.ServiceName) // Service identification
        )
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(DefaultValues.Endpoint);
            options.Protocol = OtlpExportProtocol.Grpc;
            options.Headers = DefaultValues.ApiKey;
        });
    });
});

// Configure OpenTelemetry Tracing
hostBuilder.ConfigureServices((hostContext, services) =>
{
    services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder.AddService(DefaultValues.ServiceName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(DefaultValues.ActivitySourceName) // Custom activity source
                       .AddHttpClientInstrumentation() // Automatic HTTP instrumentation
                       .AddOtlpExporter(options =>
                       {
                           options.Endpoint = new Uri(DefaultValues.Endpoint);
                           options.Protocol = OtlpExportProtocol.Grpc;
                           options.Headers = DefaultValues.ApiKey;
                       });
            });
});
```

**Key Points:**
- **Resource Builder**: Defines service metadata for telemetry correlation
- **Activity Source**: .NET's native distributed tracing mechanism
- **HTTP Instrumentation**: Automatic W3C trace propagation for HttpClient
- **OTLP Exporter**: Standard protocol for telemetry data export

### 2. Creating and Managing Activities (Spans)

In .NET, Activities are the equivalent of OpenTelemetry spans. Here's how to create and manage them:

```csharp
// SimpleTest.cs - Activity Creation and Management
public static class SimpleTest
{
    public static async Task Run(IServiceProvider services, CancellationToken cancellationToken)
    {
        using IServiceScope serviceScope = services.CreateScope();
        
        // Get required services from DI container
        WeatherClient weatherClient = serviceScope.ServiceProvider.GetRequiredService<WeatherClient>();
        ILogger<WeatherClient> logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<WeatherClient>>();
        ActivitySource activitySource = serviceScope.ServiceProvider.GetRequiredService<ActivitySource>();

        // Execute operations with proper activity management
        await GetWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
        await CreateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
        await UpdateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
    }

    private static async Task GetWeatherForecastAsync(WeatherClient weatherClient,
                                                     ILogger<WeatherClient> logger,
                                                     ActivitySource activitySource,
                                                     CancellationToken cancellationToken)
    {
        // Start a new activity (span) with descriptive name
        using Activity? activity = activitySource.StartActivity("GetWeatherForecast");

        try
        {
            // Perform the business operation
            WeatherForecastsDto forecasts = await weatherClient.GetWeatherForecastAsync(5, cancellationToken);

            // Add custom tags (attributes) to the activity
            activity?.SetTag("weather.forecast.count", forecasts.Count);
            activity?.SetTag("weather.forecast.total", forecasts.AllCount);
            activity?.SetTag("operation.type", "weather-retrieval");

            // Mark activity as successful
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log with automatic trace correlation
            logger.LogInformation("Retrieved {Count} weather forecasts (out of {Total} total)",
                                forecasts.Count, forecasts.AllCount);
        }
        catch (Exception ex)
        {
            // Handle errors and mark activity accordingly
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Add error details as tags
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);

            // Log error with trace correlation
            logger.LogError(ex, "Error retrieving weather forecasts");
            
            throw; // Re-throw to maintain exception flow
        }
        // Activity is automatically disposed and ended by 'using' statement
    }
}
```

**Best Practices:**
- **Use 'using' statements**: Ensures activities are properly disposed and ended
- **Descriptive names**: Activity names should clearly indicate the operation
- **Custom tags**: Add business context that helps with debugging and monitoring
- **Status management**: Always set appropriate status (Ok/Error) based on operation outcome
- **Exception handling**: Capture error details while maintaining proper exception flow

### 3. HTTP Client Integration with Automatic Instrumentation

The .NET OpenTelemetry integration provides automatic HTTP client instrumentation:

```csharp
// Program.cs - HTTP Client Configuration
services.AddHttpClient(DefaultValues.HttpClientName, client =>
{
    client.BaseAddress = new Uri(DefaultValues.ApiFrontServiceUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddAsKeyed(); // Register as keyed service for DI

// WeatherClient.cs - HTTP Operations with Automatic Tracing
internal class WeatherClient([FromKeyedServices("ApiServiceClient")] HttpClient httpClient, ILogger<WeatherClient> logger)
{
    public async Task<WeatherForecastsDto> GetWeatherForecastAsync(int? maxResults, CancellationToken cancellationToken)
    {
        // Local variable to hold the weather forecasts
        WeatherForecastsDto? res = null;

        try {
            // HttpClient calls are automatically instrumented
            // W3C trace headers are automatically added
            if (maxResults.HasValue) {
                res = await httpClient.GetFromJsonAsync<WeatherForecastsDto>($"weather/?maxResults={maxResults.Value}", cancellationToken);
            } else {
                res = await httpClient.GetFromJsonAsync<WeatherForecastsDto>("weather/", cancellationToken);
            }
        } catch (Exception ex) {
            // Structured logging with trace correlation
            logger.LogError($"Error retrieving weather forecasts: {ex.Message}");
        }

        return res ?? throw new Exception("Failed to retrieve weather forecasts");
    }

    public async Task<WeatherForecastDto> UpdateWeatherForecastAsync(WeatherForecastDto data, CancellationToken cancellationToken)
    {
        WeatherForecastDto? res = null;

        // Call Server
        try {
            // HttpClient calls are automatically instrumented
            // W3C trace headers are automatically added
            _ = await httpClient.PutAsJsonAsync("weather/", data, cancellationToken);
            res = data; // Assuming the server returns the updated data
        } catch (Exception ex) {
            // Structured logging with trace correlation
            logger.LogError($"Error updating weather forecasts: {ex.Message}");
        }

        return res ?? throw new Exception("Failed to set weather forecast");
    }

    public async Task<WeatherForecastDto> CreateWeatherForecastAsync(WeatherForecastDto data, CancellationToken cancellationToken)
    {
        WeatherForecastDto? res = null;

        // Call Server
        try {
            // HttpClient calls are automatically instrumented
            // W3C trace headers are automatically added
            _ = await httpClient.PostAsJsonAsync("weather/", data, cancellationToken);
            res = data; // Assuming the server returns the updated data
        } catch (Exception innerEx) {
            // Structured logging with trace correlation
            logger.LogError($"Error creating weather forecasts: {innerEx.Message}");
        }

        return res ?? throw new Exception("Failed to set weather forecast");
    }
}
```

**Automatic Features:**
- **Span Creation**: HTTP requests automatically create child spans
- **W3C Propagation**: Trace context is automatically propagated via headers
- **HTTP Attributes**: Request/response details are automatically captured
- **Error Handling**: HTTP errors are automatically recorded in spans
- **Performance Timing**: Request duration is automatically measured

## Configuration

### Default Configuration

The application uses a centralized configuration approach in `DefaultValues.cs`:

```csharp
public static class DefaultValues
{
    public const string ServiceName = "DotNet-OTEL-Client";
    public const string ActivitySourceName = "DotNet-OTEL-Client-ActivitySource";
    public const string Endpoint = "http://localhost:19198";
    public const string ApiKey = "x-otlp-api-key=335d4942d612cec23a138a9e76df2d6c";
    public const string HttpClientName = "ApiServiceClient";
    public const string ApiFrontServiceUrl = "http://localhost:5000";
}
```

### Environment-Based Configuration

For production deployments, use environment variables or configuration providers:

```csharp
// Program.cs - Environment-aware configuration
hostBuilder.ConfigureServices((hostContext, services) =>
{
    var configuration = hostContext.Configuration;
    
    services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder.AddService(
                    configuration["OTEL_SERVICE_NAME"] ?? DefaultValues.ServiceName,
                    configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0"
                );
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(DefaultValues.ActivitySourceName)
                       .AddHttpClientInstrumentation()
                       .AddOtlpExporter(options =>
                       {
                           options.Endpoint = new Uri(
                               configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? DefaultValues.Endpoint
                           );
                           options.Headers = 
                               configuration["OTEL_EXPORTER_OTLP_HEADERS"] ?? DefaultValues.ApiKey;
                       });
            });
});
```

## Observability Features Demonstrated

### 1. Distributed Tracing
- **Activity Propagation**: Traces flow across service boundaries automatically
- **Hierarchical Spans**: Parent-child relationships between operations
- **Context Preservation**: Trace context maintained through async operations
- **W3C Standards**: Compliant with W3C Trace Context specification

### 2. Structured Logging Integration
- **Automatic Correlation**: Logs automatically include trace and span IDs
- **Contextual Information**: Rich metadata attached to log entries
- **Performance Insights**: Timing information for all operations
- **Error Tracking**: Comprehensive exception capture and correlation

### 3. HTTP Observability
- **Request/Response Tracking**: Automatic instrumentation of HTTP calls
- **Performance Metrics**: Duration, status codes, and error rates
- **Header Propagation**: Trace context flows to downstream services
- **Error Classification**: HTTP vs. application errors properly categorized

## Integration Points

This C# client integrates with:
- **TypeScript Client**: Equivalent functionality for comparison and testing
- **API Services**: Target .NET services with OpenTelemetry instrumentation

## Additional Resources

### Official Documentation
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [.NET Distributed Tracing](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing)
- [Activity and ActivitySource](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity)
- [Structured Logging in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging)

---

**Note**: This project demonstrates OpenTelemetry patterns for educational and development purposes. For production deployments, consider additional configuration for sampling, resource limits, batch processing, and security requirements.
