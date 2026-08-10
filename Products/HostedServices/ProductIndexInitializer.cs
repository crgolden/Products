namespace Products.HostedServices;

using Models;
using MongoDB.Driver;

public sealed class ProductIndexInitializer : BackgroundService
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IMongoDatabase _database;
    private readonly ILogger<ProductIndexInitializer> _logger;
    private readonly TimeSpan _retryDelay;

    public ProductIndexInitializer(IMongoDatabase database, ILogger<ProductIndexInitializer> logger, TimeSpan? retryDelay = null)
    {
        _database = database;
        _logger = logger;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var collection = _database.GetCollection<Product>("Products");
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
                _logger.LogError(ex, "Could not create Product indexes; retrying in {RetryDelay}", _retryDelay);
            }

            await Task.Delay(_retryDelay, stoppingToken);
        }
    }
}
