# 05 - Data And Storage

## Primary Persistence

- `Infrastructure.DependencyInjection` wires `ProjectDbContext` with `UseSqlServer`.
- The current runtime DI uses the `ProjectDatabase` connection string.
- Repository interfaces live in `Application.Common.Interfaces.Persistence` and implementations live in `Infrastructure.Persistence.Repositories`.
- `ProjectDbContext` is exposed back into the application layer through `IProjectDbContext`.

## External Services

- Azure Blob Storage is configured from `AzureBlobStorageConnectionString`.
- Azure Monitor OpenTelemetry is enabled in the API startup.
- Blob container names are configured as `loto-images`, `actuality-images`, `event-images`, and `product-images`.

## Authentication

- JWT bearer auth is configured in Infrastructure from `JwtSettings`.
- The API exposes an `IsAdmin` authorization policy.
- Auth changes are multi-surface changes because they affect the API, the admin Angular app, and possibly the MAUI client.

## Client-Side Storage

- `MauiCashApp` registers `ProductDatabase` and ships `sqlite-net-pcl` for offline/local caching.
- The MAUI app should be treated as having its own local persistence concerns in addition to backend storage.

## Notable Storage Detail

- PostgreSQL packages were removed during the .NET 10 backend upgrade; the active DI path remains SQL Server.
- The Aspire AppHost provisions local SQL Server and Azurite for development orchestration.
- The mailing-list subscription flow and Azure Communication Email integration were removed; actuality creation no longer broadcasts emails.
- The former OCR-specific Azure Vision integration and `OcrSettings` configuration were removed in May 2026.
- Product website visibility is persisted as `Products.VisibleOnWebsite` by the `AddProductWebsiteVisibility` EF migration. Existing rows are backfilled to preserve the previous Website availability and legacy `euro`/`centime`/`€` exclusions.

## Storage Risk Zones

- Repository changes can impact multiple feature slices at once.
- Blob/table naming and storage clients are centralized in Infrastructure.
- Media upload flows depend on centralized Azure Blob configuration and should be validated carefully.
