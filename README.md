[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-products](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml/badge.svg)](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=crgolden_Products&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=crgolden_Products)

# Products

ASP.NET Core 10 OData v4 data API managing a `Products` collection in the `crgolden` MongoDB database. Fully authenticated, observable, and documented via OpenAPI.

## Tech Stack

- **.NET 10** / ASP.NET Core
- **MongoDB** (Driver 3.x) — `crgolden` database, `Products` collection
- **OData v4** (`Microsoft.AspNetCore.OData`) — server-side filtering via MongoDB aggregation pipelines
- **OpenAPI** (`Microsoft.AspNetCore.OpenApi`) — discoverable API contract at `/openapi/v1.json`
- **JWT Bearer / OIDC** — all endpoints require `scope: products`
- **Azure** — Key Vault (secrets), Blob Storage (data protection), Azure Monitor (telemetry)
- **Serilog** → Elasticsearch (`logs-dotnet-Products` data stream)

## API Endpoints

All routes require a valid JWT with the `products` scope claim.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/odata/Products` | List products with OData query support |
| `GET` | `/odata/Products({key})` | Get a single product by GUID |
| `POST` | `/odata/Products` | Create a new product |
| `PUT` | `/odata/Products({key})` | Replace a product |
| `PATCH` | `/odata/Products({key})` | Partially update a product |
| `DELETE` | `/odata/Products({key})` | Delete a product |

### OData Query Options (list endpoint)

`$filter`, `$select`, `$orderby`, `$top` (max 100), `$skip`, `$count`, `$expand`

**Example:**
```
GET /odata/Products?$filter=Name eq 'Widget'&$orderby=Price desc&$top=10
```

## Data Model

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `Guid` | Stored as string in MongoDB (`_id`) |
| `Name` | `string?` | Indexed (ascending) |
| `Price` | `decimal?` | |
| `Brand` | `string?` | |
| `ModelNumber` | `string?` | |
| `SerialNumber` | `string?` | |
| `PurchaseDate` | `DateTimeOffset?` | |
| `Category` | `string?` | |
| `Description` | `string?` | |
| `ManualUrl` | `string?` | Will be populated from the Manuals API |
| `CreatedAt` | `DateTimeOffset` | Set on POST, preserved on PUT/PATCH |
| `UpdatedAt` | `DateTimeOffset?` | Set on PUT/PATCH |

## Configuration

The following configuration keys are required. In production they are sourced from Azure Key Vault; locally, use [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

| Key | Source | Description |
|-----|--------|-------------|
| `MongoServerHost` | Config | MongoDB server hostname |
| `MongoServerPort` | Config | MongoDB server port |
| `MongoDatabaseName` | Config | Target database name |
| `MongoDbUsername` | Key Vault secret | MongoDB SCRAM-SHA-256 username |
| `MongoDbPassword` | Key Vault secret | MongoDB SCRAM-SHA-256 password |
| `OidcAuthority` | Config | OIDC authority URL for JWT validation |
| `BlobUri` | Config | Azure Blob Storage URL for data protection keys |
| `DataProtectionKeyIdentifier` | Config | Azure Key Vault key URI for data protection |
| `ElasticsearchNode` | Config | Elasticsearch node URL |
| `ElasticsearchUsername` | Key Vault secret | Elasticsearch username |
| `ElasticsearchPassword` | Key Vault secret | Elasticsearch password |

## Local Development

```bash
# Prerequisites: az login, User Secrets configured

# Build
dotnet build Products/

# Run
dotnet run --project Products/

# View OpenAPI doc
curl https://localhost:{port}/openapi/v1.json

# Run unit tests (no Azure creds required)
dotnet test --project Products.Tests/ --configuration Release -- --filter-trait "Category=Unit"
```

## Health Check

```
GET /health
```

Returns `Healthy` when the application is running. No authentication required.
