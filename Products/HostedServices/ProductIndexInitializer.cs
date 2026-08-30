namespace Products.HostedServices;

using Models;
using MongoDB.Driver;

public sealed class ProductIndexInitializer : BackgroundService
{
    public const string CollectionName = "Products";

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IMongoDatabase _database;
    private readonly TimeSpan _retryDelay;

    public ProductIndexInitializer(IMongoDatabase database, TimeSpan? retryDelay = null)
    {
        _database = database;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var collection = _database.GetCollection<Product>(CollectionName);
        var indexModels = new[]
        {
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.Name)),
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Descending(p => p.CreatedAt)),
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.OwnerId)),
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
