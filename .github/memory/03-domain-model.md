# 03 - Domain Model And Runtime Flow

## Backend Runtime

The API boots from `Vole_Papillon_Damour.Api/Program.cs` and wires:

- Swagger in development
- controllers with camelCase JSON output
- staged Entra/legacy bearer authentication with the compatibility authorization policy `IsAdmin`
- Azure Monitor OpenTelemetry
- custom error handling middleware
- rate limiting
- authentication and authorization

## CQRS Flow

Application setup registers:

- MediatR handlers from the Application assembly
- FluentValidation validators from the Application assembly
- a `ValidationBehavior<,>` pipeline behavior

The usual change path is:

1. API endpoint or controller extension receives HTTP input
2. request maps to a command or query in `Application`
3. MediatR dispatches to a handler
4. handler uses repositories or services from `Infrastructure`
5. contracts and results flow back to clients

## Live Loto Tableau Flow

- The live bingo/loto mutation path is `EventsController` -> `AddNumeroToEvent`, `RemoveLastNumero`, `AddWinPartie`, or `AddBingoWin` handler -> `IEventRepository.UpdateAsync` -> event-scoped SSE broadcast.
- SSE delivery is scoped by `AssoEventsId` through `ISSEClientManager.SendToEvent`; do not reintroduce all-client broadcasts for `/asso-events/{id}/tableau/sse`.
- `RemoveLastNumeroCommandHandler` supports rollback across multiple empty previous parties, cleans the removed partie's `AddedBingoNumber` from `BingoNumeros`, and resets `BingoHasBeenWon` when undoing a bingo partie numero.
- `Partie.RemoveLastNumero()` tolerates parties without line parties and inconsistent live state where the last drawn numero is absent from `LiveNumeros`.
- `Partie.AddWin()` now safely returns `false` when no numero is drawn, when the current line is missing, or when the last numero already won.
- Critical live-flow tests live in `Vole_Papillon_Damour.Application.tests`, `Vole_Papillon_Damour.Infrastructure.tests`, and `Domain.tests`; May 2026 coverage verified 100% line/branch on live add/remove/win handlers and validator, plus 100% line/branch on targeted domain methods `Partie.AddLiveNumero()`, `Partie.RemoveLastNumero()`, `Partie.AddWin()`, `LinePartie.AddWin()`, `LinePartie.RemoveNumero()`, `Lot.IsWonByLastNumber()`, `AssoEvents.AddBingoNumero()`, and `AssoEvents.RemoveBingoNumero()`.

## Feature Slices

Verified slices in `Application` and `Contracts` include:

- `Actuality`
- `AccountDeletion`
- `Authentication`
- `Books` (catalogue, scanning, inventory, sales, alerts, and bibliographic metadata)
- `Events`
- `Orders`
- `Products`

### Actuality social-import slice

`Domain.ActualityAggregate.Actuality` keeps manual creations `Published`, while
`CreateImported` creates a `Draft` with `ImportedAt` and an optional
`TitleNeedsReview` marker. `Publish()` enforces a non-empty title and article;
`Update()` keeps a draft in draft state and clears the review marker. The separate
`SocialPostImport` aggregate records `(Source, ExternalId)` and survives actuality
deletion through the nullable `ActualityId` relationship. The import command lives in
`Application/Actuality/Commands/Background`, downloads media before persistence, and
commits the actuality plus import trace through `IActualityImportStore`.

Residual `MailingList` folders still exist in `Application` and `Contracts`, but `Program.cs` no longer wires a mailing-list endpoint surface into the active API runtime.
The dedicated `BingoCard` OCR slice was removed from `Application`, `Contracts`, and `Api` in May 2026; automatic loto-card analysis no longer exists in the active runtime.

## Domain Aggregates

Verified aggregate folders in `Domain` include:

- `ActualityAggregate`
- `AssoEventsAggregate`
- `OrderAggregate`
- `ProductAggregate`
- `UserAggregate`

`Product` keeps `Available` separate from `VisibleOnWebsite`: both gate the public product projection, while the full `/product` projection remains available to cash clients and BackOffice.

## Books module — P1-5/P1-10 runtime slice

