namespace Products.Tests.Unit.Models;

using Products.Models;
using Products.Tests.Unit.TestSupport;

public class CatalogProductMatchKeyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenBrandIsMissing()
    {
        var modelNumber = TestValues.NewModelNumber();

        Assert.Null(CatalogProductMatchKey.Compute(null, modelNumber));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenModelNumberIsMissing()
    {
        var brand = TestValues.NewBrand();

        Assert.Null(CatalogProductMatchKey.Compute(brand, null));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenBrandIsWhitespace()
    {
        var modelNumber = TestValues.NewModelNumber();

        Assert.Null(CatalogProductMatchKey.Compute("   ", modelNumber));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_IgnoresSurroundingWhitespace_SoATypedSpaceDoesNotFragmentTheCatalog()
    {
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();

        var padded = CatalogProductMatchKey.Compute($"  {brand} ", $" {modelNumber}  ");
        var bare = CatalogProductMatchKey.Compute(brand, modelNumber);

        Assert.Equal(bare, padded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_IgnoresCase_SoTheSameProductTypedDifferentlyStillMatches()
    {
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();

        var upper = CatalogProductMatchKey.Compute(brand.ToUpperInvariant(), modelNumber.ToUpperInvariant());
        var lower = CatalogProductMatchKey.Compute(brand.ToLowerInvariant(), modelNumber.ToLowerInvariant());

        Assert.Equal(upper, lower);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_DistinguishesDifferentModelNumbersOfTheSameBrand()
    {
        var brand = TestValues.NewBrand();

        var first = CatalogProductMatchKey.Compute(brand, TestValues.NewModelNumber());
        var second = CatalogProductMatchKey.Compute(brand, TestValues.NewModelNumber());

        Assert.NotEqual(first, second);
    }
}