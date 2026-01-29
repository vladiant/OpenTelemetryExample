namespace test_OTEL_client;

public static class SimpleTest
{
    // Run simple test method
    public static async Task Run(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Create a scope to get scoped services
        using IServiceScope serviceScope = services.CreateScope();

        // Get the WeatherClient, ILogger and ActivitySource services
        WeatherClient weatherClient = serviceScope.ServiceProvider.GetRequiredService<WeatherClient>();
        ILogger<WeatherClient> logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<WeatherClient>>();
        ActivitySource activitySource = serviceScope.ServiceProvider.GetRequiredService<ActivitySource>();

        // Call the private method to get weather forecasts
        await GetWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
        // Create and update a weather forecast
        await CreateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
        // Call the private method to get weather forecasts again
        await UpdateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
    }

    // Run more complex test that calls method multiple times, concurrently, etc.
    public static async Task RunComplex(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Create a scope to get scoped services
        using IServiceScope serviceScope = services.CreateScope();

        // Get the WeatherClient, ILogger and ActivitySource services
        WeatherClient weatherClient = serviceScope.ServiceProvider.GetRequiredService<WeatherClient>();
        ILogger<WeatherClient> logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<WeatherClient>>();
        ActivitySource activitySource = serviceScope.ServiceProvider.GetRequiredService<ActivitySource>();

        // Call the private method to get weather forecasts multiple times
        List<Task> initialTasks = new();
        for (int i = 0; i < 3; i++) {
            initialTasks.Add(GetWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken));
        }

        // Call the private method to create and update weather forecasts concurrently
        List<Task> tasks = new();
        for (int i = 0; i < 5; i++) {
            tasks.Add(CreateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken));
            tasks.Add(UpdateWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken));
        }
        await Task.WhenAll(tasks);
        await Task.WhenAll(initialTasks);

        // Call the private method to get weather forecasts again
        await GetWeatherForecastAsync(weatherClient, logger, activitySource, cancellationToken);
    }

    // private static method to get weather forecast
    private static async Task GetWeatherForecastAsync(WeatherClient weatherClient,
                                                        ILogger<WeatherClient> logger,
                                                        ActivitySource activitySource,
                                                        CancellationToken cancellationToken)
    {
        // Start the activity
        using Activity? activity = activitySource.StartActivity("GetWeatherForecast");

        try {
            // Call the WeatherClient to get weather forecasts
            WeatherForecastsDto forecasts = await weatherClient.GetWeatherForecastAsync(5, cancellationToken);

            // Set some tags to the activity
            activity?.SetTag("weather.forecast.count", forecasts.Count);

            // Set activity status
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log the result
            logger.LogInformation($"Retrieved {forecasts.Count} weather forecasts (out of {forecasts.AllCount} total):");
        } catch (Exception ex) {
            // Set activity status to error
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Log the error
            logger.LogError(ex, $"Error retrieving weather forecasts.");
        }
    }

    // private static method to set weather forecast
    private static async Task UpdateWeatherForecastAsync(WeatherClient weatherClient,
                                                        ILogger<WeatherClient> logger,
                                                        ActivitySource activitySource,
                                                        CancellationToken cancellationToken)
    {
        // Start the activity
        using Activity? activity = activitySource.StartActivity("UpdateWeatherForecast");

        // Get random weather forecast temperature
        int tempC = Random.Shared.Next(-20, -5);

        try {
            // Create a new weather forecast
            WeatherForecastDto newForecast = new(new DateOnly(2025, 12, 17), tempC, "Cold");

            // Call the WeatherClient to set the weather forecast
            WeatherForecastDto updatedForecast = await weatherClient.UpdateWeatherForecastAsync(newForecast, cancellationToken);

            // Set some tags to the activity
            activity?.SetTag("weather.forecast.date", updatedForecast.Date.ToString());
            activity?.SetTag("weather.forecast.temperatureC", updatedForecast.TemperatureC);
            activity?.SetTag("weather.forecast.summary", updatedForecast.Summary);

            // Set activity status
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log the result
            logger.LogInformation($"Set weather forecast for {updatedForecast.Date}: {updatedForecast.TemperatureC}C, {updatedForecast.Summary}");
        } catch (Exception ex) {
            // Set activity status to error
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Log the error
            logger.LogError(ex, $"Error setting weather forecast.");
        }
    }

    // private static method to create weather forecast
    private static async Task CreateWeatherForecastAsync(WeatherClient weatherClient,
                                                        ILogger<WeatherClient> logger,
                                                        ActivitySource activitySource,
                                                        CancellationToken cancellationToken)
    {
        // Start the activity
        using Activity? activity = activitySource.StartActivity("CreateWeatherForecast");

        // Get random weather forecast temperature
        int tempC = Random.Shared.Next(-20, -5);

        try {
            // Create a new weather forecast
            WeatherForecastDto newForecast = new(new DateOnly(2025, 12, 17), tempC, "Cold");

            // Call the WeatherClient to create the weather forecast
            WeatherForecastDto createdForecast = await weatherClient.CreateWeatherForecastAsync(newForecast, cancellationToken);

            // Set some tags to the activity
            activity?.SetTag("weather.forecast.date", createdForecast.Date.ToString());
            activity?.SetTag("weather.forecast.temperatureC", createdForecast.TemperatureC);
            activity?.SetTag("weather.forecast.summary", createdForecast.Summary);

            // Set activity status
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log the result
            logger.LogInformation($"Set weather forecast for {createdForecast.Date}: {createdForecast.TemperatureC}C, {createdForecast.Summary}");
        } catch (Exception ex) {
            // Set activity status to error
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // Log the error
            logger.LogError(ex, $"Error setting weather forecast.");
        }
    }
}
