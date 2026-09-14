namespace Products.Tests.Unit.Controllers;

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Moq;
using Products.Controllers;
using Products.HostedServices;
using Products.Models;
using Products.Tests.Unit.TestSupport;

public class CatalogProductsControllerTests
{
    private readonly Mock<IMongoCollection<CatalogProduct>> _mockCollection;
    private readonly Mock<IMongoCollection<InventoryItem>> _mockInventoryItems;
    private readonly CatalogProductsController _controller;

    public CatalogProductsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<CatalogProduct>>(MockBehavior.Strict);
        _mockInventoryItems = new Mock<IMongoCollection<InventoryItem>>(MockBehavior.Strict);
        var mockDatabase = new Mock<IMongoDatabase>(MockBehavior.Strict);
        mockDatabase
            .Setup(d => d.GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName, null))
            .Returns(_mockCollection.Object);
        mockDatabase
            .Setup(d => d.GetCollection<InventoryItem>(InventoryItemIndexInitializer.CollectionName, null))
            .Returns(_mockInventoryItems.Object);
        _controller = new CatalogProductsController(mockDatabase.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByKey_ReturnsEmptySingleResult_WhenTheCatalogProductDoesNotExist()
    {
        var missingCatalogProductId = Guid.NewGuid();
        SetupFindReturns([]);

        var result = await _controller.Get(missingCatalogProductId, TestContext.Current.CancellationToken);

        Assert.Empty(result.Queryable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_ComputesTheMatchKey_SoTheUniqueIndexCanDeduplicate()
    {
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();
        SetupInsertSucceeds();
        var input = new CatalogProduct
        {
            Name = TestValues.NewProductName(),
            Brand = brand,
            ModelNumber = modelNumber,
        };

        var result = await _controller.Post(input, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedODataResult<CatalogProduct>>(result);
        Assert.Equal(CatalogProductMatchKey.Compute(brand, modelNumber), input.MatchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_LeavesTheMatchKeyNull_WhenTheBrandIsMissing()
    {
        SetupInsertSucceeds();
        var input = new CatalogProduct
        {
            Name = TestValues.NewProductName(),
            ModelNumber = TestValues.NewModelNumber(),
        };

        await _controller.Post(input, TestContext.Current.CancellationToken);

        Assert.Null(input.MatchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_NeverTrustsAClientSuppliedMatchKey()
    {
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();
        var spoofedMatchKey = TestValues.NewModelNumber();
        SetupInsertSucceeds();
        var input = new CatalogProduct
        {
            Brand = brand,
            ModelNumber = modelNumber,
            MatchKey = spoofedMatchKey,
        };

        await _controller.Post(input, TestContext.Current.CancellationToken);

        Assert.NotEqual(spoofedMatchKey, input.MatchKey);
        Assert.Equal(CatalogProductMatchKey.Compute(brand, modelNumber), input.MatchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsNotFound_WhenTheCatalogProductDoesNotExist()
    {
        SetupFindReturns([]);
        var catalogProductId = Guid.NewGuid();

        var result = await _controller.Patch(
            catalogProductId,
            new Delta<CatalogProduct>(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_DoesNotTurnAnUnrelatedWriteErrorIntoAConflict()
    {
        var catalogProductId = Guid.NewGuid();
        var existing = new CatalogProduct
        {
            Id = catalogProductId,
            Name = TestValues.NewProductName(),
            Brand = TestValues.NewBrand(),
            ModelNumber = TestValues.NewModelNumber(),
        };
        SetupFindReturns([existing]);
        _mockCollection
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<CatalogProduct>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(WriteExceptionWithoutADuplicateKey());

        await Assert.ThrowsAsync<MongoWriteException>(() => _controller.Patch(
            existing.Id,
            new Delta<CatalogProduct>(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_DoesNotTurnAnUnrelatedWriteErrorIntoAConflict()
    {
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<CatalogProduct>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(WriteExceptionWithoutADuplicateKey());
        var input = new CatalogProduct
        {
            Name = TestValues.NewProductName(),
            Brand = TestValues.NewBrand(),
            ModelNumber = TestValues.NewModelNumber(),
        };

        await Assert.ThrowsAsync<MongoWriteException>(() =>
            _controller.Post(input, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsConflict_WhenAnInventoryItemStillReferencesTheCatalogProduct()
    {
        var referencedCatalogProductId = Guid.NewGuid();
        SetupInventoryItemsFindReturns([new BsonDocument()]);

        var result = await _controller.Delete(referencedCatalogProductId, TestContext.Current.CancellationToken);

        Assert.IsType<ConflictResult>(result);
        _mockCollection.Verify(
            c => c.FindOneAndDeleteAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<FindOneAndDeleteOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNotFound_WhenNoCatalogProductMatched()
    {
        var missingCatalogProductId = Guid.NewGuid();
        SetupInventoryItemsFindReturns([]);
        SetupFindOneAndDeleteMatchesNothing();

        var result = await _controller.Delete(missingCatalogProductId, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNoContent_WhenTheCatalogProductWasDeleted()
    {
        var catalogProductId = Guid.NewGuid();
        SetupInventoryItemsFindReturns([]);
        SetupFindOneAndDeleteReturns(new CatalogProduct
        {
            Id = catalogProductId,
            Name = TestValues.NewProductName(),
            Brand = TestValues.NewBrand(),
            ModelNumber = TestValues.NewModelNumber(),
        });

        var result = await _controller.Delete(catalogProductId, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    private static MongoWriteException WriteExceptionWithoutADuplicateKey()
    {
        var serverId = new ServerId(new ClusterId(), new DnsEndPoint(MongoEndpointConstants.LoopbackHost, MongoEndpointConstants.DefaultPort));
        return new MongoWriteException(new ConnectionId(serverId), null, null, null);
    }

    private void SetupInsertSucceeds() =>
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<CatalogProduct>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupInventoryItemsFindReturns(IList<BsonDocument> referencingItems)
    {
        var mockCursor = new Mock<IAsyncCursor<BsonDocument>>(MockBehavior.Strict);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(referencingItems.Count > 0)
            .ReturnsAsync(false);
        mockCursor
            .Setup(c => c.Current)
            .Returns(referencingItems);
        mockCursor.Setup(c => c.Dispose());
        _mockInventoryItems
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<InventoryItem>>(),
                It.IsAny<FindOptions<InventoryItem, BsonDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }

    private void SetupFindOneAndDeleteReturns(CatalogProduct deleted) =>
        _mockCollection
            .Setup(c => c.FindOneAndDeleteAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<FindOneAndDeleteOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleted);

    private void SetupFindOneAndDeleteMatchesNothing() =>
        _mockCollection
            .Setup(c => c.FindOneAndDeleteAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<FindOneAndDeleteOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(CatalogProduct));

    private void SetupFindReturns(IList<CatalogProduct> catalogProducts)
    {
        var mockCursor = new Mock<IAsyncCursor<CatalogProduct>>(MockBehavior.Strict);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalogProducts.Count > 0)
            .ReturnsAsync(false);
        mockCursor
            .Setup(c => c.Current)
            .Returns(catalogProducts);
        mockCursor.Setup(c => c.Dispose());
        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<FindOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }
}