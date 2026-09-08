namespace Products.Tests.Unit.OpenApi;

using System.Net.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Products.OpenApi;

[Trait("Category", "Unit")]
public sealed class ODataQueryParameterTransformerTests
{
    private static readonly OpenApiDocumentTransformerContext Context = new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = new ServiceCollection().BuildServiceProvider(),
    };

    [Fact]
    public async Task TransformAsync_WithEmptyDocument_AddsBearerAndTheThreeSurvivingPaths()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.True(document.Components?.SecuritySchemes?.ContainsKey("Bearer"));
        Assert.Equal(1, document.Security?.Count);
        Assert.True(document.Paths?.ContainsKey("/odata/CatalogProducts"));
        Assert.True(document.Paths?.ContainsKey("/odata/InventoryItems"));
        Assert.True(document.Paths?.ContainsKey("/inventory/items"));
    }

    [Fact]
    public async Task TransformAsync_DoesNotDocumentTheRetiredProductsSurface()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.NotNull(document.Paths);
        Assert.DoesNotContain(
            document.Paths.Keys,
            p => p.StartsWith("/odata/Products", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransformAsync_CatalogListPath_HasSevenQueryParameters()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        var parameters = document.Paths?["/odata/CatalogProducts"]?.Operations?[HttpMethod.Get]?.Parameters;
        Assert.NotNull(parameters);
        Assert.Equal(7, parameters.Count);
        Assert.Contains(parameters, p => string.Equals(p.Name, "$filter", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$select", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$orderby", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$top", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$skip", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$count", StringComparison.Ordinal));
        Assert.Contains(parameters, p => string.Equals(p.Name, "$expand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransformAsync_CatalogRead_OptsOutOfTheDocumentBearerRequirement()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        var get = document.Paths?["/odata/CatalogProducts"]?.Operations?[HttpMethod.Get];
        Assert.NotNull(get);

        // An explicit empty list is the opt-out; null would inherit the document-level Bearer.
        Assert.NotNull(get.Security);
        Assert.Empty(get.Security);
    }

    [Theory]
    [InlineData("/odata/InventoryItems")]
    [InlineData("/inventory/items")]
    public async Task TransformAsync_InventoryOperations_InheritTheDocumentBearerRequirement(string path)
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        var operations = document.Paths?[path]?.Operations;
        Assert.NotNull(operations);
        Assert.All(operations.Values, o => Assert.Null(o.Security));
    }

    [Fact]
    public async Task TransformAsync_WithBearerAlreadyPresent_DoesNotDuplicate()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme { Scheme = "bearer" },
                },
            },
            Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = [],
                },
            ],
        };

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.Single(document.Components.SecuritySchemes);
        Assert.Single(document.Security);
    }

    [Fact]
    public async Task TransformAsync_WithBothPathsAlreadyPresent_DoesNotModifyThem()
    {
        var transformer = new ODataQueryParameterTransformer();
        var catalogPathItem = new OpenApiPathItem();
        var inventoryPathItem = new OpenApiPathItem();
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/odata/CatalogProducts"] = catalogPathItem,
                ["/odata/InventoryItems"] = inventoryPathItem,
            },
        };

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.Same(catalogPathItem, document.Paths["/odata/CatalogProducts"]);
        Assert.Same(inventoryPathItem, document.Paths["/odata/InventoryItems"]);
    }
}