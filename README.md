# Products

[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-products](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml/badge.svg)](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml)

[![Quality gate](https://sonarcloud.io/api/project_badges/quality_gate?project=crgolden_Products)](https://sonarcloud.io/summary/new_code?id=crgolden_Products)

An ASP.NET Core 10 OData v4 API over MongoDB with two entities at two trust levels: `CatalogProduct`, shared product
facts anyone may read, and `InventoryItem`, a signed-in user's private record of a product they own. Writes need a
JWT access token whose `scope` claim includes `products`.

How the parts fit together and the rules each keeps: [ARCHITECTURE.md](ARCHITECTURE.md). How to run every test tier:
[TESTING.md](TESTING.md).

## Technologies

- .NET 10, ASP.NET Core, `Microsoft.AspNetCore.OData` (OData v4), the MongoDB C# driver.
- JWT Bearer authentication against any OIDC provider.
- OpenAPI at `/openapi/v1.json`; a MongoDB health check at `/health`.
- In production only: OpenTelemetry (OTLP), Serilog to Elasticsearch, and Azure Blob Storage plus Key Vault for data
  protection keys.
- xUnit v3 on Microsoft.Testing.Platform; coverlet, ReSharper `jb inspectcode` and SonarCloud in CI.

## API

| Route | Auth | Notes |
|---|---|---|
| `GET /odata/CatalogProducts`, `GET /odata/CatalogProducts({key})` | anonymous | OData `$select`, `$filter`, `$orderby`, `$expand`, `$count`, `$top` (at most 100) |
| `POST`, `PATCH`, `DELETE /odata/CatalogProducts[({key})]` | `products` scope | `Id`, `CreatedAt`, `UpdatedAt` and `MatchKey` are set by the server; a duplicate brand and model returns 409; `DELETE` returns 409 while any inventory item references the row |
| `GET`, `POST`, `PATCH`, `DELETE /odata/InventoryItems[({key})]` | `products` scope | only the caller's own items (`OwnerId` is the token's `sub`), so another user's item is 404, never 403 |
| `GET /inventory/items?search=`, `GET /inventory/items/{id}`, `POST /inventory/items` | `products` scope | the caller's items merged with their catalog facts; `POST` finds or creates the catalog row and adds the item |
| `GET /health` | anonymous | pings MongoDB |
| `GET /openapi/v1.json` | anonymous | the OpenAPI document, with the OData routes described by hand |

## Prerequisites

- The .NET 10 SDK (`global.json` pins `10.0.201` and rolls forward to the latest feature band).
- A GitHub personal access token with the `read:packages` scope. The `Shared` package restores from the `crgolden`
  GitHub Packages feed (`NuGet.Config`), which asks for a token even for a public package. Store it as that source's
  credentials in your user NuGet configuration, never in this repo:

  ```powershell
  dotnet nuget update source GitHub --username <your GitHub user> --password <token> --store-password-in-clear-text --configfile "$env:APPDATA\NuGet\NuGet.Config"
  ```

  If the `GitHub` source is not in your user configuration yet, use `dotnet nuget add source https://nuget.pkg.github.com/crgolden/index.json --name GitHub ...` with the same options.
- MongoDB, local or remote, with a user that can read and write the database the app uses.
- An OIDC provider for the authenticated endpoints. The anonymous catalog reads work without one.

## Clone and build

```powershell
git clone https://github.com/crgolden/Products.git
cd Products
dotnet tool restore
dotnet build Products.slnx
```

## Configuration

`appsettings.Development.json` holds the non-secret development values (MongoDB on `localhost:27017` without TLS,
database `crgolden`, OIDC authority `https://localhost:7261`); change them there or override them. The secrets go in
User Secrets for the `Products` project:

```powershell
dotnet user-secrets set MongoDbUsername "<user>" --project Products
dotnet user-secrets set MongoDbPassword "<password>" --project Products
```

| Key | Needed | Purpose |
|---|---|---|
| `MongoServerHost`, `MongoServerPort`, `MongoUseTls` | always | where MongoDB is and whether to use TLS |
| `MongoDatabaseName` | always | the database; the user authenticates against it (SCRAM-SHA-256) |
| `MongoDbUsername`, `MongoDbPassword` | always | MongoDB credentials (secrets) |
| `OidcAuthority` | always | the OIDC authority whose tokens the API accepts |
| `BlobUri`, `DataProtectionKeyIdentifier` | Production | Blob container and Key Vault key for data protection |
| `ElasticsearchNode`, `ElasticsearchUsername`, `ElasticsearchPassword` | Production | log shipping |
| `AlloyEndpoint` | Production | OTLP endpoint for traces and metrics |
| `WEBSITE_SITE_NAME` | Production | the service name for logs and telemetry (App Service sets it) |
| `DefaultAzureCredentialOptions` | Production | which Azure credential to use, in `appsettings.Production.json` |

Outside Production the app uses ephemeral data protection and logs to the console, so it needs no Azure access.

## Run locally

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project Products
```

It listens on `https://localhost:7057` (`Properties/launchSettings.json`). `Products/Products.http` holds example
requests.

## Tests

```powershell
dotnet test --project Products.Tests.Unit --configuration Release -- --filter-trait "Category=Unit"
```

The unit tier needs no network, database or secrets. The integration tier needs a MongoDB database whose name ends in
`Test`; [TESTING.md](TESTING.md) has the setup for both.

## Gate

`gate.ps1` is the maintainers' gate. It loads a private toolkit from a folder beside this repo, so in a plain clone it
stops at once with `GATE: FAILED`. To gate your own change, run the checks CI runs, in this order:

```powershell
dotnet tool restore
dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
dotnet jb inspectcode Products.slnx --no-build -e=WARNING --output=inspectcode.sarif
dotnet test --project Products.Tests.Unit --no-build --configuration Release -- --filter-trait "Category=Unit"
dotnet test --project Products.Tests.Integration --no-build --configuration Release -- --filter-trait "Category=Integration"
```

`jb inspectcode` passes only when `inspectcode.sarif` holds no results; the integration tier needs the database setup in
[TESTING.md](TESTING.md).

## Deployment

`.github/workflows/main_crgolden-products.yml` builds, inspects and tests every push and pull request, then on a push
to `main` publishes for `win-x86` and deploys to an Azure App Service through OIDC federated credentials held in the
repo's secrets.
