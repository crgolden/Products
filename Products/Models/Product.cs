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

    public DateTimeOffset? UpdatedAt { get; set; }
}
