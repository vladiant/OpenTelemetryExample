import axios, { AxiosInstance } from 'axios';
import * as https from 'https';
import { DefaultValues } from './defaultValues';
import { WeatherForecastDto, WeatherForecastsDto } from './types';
import { Span, SpanStatusCode, trace, Tracer } from '@opentelemetry/api';

export class WeatherClient {
  // HTTP client
  private httpClient: AxiosInstance;
  // Tracer
  private tracer: Tracer;

  constructor() {
    this.httpClient = axios.create({
      baseURL: DefaultValues.API_FRONT_SERVICE_URL,
      headers: {
        'Accept': 'application/json',
        'Content-Type': 'application/json'
      },
      // In development, ignore SSL certificate errors (similar to C# equivalent)
      httpsAgent: process.env.NODE_ENV === 'production' ? undefined : new https.Agent({
        rejectUnauthorized: false
      })
    });

    //get tracer 
    this.tracer = trace.getTracer(DefaultValues.ACTIVITY_SOURCE_NAME);
  }

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
        throw error;
      } finally {
        // End the span
        span.end();
      }
    });
  }

  public async updateWeatherForecastAsync(data: WeatherForecastDto): Promise<WeatherForecastDto> {
    // Start a span
    return this.tracer.startActiveSpan('PUT: /weather', async (span: Span) => {
      try {
        // Make the HTTP PUT request to update weather forecasts
        await this.httpClient.put('weather/', data, {
          headers: {
            'traceparent': `00-${span.spanContext().traceId}-${span.spanContext().spanId}-01`
          }
        });
        span.setStatus({ code: SpanStatusCode.OK, message: 'Weather forecast updated successfully' }); // Set span status to OK
        return data; // Assuming the server returns the updated data
      } catch (error) {
        console.error(`Error updating weather forecasts: ${error}`);
        span.setStatus({ code: SpanStatusCode.ERROR, message: error instanceof Error ? error.message : String(error) }); // Set span status to ERROR
        throw error;
      } finally {
        // End the span
        span.end();
      }
    });

  }

  public async createWeatherForecastAsync(data: WeatherForecastDto): Promise<WeatherForecastDto> {
    // Start a span
    return this.tracer.startActiveSpan('POST: /weather', async (span: Span) => {
      // Make the HTTP POST request to create weather forecasts
      try {
        await this.httpClient.post('weather/', data, {
          headers: {
            'traceparent': `00-${span.spanContext().traceId}-${span.spanContext().spanId}-01`
          }
        });
        span.setStatus({ code: SpanStatusCode.OK, message: 'Weather forecast created successfully' }); // Set span status to OK
        return data; // Assuming the server returns the created data
      } catch (error) {
        console.error(`Error creating weather forecasts: ${error}`);
        span.setStatus({ code: SpanStatusCode.ERROR, message: error instanceof Error ? error.message : String(error) }); // Set span status to ERROR
        throw error;
      } finally {
        // End the span
        span.end();
      }
    });
  }
}