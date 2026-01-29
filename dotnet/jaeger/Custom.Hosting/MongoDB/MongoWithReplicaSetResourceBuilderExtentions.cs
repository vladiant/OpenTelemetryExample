
using Aspire.Hosting.ApplicationModel;
using Custom.Hosting.MongoDB.HelthChecks;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Aspire.Hosting;

public static class MongoWithReplicaSetResourceBuilderExtentions
{
    public static IResourceBuilder<MongoWithReplicaSetResource> AddMongoWithReplicaSet(
        this IDistributedApplicationBuilder builder,
        string name,
        int? mongoPort = null)
    {
        // Set the resource name
        var resource = new MongoWithReplicaSetResource(name);

        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, ct) => {
            connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
            if (connectionString == null) {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }
        });

        var healthCheckKey = $"{name}_check";
        IMongoClient? client = null;
        builder.Services.AddHealthChecks()
                        .AddMongoWithReplicaSetHealthCheck(
                            sp => client ??= new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable")),
                            name: healthCheckKey);

        return builder.AddResource(resource)
                      .WithImage(MongoContainerImageTags.Image)
                      .WithImageRegistry(MongoContainerImageTags.Registry)
                      .WithImageTag(MongoContainerImageTags.Tag)
                      .WithEndpoint(
                          targetPort: 27017,
                          port: mongoPort,
                          name: MongoWithReplicaSetResource.MongoEndpointName)
                      .WithArgs(
                      [
                          "--replSet", "rs0"
                      ])
                      .WithHealthCheck(healthCheckKey);
    }

    public static IResourceBuilder<MongoWithReplicaSetResource> WithDataVolume(this IResourceBuilder<MongoWithReplicaSetResource> builder, string name)
    {
        return builder.WithVolume(name, "/data/db", false);
    }

    public static IResourceBuilder<MongoWithReplicaSetResource> WithDataBindMount(this IResourceBuilder<MongoWithReplicaSetResource> builder, string source)
    {
        return builder.WithBindMount(source, "/data/db");
    }

    public static IResourceBuilder<MongoDatabaseWithReplicaSetResource> AddDatabase(this IResourceBuilder<MongoWithReplicaSetResource> builder, [ResourceName] string name, string? databaseName = null)
    {
        // Use the resource name as the database name if it's not provided
        databaseName ??= name;

        // Create the MongoDB database resource
        var mongoDBDatabase = new MongoDatabaseWithReplicaSetResource(name, databaseName, builder.Resource);

        string? connectionString = null;
        builder.ApplicationBuilder.Eventing.Subscribe<ConnectionStringAvailableEvent>(mongoDBDatabase, async (@event, ct) => {
            connectionString = await mongoDBDatabase.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
            if (connectionString == null) {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{mongoDBDatabase.Name}' resource but the connection string was null.");
            }
        });

        var healthCheckKey = $"{name}_check_db";
        IMongoDatabase? database = null;
        builder.ApplicationBuilder.Services.AddHealthChecks()
                                            .AddMongoDbWithReplicaSetHealthCheck(
                                                sp => database ??= new MongoClient(connectionString ?? throw new InvalidOperationException("Connection string is unavailable")).GetDatabase(databaseName),
                                                name: healthCheckKey);

        return builder.ApplicationBuilder
                      .AddResource(mongoDBDatabase)
                      .WithHealthCheck(healthCheckKey);
    }
}

internal static class MongoContainerImageTags
{
    internal const string Registry = "docker.io";

    internal const string Image = "mongo";

    internal const string Tag = "noble";
}
