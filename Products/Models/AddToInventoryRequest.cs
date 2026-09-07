namespace Products.Models;

public class AddToInventoryRequest
{
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
}