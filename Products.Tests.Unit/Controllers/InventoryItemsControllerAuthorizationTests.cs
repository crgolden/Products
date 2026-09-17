namespace Products.Tests.Unit.Controllers;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Products.Controllers;

public class InventoryItemsControllerAuthorizationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_RequiresAuthorization_AtTheClassLevel()
    {
        // Act
        var authorize = typeof(InventoryItemsController).GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authorize);
        Assert.Equal(nameof(Products), authorize.Policy);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_ExposesActionsToReflect_SoTheAnonymousCheckIsNotVacuous()
    {
        // Act
        var actions = PublicActions();

        // Assert
        Assert.NotEmpty(actions);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InventoryItemsController_DeclaresNoAnonymousAction()
    {
        // Act
        var anonymous = PublicActions()
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name);

        // Assert
        Assert.Empty(anonymous);
    }

    private static MethodInfo[] PublicActions() =>
        typeof(InventoryItemsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
}