namespace Products.HostedServices;

using Models;
using MongoDB.Bson;
using MongoDB.Driver;

public sealed class CatalogProductIndexInitializer : BackgroundService
{
    public const string CollectionName = "CatalogProducts";

    internal const string ComputableMatchKeyIndexName = "MatchKey_computable_unique";

    internal const string SupersededMatchKeyIndexName = "MatchKey_1";

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IMongoDatabase _database;
    private readonly TimeSpan _retryDelay;

    public CatalogProductIndexInitializer(IMongoDatabase database, TimeSpan? retryDelay = null)
    {
        _database = database;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var collection = _database.GetCollection<CatalogProduct>(CollectionName);
        var indexModels = new[]
        {
            new CreateIndexModel<CatalogProduct>(Builders<CatalogProduct>.IndexKeys.Ascending(c => c.Name)),
            new CreateIndexModel<CatalogProduct>(Builders<CatalogProduct>.IndexKeys.Descending(c => c.CreatedAt)),
            new CreateIndexModel<CatalogProduct>(
                Builders<CatalogProduct>.IndexKeys.Ascending(c => c.MatchKey),
                new CreateIndexOptions<CatalogProduct>
                {
                    Name = ComputableMatchKeyIndexName,
                    Unique = true,
                    PartialFilterExpression =
                        Builders<CatalogProduct>.Filter.Type(c => c.MatchKey, BsonType.String),
                }),
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await collection.Indexes.CreateManyAsync(indexModels, stoppingToken);
                await DropSupersededMatchKeyIndexAsync(collection, stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Telemetry.Metrics.IndexCreationFailed(ex);
            }

            await Task.Delay(_retryDelay, stoppingToken);
        }
    }

    private static async Task DropSupersededMatchKeyIndexAsync(
        IMongoCollection<CatalogProduct> collection,
        CancellationToken cancellationToken)
    {
        using var cursor = await collection.Indexes.ListAsync(cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);
        if (indexes.Any(index => index["name"] == SupersededMatchKeyIndexName))
        {
            await collection.Indexes.DropOneAsync(SupersededMatchKeyIndexName, cancellationToken);
        }
    }
}
