namespace Products.Controllers;

using Authorization;
using HostedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Models;
using MongoDB.AspNetCore.OData;
using MongoDB.Driver;

public class CatalogProductsController : ODataController
{
    private readonly IMongoCollection<CatalogProduct> _catalogProducts;

    public CatalogProductsController(IMongoDatabase database)
    {
        _catalogProducts = database.GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName);
    }

    [AllowAnonymous]
    [HttpGet]
    [MongoEnableQuery(HandleNullPropagation = HandleNullPropagationOption.False)]
    public IQueryable<CatalogProduct> Get(ODataQueryOptions<CatalogProduct> queryOptions) =>
        MongoTopZeroGuard.WithoutAServerSideLimitOfZero(_catalogProducts.AsQueryable(), queryOptions);

    [AllowAnonymous]
    [HttpGet]
    [EnableQuery]
    public async Task<SingleResult<CatalogProduct>> Get(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        var cursor = await _catalogProducts.FindAsync(c => c.Id == key, cancellationToken: cancellationToken);
        var items = await cursor.ToListAsync(cancellationToken);
        return SingleResult.Create(items.AsQueryable());
    }

    [Authorize(Policy = nameof(Products))]
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] CatalogProduct catalogProduct,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        catalogProduct.Id = Guid.NewGuid();
        catalogProduct.CreatedAt = DateTimeOffset.UtcNow;
        catalogProduct.MatchKey = CatalogProductMatchKey.Compute(catalogProduct.Brand, catalogProduct.ModelNumber);
        try
        {
            await _catalogProducts.InsertOneAsync(catalogProduct, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Conflict();
        }

        return Created(catalogProduct);
    }

    [Authorize(Policy = nameof(Products))]
    [HttpPatch]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        [FromBody] Delta<CatalogProduct> delta,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var cursor = await _catalogProducts.FindAsync(c => c.Id == key, cancellationToken: cancellationToken);
        var existing = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var createdAt = existing.CreatedAt;
        delta.Patch(existing);

        existing.CreatedAt = createdAt;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.MatchKey = CatalogProductMatchKey.Compute(existing.Brand, existing.ModelNumber);
        try
        {
            await _catalogProducts.ReplaceOneAsync(c => c.Id == key, existing, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return Conflict();
        }

        return Updated(existing);
    }
}