namespace Products.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public static class InventoryItemClassMap
{
    public static void Register() =>
        BsonClassMap.TryRegisterClassMap<InventoryItem>(bsonClassMap =>
        {
            bsonClassMap.AutoMap();
            bsonClassMap.MapIdMember(i => i.Id).SetSerializer(new GuidSerializer(BsonType.String));
            bsonClassMap.MapMember(i => i.OwnerId).SetSerializer(new GuidSerializer(BsonType.String));
            bsonClassMap.MapMember(i => i.CatalogProductId).SetSerializer(new GuidSerializer(BsonType.String));
        });
}
