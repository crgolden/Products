namespace Products.Tests.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Moq;
using Products.Controllers;
using Products.Models;
using MongoDB.Driver;

// Note: Get() (list) is not unit-tested here because AsQueryable() is a MongoDB driver
// extension that requires a live session/queryable provider. Cover it with integration tests.
public class ProductsControllerTests
{
    private readonly Mock<IMongoCollection<Product>> _mockCollection;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<Product>>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        var mockDatabase = new Mock<IMongoDatabase>();
        mockDatabase
            .Setup(d => d.GetCollection<Product>("Products", null))
            .Returns(_mockCollection.Object);
        _controller = new ProductsController(mockDatabase.Object, _mockAuthorizationService.Object);
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
    public async Task Post_ReturnsCreated_AndSetsIdAndOwnerId()
    {
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
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
        Assert.Equal(ownerId, input.OwnerId);
        Assert.Null(input.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_CallsInsertOneAsync()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
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
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        SetupFindReturns([]);
        var result = await _controller.Put(Guid.NewGuid(), new Product(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Put_ReturnsForbid_WhenNotOwner()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        var existing = MakeProduct(ownerId: Guid.NewGuid());
        SetupFindReturns([existing]);
        SetupAuthorizationFails();
        var result = await _controller.Put(existing.Id, new Product(), TestContext.Current.CancellationToken);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Put_ReturnsUpdated_AndPreservesOwnerIdAndCreatedAt_WhenOwner()
    {
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        var existing = MakeProduct(ownerId: ownerId);
        SetupFindReturns([existing]);
        SetupAuthorizationSucceeds();
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
                It.Is<Product>(p =>
                    p.Id == existing.Id &&
                    p.CreatedAt == existing.CreatedAt &&
                    p.OwnerId == ownerId),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsNotFound_WhenProductDoesNotExist()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        SetupFindReturns([]);
        var result = await _controller.Patch(Guid.NewGuid(), new Delta<Product>(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsForbid_WhenNotOwner()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        var existing = MakeProduct(ownerId: Guid.NewGuid());
        SetupFindReturns([existing]);
        SetupAuthorizationFails();
        var result = await _controller.Patch(existing.Id, new Delta<Product>(), TestContext.Current.CancellationToken);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsUpdated_AndPreservesOwnerId_WhenOwner()
    {
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        var existing = MakeProduct(ownerId: ownerId);
        SetupFindReturns([existing]);
        SetupAuthorizationSucceeds();
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
                It.Is<Product>(p => p.Name == "Patched" && p.OwnerId == ownerId),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNotFound_WhenProductDoesNotExist()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        SetupFindReturns([]);
        var result = await _controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsForbid_WhenNotOwner()
    {
        _controller.ControllerContext = MakeControllerContext(Guid.NewGuid());
        var existing = MakeProduct(ownerId: Guid.NewGuid());
        SetupFindReturns([existing]);
        SetupAuthorizationFails();
        var result = await _controller.Delete(existing.Id, TestContext.Current.CancellationToken);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNoContent_WhenOwner()
    {
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        var existing = MakeProduct(ownerId: ownerId);
        SetupFindReturns([existing]);
        SetupAuthorizationSucceeds();
        _mockCollection
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(1));
        var result = await _controller.Delete(existing.Id, TestContext.Current.CancellationToken);
        Assert.IsType<NoContentResult>(result);
    }

    private static Product MakeProduct(Guid? ownerId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Widget",
        Price = 9.99m,
        OwnerId = ownerId ?? Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
    };

    private static ControllerContext MakeControllerContext(Guid? userId = null)
    {
        var claims = userId.HasValue
            ? new[] { new Claim("sub", userId.Value.ToString()) }
            : [];
        var identity = new ClaimsIdentity(claims, authenticationType: userId.HasValue ? "Bearer" : null);
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        return new ControllerContext { HttpContext = httpContext };
    }

    private void SetupAuthorizationSucceeds()
    {
        _mockAuthorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
    }

    private void SetupAuthorizationFails()
    {
        _mockAuthorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
    }

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
