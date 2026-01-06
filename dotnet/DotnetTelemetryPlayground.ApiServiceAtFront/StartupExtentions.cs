
using DotnetTelemetryPlayground.ApiServiceAtFront.Modules.Weather.Presentation;
using DotnetTelemetryPlayground.Shared.Behaviours;
using FluentValidation;

namespace DotnetTelemetryPlayground.ApiServiceAtFront;

public static class StartupExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Mediator
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssemblies(typeof(StartupExtensions).Assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Add fluent validation
        services.AddValidatorsFromAssembly(typeof(StartupExtensions).Assembly);

        // Add Weather client
        services.AddScoped<Modules.Weather.Infrastructure.Http.WeatherClient>();

        // Add services for the weather module
        return services;
    }

    public static IEndpointRouteBuilder MapWeatherModule(this IEndpointRouteBuilder app)
    {
        // Create weather group
        RouteGroupBuilder grWeather = app.MapGroup("/weather");

        // Configure endpoints for the Weather module.
        WeatherForecastEndpoints.AddRoutes(grWeather);

        return app;
    }
}
