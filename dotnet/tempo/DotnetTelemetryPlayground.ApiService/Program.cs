using DotnetTelemetryPlayground.ApiService;
using DotnetTelemetryPlayground.ApiService.Modules.Weather.Infrastructure.Db;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.AddKeyedMongoDBClient(name: "mongodb");
builder.AddPulsarClient(connectionName: "pulsar");

// Add Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.Enrich.FromLogContext()
          .Enrich.WithThreadId()
          .Enrich.WithMachineName()
          .Enrich.WithProcessId()
          .Enrich.WithProperty("Application", "ApiService")
          .WriteTo.Console()
          .WriteTo.OpenTelemetry()
          .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
          .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
          .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);
});

// Add custom exception handler to the http pipeline
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Add modules services
builder.Services.AddModules(builder.Configuration)
                .AddCustomMetrics(builder.Configuration) // Add OpenTelemetry metrics
                .AddHostedServices(builder.Configuration); // Add hosted services

var app = builder.Build();

// Map the weather module
app.MapWeatherModule();

// Map health checks
app.MapDefaultEndpoints();

// Run startup code
// Add lifecycle event handlers
IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    // get db and create indexes
    using IServiceScope scope = app.Services.CreateScope();
    WeatherDbContext dbContext = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

    // Create indexes if they do not exist
    dbContext.Database.EnsureCreated();

    // Fill some data if needed
    StartupExtensions.FillDataIfNeeded(dbContext);
});

await app.RunAsync();



