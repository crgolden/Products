namespace Products.Tests.Integration;

using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Products.HostedServices;
using Products.Models;
using Products.Tests.Integration.Infrastructure;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CatalogProductIndexMigrationTests
{
    private readonly IMongoCollection<CatalogProduct> _catalogProducts;

    public CatalogProductIndexMigrationTests(ProductsWebApplicationFactory factory)
    {
        _catalogProducts = factory.Services
            .GetRequiredService<IMongoDatabase>()
            .GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName);
    }

    [Fact]
    public async Task TwoRowsWithNoComputableMatchKeyCanCoexist()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        Guid[] insertedIds = [firstId, secondId];
        var inserted = Builders<CatalogProduct>.Filter.In(c => c.Id, insertedIds);

        await _catalogProducts.InsertOneAsync(
            NewRowWithNoComputableMatchKey(firstId),
            cancellationToken: TestContext.Current.CancellationToken);
        await _catalogProducts.InsertOneAsync(
            NewRowWithNoComputableMatchKey(secondId),
            cancellationToken: TestContext.Current.CancellationToken);

        var surviving = await _catalogProducts.CountDocumentsAsync(
            inserted,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(insertedIds.LongLength, surviving);
    }

    private static CatalogProduct NewRowWithNoComputableMatchKey(Guid id) => new()
    {
        Id = id,
        Name = Generated.NewProductName(),
        Brand = Generated.NewBrand(),
    };
}
