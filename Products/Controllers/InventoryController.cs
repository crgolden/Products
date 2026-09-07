namespace Products.Controllers;

using System.Security.Claims;
using Authorization;
using HostedServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models;
using MongoDB.Driver;

[ApiController]
[Route("inventory")]
[Authorize(Policy = nameof(Products))]
public class InventoryController : ControllerBase
{
    private readonly IMongoCollection<InventoryItem> _inventoryItems;
    private readonly IMongoCollection<CatalogProduct> _catalogProducts;

    public InventoryController(IMongoDatabase database)
    {
        _inventoryItems = database.GetCollection<InventoryItem>(InventoryItemIndexInitializer.CollectionName);
        _catalogProducts = database.GetCollection<CatalogProduct>(CatalogProductIndexInitializer.CollectionName);
    }

    private Guid? CurrentUserId =>
        User.FindFirstValue(ProductClaims.Subject) is string s && Guid.TryParse(s, out var g) ? g : null;

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemView>>> GetMyInventory(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        var itemCursor = await _inventoryItems.FindAsync(
            i => i.OwnerId == ownerId,
            cancellationToken: cancellationToken);
        var items = await itemCursor.ToListAsync(cancellationToken);

        var catalogProductIds = items.Select(i => i.CatalogProductId).Distinct().ToList();
        var catalogCursor = await _catalogProducts.FindAsync(
            Builders<CatalogProduct>.Filter.In(c => c.Id, catalogProductIds),
            cancellationToken: cancellationToken);
        var catalogProducts = await catalogCursor.ToListAsync(cancellationToken);
        var catalogProductsById = catalogProducts.ToDictionary(c => c.Id);

        var views = items
            .Select(i => Merge(i, catalogProductsById.GetValueOrDefault(i.CatalogProductId)))
            .Where(v => Matches(v, search))
            .OrderBy(v => v.Name, StringComparer.Ordinal)
            .ToList();

        return Ok(views);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddToInventory(
        [FromBody] AddToInventoryRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId is not Guid ownerId)
        {
            return Unauthorized();
        }

        var catalogProduct = await FindOrCreateCatalogProductAsync(request, cancellationToken);
        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CatalogProductId = catalogProduct.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            SerialNumber = request.SerialNumber,
            PurchaseDate = request.PurchaseDate,
            PricePaid = request.PricePaid,
            Description = request.Description,
        };

        await _inventoryItems.InsertOneAsync(inventoryItem, cancellationToken: cancellationToken);
        return Created(
            $"/odata/InventoryItems({inventoryItem.Id})",
            Merge(inventoryItem, catalogProduct));
    }

    private static bool Matches(InventoryItemView view, string? search) =>
        string.IsNullOrWhiteSpace(search)
        || view.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true;

    private static InventoryItemView Merge(InventoryItem item, CatalogProduct? catalogProduct) => new()
    {
        Id = item.Id,
        CatalogProductId = item.CatalogProductId,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        SerialNumber = item.SerialNumber,
        PurchaseDate = item.PurchaseDate,
        PricePaid = item.PricePaid,
        Description = item.Description,
        Name = catalogProduct?.Name,
        Brand = catalogProduct?.Brand,
        ModelNumber = catalogProduct?.ModelNumber,
        Category = catalogProduct?.Category,
        ManualUrl = catalogProduct?.ManualUrl,
        MsrpPrice = catalogProduct?.MsrpPrice,
    };

    private static CatalogProduct NewCatalogProduct(AddToInventoryRequest request, string? matchKey) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        Name = request.Name,
        Brand = request.Brand,
        ModelNumber = request.ModelNumber,
        Category = request.Category,
        ManualUrl = request.ManualUrl,
        MsrpPrice = request.MsrpPrice,
        MatchKey = matchKey,
    };

    private async Task<CatalogProduct> FindOrCreateCatalogProductAsync(
        AddToInventoryRequest request,
        CancellationToken cancellationToken)
    {
        var matchKey = CatalogProductMatchKey.Compute(request.Brand, request.ModelNumber);
        if (matchKey is null)
        {
            var unmatched = NewCatalogProduct(request, matchKey: null);
            await _catalogProducts.InsertOneAsync(unmatched, cancellationToken: cancellationToken);
            return unmatched;
        }

        var candidate = NewCatalogProduct(request, matchKey);
        var update = Builders<CatalogProduct>.Update
            .SetOnInsert(c => c.Id, candidate.Id)
            .SetOnInsert(c => c.CreatedAt, candidate.CreatedAt)
            .SetOnInsert(c => c.Name, candidate.Name)
            .SetOnInsert(c => c.Brand, candidate.Brand)
            .SetOnInsert(c => c.ModelNumber, candidate.ModelNumber)
            .SetOnInsert(c => c.Category, candidate.Category)
            .SetOnInsert(c => c.ManualUrl, candidate.ManualUrl)
            .SetOnInsert(c => c.MsrpPrice, candidate.MsrpPrice)
            .SetOnInsert(c => c.MatchKey, matchKey);

        return await _catalogProducts.FindOneAndUpdateAsync<CatalogProduct>(
            c => c.MatchKey == matchKey,
            update,
            new FindOneAndUpdateOptions<CatalogProduct>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After,
            },
            cancellationToken);
    }
}