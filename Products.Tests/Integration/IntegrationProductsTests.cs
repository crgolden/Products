namespace Products.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Products.Tests.Infrastructure;

/// <summary>
/// Integration tests against the real MongoDB instance.
/// These tests verify that the <c>BsonClassMap</c> serializer configuration is correct
/// for all <see cref="Products.Models.Product"/> Guid fields.
/// </summary>
/// <remarks>
/// Requires <c>ASPNETCORE_ENVIRONMENT=Development</c> and a valid <c>az login</c> session
/// so that <c>Program.cs</c> can read MongoDB credentials from Azure Key Vault.
/// Each test cleans up any products it creates.
/// </remarks>
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
        // Arrange: create a product so the list is non-trivial.
        // POST exercises InsertOneAsync with a non-null OwnerId — this also crashes
        // with "GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified"
        // if OwnerId lacks an explicit serializer in the BsonClassMap registration.
        var productId = await CreateProductAsync("Integration Test Product");
        _createdIds.Add(productId);

        // Act: GET /odata/Products as an authenticated user with a valid Guid sub.
        // This triggers _products.AsQueryable().Where(p => p.OwnerId == ownerId)
        // in ProductsController.Get(), which serializes the ownerId Guid value.
        var response = await _client.GetAsync(
            "/odata/Products?$orderby=Name",
            TestContext.Current.CancellationToken);

        // Assert: 200 and the created product appears in the response.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var products = body.GetProperty("value").EnumerateArray().ToList();
        Assert.Contains(products, p =>
            p.GetProperty("Id").GetString() == productId.ToString());
    }

    /// <inheritdoc/>
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
