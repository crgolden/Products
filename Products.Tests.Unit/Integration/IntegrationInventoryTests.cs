namespace Products.Tests.Unit.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Products.Tests.Unit.Infrastructure;
using Products.Tests.Unit.TestSupport;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IntegrationInventoryTests : IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly List<Guid> _createdItemIds = [];

    public IntegrationInventoryTests(ProductsWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddToInventory_ThenGet_ReturnsTheItemMergedWithItsCatalogFacts()
    {
        // Arrange
        var name = TestValues.NewProductName();
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();
        var serialNumber = TestValues.NewModelNumber();

        // Act
        var itemId = await AddToInventoryAsync(name, brand, modelNumber, serialNumber);
        _createdItemIds.Add(itemId);
        var response = await _client.GetAsync("/inventory/items", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var item = items.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(name, item.GetProperty("name").GetString());
        Assert.Equal(brand, item.GetProperty("brand").GetString());
        Assert.Equal(serialNumber, item.GetProperty("serialNumber").GetString());
    }

    [Fact]
    public async Task AddToInventory_Twice_WithTheSameBrandAndModel_ReusesOneCatalogProduct()
    {
        // Arrange
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();

        // Act
        var firstId = await AddToInventoryAsync(
            TestValues.NewProductName(), brand, modelNumber, TestValues.NewModelNumber());
        _createdItemIds.Add(firstId);
        var secondId = await AddToInventoryAsync(
            TestValues.NewProductName(), brand, modelNumber, TestValues.NewModelNumber());
        _createdItemIds.Add(secondId);

        // Assert
        var response = await _client.GetAsync("/inventory/items", TestContext.Current.CancellationToken);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var first = items.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == firstId);
        var second = items.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == secondId);
        Assert.Equal(
            first.GetProperty("catalogProductId").GetGuid(),
            second.GetProperty("catalogProductId").GetGuid());
    }

    [Fact]
    public async Task PatchingACatalogProductOntoAnotherRowsMatchKey_Returns409_NotA500()
    {
        // Arrange
        var takenBrand = TestValues.NewBrand();
        var takenModelNumber = TestValues.NewModelNumber();
        var takenItemId = await AddToInventoryAsync(
            TestValues.NewProductName(), takenBrand, takenModelNumber, TestValues.NewModelNumber());
        _createdItemIds.Add(takenItemId);

        var movingItemId = await AddToInventoryAsync(
            TestValues.NewProductName(),
            TestValues.NewBrand(),
            TestValues.NewModelNumber(),
            TestValues.NewModelNumber());
        _createdItemIds.Add(movingItemId);
        var movingCatalogProductId = await GetCatalogProductIdAsync(movingItemId);

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/odata/CatalogProducts({movingCatalogProductId})",
            new { brand = takenBrand, modelNumber = takenModelNumber },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TopZeroOnTheCatalog_ReturnsAnEmptyPageAndTheRealCount_NotA500()
    {
        // Arrange
        var itemId = await AddToInventoryAsync(
            TestValues.NewProductName(),
            TestValues.NewBrand(),
            TestValues.NewModelNumber(),
            TestValues.NewModelNumber());
        _createdItemIds.Add(itemId);

        // Act
        var response = await _client.GetAsync(
            "/odata/CatalogProducts?$count=true&$top=0",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Empty(body.GetProperty("value").EnumerateArray());
        Assert.True(body.GetProperty("@odata.count").GetInt64() > 0);
    }

    [Theory]
    [InlineData("startswith")]
    [InlineData("contains")]
    public async Task NegatingAStringFunction_FiltersTheRows_NotA500(string function)
    {
        // Arrange
        var uppercaseMarker = TestValues.LowercaseToken(4).ToUpperInvariant();
        var excludedModelNumber = $"{uppercaseMarker}{TestValues.NewModelNumber()}";
        var lowercaseSurvivingModelNumber = TestValues.NewModelNumber();
        _createdItemIds.Add(await AddToInventoryAsync(
            TestValues.NewProductName(),
            TestValues.NewBrand(),
            excludedModelNumber,
            TestValues.NewModelNumber()));
        _createdItemIds.Add(await AddToInventoryAsync(
            TestValues.NewProductName(),
            TestValues.NewBrand(),
            lowercaseSurvivingModelNumber,
            TestValues.NewModelNumber()));

        // Act
        var response = await _client.GetAsync(
            $"/odata/CatalogProducts?$filter=not {function}(ModelNumber,'{uppercaseMarker}')",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(lowercaseSurvivingModelNumber, body, StringComparison.Ordinal);
        Assert.DoesNotContain(excludedModelNumber, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegatingAStringFunction_KeepsRowsWhoseFieldIsNull()
    {
        // Arrange
        var uppercaseMarker = TestValues.LowercaseToken(4).ToUpperInvariant();
        var nameOfTheRowWithANullCategory = TestValues.NewProductName();
        _createdItemIds.Add(await AddToInventoryAsync(
            nameOfTheRowWithANullCategory,
            TestValues.NewBrand(),
            TestValues.NewModelNumber(),
            TestValues.NewModelNumber()));

        // Act
        var response = await _client.GetAsync(
            $"/odata/CatalogProducts?$filter=not startswith(Category,'{uppercaseMarker}')",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameOfTheRowWithANullCategory, body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/odata/Products")]
    [InlineData("/odata/Products(00000000-0000-0000-0000-000000000001)")]
    public async Task RetiredProductsSurface_IsNotRouted(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _createdItemIds)
        {
            await _client.DeleteAsync($"/odata/InventoryItems({id})", CancellationToken.None);
        }

        _client.Dispose();
    }

    private async Task<Guid> GetCatalogProductIdAsync(Guid itemId)
    {
        var response = await _client.GetAsync("/inventory/items", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return items.EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == itemId)
            .GetProperty("catalogProductId")
            .GetGuid();
    }

    private async Task<Guid> AddToInventoryAsync(
        string name,
        string brand,
        string modelNumber,
        string serialNumber)
    {
        var response = await _client.PostAsJsonAsync(
            "/inventory/items",
            new
            {
                name,
                brand,
                modelNumber,
                serialNumber,
            },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
