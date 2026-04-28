[![Build and deploy ASP.Net Core app to Azure Web App - crgolden-products](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml/badge.svg)](https://github.com/crgolden/Products/actions/workflows/main_crgolden-products.yml)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=crgolden_Products&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=crgolden_Products)

# Products

ASP.NET Core 10 OData v4 data API managing a `Products` collection in the `crgolden` MongoDB database. Read endpoints are anonymous; write endpoints require a JWT Bearer token with the `products` scope and resource-based ownership. Observable via Azure Monitor and documented via OpenAPI.

## Sibling Applications

Products is a **resource server** in a five-app system. Reads are public; writes require a `products`-scoped JWT and ownership of the row.

| Repo | Role | How Products interacts |
|---|---|---|
| [Identity](https://github.com/crgolden/Identity) | OIDC Identity Provider | Issues the access tokens Products validates (scope `products`) |
| [Experience](https://github.com/crgolden/Experience) | Angular SPA + ASP.NET Core BFF | Sole client today — the BFF proxies authenticated calls to `/odata/Products` and an anonymous public-catalog passthrough |
| [Manuals](https://github.com/crgolden/Manuals) | Azure OpenAI chat API | Sets the `Product.ManualUrl` field via the chat panel embedded in the Experience product form |
| [Infrastructure](https://github.com/crgolden/Infrastructure) | Health monitoring dashboard | Polls Products' `/health` endpoint |

## Tech Stack

- **.NET 10** / ASP.NET Core
- **MongoDB** (Driver 3.x) — `crgolden` database, `Products` collection
- **OData v4** (`Microsoft.AspNetCore.OData`) — server-side filtering via MongoDB aggregation pipelines
- **OpenAPI** (`Microsoft.AspNetCore.OpenApi`) — discoverable API contract at `/openapi/v1.json`
- **JWT Bearer / OIDC** — all endpoints require `scope: products`
- **Azure** — Key Vault (secrets), Blob Storage (data protection), Azure Monitor (telemetry)
- **Serilog** → Elasticsearch (`logs-dotnet-Products` data stream)

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/odata/Products` | Anonymous | List products. Anonymous callers get all products; authenticated callers (Bearer + `products` scope) are filtered to their own (`OwnerId == sub`). |
| `GET` | `/odata/Products({key})` | Anonymous | Get a single product by GUID |
| `POST` | `/odata/Products` | Bearer + `products` | Create a new product; `OwnerId` is set from the JWT `sub` claim |
| `PUT` | `/odata/Products({key})` | Bearer + `products` + owner | Replace a product (403 if not owner) |
| `PATCH` | `/odata/Products({key})` | Bearer + `products` + owner | Partially update a product (403 if not owner) |
| `DELETE` | `/odata/Products({key})` | Bearer + `products` + owner | Delete a product (403 if not owner) |

OIDC tokens are issued by [Identity](https://github.com/crgolden/Identity); the [Experience](https://github.com/crgolden/Experience) BFF forwards them automatically when proxying `/products/api/**`.

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
| `ManualUrl` | `string?` | Populated from the [Manuals](https://github.com/crgolden/Manuals) chat panel embedded in the [Experience](https://github.com/crgolden/Experience) product form |
| `OwnerId` | `Guid?` | Server-managed: set on POST from the JWT `sub` claim; never accepted from client input |
| `CreatedAt` | `DateTimeOffset` | Set on POST, preserved on PUT/PATCH |
| `UpdatedAt` | `DateTimeOffset?` | Set on PUT/PATCH |

## Configuration

The following configuration keys are required. In production they are sourced from Azure Key Vault; locally, use [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

| Key | Source | Description |
|-----|--------|-------------|
| `MongoServerHost` | Config | MongoDB server hostname |
| `MongoServerPort` | Config | MongoDB server port |
| `MongoUseTls` | Config | `true` to require TLS to MongoDB |
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

See [TESTING.md](TESTING.md) for the full testing guide — unit tests, integration tests against real MongoDB, and CI pipeline details.

## Health Check

```
GET /health
```

Returns `Healthy` when the application is running. No authentication required.