P1-10 adds the cash path to the Scan PWA: a sale is recorded locally in its own
IndexedDB outbox, decrements the local quantity immediately, and replays idempotently to
`POST /scan/sales` under the `Caisse` policy without opening a triage session.

As of 2026-09-03, the P1-3 domain foundation exists locally in `Domain`: `Book` (ISBN-13 key), `BookMovement` (append-only ledger), `ScanSession`, `AssociationSettings`, and the `BookAnnouncement` entity. Strong IDs are used for movement, announcement, and scan sessions. `Book.RedirectTo()` preserves the absorbed row and prevents self/repeated redirection; movement quantities are signed and non-zero; scan-session counters and close are idempotent; settings are a typed singleton with documented defaults. New module instants are required and persisted as UTC, while calendar comparisons and local-midnight calculations use Europe/Paris in Application. Book merges keep ISBN as the public key and use a direct `RedirectedToIsbn13` link to a canonical Books row; `BookMovements` retain their original ISBN for audit. An open Books fair is the half-open `[OpenAt, CloseAt)` interval derived from `AssoEvents` date/door fields, with overlap validation and no guessing when legacy events overlap.

The first P1-4 application slice is local and tested: `ScanBook` accepts a final `Kept`/`Rejected` decision from the offline client, normalizes ISBN, resolves direct redirects, calculates the `RG-15` verdict without a bibliographic call, and commits the book projection, session counters, movement, and optional fair announcement atomically. `ClientGestureId` is unique on movements and copied to announcements; a replay returns an idempotent result. `OpenScanSession` enforces one active session per volunteer, and `CloseScanSession` is safe to call repeatedly. The internal cash and correction flows now record sales against one open Books fair, allow a traced inverse only while that fair is open, record physical quantity corrections, persist association settings, attach undated announcements, and update rare/catalog flags. `ReassignSessionMode` uses inverse correction movements and replay movements, and marks the closed session as `Resumed`; `BookMovement.ReversalOfMovementId` makes each inverse traceable and unique. Manual metadata patches update only selected fields and persist field locks; automatic bibliographic refreshes skip locked fields while retaining fetch/attempt data, including the work identifier used by work-scoped watchlists. Book deletion is allowed only when the row has no sale, movement, or announcement history, preserving the append-only ledger. `Watchlist`/`WatchlistItem` persist edition or work requests, and `BookAlertOutbox` groups matching books by member at session close with cooldown, alert-status filtering, configurable delay, and no queue entry for an undated fair. `CancelBookAlerts` and `ForceBookAlerts` operate on pending `AlertEmail` rows; `ReassignSessionMode` cancels and rebuilds pending alerts inside its transaction without recreating already-sent messages. `Watchlist.RecordEmailBounce` counts consecutive failures, suspends alerts at the starting threshold of three, and `RecordSuccessfulEmailDelivery` resets that consecutive count without automatically reactivating a suspended list. `EmailBounceEvent` records the provider event identity, member, and UTC receipt time behind a unique provider-id index; a sequential Event Grid replay returns the current state without incrementing the watchlist again. The API now exposes a dedicated ACS/Event Grid delivery-report endpoint with typed Event Grid contracts, synchronous subscription validation, shared-secret authentication, recipient-to-member resolution, and safe acknowledgement of unknown recipients or members without a watchlist. P1-5 now adds typed Scan delta/session/gesture contracts and the local PWA path: a compact catalog projection with hidden-entry removals and watchlist-state reprojection, IndexedDB `catalog`/`outbox`/`session` stores, durable `Pending`/`Kept`/`Rejected` gestures, local `RG-15` verdicts, MSAL `Tri` authentication, service-worker shell caching, and sequential outbox replay with `ClientSessionId`/`ClientGestureId` idempotence. The P1-5 metadata queue and Worker integration are now deployed; remaining checks are operational rather than local implementation.

