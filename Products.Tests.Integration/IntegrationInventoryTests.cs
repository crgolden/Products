namespace Products.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Products.Models;
using Products.OpenApi;
using Products.Tests.Integration.Infrastructure;
using Products.Tests.Unit.TestSupport;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IntegrationInventoryTests : IDisposable
{
    private const int ZeroPageSize = 0;

    private readonly HttpClient _client;

    public IntegrationInventoryTests(ProductsWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddToInventory_ThenGet_ReturnsTheItemMergedWithItsCatalogFacts()
    {
        var name = Generated.NewProductName();
        var brand = Generated.NewBrand();
        var modelNumber = Generated.NewModelNumber();
        var serialNumber = Generated.NewModelNumber();

        var itemId = await AddToInventoryAsync(name, brand, modelNumber, serialNumber);
        var response = await _client.GetAsync(ODataQueryParameterTransformer.AddToInventoryPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var item = items.EnumerateArray().Single(i => i.GetProperty(JsonPropertyName(nameof(InventoryItemView.Id))).GetGuid() == itemId);
        Assert.Equal(name, item.GetProperty(JsonPropertyName(nameof(InventoryItemView.Name))).GetString());
        Assert.Equal(brand, item.GetProperty(JsonPropertyName(nameof(InventoryItemView.Brand))).GetString());
        Assert.Equal(serialNumber, item.GetProperty(JsonPropertyName(nameof(InventoryItemView.SerialNumber))).GetString());
    }

    [Fact]
    public async Task AddToInventory_Twice_WithTheSameBrandAndModel_ReusesOneCatalogProduct()
    {
        var brand = Generated.NewBrand();
        var modelNumber = Generated.NewModelNumber();

        var firstId = await AddToInventoryAsync(
            Generated.NewProductName(), brand, modelNumber, Generated.NewModelNumber());
        var secondId = await AddToInventoryAsync(
            Generated.NewProductName(), brand, modelNumber, Generated.NewModelNumber());

        var response = await _client.GetAsync(ODataQueryParameterTransformer.AddToInventoryPath, TestContext.Current.CancellationToken);
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        var first = items.EnumerateArray().Single(i => i.GetProperty(JsonPropertyName(nameof(InventoryItemView.Id))).GetGuid() == firstId);
        var second = items.EnumerateArray().Single(i => i.GetProperty(JsonPropertyName(nameof(InventoryItemView.Id))).GetGuid() == secondId);
        Assert.Equal(
            first.GetProperty(JsonPropertyName(nameof(InventoryItemView.CatalogProductId))).GetGuid(),
            second.GetProperty(JsonPropertyName(nameof(InventoryItemView.CatalogProductId))).GetGuid());
    }

    [Fact]
    public async Task PatchingACatalogProductOntoAnotherRowsMatchKey_Returns409_NotA500()
    {
        var takenBrand = Generated.NewBrand();
        var takenModelNumber = Generated.NewModelNumber();
        await AddToInventoryAsync(
            Generated.NewProductName(), takenBrand, takenModelNumber, Generated.NewModelNumber());

        var movingItemId = await AddToInventoryAsync(
            Generated.NewProductName(),
            Generated.NewBrand(),
            Generated.NewModelNumber(),
            Generated.NewModelNumber());
        var movingCatalogProductId = await GetCatalogProductIdAsync(movingItemId);

        var response = await _client.PatchAsJsonAsync(
            $"{ODataQueryParameterTransformer.CatalogProductsPath}({movingCatalogProductId})",
            new { brand = takenBrand, modelNumber = takenModelNumber },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TopZeroOnTheCatalog_ReturnsAnEmptyPageAndTheRealCount_NotA500()
    {
        await AddToInventoryAsync(
            Generated.NewProductName(),
            Generated.NewBrand(),
            Generated.NewModelNumber(),
            Generated.NewModelNumber());

        var response = await _client.GetAsync(
            $"{ODataQueryParameterTransformer.CatalogProductsPath}" +
                $"?{ODataQueryParameterTransformer.CountQueryOption}={ODataProtocolConstants.TrueValue}" +
                $"&{ODataQueryParameterTransformer.TopQueryOption}={ZeroPageSize}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Empty(body.GetProperty(ODataQueryParameterTransformer.CollectionValueProperty).EnumerateArray());
        Assert.True(body.GetProperty(ODataProtocolConstants.CountAnnotation).GetInt64() > 0);
    }

    [Theory]
    [InlineData(ODataProtocolConstants.StartsWithFunction)]
    [InlineData(ODataProtocolConstants.ContainsFunction)]
    public async Task NegatingAStringFunction_FiltersTheRows_NotA500(string function)
    {
        var uppercaseMarker = Generated.NewUppercaseMarker();
        var excludedModelNumber = string.Concat(uppercaseMarker, Generated.NewModelNumber());
        var lowercaseSurvivingModelNumber = Generated.NewModelNumber();
        await AddToInventoryAsync(
            Generated.NewProductName(),
            Generated.NewBrand(),
            excludedModelNumber,
            Generated.NewModelNumber());
        await AddToInventoryAsync(
            Generated.NewProductName(),
            Generated.NewBrand(),
            lowercaseSurvivingModelNumber,
            Generated.NewModelNumber());

        var response = await _client.GetAsync(
            NegatedStringFunctionFilter(function, nameof(CatalogProduct.ModelNumber), uppercaseMarker),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(lowercaseSurvivingModelNumber, body, StringComparison.Ordinal);
        Assert.DoesNotContain(excludedModelNumber, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegatingAStringFunction_KeepsRowsWhoseFieldIsNull()
    {
        var uppercaseMarker = Generated.NewUppercaseMarker();
        var nameOfTheRowWithANullCategory = Generated.NewProductName();
        await AddToInventoryAsync(
            nameOfTheRowWithANullCategory,
            Generated.NewBrand(),
            Generated.NewModelNumber(),
            Generated.NewModelNumber());

        var response = await _client.GetAsync(
            NegatedStringFunctionFilter(
                ODataProtocolConstants.StartsWithFunction, nameof(CatalogProduct.Category), uppercaseMarker),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameOfTheRowWithANullCategory, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetiredProductsCollection_IsNotRouted()
    {
        var response = await _client.GetAsync(
            ODataQueryParameterTransformer.RetiredProductsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetiredProductsKeyedRoute_IsNotRouted()
    {
        var retiredProductId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"{ODataQueryParameterTransformer.RetiredProductsPath}({retiredProductId})",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose() => _client.Dispose();

    private static string JsonPropertyName(string memberName) =>
        JsonNamingPolicy.CamelCase.ConvertName(memberName);

    private static string NegatedStringFunctionFilter(string function, string propertyName, string marker) =>
        $"{ODataQueryParameterTransformer.CatalogProductsPath}" +
        $"?{ODataQueryParameterTransformer.FilterQueryOption}=" +
        $"{ODataProtocolConstants.NotOperator} {function}({propertyName},'{marker}')";

    private async Task<Guid> GetCatalogProductIdAsync(Guid itemId)
    {
        var response = await _client.GetAsync(ODataQueryParameterTransformer.AddToInventoryPath, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return items.EnumerateArray()
            .Single(i => i.GetProperty(JsonPropertyName(nameof(InventoryItemView.Id))).GetGuid() == itemId)
            .GetProperty(JsonPropertyName(nameof(InventoryItemView.CatalogProductId)))
            .GetGuid();
    }

    private async Task<Guid> AddToInventoryAsync(
        string name,
        string brand,
        string modelNumber,
        string serialNumber)
    {
        var response = await _client.PostAsJsonAsync(
            ODataQueryParameterTransformer.AddToInventoryPath,
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
        return body.GetProperty(JsonPropertyName(nameof(InventoryItemView.Id))).GetGuid();
    }
}
