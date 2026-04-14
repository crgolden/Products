namespace Products.Models;

using MongoDB.Driver;

public class MongoOptions : MongoClientSettings
{
    public string? DatabaseName { get; set; }
}
