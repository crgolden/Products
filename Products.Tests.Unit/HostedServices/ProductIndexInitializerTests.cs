namespace Products.Tests.Unit.HostedServices;

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
            .ReturnsAsync(CreatedIndexNames());
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await ExecuteTaskOf(initializer);

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
        var serverSelectionFailure = $"server-selection-timeout-{Guid.NewGuid()}";
        var indexManager = new Mock<IMongoIndexManager<Product>>(MockBehavior.Strict);
        indexManager
            .Setup(m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(serverSelectionFailure));
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        var faultedWhileMongoWasDown = ExecuteTaskOf(initializer).IsFaulted;
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
        var firstAttemptFailure = $"unreachable-{Guid.NewGuid()}";
        var indexManager = new Mock<IMongoIndexManager<Product>>(MockBehavior.Strict);
        indexManager
            .SetupSequence(m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(firstAttemptFailure))
            .ReturnsAsync(CreatedIndexNames());
        var initializer = CreateInitializer(indexManager, out _);

        // Act
        await initializer.StartAsync(TestContext.Current.CancellationToken);
        await ExecuteTaskOf(initializer);

        // Assert
        indexManager.Verify(
            m => m.CreateManyAsync(
                It.IsAny<IEnumerable<CreateIndexModel<Product>>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static Task ExecuteTaskOf(ProductIndexInitializer initializer) =>
        initializer.ExecuteTask
        ?? throw new InvalidOperationException(
            $"{nameof(initializer.ExecuteTask)} is null, so {nameof(initializer.StartAsync)} never began the loop and nothing is under test.");

    private static IEnumerable<string> CreatedIndexNames()
    {
        var nameIndexName = $"index-{Guid.NewGuid()}";
        var createdAtIndexName = $"index-{Guid.NewGuid()}";
        var ownerIdIndexName = $"index-{Guid.NewGuid()}";
        return [nameIndexName, createdAtIndexName, ownerIdIndexName];
    }

    private static ProductIndexInitializer CreateInitializer(
        Mock<IMongoIndexManager<Product>> indexManager,
        out Mock<IMongoCollection<Product>> collection)
    {
        collection = new Mock<IMongoCollection<Product>>(MockBehavior.Strict);
        collection.SetupGet(c => c.Indexes).Returns(indexManager.Object);
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.GetCollection<Product>(
                ProductIndexInitializer.CollectionName,
                It.IsAny<MongoCollectionSettings>()))
            .Returns(collection.Object);
        return new ProductIndexInitializer(database.Object, ShortRetryDelay);
    }
}
