namespace Products.Tests.Unit.Extensions;

using Microsoft.Extensions.Configuration;
using Products.Extensions;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void GetRequired_ThrowsRatherThanReturningZero_WhenAnIntKeyIsMissing()
    {
        // Arrange
        var missingKey = Generated.NewSettingKey();
        IConfiguration config = new ConfigurationBuilder().Build();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => config.GetRequired<int>(missingKey));

        // Assert
        Assert.Contains(missingKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequired_ThrowsRatherThanReturningFalse_WhenABoolKeyIsMissing()
    {
        // Arrange
        var missingKey = Generated.NewSettingKey();
        IConfiguration config = new ConfigurationBuilder().Build();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => config.GetRequired<bool>(missingKey));

        // Assert
        Assert.Contains(missingKey, exception.Message, StringComparison.Ordinal);
    }
}
