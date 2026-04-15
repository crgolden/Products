namespace Products.OpenApi;

using System.Net.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

public class ODataQueryParameterTransformer : IOpenApiDocumentTransformer
{
    private static readonly IList<OpenApiParameter> ListParameters =
    [
        MakeQueryParam("$filter", "Filters results using OData filter syntax, e.g. Name eq 'Widget'"),
        MakeQueryParam("$select", "Selects a subset of properties, e.g. Id,Name,Price"),
        MakeQueryParam("$orderby", "Orders results, e.g. Price desc"),
        MakeQueryParam("$top", "Limits the number of results returned (max 100)"),
        MakeQueryParam("$skip", "Skips the specified number of results"),
        MakeQueryParam("$count", "Includes a total count of matching results when set to true"),
        MakeQueryParam("$expand", "Expands related entities inline"),
    ];

    private static readonly IList<OpenApiParameter> SingleParameters =
    [
        MakeQueryParam("$select", "Selects a subset of properties, e.g. Id,Name,Price"),
        MakeQueryParam("$expand", "Expands related entities inline"),
    ];

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var (path, item) in document.Paths)
        {
            if (!path.StartsWith("/odata/Products", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.Operations is null || !item.Operations.TryGetValue(HttpMethod.Get, out var getOperation) || getOperation is null)
            {
                continue;
            }

            getOperation.Parameters ??= [];

            var isSingle = path.EndsWith(')');
            var paramsToAdd = isSingle ? SingleParameters : ListParameters;
            foreach (var param in paramsToAdd.Where(p => getOperation.Parameters.All(existing => !string.Equals(existing.Name, p.Name))))
            {
                getOperation.Parameters.Add(param);
            }
        }

        return Task.CompletedTask;
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
