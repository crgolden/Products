namespace Products.Tests.Extensions;

using Microsoft.Extensions.Configuration;
using Products.Extensions;

[Trait("Category", "Unit")]
public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void GetRequired_ReturnsValue_WhenKeyExists()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Foo"] = "bar" })
            .Build();

        Assert.Equal("bar", config.GetRequired<string>("Foo"));
    }

    [Fact]
    public void GetRequired_ThrowsInvalidOperationException_WhenKeyMissing()
    {
        var config = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => config.GetRequired<string>("Missing"));
    }

    [Fact]
    public void GetProductsSecrets_ReturnsBothCredentials()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbUsername"] = "mongo-user",
                ["MongoDbPassword"] = "mongo-pass",
            })
            .Build();

        var (username, password) = config.GetProductsSecrets();

        Assert.Equal("mongo-user", username);
        Assert.Equal("mongo-pass", password);
    }
}
