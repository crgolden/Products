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

        var materialized = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(queryable.ElementType))!;
        foreach (var item in queryable)
        {
            materialized.Add(item);
        }

        result.Value = materialized;
    }
}
