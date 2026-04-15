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
| Auth | JWT Bearer, OIDC authority, `products` scope required on all endpoints |
| Observability | Serilog → Elasticsearch, OpenTelemetry → Azure Monitor |
| Secrets | Azure Key Vault via `DefaultAzureCredential` |

## Endpoints

All routes are under `/odata`. All endpoints require a valid JWT with `scope: products`.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/odata/Products` | List with OData query support |
| GET | `/odata/Products({key})` | Single product by GUID key |
| POST | `/odata/Products` | Create a product |
| PUT | `/odata/Products({key})` | Full replace |
| PATCH | `/odata/Products({key})` | Partial update (OData Delta) |
| DELETE | `/odata/Products({key})` | Delete |

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
  ManualUrl     string?           future: populated from Manuals API
  CreatedAt     DateTimeOffset    set on POST, preserved on PUT/PATCH
  UpdatedAt     DateTimeOffset?   set on PUT/PATCH
```

**Id mapping**: `Guid` is mapped to MongoDB's `_id` field and serialized as a string via `GuidSerializer(BsonType.String)` — configured in `BsonClassMap.TryRegisterClassMap<Product>()` inside `HostApplicationBuilderExtensions.AddPersistenceAsync`. No BSON attributes on the model class.

## MongoDB

- **Database**: value of `MongoOptions:DatabaseName` in configuration (Key Vault secret: `MongoDbUsername`, `MongoDbPassword`)
- **Collection**: `Products`
- **Indexes**: ascending `Name`, descending `CreatedAt` — created on startup in `AddPersistenceAsync`
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
- `Category=Unit` — fast, no external dependencies; mocks `IMongoDatabase` / `IMongoCollection<Product>` with Moq
- No `Category=E2E` defined yet (the list `Get()` action requires a live MongoDB queryable and is integration-test territory)

## Key Design Decisions

- **GUID IDs** — preferred over ObjectId; stored as string in MongoDB to avoid OData/BSON serialization conflicts
- **All endpoints auth-gated** — `[Authorize(Policy = nameof(Products))]` on the controller class
- **`[MongoEnableQuery]` on list `Get()`** — translates OData to MongoDB aggregation pipeline server-side; avoids in-memory filtering
- **`[EnableQuery]` on `Get(Guid key)`** — returns `SingleResult<Product>` so OData can apply `$select`/`$expand` on a single entity
- **No attributes on `Product`** — all BSON mapping lives in `HostApplicationBuilderExtensions.AddPersistenceAsync`
- **No `[ApiController]` on `ProductsController`** — `[ApiController]` enforces attribute routing on all actions, which conflicts with OData's conventional routing conventions. OData controllers must not carry it.
- **OpenAPI paths built manually in `ODataQueryParameterTransformer`** — `Microsoft.AspNetCore.OpenApi` does not auto-discover OData conventional routes (it relies on endpoint metadata that OData's routing system does not expose). The transformer constructs all `/odata/Products` paths and operations from scratch, including OData query parameters on GET endpoints. Do not add `[ApiController]` to fix an empty OpenAPI document; it will break routing instead.

## Code Style

- `using` directives inside namespace (StyleCop SA1200)
- Blank line **before** comments, **no** blank line after (StyleCop SA1515/SA1516)
- `TreatWarningsAsErrors=true` — zero warnings policy
