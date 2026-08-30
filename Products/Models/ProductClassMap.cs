namespace Products.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

public static class ProductClassMap
{
    public static void Register() =>
        BsonClassMap.TryRegisterClassMap<Product>(bsonClassMap =>
        {
            bsonClassMap.AutoMap();
            bsonClassMap.MapIdMember(p => p.Id).SetSerializer(new GuidSerializer(BsonType.String));
            bsonClassMap.MapMember(p => p.OwnerId).SetSerializer(new NullableSerializer<Guid>(new GuidSerializer(BsonType.String)));
        });
}
