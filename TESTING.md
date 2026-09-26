# Testing

The Products test suite uses xUnit v3 and is split into two tiers: **unit tests** that run on every push with no external dependencies, and **integration tests** that exercise a real MongoDB: locally, the MongoDB Windows service installed on the dev box (`localhost:27017`), in a database whose name ends in `Test`.

Unit test coding standards (MockBehavior.Strict, argument verification, SetupSequence, no control-flow in tests, etc.) are in the workspace-level [Unit Test Standards](../AGENTS/TESTING.md#unit-test-standards).

## Test tiers

| Tier | Trait | Project | Requires Azure? | Runs in CI |
|------|-------|---------|-----------------|------------|
| Unit | `Category=Unit` | `Products.Tests.Unit` | No | Every push/PR |
| Integration | `Category=Integration` | `Products.Tests.Integration` | No: MongoDB credentials from User Secrets locally, repo variables and the `MONGO_DB_PASSWORD_TEST` secret in CI | Every push/PR except Dependabot, against the server's `crgoldenTest` |

---

## Running Tests Locally

For running tests (`dotnet test` from the repo root — never the workspace root) and `ASPNETCORE_ENVIRONMENT` discipline, see the workspace-level [TESTING.md](../AGENTS/TESTING.md).

User Secrets ID: `efff68f7-73ce-43f6-9083-6659719fc179`

### Unit Tests

No Azure credentials required.

```powershell
dotnet build Products.Tests.Unit --configuration Debug
.\Products.Tests.Unit\bin\Debug\net10.0\Products.Tests.Unit.exe -trait "Category=Unit" -showLiveOutput
```

### Integration Tests (the local MongoDB service)

Runs against the MongoDB Windows service on the dev box (`Get-Service MongoDB`), host and port from `appsettings.Development.json`, credentials from User Secrets (`MongoDbUsername`, `MongoDbPassword`). **`MongoDatabaseName` must be overridden to a database ending in `Test`**: `ProductsWebApplicationFactory` refuses to start against any other name, and `appsettings.Development.json` names the development database `crgolden`. No `az login` needed: Azure credentials are only constructed inside `IsProduction()` in `Program.cs`.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:MongoDatabaseName = "crgoldenTest"
dotnet build Products.Tests.Integration --configuration Debug
.\Products.Tests.Integration\bin\Debug\net10.0\Products.Tests.Integration.exe -trait "Category=Integration" -showLiveOutput
```

> **Data isolation:** integration tests write to the configured `MongoDatabaseName` database using `OwnerId` = `ProductsWebApplicationFactory.TestUserId`, which is generated per run (`Guid.NewGuid()`), and delete the item ids they recorded in `IAsyncDisposable.DisposeAsync` — not every document in the database. **Concurrent runs against the same database are still not supported**: inventory rows are owner-scoped, but the catalog rows `AddToInventory` find-or-creates are not, and that path is a `MatchKey` upsert, so two runs contend on the same catalog documents regardless of owner.

---

## Test infrastructure

### `ProductsWebApplicationFactory`

`WebApplicationFactory<Program>` used by integration tests. Starts the full `Program.cs` with `ASPNETCORE_ENVIRONMENT=Development`, which selects the non-production branch: User Secrets for MongoDB credentials, ephemeral Data Protection, no Azure credentials. Replaces JWT Bearer authentication with a test scheme so tests can call the API without a real access token.

### `IntegrationAuthHandler`

An `AuthenticationHandler` registered as the default scheme by `ProductsWebApplicationFactory`. Always succeeds and returns a principal whose `sub` claim is `ProductsWebApplicationFactory.TestUserId` (generated per run) and whose `scope` claim contains `products`. This satisfies the `Products` authorization policy and ensures `OwnerId` filtering produces deterministic results.

### `IntegrationCollection`

A single xUnit collection fixture (`ICollectionFixture<ProductsWebApplicationFactory>`) that wraps all integration tests so the host is started once per test run.

---

## Unit test coverage

### `Controllers/ProductsControllerTests.cs`

Tests `ProductsController` using a mocked `IMongoCollection<Product>`, mocked `IAuthorizationService`, and a fake `ClaimsPrincipal`. Covers every action method for both success and forbidden / not-found paths:

| Area | Tests |
|------|-------|
| `Get` (list) | Not covered here — `AsQueryable()` is a MongoDB driver extension requiring a live session/queryable provider a mock can't satisfy; see `Get_FiltersProductsByOwner_WhenAuthenticatedWithGuidSub` under Integration test coverage below |
| `Get(key)` | Returns a single product wrapped in `SingleResult` |
| `Post` | Sets `OwnerId` from `sub` and `CreatedAt`; returns 201 with `Location` |
| `Put` | Returns 204 on success; 404 if missing; 403 if not owner; preserves `OwnerId` and `CreatedAt` |
| `Patch` | Returns 204 on success; 404 if missing; 403 if not owner; preserves `OwnerId` |
| `Delete` | Returns 204 on success; 404 if missing; 403 if not owner |

### `Authorization/ProductAuthorizationHandlerTests.cs`

Tests the resource-based `ProductAuthorizationHandler` against the `ProductOperations.Edit` and `Delete` requirements. Covers `OwnerId == sub` matching, mismatched owners, missing `sub` claim, and unauthenticated principals.

### `Models/ProductTests.cs`

Tests the `Product` POCO — default values, nullability, equality semantics — and the real `BsonClassMap` registration by calling `ProductClassMap.Register()`, the same method `Program.cs` calls (Guid `_id` serialized as string, `OwnerId` Guid serialized as string, `ManualUrl` round-tripping as a `Uri`).

**These tests previously declared their own copy of the class map and so proved nothing about the app** — deleting the registration from `Program.cs` left them green. The registration is now a single method in the production assembly; removing its serializers turns four of these tests red with the exact production failure (`GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified`). Never re-inline a class map into a test.

---

## Integration test coverage

### `Integration/IntegrationProductsTests.cs` — real MongoDB

| Test | What it verifies |
|------|-----------------|
| `Get_FiltersProductsByOwner_WhenAuthenticatedWithGuidSub` | `POST /odata/Products` then `GET /odata/Products?$orderby=Name` round-trip succeeds against real MongoDB. Covers the wiring the unit tier cannot reach — that `Program.cs` actually calls the registration, that the OData model and the collection agree, and that the driver talks to a real server. **The `BsonClassMap` Guid serialization regression itself is now caught in the unit tier** (`Models/ProductTests.cs`), proven by removing the serializers and watching four unit tests fail; this test is no longer the only thing standing between that defect and production. |

At the end of the run `ProductsWebApplicationFactory.DisposeAsync` deletes every document in `InventoryItems` and `CatalogProducts`, after checking that the database it is connected to ends in `Test`; no test cleans up after itself. They target a `*Test` database, never the `crgolden` database the app serves: locally the local MongoDB's (a local run never reaches production), in CI the server's `crgoldenTest`. The factory's start-up refusal enforces the name.

---

## CI pipeline

The GitHub Actions workflow (`.github/workflows/main_crgolden-products.yml`) runs on every push and PR:

1. Build solution (`dotnet build --no-incremental --configuration Release`)
2. Unit tests with coverage (`dotnet coverlet … --filter-trait Category=Unit`, OpenCover → `coverage.opencover.xml`)
3. SonarCloud analysis
4. Publish artifact → deploy to Azure App Service `crgolden-products`

The integration tier runs in CI after the unit tier, against the server's `crgoldenTest` database as the `productsTest` user, which is scoped to that database and refused on `crgolden`. Its settings come from the repo variables `MONGO_SERVER_HOST`, `MONGO_SERVER_PORT`, `MONGO_USE_TLS`, `MONGO_DATABASE_NAME_TEST`, `MONGO_DB_USERNAME_TEST` and `OIDC_AUTHORITY` and the secret `MONGO_DB_PASSWORD_TEST`; runners reach port 27017 through the `GitHubActions-MongoDB` firewall rules. Locally the same tier runs against a `*Test` database on the dev box's MongoDB service.

---

## Local SonarCloud analysis

Generate coverage first, then run from `Products/`. Unit coverage is OpenCover (branch-bearing, via
`coverlet.console` pinned in `dotnet-tools.json` — restore with `dotnet tool restore`; see the workspace
`TESTING.md` for the command rationale).

```powershell
dotnet build Products.Tests.Unit --configuration Release
dotnet tool restore
dotnet coverlet Products.Tests.Unit\bin\Release\net10.0 `
  --target "dotnet" `
  --targetargs "test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"

$env:SONAR_TOKEN = "<token>"
& "$env:SystemDrive\sonar-scanner-8.0.1.6346-windows-x64\bin\sonar-scanner.bat" `
  "-Dsonar.projectKey=crgolden_Products" `
  "-Dsonar.organization=crgolden" `
  "-Dsonar.sources=Products" `
  "-Dsonar.tests=Products.Tests.Unit,Products.Tests.Integration" `
  "-Dsonar.exclusions=**/bin/**,**/obj/**" `
  "-Dsonar.cs.opencover.reportsPaths=coverage.opencover.xml"
```

Required coverage files: `coverage.opencover.xml` (unit, OpenCover).

### When to build a truth table

The coverage **score is read from SonarCloud, never hand-maintained** here. Build a per-method table — this repo has none yet; it would be added as `AGENTS/COVERAGE/Products.md` — only when SonarCloud flags a method with **cognitive complexity > 15 AND uncovered conditions > 0**: the table is escalation for the gnarly few, not a per-class deliverable. See [../AGENTS/COVERAGE/METHOD.md](../AGENTS/COVERAGE/METHOD.md).
