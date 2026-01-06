using System.CommandLine;
using System.CommandLine.Parsing;

// create command-line parser operations
RootCommand rootCommand = new("Sample Otel server");
Option<bool> useElk = new("--elk", "-e")
{
    Description = "Use ELK for tracing and metrics",
    DefaultValueFactory = _ => true
};
rootCommand.Options.Add(useElk);


IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<MongoWithReplicaSetResource> mongo = builder.AddMongoWithReplicaSet("mongo", mongoPort: 27017)
                    .WithDataVolume(name: "mongo_data")
                    .WithVolume(name: "mongo_config", "/data/configdb")
                    .WithLifetime(ContainerLifetime.Persistent);

IResourceBuilder<MongoDatabaseWithReplicaSetResource> mongodb = mongo.AddDatabase("mongodb");

IResourceBuilder<ApachePulsarResource> pulsar = builder.AddApachePulsar("pulsar", httpPort: 8080, pulsarPort: 6650);
// .WithVolume(name: "pulsardata", target: "/pulsar/data")
// .WithVolume(name: "pulsarconf", target: "/pulsar/conf");

IResourceBuilder<ProjectResource> apiService = builder.AddProject<Projects.DotnetTelemetryPlayground_ApiService>("ApiService")
                                                        .WithReference(pulsar)
                                                        .WithReference(mongodb)
                                                        .WaitFor(mongo)
                                                        .WaitFor(mongodb)
                                                        .WaitFor(pulsar);

IResourceBuilder<ProjectResource> frontendApi = builder.AddProject<Projects.DotnetTelemetryPlayground_ApiServiceAtFront>("FrontendApi")
                                                        .WithReference(apiService)
                                                        .WithReference(pulsar)
                                                        .WaitFor(pulsar)
                                                        .WaitFor(apiService);

// parse command-line arguments
rootCommand.SetAction(parseResult =>
{
    OptionResult? useElkValue = parseResult.GetResult(useElk);
    if (useElkValue != null && !useElkValue.Implicit && useElkValue.GetValueOrDefault<bool>()) {
        string? v_OTEL_EXPORTER_OTLP_ENDPOINT = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", EnvironmentVariableTarget.User);
        string? v_OTEL_EXPORTER_OTLP_HEADERS = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", EnvironmentVariableTarget.User);

        // check if environment variables are set, otherwise use default values
        if( !string.IsNullOrEmpty(v_OTEL_EXPORTER_OTLP_ENDPOINT) && !string.IsNullOrEmpty(v_OTEL_EXPORTER_OTLP_HEADERS) ) {
            apiService.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", v_OTEL_EXPORTER_OTLP_ENDPOINT)     // base endpoint;
                    .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", v_OTEL_EXPORTER_OTLP_HEADERS);      // api key for authentication;

            frontendApi.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", v_OTEL_EXPORTER_OTLP_ENDPOINT)     // base endpoint;
                        .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", v_OTEL_EXPORTER_OTLP_HEADERS);   // api key for authentication;
        }        
    }
});
rootCommand.Parse(args).Invoke();

// build and run the application
builder.Build().Run();
