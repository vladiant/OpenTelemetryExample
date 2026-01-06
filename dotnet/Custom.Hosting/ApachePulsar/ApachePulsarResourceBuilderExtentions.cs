using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class ApachePulsarResourceBuilderExtentions
{
    public static IResourceBuilder<ApachePulsarResource> AddApachePulsar(
        this IDistributedApplicationBuilder builder,
        string name,
        int? httpPort = null,
        int? pulsarPort = null)
    {
        // The AddResource method is a core API within .NET Aspire and is
        // used by resource developers to wrap a custom resource in an
        // IResourceBuilder<T> instance. Extension methods to customize
        // the resource (if any exist) target the builder interface.
        var resource = new ApachePulsarResource(name);

        return builder.AddResource(resource)
                      .WithImage(PulsarContainerImageTags.Image)
                      .WithImageRegistry(PulsarContainerImageTags.Registry)
                      .WithImageTag(PulsarContainerImageTags.Tag)
                      .WithHttpEndpoint(
                          targetPort: 8080,
                          port: httpPort,
                          name: ApachePulsarResource.HttpEndpointName)
                      .WithEndpoint(
                          targetPort: 6650,
                          port: pulsarPort,
                          name: ApachePulsarResource.PulsarEndpointName)
                      .WithEntrypoint("bin/pulsar")
                      .WithArgs([
                          "standalone"
                      ])
                      .WithHttpHealthCheck("/admin/v2/brokers/leaderBroker");
    }
}

internal static class PulsarContainerImageTags
{
    internal const string Registry = "docker.io";

    internal const string Image = "apachepulsar/pulsar";

    internal const string Tag = "4.1.1";
}
