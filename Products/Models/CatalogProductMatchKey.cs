namespace Products.Models;

public static class CatalogProductMatchKey
{
    public static string? Compute(string? brand, string? modelNumber)
    {
        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(modelNumber))
        {
            return null;
        }

        return $"{brand.Trim().ToUpperInvariant()}::{modelNumber.Trim().ToUpperInvariant()}";
    }
}