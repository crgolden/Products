namespace Products.Tests.Integration.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Products.HostedServices;

public sealed class ProductsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal static readonly string TestScheme = Guid.NewGuid().ToString();

    internal static readonly Guid TestUserId = Guid.NewGuid();

    private static readonly string[] NonSeededCollections =
        [InventoryItemIndexInitializer.CollectionName, CatalogProductIndexInitializer.CollectionName];

    private bool _hostCreated;

    public string? RefusedDatabase { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await DeleteEveryDocumentInTheTestDatabaseAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_hostCreated)
        {
            await DeleteEveryDocumentInTheTestDatabaseAsync();
        }

        await base.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        _hostCreated = true;
        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            var databaseName = context.Configuration[MongoSettingKeys.DatabaseName];
            if (databaseName is null || !databaseName.EndsWith(TestDatabaseContractConstants.TestDatabaseSuffix, StringComparison.Ordinal))
            {
                RefusedDatabase = databaseName;
                throw new InvalidOperationException(
                    $"The integration tier writes to the database it is given, so it refuses '{databaseName}': {MongoSettingKeys.DatabaseName} must end in '{TestDatabaseContractConstants.TestDatabaseSuffix}'.");
            }

            services.AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, IntegrationAuthHandler>(TestScheme, _ => { });
        });
    }

    private async Task DeleteEveryDocumentInTheTestDatabaseAsync()
    {
        var database = Services.GetRequiredService<IMongoDatabase>();
        if (!database.DatabaseNamespace.DatabaseName.EndsWith(TestDatabaseContractConstants.TestDatabaseSuffix, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var collectionName in NonSeededCollections)
        {
            await database.GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, CancellationToken.None);
        }
    }
}
