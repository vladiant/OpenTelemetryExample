using DotnetTelemetryPlayground.ApiService.Modules.Weather.Application.Consumers;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Domain.Models;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Presentation;
using DotnetTelemetryPlayground.ApiService.Shared.Infrastructure.Db;
using DotPulsar;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MongoDB.Driver;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotnetTelemetryPlayground.ApiService;

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

        // Add MongoDB
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var connectionString = configuration["ConnectionStrings__MongoDB"] 
                                 ?? configuration["MongoDB__ConnectionString"] 
                                 ?? "mongodb://admin:password@mongodb:27017";
            var databaseName = configuration["MongoDB__DatabaseName"] ?? "WeatherForecastDB";
            var client = new MongoClient(connectionString);
            return client.GetDatabase(databaseName);
        });

        // Add Weather module DB
        services.AddScoped<ISaveChangesInterceptor, FillMandatoryFields>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEvents>();
        services.AddScoped(sp =>
        {
            IMongoDatabase mongoDB = sp.GetRequiredService<IMongoDatabase>();

            DbContextOptionsBuilder optionsBuilder = new();
            optionsBuilder.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            optionsBuilder.UseMongoDB(mongoDB.Client, mongoDB.DatabaseNamespace.DatabaseName);

            return new WeatherDbContext(optionsBuilder.Options, mongoDB);
        });

        // Add services for the weather module
        return services;
    }

    // Add hosted services
    public static IServiceCollection AddHostedServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Workers
        services.AddHostedService(sp =>
        {
            // Configure options for the CreateWeatherForecastConsumer
            PulsarBackgroundServiceOptions<string> options = new()
            {
                Topic = PulsarConstants.WeatherForecastCreateTopic,
                SubscriptionName = PulsarConstants.CreateWeatherForecastSubscription,
                SubscriptionType = SubscriptionType.Exclusive,
                InitialPosition = SubscriptionInitialPosition.Earliest,
                MessagePrefetchCount = 10,
                Schema = DotPulsar.Schemas.StringSchema.UTF8
            };

            // Create and return the consumer
            return new CreateWeatherForecastConsumer(sp.GetRequiredService<IServiceScopeFactory>(),
                                                     sp.GetRequiredService<ILogger<CreateWeatherForecastConsumer>>(),
                                                     options,
                                                     sp.GetRequiredService<ApiServiceMeter>()
                                                    );
        });
        services.AddHostedService(sp =>
        {
            // Configure options for the UpdateWeatherForecastConsumer
            PulsarBackgroundServiceOptions<string> options = new()
            {
                Topic = PulsarConstants.WeatherForecastUpdateTopic,
                SubscriptionName = PulsarConstants.UpdateWeatherForecastSubscription,
                SubscriptionType = SubscriptionType.Exclusive,
                InitialPosition = SubscriptionInitialPosition.Earliest,
                MessagePrefetchCount = 10,
                Schema = DotPulsar.Schemas.StringSchema.UTF8
            };

            // Create and return the consumer
            return new UpdateWeatherForecastConsumer(sp.GetRequiredService<IServiceScopeFactory>(),
                                                     sp.GetRequiredService<ILogger<UpdateWeatherForecastConsumer>>(),
                                                     options,
                                                     sp.GetRequiredService<ApiServiceMeter>()
                                                    );
        });

        // Add hosted services here
        return services;
    }

    // Add Open Telemetry Metrics
    public static IServiceCollection AddCustomMetrics(this IServiceCollection services, IConfiguration configuration)
    {
        // Add meters && metrics
        services.AddSingleton<ApiServiceMeter>();
        services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddMeter(ApiServiceMeter.MeterName);
                });

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

    // Seed initial data if needed
    internal static void FillDataIfNeeded(WeatherDbContext weatherDb)
    {
        // Check if there is data for WeatherForecast
        if (!weatherDb.WeatherForecast.Any()) {
            // Load initial data from YAML file
            try {
                // Load initial data from YAML file
                using var stream = File.OpenText("Data/WheatherInitialData.yaml");

                // Deserialize YAML data
                var deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    .Build();
                var p = deserializer.Deserialize<WeatherForecast[]>(stream);

                // Add to context
                weatherDb.WeatherForecast.AddRange(p);
                weatherDb.SaveChanges();
            } catch (Exception ex) {
                Log.Logger.Error($"Error loading Weather data: {ex.Message}");
            }
        }
    }
}