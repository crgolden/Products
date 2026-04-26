namespace Products.Tests.Infrastructure;

/// <summary>
/// xUnit collection that shares <see cref="ProductsWebApplicationFactory"/> across all
/// integration tests. One factory instance is created per test run, which avoids
/// repeated Azure Key Vault calls and MongoDB connection setup.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ProductsWebApplicationFactory>
{
    public const string Name = "Integration";
}
