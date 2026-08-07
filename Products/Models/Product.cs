namespace Products.Models;

public class Product
{
#pragma warning disable S6964 // nullable is not appropriate for the OData entity key or a timestamp that must be present on every record
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

    public string? ManualUrl { get; set; }

    public Guid? OwnerId { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}