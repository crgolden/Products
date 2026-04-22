# CLAUDE.md — Products

ASP.NET Core 10 OData v4 API managing a `Products` collection in the `crgolden` MongoDB database.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 |
| Database | MongoDB (Driver 3.x) |
| Query protocol | OData v4 (`Microsoft.AspNetCore.OData` 9.x) |
| MongoDB-OData bridge | `MongoDB.AspNetCore.OData` — uses `[MongoEnableQuery]` to translate OData `$filter` into MongoDB aggregation pipelines |
| API docs | `Microsoft.AspNetCore.OpenApi` (first-party .NET 10) |
| Auth | JWT Bearer, OIDC authority; GET endpoints are public, write endpoints require `products` scope + resource ownership |
| Observability | Serilog → Elasticsearch, OpenTelemetry → Azure Monitor |
| Secrets | Azure Key Vault via `DefaultAzureCredential` |

## Endpoints

All routes are under `/odata`.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/odata/Products` | Anonymous | List with OData query support. Anonymous: all products. Authenticated (valid Bearer + `products` scope): only own products (filtered by `OwnerId == sub`) |
| GET | `/odata/Products({key})` | Anonymous | Single product by GUID key |
| POST | `/odata/Products` | Bearer + `products` scope | Create a product; sets `OwnerId` from JWT `sub` claim |
| PUT | `/odata/Products({key})` | Bearer + `products` scope + owner | Full replace; 403 if not owner |
| PATCH | `/odata/Products({key})` | Bearer + `products` scope + owner | Partial update (OData Delta); 403 if not owner |
| DELETE | `/odata/Products({key})` | Bearer + `products` scope + owner | Delete; 403 if not owner |

OData query options on the list endpoint: `$filter`, `$select`, `$orderby`, `$top` (max 100), `$skip`, `$count`, `$expand`.

## Data Model

```
Product
  Id            Guid              BSON _id, stored as string
  Name          string?
  Price         decimal?
  Brand         string?
  ModelNumber   string?
  SerialNumber  string?
  PurchaseDate  DateTimeOffset?
  Category      string?
  Description   string?
  ManualUrl     string?           populated from Manuals API chat panel
  OwnerId       Guid?             server-managed: set on POST from JWT sub claim; never accepted from client
  CreatedAt     DateTimeOffset    set on POST, preserved on PUT/PATCH
  UpdatedAt     DateTimeOffset?   set on PUT/PATCH
```

**Id mapping**: `Guid` is mapped to MongoDB's `_id` field and serialized as a string via `GuidSerializer(BsonType.String)` — configured in `BsonClassMap.TryRegisterClassMap<Product>()` inside `HostApplicationBuilderExtensions.AddPersistenceAsync`. No BSON attributes on the model class.

## Resource-Based Authorization

Write operations use ASP.NET Core's native resource-based authorization pattern
(see `Authorization/ProductAuthorizationHandler.cs` and `Authorization/ProductOperations.cs`).

The handler checks `resource.OwnerId == currentUserId` (where `currentUserId` is parsed from the JWT `sub` claim). If the check fails, the controller returns `403 Forbidden`.

Pattern in the controller:
```csharp
var authResult = await _authorizationService.AuthorizeAsync(User, existing, ProductOperations.Edit);
if (!authResult.Succeeded) return Forbid();
```

`OwnerId` is always preserved on PUT and PATCH — it cannot be overwritten by client input. On PATCH, the existing `OwnerId` is captured before `delta.Patch(existing)` and restored afterward.

## MongoDB

- **Database**: value of `MongoDatabaseName` in configuration (credentials from Key Vault: `MongoDbUsername`, `MongoDbPassword`)
- **Collection**: `Products`
- **Indexes**: ascending `Name`, descending `CreatedAt`, ascending `OwnerId` — created on startup in `AddPersistenceAsync`
- **Future indexes**: if server-side filtering by `Category` or `Brand` is added, create ascending indexes on those fields in `AddPersistenceAsync` alongside the existing ones

## Build & Run

```bash
# Build
dotnet build Products/

# Run locally (requires az login + User Secrets configured)
dotnet run --project Products/

# OpenAPI document
GET https://localhost:{port}/openapi/v1.json
```

## Tests

```bash
# Unit tests only (no Azure creds needed)
dotnet test --project Products.Tests/ --configuration Release -- --filter-trait "Category=Unit"
```

Test categories:
- `Category=Unit` — fast, no external dependencies; mocks `IMongoDatabase` / `IMongoCollection<Product>` / `IAuthorizationService` with Moq
- No `Category=E2E` defined yet (the list `Get()` action requires a live MongoDB queryable and is integration-test territory)

## Key Design Decisions

- **GUID IDs** — preferred over ObjectId; stored as string in MongoDB to avoid OData/BSON serialization conflicts
- **GET endpoints anonymous** — `[AllowAnonymous]` on both GET actions; the list action inspects the JWT `sub` claim to decide whether to filter by `OwnerId`
- **`[MongoEnableQuery]` on list `Get()`** — translates OData to MongoDB aggregation pipeline server-side; avoids in-memory filtering. The pre-applied `OwnerId` LINQ predicate is composed into the same pipeline — no extra round-trip
- **`[EnableQuery]` on `Get(Guid key)`** — returns `SingleResult<Product>` so OData can apply `$select`/`$expand` on a single entity
- **No attributes on `Product`** — all BSON mapping lives in `HostApplicationBuilderExtensions.AddPersistenceAsync`
- **No `[ApiController]` on `ProductsController`** — `[ApiController]` enforces attribute routing on all actions, which conflicts with OData's conventional routing conventions. OData controllers must not carry it
- **OpenAPI paths built manually in `ODataQueryParameterTransformer`** — `Microsoft.AspNetCore.OpenApi` does not auto-discover OData conventional routes (it relies on endpoint metadata that OData's routing system does not expose). The transformer constructs all `/odata/Products` paths and operations from scratch, including OData query parameters on GET endpoints. Do not add `[ApiController]` to fix an empty OpenAPI document; it will break routing instead

## Code Style

- `using` directives inside namespace (StyleCop SA1200)
- Blank line **before** comments, **no** blank line after (StyleCop SA1515/SA1516)
- `TreatWarningsAsErrors=true` — zero warnings policy
