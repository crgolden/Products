namespace Products.Tests.Unit.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Products.Models;
using Products.OpenApi;
using Products.Tests.Unit.Infrastructure;
using Products.Tests.Unit.TestSupport;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public sealed class IntegrationInventoryTests : IAsyncDisposable
{
    private const int ZeroPageSize = 0;

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
        var response = await _client.GetAsync(ODataQueryParameterTransformer.AddToInventoryPath, TestContext.Current.CancellationToken);

        // Assert
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
            $"{ODataQueryParameterTransformer.CatalogProductsPath}({movingCatalogProductId})",
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
            $"{ODataQueryParameterTransformer.CatalogProductsPath}" +
                $"?{ODataQueryParameterTransformer.CountQueryOption}={ODataProtocolConstants.TrueValue}" +
                $"&{ODataQueryParameterTransformer.TopQueryOption}={ZeroPageSize}",
            TestContext.Current.CancellationToken);

        // Assert
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
        // Arrange
        var uppercaseMarker = TestValues.NewUppercaseMarker();
        var excludedModelNumber = string.Concat(uppercaseMarker, TestValues.NewModelNumber());
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
            NegatedStringFunctionFilter(function, nameof(CatalogProduct.ModelNumber), uppercaseMarker),
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
        var uppercaseMarker = TestValues.NewUppercaseMarker();
        var nameOfTheRowWithANullCategory = TestValues.NewProductName();
        _createdItemIds.Add(await AddToInventoryAsync(
            nameOfTheRowWithANullCategory,
            TestValues.NewBrand(),
            TestValues.NewModelNumber(),
            TestValues.NewModelNumber()));

        // Act
        var response = await _client.GetAsync(
            NegatedStringFunctionFilter(
                ODataProtocolConstants.StartsWithFunction, nameof(CatalogProduct.Category), uppercaseMarker),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameOfTheRowWithANullCategory, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetiredProductsCollection_IsNotRouted()
    {
        // Act
        var response = await _client.GetAsync(
            ODataQueryParameterTransformer.RetiredProductsPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetiredProductsKeyedRoute_IsNotRouted()
    {
        // Arrange
        var retiredProductId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"{ODataQueryParameterTransformer.RetiredProductsPath}({retiredProductId})",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _createdItemIds)
        {
            await _client.DeleteAsync(
                $"{ODataQueryParameterTransformer.InventoryItemsPath}({id})", CancellationToken.None);
        }

        _client.Dispose();
    }

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
