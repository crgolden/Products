namespace Products.Tests.Unit.Controllers;

using System.Reflection;
using Microsoft.AspNetCore.OData.Query;
using Products.Controllers;

[Trait("Category", "Unit")]
public class MaterializedListActionsTests
{
    public static TheoryData<Type> ListControllers() =>
        [typeof(CatalogProductsController), typeof(InventoryItemsController)];

    [Theory]
    [MemberData(nameof(ListControllers))]
    public void TheListAction_MaterializesTheQueryBeforeTheResponseStarts(Type controller)
    {
        // Act
        var list = ListAction(controller);

        // Assert
        Assert.NotNull(list);
        Assert.NotNull(list.GetCustomAttribute<MaterializeODataListAttribute>());
    }

    private static MethodInfo? ListAction(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(m => m.GetParameters() is { Length: 1 } parameters
                && parameters[0].ParameterType.IsGenericType
                && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(ODataQueryOptions<>));
}
