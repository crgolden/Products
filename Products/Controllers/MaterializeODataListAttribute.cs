namespace Products.Controllers;

using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public sealed class MaterializeODataListAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult result || result.Value is not IQueryable queryable)
        {
            return;
        }

        var listType = typeof(List<>).MakeGenericType(queryable.ElementType);
        var materialized = Activator.CreateInstance(listType) as IList
            ?? throw new InvalidOperationException($"Could not create a list of {queryable.ElementType}.");
        foreach (var item in queryable)
        {
            materialized.Add(item);
        }

        result.Value = materialized;
    }
}
