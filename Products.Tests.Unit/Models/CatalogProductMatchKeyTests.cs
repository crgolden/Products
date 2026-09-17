namespace Products.Tests.Unit.Models;

using Products.Models;
using Products.Tests.Unit.TestSupport;

public class CatalogProductMatchKeyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenBrandIsMissing()
    {
        // Arrange
        var modelNumber = TestValues.NewModelNumber();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(null, modelNumber);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenModelNumberIsMissing()
    {
        // Arrange
        var brand = TestValues.NewBrand();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(brand, null);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_ReturnsNull_WhenBrandIsWhitespace()
    {
        // Arrange
        var modelNumber = TestValues.NewModelNumber();

        // Act
        var matchKey = CatalogProductMatchKey.Compute(TestValues.NewBlank(), modelNumber);

        // Assert
        Assert.Null(matchKey);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_IgnoresSurroundingWhitespace_SoATypedSpaceDoesNotFragmentTheCatalog()
    {
        // Arrange
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();

        var paddedBrand = string.Concat(TestValues.NewBlank(), brand, TestValues.NewBlank());
        var paddedModelNumber = string.Concat(TestValues.NewBlank(), modelNumber, TestValues.NewBlank());
        var bare = CatalogProductMatchKey.Compute(brand, modelNumber);

        // Act
        var padded = CatalogProductMatchKey.Compute(paddedBrand, paddedModelNumber);

        // Assert
        Assert.Equal(bare, padded);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_IgnoresCase_SoTheSameProductTypedDifferentlyStillMatches()
    {
        // Arrange
        var brand = TestValues.NewBrand();
        var modelNumber = TestValues.NewModelNumber();

        var upper = CatalogProductMatchKey.Compute(brand.ToUpperInvariant(), modelNumber.ToUpperInvariant());

        // Act
        var lower = CatalogProductMatchKey.Compute(brand.ToLowerInvariant(), modelNumber.ToLowerInvariant());

        // Assert
        Assert.Equal(upper, lower);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Compute_DistinguishesDifferentModelNumbersOfTheSameBrand()
    {
        // Arrange
        var brand = TestValues.NewBrand();

        var first = CatalogProductMatchKey.Compute(brand, TestValues.NewModelNumber());

        // Act
        var second = CatalogProductMatchKey.Compute(brand, TestValues.NewModelNumber());

        // Assert
        Assert.NotEqual(first, second);
    }
}