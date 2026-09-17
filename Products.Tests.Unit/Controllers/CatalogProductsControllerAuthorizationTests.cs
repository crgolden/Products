namespace Products.Tests.Unit.Controllers;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Products.Controllers;

public class CatalogProductsControllerAuthorizationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProductsController_DeleteAction_RequiresTheProductsPolicy()
    {
        // Act
        var delete = CatalogDeleteAction();

        // Assert
        Assert.NotNull(delete);
        var authorize = delete.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(nameof(Products), authorize.Policy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProductsController_DeleteAction_IsNotAnonymous()
    {
        // Act
        var delete = CatalogDeleteAction();

        // Assert
        Assert.NotNull(delete);
        Assert.Null(delete.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static MethodInfo? CatalogDeleteAction() =>
        typeof(CatalogProductsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(m => m.GetCustomAttribute<HttpDeleteAttribute>() is not null);
}
