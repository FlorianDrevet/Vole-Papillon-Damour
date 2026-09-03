# 05 - Data And Storage

## Primary Persistence

- `Infrastructure.DependencyInjection` wires `ProjectDbContext` with `UseSqlServer`.
- The current runtime DI uses the `ProjectDatabase` connection string.
- Repository interfaces live in `Application.Common.Interfaces.Persistence` and implementations live in `Infrastructure.Persistence.Repositories`.
- `ProjectDbContext` is exposed back into the application layer through `IProjectDbContext`.

## Books module persistence

- `ProjectDbContext` and `IProjectDbContext` expose `Books`, `BookMovements`, `BookAnnouncements`, `ScanSessions`, the singleton `AssociationSettings`, `Watchlists`, `WatchlistItems`, and `UserAlertHistories` sets.
- Configurations live in `Infrastructure/Persistence/Configurations/`: ISBN-13 and strong-ID conversions, SQL Server `datetime2`, UTC read/write converters, `Books.RowVersion`, accent-insensitive `Title`/`Authors`, self-redirect and non-zero/positive quantity checks, and filtered unique indexes for `ClientGestureId` and one open scan session per volunteer.
- Migrations `20260903173750_AddBookExchangeCore`, `20260903175445_AddClientGestureIdToBookAnnouncements`, `20260903181307_AddSaleReversalLink`, and `20260903185500_AddBookWatchlistsAndAlerts` create the Books foundation, complete the announcement gesture trace, add a filtered-unique self-link for movement inverses, and add the watchlist/alert-history tables with target and cascade constraints. They are generated and validated locally but have not been applied to Azure SQL.
- The Books ledger is append-only by domain convention; cancellation, correction, and session reassignment are represented by signed inverse/replay movements rather than an update/delete of the original movement.
- `BookAlertOutbox` runs inside the close-session transaction through `IBookAlertOutbox`: it matches active edition/work watchlist items, filters recent `UserAlertHistory`, groups one `AlertEmail` payload per member/session, and sets `DueAt` from `AssociationSettings.AlertDelayMinutes`. It deliberately creates no message for an undated next-fair session. The worker sender and final anti-repeat history write remain pending.
- `IBookAlertOutbox` also exposes transactional administration operations: cancellation changes only pending alert rows to `Cancelled`, while force-send clears a claim and sets `DueAt` to the supplied UTC instant. Session reassignment cancels and recalculates pending alert rows before commit; sent rows are left untouched.
- `Watchlist` stores the consecutive `BounceCount` and `AlertStatus` used by `RG-31`; the domain suspends alerts at the starting threshold of three bounces, and a successful delivery resets the counter. The `RecordEmailBounce` handler updates the existing row transactionally; provider event identity/idempotence and the ACS/Event Grid adapter remain outside this slice.
- Application tests use an in-memory SQLite connection with real EF transactions to verify scan/session/cash/correction/reassignment atomicity and idempotent gesture behavior; this provider is test-only.

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
