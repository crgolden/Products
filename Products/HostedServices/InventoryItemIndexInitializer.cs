namespace Products.HostedServices;

using Models;
using MongoDB.Driver;

public sealed class InventoryItemIndexInitializer : BackgroundService
{
    public const string CollectionName = "InventoryItems";

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IMongoDatabase _database;
    private readonly TimeSpan _retryDelay;

    public InventoryItemIndexInitializer(IMongoDatabase database, TimeSpan? retryDelay = null)
    {
        _database = database;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var collection = _database.GetCollection<InventoryItem>(CollectionName);
        var indexModels = new[]
        {
            new CreateIndexModel<InventoryItem>(Builders<InventoryItem>.IndexKeys.Ascending(i => i.OwnerId)),
            new CreateIndexModel<InventoryItem>(Builders<InventoryItem>.IndexKeys.Ascending(i => i.CatalogProductId)),
            new CreateIndexModel<InventoryItem>(Builders<InventoryItem>.IndexKeys.Descending(i => i.CreatedAt)),
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
