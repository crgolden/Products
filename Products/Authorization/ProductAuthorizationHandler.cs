namespace Products.Authorization;

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Models;

public class ProductAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, Product>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Product resource)
    {
        var sub = context.User.FindFirstValue("sub");
        var authorized = sub != null && Guid.TryParse(sub, out var userId) && resource.OwnerId == userId;

        using var activity = Telemetry.ActivitySource.StartActivity("products.authorization.check_ownership");
        activity?.SetTag("product.owner_id", resource.OwnerId?.ToString());
        activity?.SetTag("user.id", sub);
        activity?.SetTag("operation", requirement.Name);
        activity?.SetTag("authorized", authorized);

        if (authorized)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
