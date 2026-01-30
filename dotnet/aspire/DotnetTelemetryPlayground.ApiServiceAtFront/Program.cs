using DotnetTelemetryPlayground.ApiServiceAtFront;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.AddPulsarClient("pulsar", settings => settings.Endpoint = new Uri(builder.Configuration["Pulsar:ServiceUrl"]!));

// Add Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.Enrich.FromLogContext()
          .Enrich.WithThreadId()
          .Enrich.WithMachineName()
          .Enrich.WithProcessId()
          .Enrich.WithProperty("Application", "ApiServiceAtFront")
          .WriteTo.Console()
          .WriteTo.OpenTelemetry()
          .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
          .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
          .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning);
});

// Add custom exception handler to the http pipeline
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Add http Client
builder.Services.AddHttpClient("ApiServiceClient", client =>
{
    // Set your API service base address here
    client.BaseAddress = new Uri("https+http://ApiService");

    // Set result type
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddAsKeyed();

// Add modules services
builder.Services.AddModules(builder.Configuration);

var app = builder.Build();

// Map the weather module
app.MapWeatherModule();

// Map health checks
app.MapDefaultEndpoints();

// Run startup code
await app.RunAsync();

