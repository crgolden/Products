# Testing

## Test tiers

| Tier | Trait | Project | Needs | Runs in CI |
|---|---|---|---|---|
| Unit | `Category=Unit` | `Products.Tests.Unit` | nothing: no network, database or secret | every push and pull request |
| Integration | `Category=Integration` | `Products.Tests.Integration` | a MongoDB database whose name ends in `Test` | every push and pull request except Dependabot's |

Both are xUnit v3 projects on Microsoft.Testing.Platform (`global.json` selects the runner). Test data comes from the
`Shared` package's `Shared.Testing.Generated`; `Products.Tests.Integration` also compiles
`Products.Tests.Unit/TestSupport/ODataProtocolConstants.cs` through a linked `<Compile>` item, so that file's path is
part of both builds.

### Unit tier

| Folder | Covers |
|---|---|
| `Controllers` | every action of the three controllers with mocked MongoDB collections, the authorization attributes, the list materialization filter, and the `$top=0` guard |
| `Controllers/ODataPipeline` | a minimal OData pipeline hosted on the test server: a list action that fails while streaming must answer 500 before any body, and the same rows without the fault must serialize identically with and without the filter |
| `Models` | the class maps (Guids stored as strings), the EDM shape of `CatalogProduct`, `CatalogProductMatchKey`, `AddToInventoryRequest` |
| `OpenApi` | the hand-built OData paths and schemas in the OpenAPI document |
| `HealthChecks`, `Extensions`, `TelemetryTests.cs` | the MongoDB health check, `GetRequired<T>`, and the index-creation failure counter |

A test that mocks the catalog `DELETE` guard's existence check mocks `FindOptions<InventoryItem, BsonDocument>`: the
driver projects `AnyAsync` to `BsonDocument`.

### Integration tier

| File | Covers |
|---|---|
| `IntegrationInventoryTests.cs` | the API end to end against real MongoDB: another owner's item returning 404, find-or-create reusing one catalog row, a duplicate match key returning 409, `$top=0` and negated string functions answering without a 500, retired routes returning 404 |
| `CatalogProductIndexMigrationTests.cs` | the index initializers, including two rows with no match key coexisting under the unique partial index |
| `ProductsWebApplicationFactoryTests.cs`, `TestDatabaseContractTests.cs` | the factory's refusal of any database whose name does not end in `Test` (or `Triage`) |

`ProductsWebApplicationFactory` starts the real `Program.cs`, replaces JWT authentication with a test scheme whose
principal carries a per-run `sub` and the `products` scope, and empties `CatalogProducts` and `InventoryItems` before
the first test and after the last, each time after checking the database name. One database therefore serves one run
at a time: two concurrent runs delete each other's documents. CI serializes its runs with a `concurrency` group.

## Running tests locally

From the repo root.

### Unit

```powershell
dotnet build Products.slnx --configuration Release
dotnet test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
```

### Integration

Needs MongoDB and a user that can read, write and manage indexes in a database whose name ends in `Test`. With
`ASPNETCORE_ENVIRONMENT=Development` the tier reads `appsettings.Development.json` (MongoDB on `localhost:27017`) and the
`MongoDbUsername` and `MongoDbPassword` User Secrets (README.md, "Configuration"). The development file names the
database `crgolden`, which the factory refuses, so override it:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:MongoDatabaseName = "productsTest"
dotnet test --project Products.Tests.Integration --no-build --configuration Release -- --filter-trait "Category=Integration"
```

Every setting can also come from environment variables, which is how CI supplies them (below).

## CI pipeline

The `build` job in `.github/workflows/main_crgolden-products.yml`, after a job that regenerates `packages.lock.json`
on Dependabot's pull requests:

1. Restore local tools; begin SonarCloud analysis.
2. Release build with locked restore and `-warnaserror`.
3. `dotnet jb inspectcode`, failing on any result.
4. Unit tier under coverlet (`coverage.opencover.xml`), `--stop-on-fail on`.
5. Integration tier under coverlet (`coverage-integration.opencover.xml`) with `ASPNETCORE_ENVIRONMENT=CI` and the
   settings from repo variables (`OIDC_AUTHORITY`, `MONGO_SERVER_HOST`, `MONGO_SERVER_PORT`, `MONGO_USE_TLS`,
   `MONGO_DATABASE_NAME_TEST`, `MONGO_DB_USERNAME_TEST`) and the secret `MONGO_DB_PASSWORD_TEST`.
6. Upload the test results; publish the app.
7. End SonarCloud analysis with the quality gate waited; fail while SonarCloud reports any open issue.

The job's `concurrency` group (`products-integration-database`, `queue: max`) runs one build at a time, because every
run shares the test database. SonarCloud and the integration tier skip Dependabot's pull requests, which get no
secrets.

## Coverage

Coverage counts only the `Products` assembly (`--include "[Products]*"`) and excludes `Program.cs`. To produce the
unit report locally as CI does:

```powershell
dotnet tool restore
dotnet coverlet Products.Tests.Unit/bin/Release/net10.0 `
  --target "dotnet" `
  --targetargs "test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit" `
  --format opencover --output "coverage.opencover.xml" `
  --skipautoprops --exclude-by-attribute GeneratedCodeAttribute `
  --exclude-by-file "**/obj/**" --exclude-by-file "**/Program.cs" `
  --does-not-return-attribute DoesNotReturnAttribute --include "[Products]*"
```

## Local SonarCloud analysis

The analysis needs the repo's SonarCloud token and project, which only maintainers have. It reads both coverage
reports and excludes `Program.cs` and `gate.ps1` from coverage.
