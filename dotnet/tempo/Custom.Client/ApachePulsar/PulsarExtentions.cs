using Custom.Client.ApachePulsar;
using DotPulsar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

public static class PulsarExtentions
{
    public static void AddPulsarClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<PulsarSettings>? configureSettings = null) =>
        AddPulsarClient(
            builder,
            PulsarSettings.ConfigurationSectionName,
            configureSettings,
            connectionName,
            serviceKey: null);

    public static void AddKeyedPulsarClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<PulsarSettings>? configureSettings = null)
    {
        if (string.IsNullOrEmpty(name)) {
            throw new Exception("Invalid keyed name");
        }

        AddPulsarClient(
            builder,
            $"{PulsarSettings.ConfigurationSectionName}:{name}",
            configureSettings,
            name,
            serviceKey: name);
    }

    private static void AddPulsarClient(
        this IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<PulsarSettings>? configureSettings,
        string connectionName,
        object? serviceKey)
    {
        PulsarSettings settings = new();

        builder.Configuration
               .GetSection(configurationSectionName)
               .Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString) {
            settings.ParseConnectionString(connectionString);
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is null) {
            builder.Services.AddScoped(sp =>
            {
                return PulsarClient.Builder()
                                   .ServiceUrl(settings.Endpoint!)
                                   .Build();
            });
        } else {
            builder.Services.AddKeyedScoped(serviceKey, (sp, key) =>
            {
                return PulsarClient.Builder()
                                   .ServiceUrl(settings.Endpoint!)
                                   .Build();
            });
        }

        if (!settings.DisableTracing) {
            builder.Services.AddOpenTelemetry()
                            .WithTracing(traceBuilder => traceBuilder.AddSource("DotPulsar"));
        }

        if (!settings.DisableMetrics) {
            builder.Services.AddOpenTelemetry()
                            .WithMetrics(metricsBuilder => metricsBuilder.AddMeter("DotPulsar"));
        }
    }
}
