namespace Products.Tests.Unit.Controllers;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Products.Controllers;

public class InventoryItemsControllerAuthorizationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_RequiresAuthorization_AtTheClassLevel()
    {
        var authorize = typeof(InventoryItemsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(nameof(Products), authorize.Policy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_ExposesActionsToReflect_SoTheAnonymousCheckIsNotVacuous()
    {
        Assert.NotEmpty(PublicActions());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_DeclaresNoAnonymousAction()
    {
        var anonymous = PublicActions()
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name);

        Assert.Empty(anonymous);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CatalogProductsController_ExposesNoDeleteAction()
    {
        var delete = typeof(CatalogProductsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(m => m.GetCustomAttribute<HttpDeleteAttribute>() is not null);

        Assert.Null(delete);
    }

    private static MethodInfo[] PublicActions() =>
        typeof(InventoryItemsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
}