namespace Products.Tests.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for integration tests.
/// Uses the real MongoDB connection established by <c>Products/Program.cs</c> at startup
/// (requires <c>az login</c> / <c>azure/login</c> and <c>ASPNETCORE_ENVIRONMENT=Development</c>).
/// Replaces the JWT Bearer auth with a test scheme so tests can call the API
/// without a real access token.
/// </summary>
/// <remarks>
/// Tests using this factory must delete any products they create.
/// Use <see cref="TestUserId"/> to identify test data — it is the <c>sub</c> claim
/// issued by <see cref="IntegrationAuthHandler"/>, which is stored as <c>OwnerId</c>
/// on any product created through this factory's client.
/// </remarks>
public sealed class ProductsWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The <c>sub</c> claim value issued by <see cref="IntegrationAuthHandler"/>.
    /// This is used as the <c>OwnerId</c> for products created by integration tests.
    /// </summary>
    internal const string TestScheme = "Integration";

    internal static readonly Guid TestUserId = new("00000000-0000-0000-0001-000000000001");

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace JWT Bearer auth with a test scheme that always succeeds.
            services.AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, IntegrationAuthHandler>(TestScheme, _ => { });

            // Re-register the Products policy to accept the test scheme's claims.
            services.AddAuthorizationBuilder()
                .AddPolicy("Products", policy =>
                    policy.RequireAuthenticatedUser().RequireClaim("scope", "products"));
        });
    }
}
