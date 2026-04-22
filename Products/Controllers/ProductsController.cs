namespace Products.Controllers;

using System.Security.Claims;
using Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Models;
using MongoDB.AspNetCore.OData;
using MongoDB.Driver;

public class ProductsController : ODataController
{
    private readonly IMongoCollection<Product> _products;
    private readonly IAuthorizationService _authorizationService;

    public ProductsController(IMongoDatabase database, IAuthorizationService authorizationService)
    {
        _products = database.GetCollection<Product>("Products");
        _authorizationService = authorizationService;
    }

    private Guid? CurrentUserId =>
        User.FindFirstValue("sub") is string s && Guid.TryParse(s, out var g) ? g : null;

    [AllowAnonymous]
    [HttpGet]
    [MongoEnableQuery]
    public IQueryable<Product> Get()
    {
        var sub = User.FindFirstValue("sub");
        if (sub != null && Guid.TryParse(sub, out var ownerId))
        {
            return _products.AsQueryable().Where(p => p.OwnerId == ownerId);
        }

        return _products.AsQueryable();
    }

    [AllowAnonymous]
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

    [Authorize(Policy = nameof(Products))]
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
        product.OwnerId = CurrentUserId;
        await _products.InsertOneAsync(product, cancellationToken: cancellationToken);
        return Created(product);
    }

    [Authorize(Policy = nameof(Products))]
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

        var authResult = await _authorizationService.AuthorizeAsync(User, existing, ProductOperations.Edit);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        product.Id = key;
        product.CreatedAt = existing.CreatedAt;
        product.OwnerId = existing.OwnerId;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _products.ReplaceOneAsync(p => p.Id == key, product, cancellationToken: cancellationToken);
        return Updated(product);
    }

    [Authorize(Policy = nameof(Products))]
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

        var authResult = await _authorizationService.AuthorizeAsync(User, existing, ProductOperations.Edit);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var ownerId = existing.OwnerId;
        var createdAt = existing.CreatedAt;
        delta.Patch(existing);

        // Restore server-managed fields that the Delta must not overwrite.
        existing.OwnerId = ownerId;
        existing.CreatedAt = createdAt;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _products.ReplaceOneAsync(p => p.Id == key, existing, cancellationToken: cancellationToken);
        return Updated(existing);
    }

    [Authorize(Policy = nameof(Products))]
    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
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

        var authResult = await _authorizationService.AuthorizeAsync(User, existing, ProductOperations.Delete);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        await _products.DeleteOneAsync(p => p.Id == key, cancellationToken);
        return NoContent();
    }
}
