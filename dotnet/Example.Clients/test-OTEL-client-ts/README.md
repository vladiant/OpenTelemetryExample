# TypeScript OpenTelemetry Client

A TypeScript/Node.js OpenTelemetry test client that demonstrates distributed tracing, telemetry collection, and observability patterns. This project serves as a practical example of implementing OpenTelemetry in Node.js applications, showing how to instrument HTTP clients, create custom spans, and export telemetry data to OTLP-compatible endpoints.

## What is OpenTelemetry?

OpenTelemetry (OTEL) is an open-source observability framework that provides:
- **Distributed Tracing**: Track requests across multiple services
- **Metrics Collection**: Gather performance and business metrics  
- **Logging Integration**: Structured logging with trace correlation
- **Vendor Neutrality**: Works with any observability backend (Jaeger, Zipkin, etc.)

This client demonstrates how to implement OTEL in TypeScript/Node.js to create observable, traceable applications.

## Features

- **OTLP gRPC Export**: Sends traces to OpenTelemetry Protocol endpoints
- **Custom Span Creation**: Manual instrumentation with detailed attributes
- **HTTP Client Instrumentation**: Tracing of outbound HTTP requests
- **Error Tracking**: Proper span status and error handling
- **Concurrent Operations**: Complex scenarios with parallel requests
- **Weather API Integration**: W3C Trace context manual propagation of HTTP client calls example

## Prerequisites

- **Node.js 18+** - Required for OpenTelemetry SDK compatibility
- **npm or yarn** - Package manager
- **Running API Service** - Target service at `https://localhost:7055` (or other in your local setup)
- **OTLP Collector** - Telemetry endpoint at `https://localhost:21226` (or other in your local setup)

## Installation

```bash
# Clone the repository (if not already cloned)
git clone <repository-url>
cd Example.Clients/test-OTEL-client-ts

# Install dependencies
npm install

# Build the project
npm run build
```

## Usage

### Quick Start

```bash
# Run simple sequential test
npm run simple

# Run complex concurrent test
npm run complex
```

## Project Structure

```
test-OTEL-client-ts/
├── src/
│   ├── index.ts              # Entry point and argument parsing
│   ├── telemetry.ts          # OpenTelemetry SDK configuration
│   ├── defaultValues.ts      # Configuration constants
│   ├── weatherClient.ts      # HTTP client with OTEL instrumentation
│   ├── simpleTest.ts         # Test scenarios and span management
│   └── types.ts              # TypeScript interfaces
├── package.json              # Dependencies and scripts
├── tsconfig.json             # TypeScript configuration
└── README.md                 # This file
```

## Key OpenTelemetry Concepts & Code Examples

### 1. Defining OpenTelemetry SDK

The foundation of any OTEL application is proper SDK initialization:

```typescript
// src/telemetry.ts
import { NodeSDK } from '@opentelemetry/sdk-node';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-grpc';
import { DefaultValues } from './defaultValues';

// Configure the OpenTelemetry SDK
const sdk = new NodeSDK({
    resource: resourceFromAttributes({
        [ATTR_SERVICE_NAME]: 'weather-client-ts',
        [ATTR_SERVICE_VERSION]: '1.0',
    }),
    traceExporter: new OTLPTraceExporter({
        url: 'https://localhost:21226',
        headers: {
            'x-otlp-api-key': '335d4942d612cec23a138a9e76df2d6c'
        }
    })
});

// Initialize the SDK - this must be done before importing instrumented modules
sdk.start();
```

**Key Points:**
- **Resource**: Defines service metadata (name, version, environment)
- **Exporter**: Configures where telemetry data is sent
- **Headers**: Authentication for OTLP endpoints
- **Initialization Order**: SDK must start before other modules load

### 2. Creating and Managing Spans

Spans represent units of work in your application. Here's how to create them:

```typescript
// src/simpleTest.ts
import { trace, Span } from '@opentelemetry/api';
import { DefaultValues } from './defaultValues';

export class SimpleTest {
    private static tracer: Tracer = trace.getTracer(DefaultValues.ACTIVITY_SOURCE_NAME);

    // Run simple test method
  public static async run(): Promise<void> {

    // Get Weather Client
    const weatherClient = new WeatherClient();

    // Start a parent span
    SimpleTest.tracer.startActiveSpan('get-weather-forecast', async (parentSpan: Span) => {
      try {
        // Call the private method to get weather forecasts
        await this.getWeatherForecastAsync(weatherClient);
      } finally {
        // End parent span
        parentSpan.end();
      }
    });

    // Start a parent span
    SimpleTest.tracer.startActiveSpan('create-weather-forecast', async (parentSpan: Span) => {
      try {
        // Call the private method to create weather forecasts
        await this.createWeatherForecastAsync(weatherClient);
      } finally {
        // End parent span
        parentSpan.end();
      }
    });

    // Start a parent span
    SimpleTest.tracer.startActiveSpan('update-weather-forecast', async (parentSpan: Span) => {
      try {
        // Call the private method to update weather forecasts
        await this.updateWeatherForecastAsync(weatherClient);
      } finally {
        // End parent span
        parentSpan.end();
      }
    });
  }
}
```

