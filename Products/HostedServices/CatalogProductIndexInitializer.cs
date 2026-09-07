namespace Products.HostedServices;

using Models;
using MongoDB.Driver;

public sealed class CatalogProductIndexInitializer : BackgroundService
{
    public const string CollectionName = "CatalogProducts";

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
                    Unique = true,
                    PartialFilterExpression = Builders<CatalogProduct>.Filter.Exists(c => c.MatchKey),
                }),
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await collection.Indexes.CreateManyAsync(indexModels, stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Telemetry.Metrics.IndexCreationFailed(ex);
            }

            await Task.Delay(_retryDelay, stoppingToken);
        }
    }
}
