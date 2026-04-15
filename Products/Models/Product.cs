namespace Products.Models;

public class Product
{
    // Server-managed fields: always set by the controller, never trusted from client input.
    // S6964 suppressed — nullable is not appropriate for the OData entity key or a timestamp
    // that must be present on every record.
#pragma warning disable S6964
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
#pragma warning restore S6964

    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public string? Brand { get; set; }

    public string? ModelNumber { get; set; }

    public string? SerialNumber { get; set; }

    public DateTimeOffset? PurchaseDate { get; set; }

    public string? Category { get; set; }

    public string? Description { get; set; }

    // Will be populated from the Manuals API once a product-manual linking feature is built.
    public string? ManualUrl { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
