namespace Products.Tests.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
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
        // Mirror the registration in HostApplicationBuilderExtensions.AddPersistenceAsync.
        // Without the NullableSerializer<Guid> on OwnerId, ToBsonDocument() throws:
        // "GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified."
        BsonClassMap.TryRegisterClassMap<Product>(bsonClassMap =>
        {
            bsonClassMap.AutoMap();
            bsonClassMap.MapIdMember(p => p.Id).SetSerializer(new GuidSerializer(BsonType.String));
            bsonClassMap.MapMember(p => p.OwnerId).SetSerializer(new NullableSerializer<Guid>(new GuidSerializer(BsonType.String)));
        });

        var ownerId = Guid.NewGuid();
        var product = new Product { Id = Guid.NewGuid(), OwnerId = ownerId };
        var document = product.ToBsonDocument();

        Assert.Equal(BsonType.String, document["OwnerId"].BsonType);
        Assert.Equal(ownerId.ToString(), document["OwnerId"].AsString);
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
