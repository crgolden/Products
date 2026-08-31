namespace Products.Tests.Unit.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using MongoDB.Driver;
using Moq;
using Products.Controllers;
using Products.Models;
using Products.Tests.Unit.TestSupport;

public class ProductsControllerTests
{
    private readonly Mock<IMongoCollection<Product>> _mockCollection;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<Product>>(MockBehavior.Strict);
        _mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var mockDatabase = new Mock<IMongoDatabase>(MockBehavior.Strict);
        mockDatabase
            .Setup(d => d.GetCollection<Product>("Products", null))
            .Returns(_mockCollection.Object);
        _controller = new ProductsController(mockDatabase.Object, _mockAuthorizationService.Object);
    }

    public enum WriteOperation
    {
        Put,
        Patch,
        Delete,
    }

    public static TheoryData<WriteOperation> WriteOperations() => new()
    {
        WriteOperation.Put,
        WriteOperation.Patch,
        WriteOperation.Delete,
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByKey_ReturnsEmptySingleResult_WhenProductDoesNotExist()
    {
        var missingProductId = Guid.NewGuid();
        SetupFindReturns([]);
        var result = await _controller.Get(missingProductId, TestContext.Current.CancellationToken);
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
        var onlyProduct = Assert.Single(result.Queryable);
        Assert.Equal(product.Id, onlyProduct.Id);
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
        var input = new Product { Name = TestValues.NewProductName(), Price = TestValues.NewPrice() };
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
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var input = new Product { Name = TestValues.NewProductName(), Price = TestValues.NewPrice() };
        await _controller.Post(input, TestContext.Current.CancellationToken);
        _mockCollection.Verify(
            c => c.InsertOneAsync(
                It.Is<Product>(p => p.OwnerId == ownerId && p.Id != Guid.Empty && p.CreatedAt != default),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [MemberData(nameof(WriteOperations))]
    [Trait("Category", "Unit")]
    public async Task WriteOperation_ReturnsNotFound_WhenProductDoesNotExist(WriteOperation operation)
    {
        var signedInOwnerId = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        SetupFindReturns([]);
        var result = await InvokeWriteAsync(
            operation, _controller, missingProductId, TestContext.Current.CancellationToken);
        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [MemberData(nameof(WriteOperations))]
    [Trait("Category", "Unit")]
    public async Task WriteOperation_ReturnsForbid_WhenNotOwner(WriteOperation operation)
    {
        var signedInViewerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInViewerId);
        var existing = MakeProduct(ownerId: otherOwnerId);
        SetupFindReturns([existing]);
        SetupAuthorizationFails();
        var result = await InvokeWriteAsync(
            operation, _controller, existing.Id, TestContext.Current.CancellationToken);
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
        var update = new Product { Name = TestValues.NewProductName(), Price = TestValues.NewPrice() };
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
        var patchedProductName = $"patched-{Guid.NewGuid()}";
        var delta = new Delta<Product>();
        delta.TrySetPropertyValue(nameof(Product.Name), patchedProductName);
        var result = await _controller.Patch(existing.Id, delta, TestContext.Current.CancellationToken);
        Assert.IsType<UpdatedODataResult<Product>>(result);
        _mockCollection.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.Is<Product>(p =>
                    string.Equals(p.Name, patchedProductName, StringComparison.Ordinal) && p.OwnerId == ownerId),
                It.IsAny<ReplaceOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_WhenModelStateInvalid_ReturnsBadRequest()
    {
        var signedInOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        _controller.ModelState.AddModelError(nameof(Product.Name), TestValues.NewModelErrorMessage());
        var result = await _controller.Post(new Product(), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Put_WhenModelStateInvalid_ReturnsBadRequest()
    {
        var signedInOwnerId = Guid.NewGuid();
        var targetProductId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        _controller.ModelState.AddModelError(nameof(Product.Name), TestValues.NewModelErrorMessage());
        var result = await _controller.Put(targetProductId, new Product(), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_WhenModelStateInvalid_ReturnsBadRequest()
    {
        var signedInOwnerId = Guid.NewGuid();
        var targetProductId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        _controller.ModelState.AddModelError(nameof(Product.Name), TestValues.NewModelErrorMessage());
        var result = await _controller.Patch(targetProductId, new Delta<Product>(), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_WhenModelStateInvalid_ReturnsBadRequest()
    {
        var signedInOwnerId = Guid.NewGuid();
        var targetProductId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        _controller.ModelState.AddModelError(nameof(Product.Name), TestValues.NewModelErrorMessage());
        var result = await _controller.Delete(targetProductId, TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_WhenSubClaimMissing_SetsOwnerIdToNull()
    {
        _controller.ControllerContext = MakeControllerContext(userId: null);
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var input = new Product { Name = TestValues.NewProductName(), Price = TestValues.NewPrice() };
        await _controller.Post(input, TestContext.Current.CancellationToken);
        Assert.Null(input.OwnerId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_WhenSubClaimIsNotValidGuid_SetsOwnerIdToNull()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "not-a-guid")], authenticationType: "Bearer");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<Product>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var input = new Product { Name = TestValues.NewProductName(), Price = TestValues.NewPrice() };
        await _controller.Post(input, TestContext.Current.CancellationToken);
        Assert.Null(input.OwnerId);
    }

    private static Task<IActionResult> InvokeWriteAsync(
        WriteOperation operation,
        ProductsController controller,
        Guid productId,
        CancellationToken cancellationToken) =>
        operation switch
        {
            WriteOperation.Put => controller.Put(productId, new Product(), cancellationToken),
            WriteOperation.Patch => controller.Patch(productId, new Delta<Product>(), cancellationToken),
            WriteOperation.Delete => controller.Delete(productId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static Product MakeProduct(Guid? ownerId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = TestValues.NewProductName(),
        Price = TestValues.NewPrice(),
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
        var mockCursor = new Mock<IAsyncCursor<Product>>(MockBehavior.Strict);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.Count > 0)
            .ReturnsAsync(false);
        mockCursor
            .Setup(c => c.Current)
            .Returns(products);
        mockCursor.Setup(c => c.Dispose());
        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<Product>>(),
                It.IsAny<FindOptions<Product, Product>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }
}