P1-5 is externally deployed for the API/Worker/Scan path, and the metadata queue has retry/backoff fairness through `LastAttemptAt`. The Worker now runs the scheduled sweep/enrichment and alert delivery; ACS delivery is enabled in DEV after domain verification. Bibliographic enrichment now carries a normalized `Genre` from BnF, Open Library or Google Books, merges provider fallbacks when the first source has no genre, and the scheduled Worker sweep backfills resolved books missing a genre without overwriting manually locked fields. A resolved-book refresh records its own failed-attempt cooldown while preserving the existing metadata status. The new `Sweep`/`Enrich` heartbeat, an authorized-recipient email test, `QT-02`, and physical `QT-08` checks remain future work; the 300-book S0-4 campaign was reported successful without detailed sub-metrics.

The account-deletion finalization now matches the privacy model: the pending-message lookup
uses a provider-neutral SQLite/Aspire path, and member-only projections (`WatchlistItems`,
`Watchlist`, `UserAlertHistory`, `EmailBounceEvents`) plus `AlertEmail` outbox rows are removed
in the same transaction before a user is anonymized or deleted. Book movements and scan
sessions remain available for audit when retention is required. Infrastructure regressions
cover both the retained-movements and no-retained-movements branches; the backend suite has
291 tests after PR #61.

## Books administration — dead-stock query

`GetDeadStockQuery` is the first P1-9 administration read: it returns canonical, currently
available books whose current quantity is above the requested threshold, whose first positive
availability movement is older than the requested age, and which have no sale movement. It
derives the age from the append-only `BookMovements` ledger, excludes redirected fiches, sorts
by quantity descending, then availability date and ISBN, and logs the query duration. The API
surface is `GET /books/admin/dead-stock`, protected by the `Administration` policy; default
filters are six months and more than three copies. The benchmark remains to be executed; the
catalog administration screen is implemented in BackOffice and Catalog.

## Books administration — P2/P3 completion (2026-09-06)

The catalog administration slice now follows the existing layered CQRS boundaries:
`Application/Books/Commands/Admin` contains manual book creation, metadata/quantity and
announcement corrections, withdrawals, rare/visibility flags, canonical ISBN merges,
fair revenue, session movement removal/cancellation/reassignment, alert message control,
member alert blocking/deletion, and settings writes. `Application/Books/Queries/Admin`
contains overview, paged books, fair statistics, scan sessions, outbox alerts, members,
and settings reads. `BookAdministrationController` exposes the corresponding
`/books/admin/*` surface under the `Administration` policy.

Corrections remain append-only: session removal/cancellation creates reversal movements,
skips movements already reversed, rejects a reversal when stock has already been consumed,
and resolves a canonical target after a merge. Announcement correction/reversal notes are
prefixed `Announcement.Correction` so they do not inflate available stock projections.
`BookAlertOutbox.QueueForSessionAsync` also excludes an original movement with a reversal,
preventing a corrected session from recreating its alert.

The optional `AssoEvents.BookRevenue` field is persisted as nullable `decimal(12,2)` by
`20260906101759_AddBookFairRevenue`; absence means “not entered”, never zero. Member alert
preferences are persisted through `PATCH /catalog/me/alerts`; a member can suspend or
reactivate their own alerts but cannot override an administrative `Blocked` state.

The current administrator presentations are the Angular BackOffice route and the V2
`src/Catalog` route `/administration`, both with responsive workspaces for overview,
books/stock, fairs/statistics, sessions, alerts, members, and settings. The Catalog also
owns the public member/watchlist flows; role assignment remains an Entra concern and
physical cartons are not represented by the domain.

## Scan session identity projection

The Catalog scan-session opening endpoint ensures a local `User` projection from the
authenticated external identity before creating the session. It stores the optional token
display name as the domain `Name` value. Administrative session list/detail projections
render only the formatted first and last name; they do not fall back to the volunteer email.
A session whose volunteer ID has no local user or whose projection has no name therefore
shows a neutral missing-name label in the UI, because `ScanSession` stores only that ID.

## Conventions To Preserve

- Keep commands and queries in their feature folders under `Application`.
- Keep validators and MediatR handlers close to the feature they serve.
- Keep domain rules in aggregates and domain types, not in controllers.
- Keep transport DTOs in `Contracts`, not inside Angular or MAUI code.
- When a shared contract changes, review both Angular apps and the MAUI client.
