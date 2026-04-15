namespace Products.OpenApi;

using System.Net.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

public class ODataQueryParameterTransformer : IOpenApiDocumentTransformer
{
    private static readonly HashSet<OpenApiTagReference> ProductsTags = new HashSet<OpenApiTagReference>
    {
        new OpenApiTagReference("Products"),
    };

    private static readonly List<IOpenApiParameter> ListParameters = new List<IOpenApiParameter>
    {
        MakeQueryParam("$filter", "Filters results using OData filter syntax, e.g. Name eq 'Widget'"),
        MakeQueryParam("$select", "Selects a subset of properties, e.g. Id,Name,Price"),
        MakeQueryParam("$orderby", "Orders results, e.g. Price desc"),
        MakeQueryParam("$top", "Limits the number of results returned (max 100)"),
        MakeQueryParam("$skip", "Skips the specified number of results"),
        MakeQueryParam("$count", "Includes a total count of matching results when set to true"),
        MakeQueryParam("$expand", "Expands related entities inline"),
    };

    private static readonly List<IOpenApiParameter> SingleParameters = new List<IOpenApiParameter>
    {
        MakeQueryParam("$select", "Selects a subset of properties, e.g. Id,Name,Price"),
        MakeQueryParam("$expand", "Expands related entities inline"),
    };

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        EnsurePaths(document);
        return Task.CompletedTask;
    }

    private static void EnsurePaths(OpenApiDocument document)
    {
        document.Paths ??= new OpenApiPaths();

        if (!document.Paths.ContainsKey("/odata/Products"))
        {
            document.Paths["/odata/Products"] = BuildListPath();
        }

        if (!document.Paths.ContainsKey("/odata/Products({key})"))
        {
            document.Paths["/odata/Products({key})"] = BuildSinglePath();
        }
    }

    private static OpenApiPathItem BuildListPath()
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Get all products",
                    Parameters = new List<IOpenApiParameter>(ListParameters),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(CollectionSchema()),
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                    },
                },
                [HttpMethod.Post] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Create a product",
                    RequestBody = JsonBody(ProductSchema()),
                    Responses = new OpenApiResponses
                    {
                        ["201"] = JsonResponse(ProductSchema(), "Created"),
                        ["400"] = new OpenApiResponse { Description = "Bad Request" },
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                    },
                },
            },
        };
    }

    private static OpenApiPathItem BuildSinglePath()
    {
        return new OpenApiPathItem
        {
            Parameters = new List<IOpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "key",
                    In = ParameterLocation.Path,
                    Required = true,
                    Description = "The product GUID key",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                },
            },
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Get a product by key",
                    Parameters = new List<IOpenApiParameter>(SingleParameters),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(ProductSchema()),
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                        ["404"] = new OpenApiResponse { Description = "Not Found" },
                    },
                },
                [HttpMethod.Put] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Replace a product",
                    RequestBody = JsonBody(ProductSchema()),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(ProductSchema()),
                        ["400"] = new OpenApiResponse { Description = "Bad Request" },
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                        ["404"] = new OpenApiResponse { Description = "Not Found" },
                    },
                },
                [HttpMethod.Patch] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Partially update a product",
                    RequestBody = JsonBody(ProductSchema()),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(ProductSchema()),
                        ["400"] = new OpenApiResponse { Description = "Bad Request" },
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                        ["404"] = new OpenApiResponse { Description = "Not Found" },
                    },
                },
                [HttpMethod.Delete] = new OpenApiOperation
                {
                    Tags = ProductsTags,
                    Summary = "Delete a product",
                    Responses = new OpenApiResponses
                    {
                        ["204"] = new OpenApiResponse { Description = "No Content" },
                        ["401"] = new OpenApiResponse { Description = "Unauthorized" },
                        ["404"] = new OpenApiResponse { Description = "Not Found" },
                    },
                },
            },
        };
    }

    private static OpenApiSchema ProductSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["price"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" },
                ["brand"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["modelNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["serialNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["purchaseDate"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
                ["category"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["description"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["manualUrl"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["createdAt"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                ["updatedAt"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
            },
        };
    }

    private static OpenApiSchema CollectionSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["value"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = ProductSchema() },
            },
        };
    }

    private static OpenApiResponse JsonResponse(OpenApiSchema schema, string description = "OK")
    {
        return new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType { Schema = schema },
            },
        };
    }

    private static OpenApiRequestBody JsonBody(OpenApiSchema schema)
    {
        return new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType { Schema = schema },
            },
        };
    }

    private static OpenApiParameter MakeQueryParam(string name, string description)
    {
        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            Description = description,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
        };
    }
}
