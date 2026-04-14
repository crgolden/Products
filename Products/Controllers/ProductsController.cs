namespace Products.Controllers;

using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.Options;
using Models;
using MongoDB.AspNetCore.OData;
using MongoDB.Driver;

public class ProductsController : ODataController
{
    private readonly IMongoCollection<Product> _products;
#pragma warning disable S4487
    private readonly MongoOptions _options;
#pragma warning restore S4487

    public ProductsController(IMongoDatabase database, IOptions<MongoOptions> options)
    {
        _options = options.Value;
        _products = database.GetCollection<Product>("Products");
    }

    [MongoEnableQuery] // Use this instead of [EnableQuery]
    public IQueryable<Product> Get()
    {
        return _products.AsQueryable();
    }
}