namespace Products.Tests.Unit.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.OData.ModelBuilder;
using MongoDB.Driver;
using Moq;
using Products.Authorization;
using Products.Controllers;
using Products.HostedServices;
using Products.Models;
using Products.Tests.Unit.TestSupport;

public class InventoryItemsControllerTests
{
    private readonly Mock<IMongoCollection<InventoryItem>> _mockCollection;
    private readonly InventoryItemsController _controller;

    public InventoryItemsControllerTests()
    {
        _mockCollection = new Mock<IMongoCollection<InventoryItem>>(MockBehavior.Strict);
        var mockDatabase = new Mock<IMongoDatabase>(MockBehavior.Strict);
        mockDatabase
            .Setup(d => d.GetCollection<InventoryItem>(InventoryItemIndexInitializer.CollectionName, null))
            .Returns(_mockCollection.Object);
        _controller = new InventoryItemsController(mockDatabase.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Get_ReturnsUnauthorized_WhenThereIsNoSubClaim()
    {
        _controller.ControllerContext = MakeControllerContext(userId: null);

        var result = _controller.Get(EmptyQueryOptions());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Get_ReturnsUnauthorized_WhenTheSubClaimIsNotAGuid()
    {
        _controller.ControllerContext = MakeControllerContextWithSubject("not-a-guid");

        var result = _controller.Get(EmptyQueryOptions());

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetByKey_ReturnsUnauthorized_WhenThereIsNoSubClaim()
    {
        _controller.ControllerContext = MakeControllerContext(userId: null);

        var result = await _controller.Get(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_SetsOwnerIdFromTheClaim_AndNeverTrustsTheBody()
    {
        var ownerId = Guid.NewGuid();
        var spoofedOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupInsertSucceeds();
        var input = new InventoryItem
        {
            OwnerId = spoofedOwnerId,
            CatalogProductId = Guid.NewGuid(),
            SerialNumber = TestValues.NewModelNumber(),
        };

        var result = await _controller.Post(input, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedODataResult<InventoryItem>>(result);
        Assert.Equal(ownerId, input.OwnerId);
        Assert.NotEqual(spoofedOwnerId, input.OwnerId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Post_ReturnsUnauthorized_WhenThereIsNoSubClaim()
    {
        _controller.ControllerContext = MakeControllerContext(userId: null);
        var input = new InventoryItem { CatalogProductId = Guid.NewGuid() };

        var result = await _controller.Post(input, TestContext.Current.CancellationToken);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Patch_ReturnsNotFound_WhenTheItemBelongsToAnotherOwner()
    {
        var signedInOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        SetupFindReturns([]);

        var result = await _controller.Patch(
            Guid.NewGuid(),
            new Delta<InventoryItem>(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNotFound_WhenNothingMatchedTheOwnerScopedFilter()
    {
        var signedInOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        SetupDeleteReturns(0);

        var result = await _controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Delete_ReturnsNoContent_WhenTheOwnerScopedFilterMatched()
    {
        var signedInOwnerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(signedInOwnerId);
        SetupDeleteReturns(1);

        var result = await _controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
    }

    private static ODataQueryOptions<InventoryItem> EmptyQueryOptions()
    {
        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EntitySet<InventoryItem>(InventoryItemIndexInitializer.CollectionName);
        var context = new ODataQueryContext(modelBuilder.GetEdmModel(), typeof(InventoryItem), null);
        return new ODataQueryOptions<InventoryItem>(context, new DefaultHttpContext().Request);
    }

    private static ControllerContext MakeControllerContext(Guid? userId) =>
        MakeControllerContextWithSubject(userId?.ToString());

    private static ControllerContext MakeControllerContextWithSubject(string? subject)
    {
        var claims = subject is null
            ? []
            : new[] { new Claim(ProductClaims.Subject, subject) };
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: subject is null ? null : JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new ControllerContext { HttpContext = httpContext };
    }

    private void SetupInsertSucceeds() =>
        _mockCollection
            .Setup(c => c.InsertOneAsync(
                It.IsAny<InventoryItem>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    private void SetupDeleteReturns(long deletedCount) =>
        _mockCollection
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<InventoryItem>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteResult.Acknowledged(deletedCount));

    private void SetupFindReturns(IList<InventoryItem> items)
    {
        var mockCursor = new Mock<IAsyncCursor<InventoryItem>>(MockBehavior.Strict);
        mockCursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.Count > 0)
            .ReturnsAsync(false);
        mockCursor
            .Setup(c => c.Current)
            .Returns(items);
        mockCursor.Setup(c => c.Dispose());
        _mockCollection
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<InventoryItem>>(),
                It.IsAny<FindOptions<InventoryItem, InventoryItem>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
    }
}