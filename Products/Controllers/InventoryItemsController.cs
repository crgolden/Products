namespace Products.Controllers;

using System.Security.Claims;
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

[Authorize(Policy = nameof(Products))]
public class InventoryItemsController : ODataController
{
    private readonly IMongoCollection<InventoryItem> _inventoryItems;

    public InventoryItemsController(IMongoDatabase database)
    {
        _inventoryItems = database.GetCollection<InventoryItem>(InventoryItemIndexInitializer.CollectionName);
    }

    private Guid? CurrentUserId =>
        User.FindFirstValue(ProductClaims.Subject) is string s && Guid.TryParse(s, out var g) ? g : null;

    [HttpGet]
    [MongoEnableQuery]
    public ActionResult<IQueryable<InventoryItem>> Get(ODataQueryOptions<InventoryItem> queryOptions)
    {
        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        return Ok(MongoTopZeroGuard.WithoutAServerSideLimitOfZero(
            _inventoryItems.AsQueryable().Where(i => i.OwnerId == ownerId),
            queryOptions));
    }

    [HttpGet]
    [EnableQuery]
    public async Task<ActionResult<SingleResult<InventoryItem>>> Get(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        var cursor = await _inventoryItems.FindAsync(
            i => i.Id == key && i.OwnerId == ownerId,
            cancellationToken: cancellationToken);
        var items = await cursor.ToListAsync(cancellationToken);
        return Ok(SingleResult.Create(items.AsQueryable()));
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] InventoryItem inventoryItem,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        inventoryItem.Id = Guid.NewGuid();
        inventoryItem.CreatedAt = DateTimeOffset.UtcNow;
        inventoryItem.OwnerId = ownerId;
        await _inventoryItems.InsertOneAsync(inventoryItem, cancellationToken: cancellationToken);
        return Created(inventoryItem);
    }

    [HttpPatch]
    public async Task<IActionResult> Patch(
        [FromRoute] Guid key,
        [FromBody] Delta<InventoryItem> delta,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        var cursor = await _inventoryItems.FindAsync(
            i => i.Id == key && i.OwnerId == ownerId,
            cancellationToken: cancellationToken);
        var existing = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var createdAt = existing.CreatedAt;
        var catalogProductId = existing.CatalogProductId;
        delta.Patch(existing);

        existing.OwnerId = ownerId;
        existing.CreatedAt = createdAt;
        existing.CatalogProductId = catalogProductId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _inventoryItems.ReplaceOneAsync(i => i.Id == key, existing, cancellationToken: cancellationToken);
        return Updated(existing);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid key,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        var result = await _inventoryItems.DeleteOneAsync(
            i => i.Id == key && i.OwnerId == ownerId,
            cancellationToken);

        return result.DeletedCount == 0 ? NotFound() : NoContent();
    }
}