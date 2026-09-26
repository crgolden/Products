namespace Products.Tests.Unit.TestSupport;

using Microsoft.AspNetCore.OData.Routing.Controllers;

internal static class ConventionalEntitySets
{
    internal static string For<TController>()
        where TController : ODataController
    {
        const string suffix = AspNetCoreConventionConstants.ControllerSuffix;
        var controllerName = typeof(TController).Name;
        if (!controllerName.EndsWith(suffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{controllerName} does not end in '{suffix}', so it names no conventional entity set.");
        }

        return controllerName[..^suffix.Length];
    }
}
