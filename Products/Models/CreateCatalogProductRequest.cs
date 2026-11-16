namespace Products.Models;

using JetBrains.Annotations;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CreateCatalogProductRequest
{
    public string? Name { get; set; }

    public string? Brand { get; set; }

    public string? ModelNumber { get; set; }

    public string? Category { get; set; }

    public Uri? ManualUrl { get; set; }

    public decimal? MsrpPrice { get; set; }
}
