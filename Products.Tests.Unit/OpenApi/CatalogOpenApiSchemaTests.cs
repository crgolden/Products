namespace Products.Tests.Unit.OpenApi;

using Microsoft.OpenApi;
using Products.OpenApi;

public class CatalogOpenApiSchemaTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_DeclaresNoOwnerId_SoThePublicDocumentNeverPromisesIt()
    {
        var properties = await CatalogProductPropertiesAsync();

        Assert.False(properties.ContainsKey("ownerId"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_DeclaresNoOwnerPrivateFields()
    {
        var properties = await CatalogProductPropertiesAsync();

        Assert.False(properties.ContainsKey("serialNumber"));
        Assert.False(properties.ContainsKey("purchaseDate"));
        Assert.False(properties.ContainsKey("pricePaid"));
        Assert.False(properties.ContainsKey("description"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogProductSchema_StillDeclaresTheUniversalFields_SoTheAbsenceChecksAreNotVacuous()
    {
        var properties = await CatalogProductPropertiesAsync();

        Assert.True(properties.ContainsKey("name"));
        Assert.True(properties.ContainsKey("brand"));
        Assert.True(properties.ContainsKey("modelNumber"));
        Assert.True(properties.ContainsKey("msrpPrice"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CatalogListOperation_IsAnonymous_WhileTheInventoryListOperationIsNot()
    {
        var document = await TransformedDocumentAsync();

        var catalogGet = OperationFor(document, "/odata/CatalogProducts");
        var inventoryGet = OperationFor(document, "/odata/InventoryItems");

        Assert.NotNull(catalogGet.Security);
        Assert.Empty(catalogGet.Security);
        Assert.Null(inventoryGet.Security);
    }

    private static async Task<OpenApiDocument> TransformedDocumentAsync()
    {
        var document = new OpenApiDocument();
        var transformer = new ODataQueryParameterTransformer();
        await transformer.TransformAsync(document, null!, TestContext.Current.CancellationToken);
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
        var operation = OperationFor(document, "/odata/CatalogProducts");

        Assert.NotNull(operation.Responses);
        var response = operation.Responses["200"];
        Assert.NotNull(response.Content);
        var schema = response.Content["application/json"].Schema;
        Assert.NotNull(schema);
        Assert.NotNull(schema.Properties);
        var items = schema.Properties["value"].Items;
        Assert.NotNull(items);
        Assert.NotNull(items.Properties);
        return items.Properties;
    }
}