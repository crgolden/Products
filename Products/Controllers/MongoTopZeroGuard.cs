namespace Products.Controllers;

using Microsoft.AspNetCore.OData.Query;

internal static class MongoTopZeroGuard
{
    internal static IQueryable<T> WithoutAServerSideLimitOfZero<T>(
        IQueryable<T> source,
        ODataQueryOptions queryOptions) =>
        queryOptions.Top?.Value == 0 ? Streamed(source).AsQueryable() : source;

    private static IEnumerable<T> Streamed<T>(IQueryable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
    }
}
