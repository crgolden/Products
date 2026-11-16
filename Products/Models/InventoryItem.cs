namespace Products.Models;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class InventoryItem
{
    internal const string EntitySetName = "InventoryItems";

    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid CatalogProductId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? SerialNumber { get; set; }

    public DateTimeOffset? PurchaseDate { get; set; }

    public decimal? PricePaid { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
