namespace Products.Tests.Unit.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public sealed class ProductsWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string TestScheme = "Integration";

    internal static readonly Guid TestUserId = new("00000000-0000-0000-0001-000000000001");

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
