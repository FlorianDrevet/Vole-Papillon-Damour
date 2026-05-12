# 05 - Data And Storage

## Primary Persistence

- `Infrastructure.DependencyInjection` wires `ProjectDbContext` with `UseSqlServer`.
- The current runtime DI uses the `ProjectDatabase` connection string.
- Repository interfaces live in `Application.Common.Interfaces.Persistence` and implementations live in `Infrastructure.Persistence.Repositories`.
- `ProjectDbContext` is exposed back into the application layer through `IProjectDbContext`.

## External Services

- Azure Blob Storage and Azure Table Storage are configured from `AzureBlobStorageConnectionString`.
- Azure Table Storage is named `MailingListStorage` in DI.
- Azure Communication Email is configured from the `EmailService` connection string.
- Azure AI Vision OCR is configured from `OcrSettings`.
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

- PostgreSQL packages exist in the Infrastructure project, but the active DI path currently uses SQL Server.
- Do not assume PostgreSQL is live without checking runtime configuration first.

## Storage Risk Zones

- Repository changes can impact multiple feature slices at once.
- Blob/table naming and storage clients are centralized in Infrastructure.
- Mailing list, OCR, and media flows depend on external Azure services and should be validated carefully.