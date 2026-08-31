namespace Products.Tests.Unit.Models;

using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Products.Models;
using Products.Tests.Unit.TestSupport;

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
    public void Product_DefaultOwnerId_IsNull()
    {
        var product = new Product();
        Assert.Null(product.OwnerId);
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
    public void Product_OwnerId_SerializesToStringGuid_WhenBsonClassMapRegistered()
    {
        ProductClassMap.Register();

        var ownerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, OwnerId = ownerId };
        var document = product.ToBsonDocument();

        Assert.Equal(BsonType.String, document[nameof(Product.OwnerId)].BsonType);
        Assert.Equal(ownerId.ToString(), document[nameof(Product.OwnerId)].AsString);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_ManualUrl_RoundTripsThroughBson_WhenBsonClassMapRegistered()
    {
        ProductClassMap.Register();

        var manualUrl = $"https://example.invalid/manuals/{Guid.NewGuid():N}.pdf";
        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, ManualUrl = manualUrl };

        var restored = BsonSerializer.Deserialize<Product>(product.ToBsonDocument());

        Assert.Equal(manualUrl, restored.ManualUrl);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_ManualUrl_ReadsADocumentWrittenWhileTheMemberWasAUri()
    {
        ProductClassMap.Register();

        var productId = Guid.NewGuid();
        var writtenByTheUriTypedBuild = new Product { Id = productId }.ToBsonDocument();
        var storedUrl = new Uri($"https://example.invalid/manuals/{Guid.NewGuid():N}.pdf").AbsoluteUri;
        writtenByTheUriTypedBuild[nameof(Product.ManualUrl)] = storedUrl;

        var restored = BsonSerializer.Deserialize<Product>(writtenByTheUriTypedBuild);

        Assert.Equal(storedUrl, restored.ManualUrl);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_ManualUrl_IsStoredAsABsonString_NotANestedDocument()
    {
        ProductClassMap.Register();

        var manualUrl = $"https://example.invalid/manuals/{Guid.NewGuid():N}.pdf";
        var productId = Guid.NewGuid();
        var product = new Product { Id = productId, ManualUrl = manualUrl };

        var document = product.ToBsonDocument();

        Assert.Equal(BsonType.String, document[nameof(Product.ManualUrl)].BsonType);
        Assert.Equal(manualUrl, document[nameof(Product.ManualUrl)].AsString);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_ManualUrl_IsDeclaredAsAnEdmString_SoAClientCanSendAUrl()
    {
        var modelBuilder = new ODataConventionModelBuilder();
        modelBuilder.EntitySet<Product>("Products");

        var model = modelBuilder.GetEdmModel();
        var entity = Assert.IsType<IEdmEntityType>(
            model.FindDeclaredType($"{typeof(Product).Namespace}.{nameof(Product)}"),
            exactMatch: false);
        var manualUrl = Assert.IsType<IEdmStructuralProperty>(
            entity.FindProperty(nameof(Product.ManualUrl)),
            exactMatch: false);

        var kind = manualUrl.Type.Definition.AsElementType() is IEdmPrimitiveType primitive
            ? primitive.PrimitiveKind
            : EdmPrimitiveTypeKind.None;

        Assert.Equal(EdmPrimitiveTypeKind.String, kind);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_ManualUrl_RoundTripsAsNull_WhenUnset()
    {
        ProductClassMap.Register();

        var productId = Guid.NewGuid();
        var restored = BsonSerializer.Deserialize<Product>(new Product { Id = productId }.ToBsonDocument());

        Assert.Null(restored.ManualUrl);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Product_CanSetProperties()
    {
        var productId = Guid.NewGuid();
        var productName = TestValues.NewProductName();
        var price = TestValues.NewPrice();
        var now = DateTimeOffset.UtcNow;
        var product = new Product
        {
            Id = productId,
            Name = productName,
            Price = price,
            CreatedAt = now,
            UpdatedAt = now,
        };

        Assert.Equal(productId, product.Id);
        Assert.Equal(productName, product.Name);
        Assert.Equal(price, product.Price);
        Assert.Equal(now, product.CreatedAt);
        Assert.Equal(now, product.UpdatedAt);
    }
}