namespace Products.Models;

public class InventoryItem
{
#pragma warning disable S6964 // nullable is not appropriate for the OData entity key, the owner, the catalog pointer, or a timestamp that must be present on every record
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid CatalogProductId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
#pragma warning restore S6964

    public string? SerialNumber { get; set; }

    public DateTimeOffset? PurchaseDate { get; set; }

    public decimal? PricePaid { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}