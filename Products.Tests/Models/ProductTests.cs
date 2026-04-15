namespace Products.Tests.Models;

using Products.Models;

public class ProductTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Product_DefaultId_IsEmptyGuid()
    {
        var product = new Product();
        Assert.Equal(Guid.Empty, product.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_DefaultPrice_IsNull()
    {
        var product = new Product();
        Assert.Null(product.Price);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_DefaultName_IsNull()
    {
        var product = new Product();
        Assert.Null(product.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_DefaultCreatedAt_IsNotDefault()
    {
        var product = new Product();
        Assert.NotEqual(default, product.CreatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_DefaultUpdatedAt_IsNull()
    {
        var product = new Product();
        Assert.Null(product.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_CanSetProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = id,
            Name = "Widget",
            Price = 9.99m,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Assert.Equal(id, product.Id);
        Assert.Equal("Widget", product.Name);
        Assert.Equal(9.99m, product.Price);
        Assert.Equal(now, product.CreatedAt);
        Assert.Equal(now, product.UpdatedAt);
    }
}
