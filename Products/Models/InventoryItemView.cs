namespace Products.Models;

public class InventoryItemView
{
#pragma warning disable S6964 // nullable is not appropriate for the key, the catalog pointer, or a timestamp that must be present on every record
    public Guid Id { get; set; }

    public Guid CatalogProductId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
#pragma warning restore S6964

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