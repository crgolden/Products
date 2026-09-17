namespace Products.Tests.Unit.OpenApi;

using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Products.Models;
using Products.OpenApi;
using TestSupport;

public class CatalogOpenApiSchemaTests
{
    private static readonly OpenApiDocumentTransformerContext Context = new()
    {
        DocumentName = TestValues.NewOpenApiDocumentName(),
        DescriptionGroups = [],
        ApplicationServices = new ServiceCollection().BuildServiceProvider(),
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_DeclaresNoOwnerId_SoThePublicDocumentNeverPromisesIt()
    {
        // Act
        var properties = await CatalogProductPropertiesAsync();

        // Assert
        Assert.False(properties.ContainsKey(JsonPropertyName(nameof(InventoryItem.OwnerId))));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_DeclaresNoOwnerPrivateFields()
    {
        // Act
        var properties = await CatalogProductPropertiesAsync();

        // Assert
        Assert.False(properties.ContainsKey(JsonPropertyName(nameof(InventoryItem.SerialNumber))));
        Assert.False(properties.ContainsKey(JsonPropertyName(nameof(InventoryItem.PurchaseDate))));
        Assert.False(properties.ContainsKey(JsonPropertyName(nameof(InventoryItem.PricePaid))));
        Assert.False(properties.ContainsKey(JsonPropertyName(nameof(InventoryItem.Description))));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_StillDeclaresTheUniversalFields_SoTheAbsenceChecksAreNotVacuous()
    {
        // Act
        var properties = await CatalogProductPropertiesAsync();

        // Assert
        Assert.True(properties.ContainsKey(JsonPropertyName(nameof(CatalogProduct.Name))));
        Assert.True(properties.ContainsKey(JsonPropertyName(nameof(CatalogProduct.Brand))));
        Assert.True(properties.ContainsKey(JsonPropertyName(nameof(CatalogProduct.ModelNumber))));
        Assert.True(properties.ContainsKey(JsonPropertyName(nameof(CatalogProduct.MsrpPrice))));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogListOperation_IsAnonymous_WhileTheInventoryListOperationIsNot()
    {
        // Arrange
        var document = await TransformedDocumentAsync();

        // Act
        var catalogGet = OperationFor(document, ODataQueryParameterTransformer.CatalogProductsPath);
        var inventoryGet = OperationFor(document, ODataQueryParameterTransformer.InventoryItemsPath);

        // Assert
        Assert.NotNull(catalogGet.Security);
        Assert.Empty(catalogGet.Security);
        Assert.Null(inventoryGet.Security);
    }

    private static string JsonPropertyName(string memberName) =>
        JsonNamingPolicy.CamelCase.ConvertName(memberName);

    private static async Task<OpenApiDocument> TransformedDocumentAsync()
    {
        var document = new OpenApiDocument();
        var transformer = new ODataQueryParameterTransformer();
        await transformer.TransformAsync(document, Context, TestContext.Current.CancellationToken);
        return document;
    }

    private static OpenApiOperation OperationFor(OpenApiDocument document, string path)
    {
        Assert.NotNull(document.Paths);
        var pathItem = document.Paths[path];
        Assert.NotNull(pathItem.Operations);
        return pathItem.Operations[HttpMethod.Get];
    }

    private static async Task<IDictionary<string, IOpenApiSchema>> CatalogProductPropertiesAsync()
    {
        var document = await TransformedDocumentAsync();
        var operation = OperationFor(document, ODataQueryParameterTransformer.CatalogProductsPath);

        Assert.NotNull(operation.Responses);
        var response = operation.Responses[ODataQueryParameterTransformer.OkStatusCode];
        Assert.NotNull(response.Content);
        var schema = response.Content[MediaTypeNames.Application.Json].Schema;
        Assert.NotNull(schema);
        Assert.NotNull(schema.Properties);
        var items = schema.Properties[ODataQueryParameterTransformer.CollectionValueProperty].Items;
        Assert.NotNull(items);
        Assert.NotNull(items.Properties);
        return items.Properties;
    }
}
