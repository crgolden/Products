namespace Products.Tests.Unit.OpenApi;

using System.Net.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Products.OpenApi;
using TestSupport;

[Trait("Category", "Unit")]
public sealed class ODataQueryParameterTransformerTests
{
    private const int SingleSecurityRequirement = 1;

    private static readonly OpenApiDocumentTransformerContext Context = new()
    {
        DocumentName = TestValues.NewOpenApiDocumentName(),
        DescriptionGroups = [],
        ApplicationServices = new ServiceCollection().BuildServiceProvider(),
    };

    public static TheoryData<string> ListQueryOptions() =>
        [.. ODataQueryParameterTransformer.ListQueryOptions];

    [Fact]
    public async Task TransformAsync_WithEmptyDocument_AddsBearerAndTheThreeSurvivingPaths()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        Assert.True(document.Components?.SecuritySchemes?.ContainsKey(ODataQueryParameterTransformer.BearerSecuritySchemeName));
        Assert.Equal(SingleSecurityRequirement, document.Security?.Count);
        Assert.True(document.Paths?.ContainsKey(ODataQueryParameterTransformer.CatalogProductsPath));
        Assert.True(document.Paths?.ContainsKey(ODataQueryParameterTransformer.InventoryItemsPath));
        Assert.True(document.Paths?.ContainsKey(ODataQueryParameterTransformer.AddToInventoryPath));
    }

    [Fact]
    public async Task TransformAsync_DoesNotDocumentTheRetiredProductsSurface()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        Assert.NotNull(document.Paths);
        Assert.DoesNotContain(
            document.Paths.Keys,
            p => p.StartsWith(ODataQueryParameterTransformer.RetiredProductsPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransformAsync_CatalogListPath_DeclaresEveryListQueryOption()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var parameters = document.Paths?[ODataQueryParameterTransformer.CatalogProductsPath]?.Operations?[HttpMethod.Get]?.Parameters;
        Assert.NotNull(parameters);
        Assert.Equal(ODataQueryParameterTransformer.ListQueryOptions.Length, parameters.Count);
    }

    [Theory]
    [MemberData(nameof(ListQueryOptions))]
    public async Task TransformAsync_CatalogListPath_DeclaresTheQueryOption(string queryOption)
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var parameters = document.Paths?[ODataQueryParameterTransformer.CatalogProductsPath]?.Operations?[HttpMethod.Get]?.Parameters;
        Assert.NotNull(parameters);
        Assert.Contains(parameters, p => string.Equals(p.Name, queryOption, StringComparison.Ordinal));
    }

    [Fact]
    public void ListQueryOptions_AreTheODataV4SystemQueryOptionNames()
    {
        // Act
        var queryOptions = ODataQueryParameterTransformer.ListQueryOptions;

        // Assert
        Assert.Equal(["$filter", "$select", "$orderby", "$top", "$skip", "$count", "$expand"], queryOptions);
    }

    [Fact]
    public async Task TransformAsync_CatalogRead_SecurityIsAnEmptyList_SoItOptsOutOfTheDocumentBearer()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var get = document.Paths?[ODataQueryParameterTransformer.CatalogProductsPath]?.Operations?[HttpMethod.Get];
        Assert.NotNull(get);
        Assert.NotNull(get.Security);
        Assert.Empty(get.Security);
    }

    [Fact]
    public async Task TransformAsync_InventoryRead_SecurityIsNull_SoItInheritsTheDocumentBearer()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var get = document.Paths?[ODataQueryParameterTransformer.InventoryItemsPath]?.Operations?[HttpMethod.Get];
        Assert.NotNull(get);
        Assert.Null(get.Security);
    }

    [Theory]
    [InlineData(ODataQueryParameterTransformer.InventoryItemsPath)]
    [InlineData(ODataQueryParameterTransformer.AddToInventoryPath)]
    public async Task TransformAsync_InventoryOperations_InheritTheDocumentBearerRequirement(string path)
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var operations = document.Paths?[path]?.Operations;
        Assert.NotNull(operations);
        Assert.All(operations.Values, o => Assert.Null(o.Security));
    }

    [Fact]
    public async Task TransformAsync_WithBearerAlreadyPresent_DoesNotDuplicate()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [ODataQueryParameterTransformer.BearerSecuritySchemeName] =
                        new OpenApiSecurityScheme { Scheme = ODataQueryParameterTransformer.BearerSchemeValue },
                },
            },
            Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(ODataQueryParameterTransformer.BearerSecuritySchemeName)] = [],
                },
            ],
        };

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        Assert.Single(document.Components.SecuritySchemes);
        Assert.Single(document.Security);
    }

    [Fact]
    public async Task TransformAsync_WithBothPathsAlreadyPresent_DoesNotModifyThem()
    {
        // Arrange
        var transformer = new ODataQueryParameterTransformer();
        var catalogPathItem = new OpenApiPathItem();
        var inventoryPathItem = new OpenApiPathItem();
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                [ODataQueryParameterTransformer.CatalogProductsPath] = catalogPathItem,
                [ODataQueryParameterTransformer.InventoryItemsPath] = inventoryPathItem,
            },
        };

        // Act
        await transformer.TransformAsync(document, Context, CancellationToken.None);

        // Assert
        Assert.Same(catalogPathItem, document.Paths[ODataQueryParameterTransformer.CatalogProductsPath]);
        Assert.Same(inventoryPathItem, document.Paths[ODataQueryParameterTransformer.InventoryItemsPath]);
    }
}