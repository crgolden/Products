namespace Products.Tests.Unit.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Moq;
using Products.Authorization;
using Products.Controllers;
using Products.HostedServices;
using Products.Models;

[Trait("Category", "Unit")]
public class InventoryControllerTests
{
    private readonly Mock<IMongoCollection<InventoryItem>> _mockItems;
    private readonly Mock<IMongoCollection<CatalogProduct>> _mockCatalog;
    private readonly InventoryController _controller;

    public InventoryControllerTests()
    {
        _mockItems = new Mock<IMongoCollection<InventoryItem>>(MockBehavior.Strict);
        _mockCatalog = new Mock<IMongoCollection<CatalogProduct>>(MockBehavior.Strict);
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.GetCollection<InventoryItem>(InventoryItemIndexInitializer.CollectionName, null))
            .Returns(_mockItems.Object);
        database
            .Setup(d => d.GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName, null))
            .Returns(_mockCatalog.Object);
        _controller = new InventoryController(database.Object);
    }

    [Fact]
    public async Task GetMyInventory_ReturnsUnauthorized_WhenThereIsNoSubClaim()
    {
        // Arrange
        _controller.ControllerContext = MakeControllerContext(userId: null);

        // Act
        var result = await _controller.GetMyInventory(search: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetMyInventory_MergesTheCatalogFactsOntoTheOwnersItem()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var catalogProduct = MakeCatalogProduct();
        var item = MakeItem(ownerId, catalogProduct.Id);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([item]);
        SetupCatalogReturns([catalogProduct]);

        // Act
        var result = await _controller.GetMyInventory(search: null, TestContext.Current.CancellationToken);

        // Assert
        var views = Assert.IsType<IReadOnlyList<InventoryItemView>>(GetValue(result), exactMatch: false);
        var view = Assert.Single(views);
        Assert.Equal(catalogProduct.Name, view.Name);
        Assert.Equal(catalogProduct.Brand, view.Brand);
        Assert.Equal(catalogProduct.MsrpPrice, view.MsrpPrice);
        Assert.Equal(item.SerialNumber, view.SerialNumber);
        Assert.Equal(item.PricePaid, view.PricePaid);
    }

    [Fact]
    public async Task GetMyInventory_StillReturnsTheItem_WhenItsCatalogProductIsMissing()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var missingCatalogProductId = Guid.NewGuid();
        var item = MakeItem(ownerId, missingCatalogProductId);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([item]);
        SetupCatalogReturns([]);

        // Act
        var result = await _controller.GetMyInventory(search: null, TestContext.Current.CancellationToken);

        // Assert
        var views = Assert.IsType<IReadOnlyList<InventoryItemView>>(GetValue(result), exactMatch: false);
        var view = Assert.Single(views);
        Assert.Equal(item.Id, view.Id);
        Assert.Null(view.Name);
    }

    [Fact]
    public async Task GetMyInventory_FiltersByNameCaseInsensitively()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var wanted = MakeCatalogProduct();
        var other = MakeCatalogProduct();
        var wantedItem = MakeItem(ownerId, wanted.Id);
        var otherItem = MakeItem(ownerId, other.Id);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([wantedItem, otherItem]);
        SetupCatalogReturns([wanted, other]);

        // Act
        var result = await _controller.GetMyInventory(
            wanted.Name?.ToUpperInvariant(),
            TestContext.Current.CancellationToken);

        // Assert
        var views = Assert.IsType<IReadOnlyList<InventoryItemView>>(GetValue(result), exactMatch: false);
        var view = Assert.Single(views);
        Assert.Equal(wantedItem.Id, view.Id);
    }

    [Fact]
    public async Task GetMyInventory_OrdersByNameOrdinally_SoUppercaseSortsBeforeLowercase()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var uppercaseFirst = MakeCatalogProduct();
        uppercaseFirst.Name = Generated.NewUppercaseSortingName();
        var lowercaseFirst = MakeCatalogProduct();
        lowercaseFirst.Name = Generated.NewLowercaseSortingName();
        var uppercaseItem = MakeItem(ownerId, uppercaseFirst.Id);
        var lowercaseItem = MakeItem(ownerId, lowercaseFirst.Id);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([lowercaseItem, uppercaseItem]);
        SetupCatalogReturns([lowercaseFirst, uppercaseFirst]);

        // Act
        var result = await _controller.GetMyInventory(search: null, TestContext.Current.CancellationToken);

        // Assert
        var views = Assert.IsType<IReadOnlyList<InventoryItemView>>(GetValue(result), exactMatch: false);
        Assert.Equal([uppercaseItem.Id, lowercaseItem.Id], views.Select(v => v.Id));
    }

    [Fact]
    public async Task GetMyInventoryItem_NoSubClaim_ReturnsUnauthorized()
    {
        // Arrange
        var requestedItemId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(userId: null);

        // Act
        var result = await _controller.GetMyInventoryItem(requestedItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetMyInventoryItem_OwnersItem_ReturnsItMergedWithItsCatalogFacts()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var catalogProduct = MakeCatalogProduct();
        var item = MakeItem(ownerId, catalogProduct.Id);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([item]);
        SetupCatalogReturns([catalogProduct]);

        // Act
        var result = await _controller.GetMyInventoryItem(item.Id, TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.IsType<InventoryItemView>(GetValue(result));
        Assert.Equal(item.Id, view.Id);
        Assert.Equal(catalogProduct.Id, view.CatalogProductId);
        Assert.Equal(catalogProduct.Name, view.Name);
        Assert.Equal(catalogProduct.Brand, view.Brand);
        Assert.Equal(catalogProduct.MsrpPrice, view.MsrpPrice);
        Assert.Equal(item.SerialNumber, view.SerialNumber);
        Assert.Equal(item.PricePaid, view.PricePaid);
    }

    [Fact]
    public async Task GetMyInventoryItem_NoItemFound_ReturnsNotFoundWithoutReadingTheCatalog()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var requestedItemId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([]);

        // Act
        var result = await _controller.GetMyInventoryItem(requestedItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMyInventoryItem_CatalogProductIsMissing_StillReturnsTheItem()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var missingCatalogProductId = Guid.NewGuid();
        var item = MakeItem(ownerId, missingCatalogProductId);
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupItemsReturn([item]);
        SetupCatalogReturns([]);

        // Act
        var result = await _controller.GetMyInventoryItem(item.Id, TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.IsType<InventoryItemView>(GetValue(result));
        Assert.Equal(item.Id, view.Id);
        Assert.Null(view.Name);
    }

    [Fact]
    public async Task AddToInventory_ReturnsUnauthorized_WhenThereIsNoSubClaim()
    {
        // Arrange
        _controller.ControllerContext = MakeControllerContext(userId: null);

        // Act
        var result = await _controller.AddToInventory(
            MakeRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task AddToInventory_ReturnsTheMergedView_SoTheClientNeedsNoSecondRequest()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var catalogProduct = MakeCatalogProduct();
        var request = MakeRequest();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupFindOrCreateReturns(catalogProduct);
        SetupInsertSucceeds();

        // Act
        var result = await _controller.AddToInventory(request, TestContext.Current.CancellationToken);

        // Assert
        var view = Assert.IsType<InventoryItemView>(Assert.IsType<CreatedResult>(result).Value);
        Assert.Equal(catalogProduct.Id, view.CatalogProductId);
        Assert.Equal(catalogProduct.Name, view.Name);
        Assert.Equal(catalogProduct.MsrpPrice, view.MsrpPrice);
        Assert.Equal(request.SerialNumber, view.SerialNumber);
        Assert.Equal(request.PricePaid, view.PricePaid);
    }

    [Fact]
    public async Task AddToInventory_SetsOwnerIdFromTheClaim_AndNeverFromTheRequest()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        _controller.ControllerContext = MakeControllerContext(ownerId);
        SetupFindOrCreateReturns(MakeCatalogProduct());
        var inserted = SetupInsertSucceeds();

        // Act
        await _controller.AddToInventory(MakeRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ownerId, Assert.Single(inserted).OwnerId);
    }

    private static AddToInventoryRequest MakeRequest() => new()
    {
        Name = Generated.NewProductName(),
        Brand = Generated.NewBrand(),
        ModelNumber = Generated.NewModelNumber(),
        MsrpPrice = Generated.NewPrice(),
        SerialNumber = Generated.NewModelNumber(),
        PricePaid = Generated.NewPrice(),
    };

    private static object? GetValue(ActionResult<IReadOnlyList<InventoryItemView>> result) =>
        Assert.IsType<OkObjectResult>(result.Result).Value;

    private static object? GetValue(ActionResult<InventoryItemView> result) =>
        Assert.IsType<OkObjectResult>(result.Result).Value;

    private static CatalogProduct MakeCatalogProduct()
    {
        var catalogProductId = Guid.NewGuid();
        return new CatalogProduct
        {
            Id = catalogProductId,
            Name = Generated.NewProductName(),
            Brand = Generated.NewBrand(),
            ModelNumber = Generated.NewModelNumber(),
            MsrpPrice = Generated.NewPrice(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static InventoryItem MakeItem(Guid ownerId, Guid catalogProductId)
    {
        var itemId = Guid.NewGuid();
        return new InventoryItem
        {
            Id = itemId,
            OwnerId = ownerId,
            CatalogProductId = catalogProductId,
            SerialNumber = Generated.NewModelNumber(),
            PricePaid = Generated.NewPrice(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static ControllerContext MakeControllerContext(Guid? userId)
    {
        var claims = userId is null
            ? []
            : new[] { new Claim(ProductClaims.Subject, userId.Value.ToString()) };
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: userId is null ? null : JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new ControllerContext { HttpContext = httpContext };
    }

    private void SetupItemsReturn(IList<InventoryItem> items)
    {
        var cursor = new Mock<IAsyncCursor<InventoryItem>>(MockBehavior.Strict);
        cursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items.Count > 0)
            .ReturnsAsync(false);
        cursor.Setup(c => c.Current).Returns(items);
        cursor.Setup(c => c.Dispose());
        _mockItems
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<InventoryItem>>(),
                It.IsAny<FindOptions<InventoryItem, InventoryItem>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
    }

    private void SetupFindOrCreateReturns(CatalogProduct catalogProduct)
    {
        _mockCatalog
            .Setup(c => c.FindOneAndUpdateAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<UpdateDefinition<CatalogProduct>>(),
                It.IsAny<FindOneAndUpdateOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalogProduct);
    }

    private List<InventoryItem> SetupInsertSucceeds()
    {
        var inserted = new List<InventoryItem>();
        _mockItems
            .Setup(c => c.InsertOneAsync(
                It.IsAny<InventoryItem>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryItem, InsertOneOptions, CancellationToken>((item, _, _) => inserted.Add(item))
            .Returns(Task.CompletedTask);
        return inserted;
    }

    private void SetupCatalogReturns(IList<CatalogProduct> catalogProducts)
    {
        var cursor = new Mock<IAsyncCursor<CatalogProduct>>(MockBehavior.Strict);
        cursor
            .SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalogProducts.Count > 0)
            .ReturnsAsync(false);
        cursor.Setup(c => c.Current).Returns(catalogProducts);
        cursor.Setup(c => c.Dispose());
        _mockCatalog
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<CatalogProduct>>(),
                It.IsAny<FindOptions<CatalogProduct, CatalogProduct>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
    }
}
