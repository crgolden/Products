namespace Products.Tests.Unit.Authorization;

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Products.Authorization;
using Products.Models;

public sealed class ProductAuthorizationHandlerTests : IDisposable
{
    private static readonly OperationAuthorizationRequirement EditRequirement = ProductOperations.Edit;
    private static readonly OperationAuthorizationRequirement DeleteRequirement = ProductOperations.Delete;

    private readonly ActivityListener _activityListener;

    public ProductAuthorizationHandlerTests()
    {
        // A listener makes Telemetry.ActivitySource.StartActivity return a non-null Activity, so the
        // handler's activity?.SetTag(...) branches execute rather than short-circuiting on null.
        _activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose() => _activityListener.Dispose();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_Succeeds_WhenOwner()
    {
        var ownerId = Guid.NewGuid();
        var product = new Product { OwnerId = ownerId };
        var user = MakeUser(ownerId);
        var context = MakeContext(user, product, EditRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenNotOwner()
    {
        var product = new Product { OwnerId = Guid.NewGuid() };
        var user = MakeUser(Guid.NewGuid());
        var context = MakeContext(user, product, EditRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenProductHasNoOwner()
    {
        var product = new Product { OwnerId = null };
        var user = MakeUser(Guid.NewGuid());
        var context = MakeContext(user, product, EditRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenUserHasNoSubClaim()
    {
        var product = new Product { OwnerId = Guid.NewGuid() };
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Bearer"));
        var context = MakeContext(user, product, EditRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenSubClaimIsNotAGuid()
    {
        var product = new Product { OwnerId = Guid.NewGuid() };
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "not-a-guid")], "Bearer"));
        var context = MakeContext(user, product, EditRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HandleRequirementAsync_Succeeds_ForDeleteRequirement_WhenOwner()
    {
        var ownerId = Guid.NewGuid();
        var product = new Product { OwnerId = ownerId };
        var user = MakeUser(ownerId);
        var context = MakeContext(user, product, DeleteRequirement);
        var handler = new ProductAuthorizationHandler();

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private static ClaimsPrincipal MakeUser(Guid userId) =>
        new(new ClaimsIdentity(
            [new Claim("sub", userId.ToString())],
            authenticationType: "Bearer"));

    private static AuthorizationHandlerContext MakeContext(
        ClaimsPrincipal user,
        Product resource,
        IAuthorizationRequirement requirement) =>
        new([requirement], user, resource);
}
