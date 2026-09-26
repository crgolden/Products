namespace Products.Tests.Unit.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Products.HealthChecks;

[Trait("Category", "Unit")]
public sealed class MongoDbHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenPingSucceeds()
    {
        // Arrange
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocument(MongoDbHealthCheck.CommandOkField, 1));
        var healthCheck = new MongoDbHealthCheck(database.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(MongoDbHealthCheck.HealthyDescription, result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWithLastException_WhenPingAlwaysFails()
    {
        // Arrange
        var expected = new TimeoutException(Generated.NewTimeoutMessage());
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);
        var healthCheck = new MongoDbHealthCheck(database.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(expected.Message, result.Description);
        Assert.Same(expected, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_RetriesOnce_BeforeReportingUnhealthy()
    {
        // Arrange
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .Setup(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(Generated.NewTimeoutMessage()));
        var healthCheck = new MongoDbHealthCheck(database.Object);

        // Act
        await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        // Assert
        database.Verify(
            d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(MongoDbHealthCheck.MaxAttempts));
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenFirstAttemptFailsAndRetrySucceeds()
    {
        // Arrange
        var database = new Mock<IMongoDatabase>(MockBehavior.Strict);
        database
            .SetupSequence(d => d.RunCommandAsync(
                It.IsAny<Command<BsonDocument>>(),
                It.IsAny<ReadPreference>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException(Generated.NewTimeoutMessage()))
            .ReturnsAsync(new BsonDocument(MongoDbHealthCheck.CommandOkField, 1));
        var healthCheck = new MongoDbHealthCheck(database.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
