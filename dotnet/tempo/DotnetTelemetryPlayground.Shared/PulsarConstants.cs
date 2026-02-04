namespace DotnetTelemetryPlayground.Shared;

public static class PulsarConstants
{
    // Topic for weather forecasts creation
    public const string WeatherForecastCreateTopic = "weather-forecast-create";

    // Topic for weather forecasts update
    public const string WeatherForecastUpdateTopic = "weather-forecast-update";

    // Tag for traceId in message properties
    public const string TraceIdTAG = "traceparent";

    // Tag for traceState in message properties
    public const string TraceStateTAG = "tracestate";

    // Subscription name for create weather forecast consumer
    public const string CreateWeatherForecastSubscription = "create-weather-forecast-subscription";

    // Subscription name for update weather forecast consumer
    public const string UpdateWeatherForecastSubscription = "update-weather-forecast-subscription";
}
