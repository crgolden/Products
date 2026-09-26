namespace Products.Authorization;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

internal static class ProductClaims
{
    internal const string Subject = JwtRegisteredClaimNames.Sub;

    internal const string Scope = OpenIdConnectParameterNames.Scope;

    internal const string ProductsScope = "products";
}
