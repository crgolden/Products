namespace Products.Tests.Unit.Controllers;

using System.Collections;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Products.Controllers;
using Products.Models;

public class MaterializeODataListAttributeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void AProviderFailure_SurfacesBeforeTheResponseStarts_RatherThanTruncatingA200()
    {
        var context = ResultExecuting(new ObjectResult(new ThrowingQueryable<CatalogProduct>()));

        var thrown = Record.Exception(() => new MaterializeODataListAttribute().OnResultExecuting(context));

        Assert.IsType<InvalidOperationException>(thrown);
        Assert.False(
            context.HttpContext.Response.HasStarted,
            "the whole point is that the failure happens while the status code can still be changed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AListResult_IsReplacedByAMaterialisedListOfTheSameElementType()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var rows = new[] { new CatalogProduct { Id = firstProductId }, new CatalogProduct { Id = secondProductId } };
        var result = new ObjectResult(rows.AsQueryable());
        var context = ResultExecuting(result);

        new MaterializeODataListAttribute().OnResultExecuting(context);

        var materialized = Assert.IsType<List<CatalogProduct>>(result.Value);
        Assert.Equal(rows.Length, materialized.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AResultThatIsNotAQueryable_IsLeftAlone()
    {
        var productId = Guid.NewGuid();
        var result = new ObjectResult(new CatalogProduct { Id = productId });
        var context = ResultExecuting(result);

        new MaterializeODataListAttribute().OnResultExecuting(context);

        var unchanged = Assert.IsType<CatalogProduct>(result.Value);
        Assert.Equal(productId, unchanged.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ANonObjectResult_IsLeftAlone()
    {
        var context = ResultExecuting(new UnauthorizedResult());

        new MaterializeODataListAttribute().OnResultExecuting(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    private static ResultExecutingContext ResultExecuting(IActionResult result) =>
        new(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            [],
            result,
            controller: new object());

    private sealed class ThrowingQueryable<T> : IQueryable<T>
    {
        public Type ElementType => typeof(T);

        public Expression Expression => Expression.Constant(this);

        public IQueryProvider Provider => throw new InvalidOperationException("the provider failed");

        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("the provider failed");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
