# Testing

The Products test suite uses xUnit v3 and is split into two tiers: **unit tests** that run on every push with no external dependencies, and **integration tests** that exercise the real MongoDB instance.

## Test tiers

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Products.Tests` | No | Every push/PR |
| Integration | `Category=Integration` | `Products.Tests` | Yes — real MongoDB via Azure Key Vault | Local only (not yet in CI) |

---

## Running tests locally

> **Build configuration:** `--configuration Release` is shown for consistency with CI; `--configuration Debug` is equally valid for local runs and compiles faster. There is no Angular build or other Release-only artifact involved.

### Prerequisites

Unit tests require no Azure credentials. For integration tests, authenticate first:

```bash
az login
```

User Secrets must supply `KeyVaultUri`, `MongoServerHost`, `MongoServerPort`, `MongoUseTls`, `MongoDatabaseName`, and `DefaultAzureCredentialOptions` (see [README.md](README.md#configuration)). MongoDB credentials are read from Azure Key Vault at startup.

### Unit tests

```bash
dotnet test --project Products.Tests --configuration Debug \
  -- --filter-trait "Category=Unit"
```

### Integration tests (require live MongoDB + Key Vault)

```bash
# Bash / WSL
ASPNETCORE_ENVIRONMENT=Development \
dotnet test --project Products.Tests --configuration Debug \
  -- --filter-trait "Category=Integration"

# PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet test --project Products.Tests --configuration Debug -- --filter-trait "Category=Integration"
```

> **Data isolation:** integration tests write to the configured `MongoDatabaseName` Mongo database using the `OwnerId` `ProductsWebApplicationFactory.TestUserId` (`00000000-0000-0000-0001-000000000001`) and clean up every document they create in `IAsyncDisposable.DisposeAsync`. Running multiple integration test runs concurrently against the same database is not supported.

### Run all tests

```bash
ASPNETCORE_ENVIRONMENT=Development \
dotnet test Products.slnx --configuration Debug
```

---

## Test infrastructure

### `ProductsWebApplicationFactory`

`WebApplicationFactory<Program>` used by integration tests. Starts the full application with production configuration (real MongoDB via Key Vault credentials). Replaces JWT Bearer authentication with a test scheme so tests can call the API without a real access token.

### `IntegrationAuthHandler`

An `AuthenticationHandler` registered as the default scheme by `ProductsWebApplicationFactory`. Always succeeds and returns a principal whose `sub` claim is `ProductsWebApplicationFactory.TestUserId` (`00000000-0000-0000-0001-000000000001`) and whose `scope` claim contains `products`. This satisfies the `Products` authorization policy and ensures `OwnerId` filtering produces deterministic results.

### `IntegrationCollection`

A single xUnit collection fixture (`ICollectionFixture<ProductsWebApplicationFactory>`) that wraps all integration tests so the host is started once per test run.

---

## Unit test coverage

### `Controllers/ProductsControllerTests.cs`

Tests `ProductsController` using a mocked `IMongoCollection<Product>`, mocked `IAuthorizationService`, and a fake `ClaimsPrincipal`. Covers every action method for both success and forbidden / not-found paths:

| Area | Tests |
|------|-------|
| `Get` (list) | Anonymous returns all; authenticated filters by `OwnerId` |
| `Get(key)` | Returns a single product wrapped in `SingleResult` |
| `Post` | Sets `OwnerId` from `sub` and `CreatedAt`; returns 201 with `Location` |
| `Put` | Returns 204 on success; 404 if missing; 403 if not owner; preserves `OwnerId` and `CreatedAt` |
| `Patch` | Returns 204 on success; 404 if missing; 403 if not owner; preserves `OwnerId` |
| `Delete` | Returns 204 on success; 404 if missing; 403 if not owner |

### `Authorization/ProductAuthorizationHandlerTests.cs`

Tests the resource-based `ProductAuthorizationHandler` against the `ProductOperations.Edit` and `Delete` requirements. Covers `OwnerId == sub` matching, mismatched owners, missing `sub` claim, and unauthenticated principals.

### `Models/ProductTests.cs`

Tests the `Product` POCO — default values, nullability, equality semantics, and the `BsonClassMap` registration in `HostApplicationBuilderExtensions.AddPersistenceAsync` (Guid `_id` serialized as string, `OwnerId` Guid serialized as string).

---

## Integration test coverage

### `Integration/IntegrationProductsTests.cs` — real MongoDB

| Test | What it verifies |
|------|-----------------|
| `Get_FiltersProductsByOwner_WhenAuthenticatedWithGuidSub` | `POST /odata/Products` then `GET /odata/Products?$orderby=Name` round-trip succeeds against real MongoDB. Catches `BsonClassMap` Guid serialization regressions ("`GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified`") that unit tests with mocked `IMongoCollection<Product>` cannot detect. |

The test creates products and deletes them in `DisposeAsync`. Integration tests target the same MongoDB database used in development; do not run against a production database.

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-products.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`)
2. Unit tests with coverage (`dotnet-coverage collect … --filter-trait Category=Unit`)
3. SonarCloud analysis
4. Publish artifact → deploy to Azure App Service `crgolden-products`

Integration tests are not yet wired into the CI workflow. They run on developer machines against the same MongoDB instance used for development.
