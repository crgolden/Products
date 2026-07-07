namespace Products.Tests.Unit.Extensions;

using Azure;
using Azure.Security.KeyVault.Secrets;
using Moq;
using Products.Extensions;

[Trait("Category", "Unit")]
public sealed class SecretClientExtensionsTests
{
    [Fact]
    public void GetProductsSecrets_ReturnsTupleWithAllFourSecretValues()
    {
        var values = new Dictionary<string, string>
        {
            ["ElasticsearchUsername"] = "es-user",
            ["ElasticsearchPassword"] = "es-pass",
            ["MongoDbUsername"] = "mongo-user",
            ["MongoDbPassword"] = "mongo-pass",
        };
        var mock = new Mock<SecretClient>();
        mock.Setup(c => c.GetSecret(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .Returns<string, string?, SecretContentType?, CancellationToken>((name, _, _, _) => SecretResponse(name, values[name]));

        var (esUsername, esPassword, mongoUsername, mongoPassword) = mock.Object.GetProductsSecrets();

        Assert.Equal("es-user", esUsername.Value);
        Assert.Equal("es-pass", esPassword.Value);
        Assert.Equal("mongo-user", mongoUsername.Value);
        Assert.Equal("mongo-pass", mongoPassword.Value);
    }

    private static Response<KeyVaultSecret> SecretResponse(string name, string value) =>
        Response.FromValue(new KeyVaultSecret(name, value), Mock.Of<Response>());
}
