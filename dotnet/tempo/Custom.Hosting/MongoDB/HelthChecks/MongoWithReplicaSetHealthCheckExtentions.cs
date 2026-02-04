
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace Custom.Hosting.MongoDB.HelthChecks;

public static class MongoWithReplicaSetHealthCheckExtentions
{
    private const string NAME = "mongodb";

    public static IHealthChecksBuilder AddMongoWithReplicaSetHealthCheck(this IHealthChecksBuilder builder,
        Func<IServiceProvider, IMongoClient>? clientFactory = default,
        Func<IServiceProvider, string>? databaseNameFactory = default,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {
        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp => Factory(sp, clientFactory, databaseNameFactory),
            failureStatus,
            tags,
            timeout));

        static MongoWithReplicaSetHealthCheck Factory(IServiceProvider sp, Func<IServiceProvider, IMongoClient>? clientFactory, Func<IServiceProvider, string>? databaseNameFactory)
        {
            // The user might have registered a factory for MongoClient type, but not for the abstraction (IMongoClient).
            // That is why we try to resolve MongoClient first.
            IMongoClient client = clientFactory?.Invoke(sp) ?? sp.GetService<MongoClient>() ?? sp.GetRequiredService<IMongoClient>();
            string? databaseName = databaseNameFactory?.Invoke(sp);
            return new(client, databaseName);
        }
    }

    public static IHealthChecksBuilder AddMongoDbWithReplicaSetHealthCheck(
        this IHealthChecksBuilder builder,
        Func<IServiceProvider, IMongoDatabase> dbFactory,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
    {

        return builder.Add(new HealthCheckRegistration(
            name ?? NAME,
            sp => {
                IMongoDatabase db = dbFactory.Invoke(sp);
                return new MongoWithReplicaSetHealthCheck(db.Client, db.DatabaseNamespace.DatabaseName);
            },
            failureStatus,
            tags,
            timeout));
    }
}
