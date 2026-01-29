namespace Aspire.Hosting.ApplicationModel;

public sealed class ApachePulsarResource(string name) : ContainerResource(name), IResourceWithConnectionString
{

    // Apache Pulsar endpoint names
    // Pulsar endpoint name
    internal const string PulsarEndpointName = "pulsar";
    // Http endpoint name
    internal const string HttpEndpointName = "http";

    private EndpointReference? _pulsarReference;

    public EndpointReference PulsarEndpoint =>
        _pulsarReference ??= new(this, PulsarEndpointName);

    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
            $"{PulsarEndpointName}://{PulsarEndpoint.Property(EndpointProperty.HostAndPort)}"
        );
}
