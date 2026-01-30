namespace Aspire.Hosting.ApplicationModel;

public class MongoDatabaseWithReplicaSetResource(string name, string databaseName, MongoWithReplicaSetResource parent)
    : Resource(name), IResourceWithParent<MongoWithReplicaSetResource>, IResourceWithConnectionString
{
    public MongoWithReplicaSetResource Parent => parent;

    public ReferenceExpression ConnectionStringExpression => Parent.BuildConnectionString(DatabaseName);

    public string DatabaseName { get; } = databaseName;
}
