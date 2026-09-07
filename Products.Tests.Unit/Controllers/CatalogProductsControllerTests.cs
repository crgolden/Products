namespace Products.Tests.Unit.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using MongoDB.Driver;
using Moq;
using Products.Controllers;
using Products.HostedServices;
using Products.Models;
using Products.Tests.Unit.TestSupport;

public class CatalogProductsControllerTests
{
    private readonly Mock<IMongoCollection<CatalogProduct>> _mockCollection;
    private readonly CatalogProductsController _controller;

    public CatalogProductsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<CatalogProduct>>(MockBehavior.Strict);
        var mockDatabase = new Mock<IMongoDatabase>(MockBehavior.Strict);
        mockDatabase
            .Setup(d => d.GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName, null))
            .Returns(_mockCollection.Object);
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

        var result = await _controller.Patch(
            Guid.NewGuid(),
            new Delta<CatalogProduct>(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    private void SetupInsertSucceeds() =>
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<CatalogProduct>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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