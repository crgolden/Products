# Architecture

## Bird's-eye view

One ASP.NET Core app, `Products`, serves an OData v4 API over two MongoDB collections: `CatalogProducts` (shared
product facts, anonymous read, any `products`-scoped caller may write) and `InventoryItems` (each user's own records,
reachable only by their owner). A plain controller beside the OData ones returns a user's items merged with their
catalog facts. Two hosted services create the collections' indexes at startup.

## Codemap

| Part | What it does |
|---|---|
| `Products/Program.cs` | configuration, MongoDB client and class maps, the OData model, JWT authentication and the `Products` policy, health checks, and the production-only telemetry and data protection |
| `Products/Controllers/CatalogProductsController.cs`, `InventoryItemsController.cs` | the two OData entity sets |
| `Products/Controllers/InventoryController.cs` | `/inventory/items`: a user's items merged with their catalog facts, and find-or-create on add |
| `Products/Controllers/MaterializeODataListAttribute.cs`, `MongoTopZeroGuard.cs` | the list actions' guards against a mid-stream failure and a `$top=0` the driver rejects |
| `Products/Models` | `CatalogProduct`, `InventoryItem`, their class maps, `CatalogProductMatchKey`, the request and view types |
| `Products/HostedServices` | `CatalogProductIndexInitializer` and `InventoryItemIndexInitializer` |
| `Products/OpenApi/ODataQueryParameterTransformer.cs` | writes the OData paths and query options into the OpenAPI document by hand |
| `Products/HealthChecks/MongoDbHealthCheck.cs`, `Telemetry.cs`, `TelemetryOptions.cs` | `/health` and the one custom metric |
| `Products/Authorization/ProductClaims.cs`, `Extensions`, `MongoSettingKeys.cs` | claim names, `GetRequired<T>`, setting names |
| `Products.Tests.Unit`, `Products.Tests.Integration` | the two test tiers ([TESTING.md](TESTING.md)) |
| `.github/workflows/main_crgolden-products.yml` | build, inspect, test, analyze and deploy |
| `NuGet.Config`, `packages.lock.json` files, `dotnet-tools.json`, `global.json` | package sources (the `Shared` package from GitHub Packages), locked restore, pinned tools, the SDK |
| `gate.ps1` | the maintainers' local gate (README.md, "Gate") |

## Invariants

- **The owner predicate is inside every inventory query** (`OwnerId == sub`), so another user's item is 404, never 403.
  `OwnerId` and `CreatedAt` are set by the server, and `CatalogProductId` is fixed after the item is created.
- **A catalog row is identified for reuse by its match key**: trimmed, upper-cased `BRAND::MODEL`, null when either
  half is blank, recomputed on every write and hidden from the OData model. Find-or-create is an upsert on the match
  key that only sets fields on insert, so under a race the first contributor's fields win.
- **The unique index on the match key is partial on `$type: string`**, so any number of rows with no match key
  coexist; `$exists` would put every null key in the index.
- **A catalog `DELETE` refuses a row any inventory item references, with 409.** The check and the delete are two
  operations without a transaction, so an add landing between them can still reference a deleted row.
- **A changed index definition ships under a new name**, and the initializer drops the old one once the new one
  exists, because MongoDB refuses to redefine an index under an existing name.
- **Every `Guid` is stored as a string** by the class maps, which the tests register through the same `Register()`
  methods `Program.cs` calls.
- **`ManualUrl` is a `string`** on `CatalogProduct`: the type is also the EDM entity and the PATCH target, and EDM has
  no URI primitive.

## Boundaries

- MongoDB is the only store. The API trusts any token from `OidcAuthority` whose `scope` includes `products`; it does
  not validate the audience.
- Outside Production it needs no Azure service: data protection is ephemeral and logs go to the console.

## Cross-cutting concerns

- **List responses are materialized before they start**, so a failure while reading MongoDB answers 500 instead of a
  200 whose body stops mid-array. Null propagation is off on the list actions, so a negated string function compiles
  to a query MongoDB accepts.
- **Telemetry** is production-only: OpenTelemetry traces (ASP.NET Core, HttpClient, the MongoDB driver) and metrics
  over OTLP, and Serilog to an Elasticsearch data stream. The one custom metric counts index-creation failures.
- **Warnings are errors**, and CI also fails on any `jb inspectcode` result and any open SonarCloud issue.
