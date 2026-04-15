namespace Products.Tests.Controllers;

using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Products.Controllers;
using Products.Models;
using MongoDB.Driver;

// Note: Get() (list) is not unit-tested here because AsQueryable() is a MongoDB driver
// extension that requires a live session/queryable provider. Cover it with integration tests.
public class ProductsControllerTests
{
    private readonly Mock<IMongoCollection<Product>> _mockCollection;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<Product>>();
        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase
            .Setup(d => d.GetCollection<Product>("Products", null))
            .Returns(_mockCollection.Object);
        _controller = new ProductsController(mockDatabase.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByKey_ReturnsEmptySingleResult_WhenProductDoesNotExist()
    {
        SetupFindReturns([]);
        var result = await _controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Empty(result.Queryable);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByKey_ReturnsSingleResult_WhenProductExists()
    {
        var product = MakeProduct();
        SetupFindReturns([product]);
        var result = await _controller.Get(product.Id, TestContext.Current.CancellationToken);
        Assert.IsType<SingleResult<Product>>(result);
        Assert.Single(result.Queryable);
        Assert.Equal(product.Id, result.Queryable.First().Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_ReturnsCreated_AndSetsId()
    {
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var input = new Product { Name = "Widget", Price = 1.99m };
        var result = await _controller.Post(input, TestContext.Current.CancellationToken);
        Assert.IsType<CreatedODataResult<Product>>(result);
        Assert.NotEqual(Guid.Empty, input.Id);
        Assert.NotEqual(default, input.CreatedAt);
        Assert.Null(input.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_CallsInsertOneAsync()
    {
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await _controller.Post(new Product { Name = "Widget", Price = 1.99m }, TestContext.Current.CancellationToken);
        _mockCollection.Verify(
            c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Put_ReturnsNotFound_WhenProductDoesNotExist()
    {
        SetupFindReturns([]);
        var result = await _controller.Put(Guid.NewGuid(), new Product(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Put_ReturnsUpdated_AndCallsReplaceOne_WhenProductExists()
    {
        var existing = MakeProduct();
        SetupFindReturns([existing]);
        _mockCollection
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<Product>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
        var update = new Product { Name = "Updated", Price = 5.00m };
        var result = await _controller.Put(existing.Id, update, TestContext.Current.CancellationToken);
        Assert.IsType<UpdatedODataResult<Product>>(result);
        _mockCollection.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.Is<Product>(p => p.Id == existing.Id && p.CreatedAt == existing.CreatedAt),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsNotFound_WhenProductDoesNotExist()
    {
        SetupFindReturns([]);
        var result = await _controller.Patch(Guid.NewGuid(), new Delta<Product>(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsUpdated_AndCallsReplaceOne_WhenProductExists()
    {
        var existing = MakeProduct();
        SetupFindReturns([existing]);
        _mockCollection
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<Product>(),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReplaceOneResult.Acknowledged(1, 1, null));
        var delta = new Delta<Product>();
        delta.TrySetPropertyValue(nameof(Product.Name), "Patched");
        var result = await _controller.Patch(existing.Id, delta, TestContext.Current.CancellationToken);
        Assert.IsType<UpdatedODataResult<Product>>(result);
        _mockCollection.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.Is<Product>(p => p.Name == "Patched"),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNotFound_WhenProductDoesNotExist()
    {
        _mockCollection
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(0));
        var result = await _controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNoContent_WhenProductDeleted()
    {
        _mockCollection
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));
        var result = await _controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.IsType<NoContentResult>(result);
    }

    private static Product MakeProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Widget",
        Price = 9.99m,
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
    };

    private void SetupFindReturns(IList<Product> products)
    {
        var mockCursor = new Mock<IAsyncCursor<Product>>();
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.Count > 0)
            .ReturnsAsync(false);
        mockCursor
            .Setup(c => c.Current)
            .Returns(products);
        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<FindOptions<Product, Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }
}
