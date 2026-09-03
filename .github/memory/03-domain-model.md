# 03 - Domain Model And Runtime Flow

## Backend Runtime

The API boots from `Vole_Papillon_Damour.Api/Program.cs` and wires:

- Swagger in development
- controllers with camelCase JSON output
- authorization policy `IsAdmin`
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
- `Authentication`
- `Events`
- `Orders`
- `Products`

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

## Books module — P1-4 local runtime slice

As of 2026-09-03, the P1-3 domain foundation exists locally in `Domain`: `Book` (ISBN-13 key), `BookMovement` (append-only ledger), `ScanSession`, `AssociationSettings`, and the `BookAnnouncement` entity. Strong IDs are used for movement, announcement, and scan sessions. `Book.RedirectTo()` preserves the absorbed row and prevents self/repeated redirection; movement quantities are signed and non-zero; scan-session counters and close are idempotent; settings are a typed singleton with documented defaults. New module instants are required and persisted as UTC, while calendar comparisons and local-midnight calculations use Europe/Paris in Application. Book merges keep ISBN as the public key and use a direct `RedirectedToIsbn13` link to a canonical Books row; `BookMovements` retain their original ISBN for audit. An open Books fair is the half-open `[OpenAt, CloseAt)` interval derived from `AssoEvents` date/door fields, with overlap validation and no guessing when legacy events overlap.

The first P1-4 application slice is local and tested: `ScanBook` accepts a final `Kept`/`Rejected` decision from the offline client, normalizes ISBN, resolves direct redirects, calculates the `RG-15` verdict without a bibliographic call, and commits the book projection, session counters, movement, and optional fair announcement atomically. `ClientGestureId` is unique on movements and copied to announcements; a replay returns an idempotent result. `OpenScanSession` enforces one active session per volunteer, and `CloseScanSession` is safe to call repeatedly. The internal cash and correction flows now record sales against one open Books fair, allow a traced inverse only while that fair is open, record physical quantity corrections, persist association settings, attach undated announcements, and update rare/catalog flags. `ReassignSessionMode` uses inverse correction movements and replay movements, and marks the closed session as `Resumed`; `BookMovement.ReversalOfMovementId` makes each inverse traceable and unique. Manual metadata patches update only selected fields and persist field locks; automatic bibliographic refreshes skip locked fields while retaining fetch/attempt data, including the work identifier used by work-scoped watchlists. Book deletion is allowed only when the row has no sale, movement, or announcement history, preserving the append-only ledger. `Watchlist`/`WatchlistItem` persist edition or work requests, and `BookAlertOutbox` groups matching books by member at session close with cooldown, alert-status filtering, configurable delay, and no queue entry for an undated fair. `CancelBookAlerts` and `ForceBookAlerts` operate on pending `AlertEmail` rows; `ReassignSessionMode` cancels and rebuilds pending alerts inside its transaction without recreating already-sent messages. `Watchlist.RecordEmailBounce` counts consecutive failures, suspends alerts at the starting threshold of three, and `RecordSuccessfulEmailDelivery` resets that consecutive count without automatically reactivating a suspended list. `EmailBounceEvent` records the provider event identity, member, and UTC receipt time behind a unique provider-id index; a sequential Event Grid replay returns the current state without incrementing the watchlist again. The ACS/Event Grid transport, API contract, metadata queue, Scan PWA, and worker sending/integration are not implemented yet.

P1-4 is not yet an externally deployed runtime fact: API contracts, the Scan PWA, metadata queue, alert outbox, remaining handlers, and migration application remain future work, and `QT-02` must still be closed before the worker is changed.

## Conventions To Preserve

- Keep commands and queries in their feature folders under `Application`.
- Keep validators and MediatR handlers close to the feature they serve.
- Keep domain rules in aggregates and domain types, not in controllers.
- Keep transport DTOs in `Contracts`, not inside Angular or MAUI code.
- When a shared contract changes, review both Angular apps and the MAUI client.
