namespace Products.OpenApi;

using System.Net.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

public class ODataQueryParameterTransformer : IOpenApiDocumentTransformer
{
    private const string UnauthorizedDescription = "Unauthorized";

    private static readonly HashSet<OpenApiTagReference> CatalogTags = new HashSet<OpenApiTagReference>
    {
        new OpenApiTagReference("CatalogProducts"),
    };

    private static readonly HashSet<OpenApiTagReference> InventoryTags = new HashSet<OpenApiTagReference>
    {
        new OpenApiTagReference("InventoryItems"),
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

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        EnsureSecurity(document);
        EnsurePaths(document);
        return Task.CompletedTask;
    }

    private static void EnsureSecurity(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (!document.Components.SecuritySchemes.ContainsKey("Bearer"))
        {
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Bearer token issued by the OIDC authority.",
            };
        }

        document.Security ??= new List<OpenApiSecurityRequirement>();
        if (document.Security.Count == 0)
        {
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = new List<string>(),
            });
        }
    }

    private static void EnsurePaths(OpenApiDocument document)
    {
        document.Paths ??= new OpenApiPaths();

        if (!document.Paths.ContainsKey("/odata/CatalogProducts"))
        {
            document.Paths["/odata/CatalogProducts"] = BuildCatalogListPath();
        }

        if (!document.Paths.ContainsKey("/odata/InventoryItems"))
        {
            document.Paths["/odata/InventoryItems"] = BuildInventoryListPath();
        }

        if (!document.Paths.ContainsKey("/inventory/items"))
        {
            document.Paths["/inventory/items"] = BuildAddToInventoryPath();
        }
    }

    private static OpenApiPathItem BuildCatalogListPath()
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    Tags = CatalogTags,
                    Summary = "Browse the public product catalog (anonymous)",
                    Parameters = new List<IOpenApiParameter>(ListParameters),
                    Security = new List<OpenApiSecurityRequirement>(),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(CollectionOf(CatalogProductSchema())),
                    },
                },
            },
        };
    }

    private static OpenApiPathItem BuildInventoryListPath()
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    Tags = InventoryTags,
                    Summary = "Get the signed-in owner's inventory items",
                    Parameters = new List<IOpenApiParameter>(ListParameters),
                    Responses = new OpenApiResponses
                    {
                        ["200"] = JsonResponse(CollectionOf(InventoryItemSchema())),
                        ["401"] = new OpenApiResponse { Description = UnauthorizedDescription },
                    },
                },
            },
        };
    }

    private static OpenApiPathItem BuildAddToInventoryPath()
    {
        return new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Post] = new OpenApiOperation
                {
                    Tags = InventoryTags,
                    Summary = "Add an item to the signed-in owner's inventory, creating the catalog product if none matches",
                    RequestBody = JsonBody(AddToInventoryRequestSchema()),
                    Responses = new OpenApiResponses
                    {
                        ["201"] = JsonResponse(InventoryItemSchema(), "Created"),
                        ["400"] = new OpenApiResponse { Description = "Bad Request" },
                        ["401"] = new OpenApiResponse { Description = UnauthorizedDescription },
                    },
                },
            },
        };
    }

    private static OpenApiSchema CatalogProductSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["brand"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["modelNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["category"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["manualUrl"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["msrpPrice"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" },
                ["createdAt"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                ["updatedAt"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
            },
        };
    }

    private static OpenApiSchema InventoryItemSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["id"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["ownerId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid", ReadOnly = true },
                ["catalogProductId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["serialNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["purchaseDate"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
                ["pricePaid"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" },
                ["description"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["createdAt"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" },
                ["updatedAt"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
            },
        };
    }

    private static OpenApiSchema AddToInventoryRequestSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["brand"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["modelNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["category"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["manualUrl"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["msrpPrice"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" },
                ["serialNumber"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["purchaseDate"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "date-time" },
                ["pricePaid"] = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "decimal" },
                ["description"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            },
        };
    }

    private static OpenApiSchema CollectionOf(OpenApiSchema itemSchema)
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["value"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = itemSchema },
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