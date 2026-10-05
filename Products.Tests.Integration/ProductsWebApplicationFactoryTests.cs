namespace Products.Tests.Integration;

using Microsoft.Extensions.Configuration;
using Products.Tests.Integration.Infrastructure;

[Trait("Category", "Integration")]
public sealed class ProductsWebApplicationFactoryTests
{
    [Fact]
    public void StartingAgainstADatabaseWithoutADisposableSuffix_IsRefusedBeforeAnythingIsWritten()
    {
        var productionLikeDatabase = Generated.NewDatabaseName();
        using var configuredFactory = new ProductsWebApplicationFactory();
        using var factory = configuredFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [MongoSettingKeys.DatabaseName] = productionLikeDatabase })));

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Equal(productionLikeDatabase, configuredFactory.RefusedDatabase);
    }
}
