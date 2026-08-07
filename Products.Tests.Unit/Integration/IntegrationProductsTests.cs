namespace Products.Tests.Unit.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Products.Tests.Unit.Infrastructure;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IntegrationProductsTests : IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly List<Guid> _createdIds = [];

    public IntegrationProductsTests(ProductsWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_FiltersProductsByOwner_WhenAuthenticatedWithGuidSub()
    {
        // Arrange
        var productId = await CreateProductAsync("Integration Test Product");
        _createdIds.Add(productId);

        // Act
        var response = await _client.GetAsync(
            "/odata/Products?$orderby=Name",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var products = body.GetProperty("value").EnumerateArray().ToList();
        Assert.Contains(products, p =>
            p.GetProperty("Id").GetString() == productId.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _createdIds)
        {
            await _client.DeleteAsync(
                $"/odata/Products({id})",
                CancellationToken.None);
        }

        _client.Dispose();
    }

    private async Task<Guid> CreateProductAsync(string name)
    {
        var content = JsonContent.Create(
            new { Name = name },
            options: new JsonSerializerOptions());
        var response = await _client.PostAsync(
            "/odata/Products",
            content,
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return body.GetProperty("Id").GetGuid();
    }
}