**Best Practices:**
- **Always use try-finally**: Ensure spans are ended even if errors occur
- **Use descriptive names**: Span names should clearly indicate the operation

### 3. HTTP Client Instrumentation

Instrumenting HTTP clients to automatically create spans:

```typescript
// src/weatherClient.ts
export class WeatherClient {

    public async getWeatherForecastAsync(maxResults?: number): Promise<WeatherForecastsDto> {
    // Start a span
    return this.tracer.startActiveSpan('GET: /weather', async (span: Span) => {
      // Make the HTTP GET request to retrieve weather forecasts
      try {
        const url = maxResults ? `weather/?maxResults=${maxResults}` : 'weather/';
        const response = await this.httpClient.get<WeatherForecastsDto>(url, {
          headers: {
            'traceparent': `00-${span.spanContext().traceId}-${span.spanContext().spanId}-01`
          }
        });
        span.setStatus({ code: SpanStatusCode.OK, message: 'Weather forecast retrieved successfully' }); // Set span status to OK
        return response.data;
      } catch (error) {
        console.error(`Error retrieving weather forecasts: ${error}`);
        span.setStatus({ code: SpanStatusCode.ERROR, message: error instanceof Error ? error.message : String(error) }); // Set span status to ERROR
      } finally {
        // End the span
        span.end();
      }
    });
  }
}
```

**Best Practices:**
- **Manual propagation**: Ensure trace context is passed between services
- **Always use try-finally**: Ensure spans are ended even if errors occur
- **Set meaningful attributes**: Add context that helps with debugging
- **Record exceptions**: Capture error details for troubleshooting
- **Use descriptive names**: Span names should clearly indicate the operation

## ⚙️ Configuration

### Default Configuration

The application comes with sensible defaults in `src/defaultValues.ts`:

```typescript
export class DefaultValues {
    public static readonly SERVICE_NAME = 'weather-client-ts';
    public static readonly ACTIVITY_SOURCE_NAME = 'weather-client-tracer';
    public static readonly ENDPOINT = 'https://localhost:21226';
    public static readonly API_KEY = 'x-otlp-api-key=335d4942d612cec23a138a9e76df2d6c';
    public static readonly API_FRONT_SERVICE_URL = 'https://localhost:7055';
}
```

### Customizing Configuration

You can modify these values or use environment variables:

```typescript
// Using environment variables
const config = {
    endpoint: process.env.OTEL_EXPORTER_OTLP_ENDPOINT || DefaultValues.ENDPOINT,
    apiKey: process.env.OTEL_API_KEY || DefaultValues.API_KEY,
    serviceName: process.env.OTEL_SERVICE_NAME || DefaultValues.SERVICE_NAME
};
```

## 🔍 Observability Features Demonstrated

### 1. Distributed Tracing
- **Trace Propagation**: Correlates requests across service boundaries
- **Span Hierarchy**: Parent-child relationships between operations
- **Context Propagation**: Maintains trace context through async operations

### 2. Error Tracking
- **Exception Recording**: Captures full error details
- **Span Status**: Marks operations as successful, failed, or unknown
- **Error Attributes**: Adds contextual information about failures

### 3. Performance Monitoring
- **Operation Timing**: Automatic duration measurement
- **HTTP Metrics**: Request/response timing and status codes
- **Concurrency Patterns**: Tracks parallel operation performance

### 4. Custom Instrumentation
- **Business Logic Spans**: Traces application-specific operations
- **Custom Attributes**: Adds domain-specific context
- **Manual Timing**: Precise control over span boundaries

## 🔗 Related Projects

This TypeScript client is part of a larger telemetry playground that includes:
- **C# OpenTelemetry Client**: Equivalent functionality in .NET
- **C++ OpenTelemetry Client**: Equivalent functionality in C++
- **API Services**: Target services for testing

## Additional Resources

- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [OpenTelemetry JavaScript SDK](https://opentelemetry.io/docs/languages/js/)
- [OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)
- [Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)

---

**Note**: This project is designed for educational and testing purposes. For production use, consider additional configuration for sampling, batching, and error handling.