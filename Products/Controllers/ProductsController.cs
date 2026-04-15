namespace Products.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Models;
using MongoDB.AspNetCore.OData;
using MongoDB.Driver;

[Authorize(Policy = nameof(Products))]
public class ProductsController : ODataController
{
    private readonly IMongoCollection<Product> _products;

    public ProductsController(IMongoDatabase database)
    {
        _products = database.GetCollection<Product>("Products");
    }

    [HttpGet]
    [MongoEnableQuery]
    public IQueryable<Product> Get()
    {
        return _products.AsQueryable();
    }

    [HttpGet]
    [EnableQuery]
    public async Task<SingleResult<Product>> Get(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        var cursor = await _products.FindAsync(p => p.Id == key, cancellationToken: cancellationToken);
        var items = await cursor.ToListAsync(cancellationToken);
        return SingleResult.Create(items.AsQueryable());
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Product product,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTimeOffset.UtcNow;
        await _products.InsertOneAsync(product, cancellationToken: cancellationToken);
        return Created(product);
    }

    [HttpPut]
    public async Task<IActionResult> Put(
        [FromRoute] Guid key,
        [FromBody] Product product,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cursor = await _products.FindAsync(p => p.Id == key, cancellationToken: cancellationToken);
        var existing = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        product.Id = key;
        product.CreatedAt = existing.CreatedAt;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _products.ReplaceOneAsync(p => p.Id == key, product, cancellationToken: cancellationToken);
        return Updated(product);
    }

    [HttpPatch]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        [FromBody] Delta<Product> delta,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cursor = await _products.FindAsync(p => p.Id == key, cancellationToken: cancellationToken);
        var existing = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        delta.Patch(existing);
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _products.ReplaceOneAsync(p => p.Id == key, existing, cancellationToken: cancellationToken);
        return Updated(existing);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _products.DeleteOneAsync(p => p.Id == key, cancellationToken);
        if (result.DeletedCount == 0)
        {
            return NotFound();
        }

        return NoContent();
    }
}
