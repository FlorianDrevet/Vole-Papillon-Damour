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
- The bibliographic resolver calls BnF SRU first and Open Library second; the current ISBN probe does not persist books.

## Authentication

- Infrastructure supports the staged Entra bearer scheme alongside the legacy JWT scheme from `JwtSettings`.
- The API exposes `IsAdmin` as a compatibility policy while the Entra role policies are rolled out.
- Auth changes are multi-surface changes because they affect the API, the admin Angular app, and possibly the MAUI client.

## Client-Side Storage

- `MauiCashApp` registers `ProductDatabase` and ships `sqlite-net-pcl` for offline/local caching.
- The MAUI app should be treated as having its own local persistence concerns in addition to backend storage.

## Notable Storage Detail

- PostgreSQL packages were removed during the .NET 10 backend upgrade; the active DI path remains SQL Server.
- The Aspire AppHost provisions local SQL Server and Azurite for development orchestration.
- The legacy mailing-list subscription flow and actuality email broadcast were removed; the application runtime no longer sends those messages.
- The deployment infrastructure provisions an Azure Communication Services Email resource for the deployment plan, but no active actuality mailing-list send path is present in the application runtime.
- Account deletion is persisted through an outbox-backed local retention flow and completed against the Microsoft Graph directory by the private Worker.
- The former OCR-specific Azure Vision integration and `OcrSettings` configuration were removed in May 2026.
- Product website visibility is persisted as `Products.VisibleOnWebsite` by the `AddProductWebsiteVisibility` EF migration. Existing rows are backfilled to preserve the previous Website availability and legacy `euro`/`centime`/`€` exclusions.

## Storage Risk Zones

- Repository changes can impact multiple feature slices at once.
- Blob/table naming and storage clients are centralized in Infrastructure.
- Media upload flows depend on centralized Azure Blob configuration and should be validated carefully.
