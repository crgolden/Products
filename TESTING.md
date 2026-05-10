# Testing

The Products test suite uses xUnit v3 and is split into two tiers: **unit tests** that run on every push with no external dependencies, and **integration tests** that exercise the real MongoDB instance.

## Test tiers

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Products.Tests` | No | Every push/PR |
| Integration | `Category=Integration` | `Products.Tests` | No — MongoDB credentials from User Secrets; no Azure credentials needed | Local only (not yet in CI) |

---

## Running Tests Locally

For the `.NET 10 SDK xUnit caveat` (why `dotnet test` doesn't work) and `ASPNETCORE_ENVIRONMENT` discipline, see the workspace-level [TESTING.md](../TESTING.md).

User Secrets ID: `efff68f7-73ce-43f6-9083-6659719fc179`

### Unit Tests

No Azure credentials required.

```powershell
dotnet build Products.Tests --configuration Debug
.\Products.Tests\bin\Debug\net10.0\Products.Tests.exe -trait "Category=Unit" -showLiveOutput
```

### Integration Tests (require live MongoDB)

Requires `ASPNETCORE_ENVIRONMENT=Development` and configured User Secrets (`MongoDbUsername`, `MongoDbPassword`, `MongoServerHost`, `MongoServerPort`, `MongoUseTls`, `MongoDatabaseName`). No `az login` needed — MongoDB credentials come from User Secrets in non-production; Azure credentials are only constructed inside `IsProduction()` in `Program.cs`.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet build Products.Tests --configuration Debug
.\Products.Tests\bin\Debug\net10.0\Products.Tests.exe -trait "Category=Integration" -showLiveOutput
```

> **Data isolation:** integration tests write to the configured `MongoDatabaseName` database using `OwnerId` = `ProductsWebApplicationFactory.TestUserId` (`00000000-0000-0000-0001-000000000001`) and clean up every document in `IAsyncDisposable.DisposeAsync`. Concurrent runs against the same database are not supported.

---

## Test infrastructure

### `ProductsWebApplicationFactory`

`WebApplicationFactory<Program>` used by integration tests. Starts the full `Program.cs` with `ASPNETCORE_ENVIRONMENT=Development`, which selects the non-production branch: User Secrets for MongoDB credentials, ephemeral Data Protection, no Azure credentials. Replaces JWT Bearer authentication with a test scheme so tests can call the API without a real access token.

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

---

## Local SonarCloud analysis

Generate coverage first (unit tests only; integration coverage is not collected), then run from `Products/`:

```powershell
$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Products" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Products" `
  "-Dsonar.tests=Products.Tests" `
  "-Dsonar.exclusions=**/bin/**,**/obj/**" `
  "-Dsonar.cs.vscoveragexml.reportsPaths=coverage.xml"
```

Required coverage files: `coverage.xml`.
