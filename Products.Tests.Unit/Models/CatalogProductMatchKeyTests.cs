namespace Products.Tests.Unit.Models;

using Products.Models;

[Trait("Category", "Unit")]
public class CatalogProductMatchKeyTests
{
    [Fact]
    public void Compute_ReturnsNull_WhenBrandIsMissing()
    {
        // Arrange
        var modelNumber = Generated.NewModelNumber();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(null, modelNumber);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    public void Compute_ReturnsNull_WhenModelNumberIsMissing()
    {
        // Arrange
        var brand = Generated.NewBrand();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(brand, null);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    public void Compute_ReturnsNull_WhenBrandIsWhitespace()
    {
        // Arrange
        var modelNumber = Generated.NewModelNumber();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(Generated.NewBlank(), modelNumber);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    public void Compute_IgnoresSurroundingWhitespace_SoATypedSpaceDoesNotFragmentTheCatalog()
    {
        // Arrange
        var brand = Generated.NewBrand();
        var modelNumber = Generated.NewModelNumber();

        var paddedBrand = string.Concat(Generated.NewBlank(), brand, Generated.NewBlank());
        var paddedModelNumber = string.Concat(Generated.NewBlank(), modelNumber, Generated.NewBlank());
        var bare = CatalogProductMatchKey.Compute(brand, modelNumber);

        // Act
        var padded = CatalogProductMatchKey.Compute(paddedBrand, paddedModelNumber);

        // Assert
        Assert.Equal(bare, padded);
    }

    [Fact]
    public void Compute_IgnoresCase_SoTheSameProductTypedDifferentlyStillMatches()
    {
        // Arrange
        var brand = Generated.NewBrand();
        var modelNumber = Generated.NewModelNumber();

        var upper = CatalogProductMatchKey.Compute(brand.ToUpperInvariant(), modelNumber.ToUpperInvariant());

        // Act
        var lower = CatalogProductMatchKey.Compute(brand.ToLowerInvariant(), modelNumber.ToLowerInvariant());

        // Assert
        Assert.Equal(upper, lower);
    }

    [Fact]
    public void Compute_DistinguishesDifferentModelNumbersOfTheSameBrand()
    {
        // Arrange
        var brand = Generated.NewBrand();

        var first = CatalogProductMatchKey.Compute(brand, Generated.NewModelNumber());

        // Act
        var second = CatalogProductMatchKey.Compute(brand, Generated.NewModelNumber());

        // Assert
        Assert.NotEqual(first, second);
    }
}
