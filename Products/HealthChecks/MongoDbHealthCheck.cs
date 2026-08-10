namespace Products.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly IMongoDatabase _database;

    public MongoDbHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        Exception lastException;
        var attempt = 0;
        do
        {
            attempt++;
            try
            {
                var document = new BsonDocument("ping", 1);
                var command = new BsonDocumentCommand<BsonDocument>(document);
                await _database.RunCommandAsync(command, cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy("Connected");
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
            }
        }
        while (attempt < MaxAttempts);

        return HealthCheckResult.Unhealthy(lastException.Message, lastException);
    }
}
