namespace Products.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public static class CatalogProductClassMap
{
    public static void Register() =>
        BsonClassMap.TryRegisterClassMap<CatalogProduct>(bsonClassMap =>
        {
            bsonClassMap.AutoMap();
            bsonClassMap.MapIdMember(c => c.Id).SetSerializer(new GuidSerializer(BsonType.String));
        });
}
