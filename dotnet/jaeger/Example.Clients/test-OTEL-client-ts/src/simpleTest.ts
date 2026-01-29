import { WeatherClient } from './weatherClient';
import { WeatherForecastDto, WeatherForecastsDto } from './types';
import { Span, trace, Tracer } from '@opentelemetry/api';
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

  // Run more complex test that calls method multiple times, concurrently, etc.
  public static async runComplex(): Promise<void> {
    const weatherClient = new WeatherClient();

    // Call the private method to get weather forecasts multiple times
    const initialTasks: Promise<void>[] = [];
    for (let i = 0; i < 3; i++) {
      initialTasks.push(// Start a parent span
        SimpleTest.tracer.startActiveSpan('get-weather-forecast', async (parentSpan: Span) => {
          try {
            // Call the private method to get weather forecasts
            await this.getWeatherForecastAsync(weatherClient);
          } finally {
            // End parent span
            parentSpan.end();
          }
        }));
    }

    // Call the private method to create and update weather forecasts concurrently
    const tasks: Promise<void>[] = [];
    for (let i = 0; i < 5; i++) {
      tasks.push(// Start a parent span
        SimpleTest.tracer.startActiveSpan('create-weather-forecast', async (parentSpan: Span) => {
          try {
            // Call the private method to create weather forecasts
            await this.createWeatherForecastAsync(weatherClient);
          } finally {
            // End parent span
            parentSpan.end();
          }
        }));
      tasks.push(// Start a parent span
        SimpleTest.tracer.startActiveSpan('update-weather-forecast', async (parentSpan: Span) => {
          try {
            // Call the private method to update weather forecasts
            await this.updateWeatherForecastAsync(weatherClient);
          } finally {
            // End parent span
            parentSpan.end();
          }
        }));
    }
    await Promise.all(tasks);
    await Promise.all(initialTasks);
  }

  // private static method to get weather forecast
  private static async getWeatherForecastAsync(weatherClient: WeatherClient): Promise<void> {
    try {
      // Call the WeatherClient to get weather forecasts
      const forecasts: WeatherForecastsDto = await weatherClient.getWeatherForecastAsync(5);
      // Log the result
      console.log(`Retrieved ${forecasts.count} weather forecasts (out of ${forecasts.allCount} total):`);
    } catch (error) {
      // Log the error
      console.error('Error retrieving weather forecasts:', error);
    }
  }

  // private static method to set weather forecast
  private static async updateWeatherForecastAsync(weatherClient: WeatherClient): Promise<void> {
    // Get random weather forecast temperature
    const tempC = Math.floor(Math.random() * 15) - 20; // -20 to -5

    try {
      // Create a new weather forecast
      const newForecast: WeatherForecastDto = {
        date: '2025-12-17',
        temperatureC: tempC,
        summary: 'Cold'
      };

      // Call the WeatherClient to set the weather forecast
      const updatedForecast = await weatherClient.updateWeatherForecastAsync(newForecast);

      // Log the result
      console.log(`Updated weather forecast for ${updatedForecast.date}: ${updatedForecast.temperatureC}C, ${updatedForecast.summary}`);
    } catch (error) {
      // Log the error
      console.error('Error updating weather forecast:', error);
    }
  }

  // private static method to create weather forecast
  private static async createWeatherForecastAsync(weatherClient: WeatherClient): Promise<void> {
    // Get random weather forecast temperature
    const tempC = Math.floor(Math.random() * 15) - 20; // -20 to -5

    try {
      // Create a new weather forecast
      const newForecast: WeatherForecastDto = {
        date: '2025-12-17',
        temperatureC: tempC,
        summary: 'Cold'
      };

      // Call the WeatherClient to create the weather forecast
      const createdForecast = await weatherClient.createWeatherForecastAsync(newForecast);

      // Log the result
      console.log(`Created weather forecast for ${createdForecast.date}: ${createdForecast.temperatureC}C, ${createdForecast.summary}`);
    } catch (error) {
      // Log the error
      console.error('Error creating weather forecast:', error);
    }
  }
}