using System.CommandLine;
using System.CommandLine.Parsing;
using test_OTEL_client;

// create command-line parser operations
RootCommand rootCommand = new("Sample Otel server");
Option<bool> useElk = new("--elk", "-e")
{
    Description = "Use ELK for tracing and metrics",
    DefaultValueFactory = _ => true
};
rootCommand.Options.Add(useElk);
Command complex = new("complex")
{
    Description = "Run complex test"
};
rootCommand.Subcommands.Add(complex);

// Parse the command line
ParseResult parseResult = rootCommand.Parse(args);
OptionResult? useElkRes = parseResult.GetResult(useElk);
CommandResult? complexRes = parseResult.GetResult(complex);

// Determin the flags
bool runComplexTest = complexRes != null;
bool useElkFlag = useElkRes != null && !useElkRes.Implicit && useElkRes.GetValueOrDefault<bool>();

// Read some Env variables to override defaults
string? v_OTEL_EXPORTER_OTLP_ENDPOINT = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", EnvironmentVariableTarget.User);
string? v_OTEL_EXPORTER_OTLP_HEADERS = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", EnvironmentVariableTarget.User);

// Create the host builder
IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args);

// Configure logging to use OpenTelemetry
hostBuilder.ConfigureLogging(logging =>
{
    // Configure OpenTelemetry to enhance logging
    logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                                   .AddService(DefaultValues.ServiceName) // Set the service name to be shown in the logs
                )
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(useElkFlag ? v_OTEL_EXPORTER_OTLP_ENDPOINT ?? DefaultValues.Endpoint : DefaultValues.Endpoint);
                    options.Protocol = OtlpExportProtocol.Grpc;
                    options.Headers = useElkFlag ? v_OTEL_EXPORTER_OTLP_HEADERS ?? DefaultValues.ApiKey : DefaultValues.ApiKey;
                }); // Export traces to OTLP endpoint
    });
});

// Configure services
hostBuilder.ConfigureServices((hostContext, services) =>
{
    // Add OpenTelemetry Tracing
    services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder.AddService(DefaultValues.ServiceName); // Set the service name to be shown in the traces
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(DefaultValues.ActivitySourceName) // Start tracing from this source !
                        .AddHttpClientInstrumentation() // Instrument HttpClient calls - And W3C header propagation is automatic
                        .AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(useElkFlag ? v_OTEL_EXPORTER_OTLP_ENDPOINT ?? DefaultValues.Endpoint : DefaultValues.Endpoint);
                            options.Protocol = OtlpExportProtocol.Grpc;
                            options.Headers = useElkFlag ? v_OTEL_EXPORTER_OTLP_HEADERS ?? DefaultValues.ApiKey : DefaultValues.ApiKey;
                        }); // Export traces to OTLP endpoint
            });

    // Add HttpClient for making API calls
    services.AddHttpClient(DefaultValues.HttpClientName, client =>
    {
        // Set your API service base address here
        client.BaseAddress = new Uri(DefaultValues.ApiFrontServiceUrl);

        // Set result type
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    }).AddAsKeyed();

    // Add an ActivitySource for creating custom activities
    services.AddSingleton((services) => new ActivitySource(DefaultValues.ActivitySourceName));

    // Add Weather client
    services.AddScoped<WeatherClient>();
});

// ===================================================================================================================

// Create the host
IHost host = hostBuilder.Build();

// Get the application lifetime service
IHostApplicationLifetime appLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

// Register a callback to be called when the application is starting
appLifetime.ApplicationStarted.Register(async () =>
{
    if (runComplexTest) {
        Console.WriteLine("Running complex test...");
        await SimpleTest.RunComplex(host.Services, CancellationToken.None);
    } else {
        Console.WriteLine("Running simple test...");
        await SimpleTest.Run(host.Services, CancellationToken.None);
    }
});

// ===================================================================================================================

// Start it
await host.RunAsync();