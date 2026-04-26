namespace Products.Tests.Infrastructure;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Test-only authentication handler for integration tests.
/// Always authenticates as an integration test user with the <c>products</c> scope.
/// The <c>sub</c> claim is a Guid string so that the <c>OwnerId</c> filter path in
/// <see cref="Products.Controllers.ProductsController.Get()"/> is exercised.
/// </summary>
internal sealed class IntegrationAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public IntegrationAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", ProductsWebApplicationFactory.TestUserId.ToString()),
            new Claim("scope", "products"),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
