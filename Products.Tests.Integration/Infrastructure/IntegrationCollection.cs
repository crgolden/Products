namespace Products.Tests.Integration.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ProductsWebApplicationFactory>
{
    public const string Name = "Integration";
}
