namespace Products.Models;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class InventoryItemView
{
    public Guid Id { get; set; }

    public Guid CatalogProductId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? Name { get; set; }

    public string? Brand { get; set; }

    public string? ModelNumber { get; set; }

    public string? Category { get; set; }

    public string? ManualUrl { get; set; }

    public decimal? MsrpPrice { get; set; }

    public string? SerialNumber { get; set; }

    public DateTimeOffset? PurchaseDate { get; set; }

    public decimal? PricePaid { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
