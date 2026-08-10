namespace Products.Tests.Unit.HostedServices;

using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using Products.HostedServices;
using Products.Models;

public sealed class ProductIndexInitializerTests
{
    private static readonly TimeSpan ShortRetryDelay = TimeSpan.FromMilliseconds(10);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_CreatesIndexesOnce_WhenMongoIsReachable()
    {
        // Arrange
        var indexManager = new Mock<IMongoIndexManager<Product>>(MockBehavior.Strict);
        indexManager
            .Setup(m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Name_1", "CreatedAt_-1", "OwnerId_1"]);
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await initializer.ExecuteTask!;

        // Assert
        indexManager.Verify(
            m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_DoesNotFault_WhenMongoIsUnreachable()
    {
        // Arrange
        var indexManager = new Mock<IMongoIndexManager<Product>>(MockBehavior.Strict);
        indexManager
            .Setup(m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("A timeout occurred after 30000ms selecting a server"));
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        var faultedWhileMongoWasDown = initializer.ExecuteTask?.IsFaulted;
        await initializer.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(faultedWhileMongoWasDown);
        indexManager.Verify(
            m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteAsync_CompletesAfterRetry_WhenMongoRecovers()
    {
        // Arrange
        var indexManager = new Mock<IMongoIndexManager<Product>>(MockBehavior.Strict);
        indexManager
            .SetupSequence(m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("unreachable"))
            .ReturnsAsync(["Name_1", "CreatedAt_-1", "OwnerId_1"]);
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await initializer.ExecuteTask!;

        // Assert
        indexManager.Verify(
            m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static ProductIndexInitializer CreateInitializer(
        Mock<IMongoIndexManager<Product>> indexManager,
        out Mock<IMongoCollection<Product>> collection)
    {
        collection = new Mock<IMongoCollection<Product>>(MockBehavior.Strict);
        collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.GetCollection<Product>("Products", It.IsAny<MongoCollectionSettings>()))
            .Returns(collection.Object);
        return new ProductIndexInitializer(
            database.Object,
            NullLogger<ProductIndexInitializer>.Instance,
            ShortRetryDelay);
    }
}
