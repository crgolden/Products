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
    public async Task TransformAsync_WithEmptyDocument_AddsBearerAndBothPaths()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.True(document.Components?.SecuritySchemes?.ContainsKey("Bearer") == true);
        Assert.True(document.Security?.Count == 1);
        Assert.True(document.Paths?.ContainsKey("/odata/Products") == true);
        Assert.True(document.Paths?.ContainsKey("/odata/Products({key})") == true);
    }

    [Fact]
    public async Task TransformAsync_ListPath_HasSevenQueryParameters()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        var parameters = document.Paths?["/odata/Products"]?.Operations?[HttpMethod.Get]?.Parameters;
        Assert.NotNull(parameters);
        Assert.Equal(7, parameters.Count);
        Assert.Contains(parameters, p => p.Name == "$filter");
        Assert.Contains(parameters, p => p.Name == "$select");
        Assert.Contains(parameters, p => p.Name == "$orderby");
        Assert.Contains(parameters, p => p.Name == "$top");
        Assert.Contains(parameters, p => p.Name == "$skip");
        Assert.Contains(parameters, p => p.Name == "$count");
        Assert.Contains(parameters, p => p.Name == "$expand");
    }

    [Fact]
    public async Task TransformAsync_SinglePath_HasTwoQueryParametersAndKeyPathParam()
    {
        var transformer = new ODataQueryParameterTransformer();
        var document = new OpenApiDocument();

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        var singlePath = document.Paths?["/odata/Products({key})"];
        Assert.NotNull(singlePath);
        Assert.Contains(singlePath.Parameters ?? [], p => p.Name == "key" && p.In == ParameterLocation.Path);
        var queryParams = singlePath.Operations?[HttpMethod.Get]?.Parameters;
        Assert.NotNull(queryParams);
        Assert.Equal(2, queryParams.Count);
        Assert.Contains(queryParams, p => p.Name == "$select");
        Assert.Contains(queryParams, p => p.Name == "$expand");
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

        Assert.True(document.Components.SecuritySchemes.Count == 1);
        Assert.True(document.Security.Count == 1);
    }

    [Fact]
    public async Task TransformAsync_WithBothPathsAlreadyPresent_DoesNotModifyThem()
    {
        var transformer = new ODataQueryParameterTransformer();
        var listPathItem = new OpenApiPathItem();
        var singlePathItem = new OpenApiPathItem();
        var document = new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/odata/Products"] = listPathItem,
                ["/odata/Products({key})"] = singlePathItem,
            },
        };

        await transformer.TransformAsync(document, Context, CancellationToken.None);

        Assert.Same(listPathItem, document.Paths["/odata/Products"]);
        Assert.Same(singlePathItem, document.Paths["/odata/Products({key})"]);
    }
}
