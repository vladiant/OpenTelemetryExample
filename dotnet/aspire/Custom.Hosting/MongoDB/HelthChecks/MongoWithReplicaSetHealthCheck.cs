using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Custom.Hosting.MongoDB.HelthChecks;

public class MongoWithReplicaSetHealthCheck(IMongoClient client, string? databaseName = default) : IHealthCheck
{

    private const int MAX_ATTEMPTS = 3;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try {

            for (int attempt = 0; attempt <= MAX_ATTEMPTS; attempt++) {
                try {
                    if (string.IsNullOrEmpty(databaseName)) {
                        using var cursor = await client.ListDatabaseNamesAsync(cancellationToken).ConfigureAwait(false);
                        await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                    } else {
                        IMongoDatabase mDb = client.GetDatabase(databaseName);
                        using var cursor = await mDb.ListCollectionNamesAsync(cancellationToken: cancellationToken);
                        _ = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                    }

                    break;
                } catch (MongoNotPrimaryException) {
                    // Do it some timnes
                    if (attempt <= MAX_ATTEMPTS) {
                        if (string.IsNullOrEmpty(databaseName) && !string.IsNullOrEmpty(client.Settings.ReplicaSetName)) {
                            BsonDocument config = new BsonDocument(
                                "replSetInitiate", new BsonDocument
                                {
                                    { "_id", client.Settings.ReplicaSetName},
                                    { "members", new BsonArray
                                        {
                                            new BsonDocument { { "_id", 0 }, { "host", "127.0.0.1:27017" } } // This is only used for local aspire initiation
                                        }
                                    }
                                }
                            );
                            IMongoDatabase mDb = client.GetDatabase("admin");
                            BsonDocument result = await mDb.RunCommandAsync<BsonDocument>(config);
                        }
                    } else {
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                } catch (Exception) {
                    if (MAX_ATTEMPTS < attempt) {
                        throw;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                await Task.Delay(250 * (attempt + 1));
            }

            return HealthCheckResult.Healthy();
        } catch (Exception ex) {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
