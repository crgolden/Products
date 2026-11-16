namespace Products.Models;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CatalogProduct
{
    internal const string EntitySetName = "CatalogProducts";

    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Name { get; set; }

    public string? Brand { get; set; }

    public string? ModelNumber { get; set; }

    public string? Category { get; set; }

    public string? ManualUrl { get; set; }

    public decimal? MsrpPrice { get; set; }

    public string? MatchKey { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
