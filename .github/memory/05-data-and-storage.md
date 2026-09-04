# 05 - Data And Storage

## Primary Persistence

- `Infrastructure.DependencyInjection` wires `ProjectDbContext` with `UseSqlServer`.
- The current runtime DI uses the `ProjectDatabase` connection string.
- Repository interfaces live in `Application.Common.Interfaces.Persistence` and implementations live in `Infrastructure.Persistence.Repositories`.
- `ProjectDbContext` is exposed back into the application layer through `IProjectDbContext`.

## Books module persistence

- `ProjectDbContext` and `IProjectDbContext` expose `Books`, `BookMovements`, `BookAnnouncements`, `ScanSessions`, the singleton `AssociationSettings`, `Watchlists`, `WatchlistItems`, `UserAlertHistories`, and the `EmailBounceEvents` provider-event ledger sets.
- Configurations live in `Infrastructure/Persistence/Configurations/`: ISBN-13 and strong-ID conversions, SQL Server `datetime2`, UTC read/write converters, `Books.RowVersion`, accent-insensitive `Title`/`Authors`, self-redirect and non-zero/positive quantity checks, and filtered unique indexes for `ClientGestureId` and one open scan session per volunteer.
- Migrations `20260903173750_AddBookExchangeCore`, `20260903175445_AddClientGestureIdToBookAnnouncements`, `20260903181307_AddSaleReversalLink`, `20260903185500_AddBookWatchlistsAndAlerts`, `20260903192839_AddEmailBounceEventLedger`, `20260903211547_AddWatchlistUpdatedAt`, and `20260903230825_AddCancelledBookFair` create the Books foundation, complete the announcement gesture trace, add a filtered-unique self-link for movement inverses, add the watchlist/alert-history tables with target and cascade constraints, add the provider-event ledger with its unique provider identity, persist a UTC `UpdatedAt` watermark on watchlists (backfilled from `CreatedAt`), and persist cancelled-fair state. `20260904193041_AddDeadStockQueryIndex` adds `Isbn13 + Type + OccurredAt` for the administration dead-stock read, and `20260904213549_AddWatchlistItemUniqueness` protects one equivalent watchlist item. The pending migrations were applied to DEV Azure SQL by `Books runtime - deploy` `33922677695`; later application rollouts did not rerun them.
- The Books ledger is append-only by domain convention; cancellation, correction, and session reassignment are represented by signed inverse/replay movements rather than an update/delete of the original movement.
- `BookAlertOutbox` runs inside the close-session transaction through `IBookAlertOutbox`: it matches active edition/work watchlist items, filters recent `UserAlertHistory`, groups one `AlertEmail` payload per member/session, and sets `DueAt` from `AssociationSettings.AlertDelayMinutes`. It deliberately creates no message for an undated next-fair session. The worker sender and final anti-repeat history write remain pending.
- `IBookAlertOutbox` also exposes transactional administration operations: cancellation changes only pending alert rows to `Cancelled`, while force-send clears a claim and sets `DueAt` to the supplied UTC instant. Session reassignment cancels and recalculates pending alert rows before commit; sent rows are left untouched.
- `Watchlist` stores the consecutive `BounceCount`, `AlertStatus`, and UTC `UpdatedAt` watermark used by `RG-31` and Scan catalog reprojection; every alert-state transition updates the timestamp, and the migration backfills existing rows from `CreatedAt`. The domain suspends alerts at the starting threshold of three bounces, and a successful delivery resets the counter. The `RecordEmailBounce` handler updates the existing row and inserts `EmailBounceEvent` in one transaction; the unique provider-event index makes a sequential ACS/Event Grid replay return the current state without a second increment. The ACS/Event Grid adapter remains outside this slice.
- The API endpoint `POST /integrations/acs/email-delivery-reports` accepts the standard Event Grid array, responds synchronously to `SubscriptionValidationEvent`, and authenticates deliveries with the configured `EmailBounceWebhook:SharedSecret` sent as `X-Vpd-EventGrid-Secret`. Typed ACS delivery reports with a non-success status are resolved by recipient email and delegated to the application handler; delivered/expanded reports and unknown recipients are acknowledged without a write. Azure Event Grid delivery properties and the webhook secret are not configured in Azure yet.
- Application tests use an in-memory SQLite connection with real EF transactions to verify scan/session/cash/correction/reassignment atomicity and idempotent gesture behavior; this provider is test-only.

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
- `src/Scan` uses native IndexedDB database `vpd-scan` with separate `catalog`, `outbox`, and `session` stores. The catalog is replaceable projection data; the outbox is durable volunteer work with `Pending` until a local decision, then `Kept`/`Rejected` for sequential authenticated replay. Local decisions update the projection atomically and are never discarded by a catalog refresh; `CancelledLocal` entries remain local audit state until explicitly purged by a future policy. `navigator.storage.persist()` is requested at startup, while the Angular service worker caches only the app shell and public bibliographic metadata, never protected scan responses.
- Scan authentication is MSAL Browser/Angular (`Tri` scope) and is deliberately separate from local persistence: an unauthenticated volunteer can continue recording offline gestures, and synchronization waits until an authorized account and network are available.

## Notable Storage Detail

- The legacy `AssoEvents` date fields are `DateTimeOffset` values passed through the API,
  domain mapping, and SQL Server `datetimeoffset` storage without a backend timezone
  conversion. Existing event data uses UTC wall-clock components (`00:00Z` for calendar
  dates and `14:00Z` for a displayed 14:00 opening); frontend normalization is therefore
  required when editing or rendering those values. Existing drifted rows need a controlled
  data repair rather than an automatic schema migration.
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

## Books runtime update — 2026-09-04

- The Books schema now includes cancelled-fair state (`AssoEvents.IsCancelled`) and the migration `20260903230825_AddCancelledBookFair`; cancelled Books fairs remain auditable but are excluded from release, opening, next-fair selection, announcements, sales, and alert delivery. Attached announcements are detached when the fair is cancelled.
- `AccountDeletionStore` claims only `OutboxMessageKind.AccountDeletion`; its SQL Server claim uses update/read-past locks, while the provider query keeps the SQLite test harness executable. `AlertEmail` rows therefore cannot be deserialized by the account-deletion worker.
- The Books alert outbox has an exact claim lease token carried through revalidation, cancellation, success, and failure. Sent alerts write `UserAlertHistory` after ACS delivery so a retry cannot deliberately create another alert for the same book/member cooldown window.
- Bibliographic enrichment is retryable and negative-caches not-found results. Transient provider/cover failures keep the current `Pending`/`NotFound` state, record `LastAttemptAt` without consuming the negative-cache attempt budget, and use a one-hour `Pending` cooldown so failed early rows cannot starve never-attempted books. Optional cover downloads accept only the explicit HTTPS host allowlist (`covers.openlibrary.org`, `openapi.bnf.fr`), reject redirects and oversized/non-image payloads, and store stable keys under the `book-covers` container.
- Azure SQL migrations are applied explicitly by deployment workflows. API startup migrations are limited to `Development`; the production/dev rollout workflow runs migrations before the new API/Worker revisions.

## Dead-stock query persistence

The dead-stock read deliberately keeps its aggregate filters on `Books` and its historical
availability/sale checks on `BookMovements`. The movement index `Isbn13 + Type + OccurredAt`
supports the correlated sale and first-availability subqueries; SQLite-backed application tests
also exercise the translated query rather than evaluating it in memory.
