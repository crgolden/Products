namespace Products.Authorization;

using Microsoft.AspNetCore.Authorization.Infrastructure;

public static class ProductOperations
{
    public static readonly OperationAuthorizationRequirement Edit = new() { Name = nameof(Edit) };

    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}
