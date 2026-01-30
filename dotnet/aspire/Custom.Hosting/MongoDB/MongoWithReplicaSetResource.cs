namespace Aspire.Hosting.ApplicationModel;

public class MongoWithReplicaSetResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    // Mongo endpoint name
    internal const string MongoEndpointName = "mongodb";

    private EndpointReference? _pulsarReference;

    public EndpointReference MongoEndpoint =>
        _pulsarReference ??= new(this, MongoEndpointName);

    public ReferenceExpression ConnectionStringExpression => BuildConnectionString();

    internal ReferenceExpression BuildConnectionString(string? databaseName = null)
    {
        if (string.IsNullOrEmpty(databaseName)) {
            return ReferenceExpression.Create(
                $"{MongoEndpointName}://{MongoEndpoint.Property(EndpointProperty.HostAndPort)}?replicaSet=rs0&directConnection=true&w=majority&journal=true&readConcernLevel=majority"
            );
        } else {
            return ReferenceExpression.Create(
                $"{MongoEndpointName}://{MongoEndpoint.Property(EndpointProperty.HostAndPort)}/{databaseName}?replicaSet=rs0&directConnection=true&w=majority&journal=true&readConcernLevel=majority"
            );
        }
    }
}
