# 03 - Domain Model And Runtime Flow

## Backend and CQRS

- `Api/Program.cs` wires authentication/authorization, rate limiting, error handling, OpenTelemetry, and endpoint extensions.
- The normal request path is endpoint mapping → typed command/query in `Application` → MediatR handler → domain rules and `Infrastructure` ports → typed `Contracts` response.
- Keep domain rules in aggregates, transport DTOs in `Contracts`, HTTP wiring in `Api`, use cases in `Application`, and persistence/adapters in `Infrastructure`.

## Live Loto

- `AssoEvents.LiveRevision` is an EF concurrency token. Each successful number/win mutation increments it; conflicting writes return `409`.
- `/asso-events/{id}/tableau/sse` subscribes before reading its initial snapshot, uses the revision as SSE event ID, suggests a 3-second retry, and sends a `ping` after 15 seconds idle. `LiveTableauSyncService` checks revisions for events watched on its replica every second and publishes changed snapshots, so clients on different replicas converge.
- `GET /asso-events/today` uses the Paris calendar, skips cancelled events, prefers today's Bingo then the earliest event, returns `204` when empty, and caches for 10 seconds.
- `CurrentLineIndex` is a zero-based position in `LineParties` ordered by persisted `Index`, not that persisted index. Add-win and undo use the same ordering; undo also handles completed events and multiple empty parties.
- `Partie.AddWin()` safely returns false without a draw, a valid current line, or when the last number already won. Keep regression coverage for sparse lines, bingo undo, and replica synchronization.

## Domain slices and privacy

- Active slices include Actuality, AccountDeletion, Authentication, Books, Events, Orders, and Products. Mailing-list folders remain but are not mapped at runtime; automatic Bingo-card OCR was removed.
- Manual actuality is published on creation. Imported actuality records keep a unique `(Source, ExternalId)` trace; successful social imports publish immediately, and deletion of an actuality does not permit re-import.
- `Product.Available` and `VisibleOnWebsite` are independent gates for public product display; the full product projection remains for cashier/admin clients.
- `MemberSelectionItem` belongs to one member and targets one edition ISBN-13 or rare-book ID. Members cannot set `Purchased`; association to a sale sets that state. The aggregate caps selection at 500.
- `MemberCard` uses a versioned identifier and HMAC-signed QR without personal data; the API checks current card state. `CheckoutPassage` stores purchase-line metadata snapshots without price. Account deletion removes member-only projections, anonymizes passages, and retains audit movements without a usable member link.
- `BookNotFoundReport` targets an edition or rare-book fiche, holds an optional comment and closure history, and has no location field. Detaching a member clears both their identity and free text while retaining history.

## Books domain invariants

- Ordinary ISBN `Book` and `RareBook` are distinct aggregates. Ordinary books use ISBN-13 as canonical key and an append-only `BookMovement` inventory ledger. Corrections, cancellations, and session reassignment use signed inverse/replay movements; only deletion of a provably unused erroneous fiche is an explicit hard-delete exception.
- ISBN merges preserve redirect rows; ledger movements retain their original ISBN. Public/catalogue projections require a completed metadata state and title, and hide books while an `InProgress` scan session owns their movement.
- Scan decisions and cash sales use client gesture IDs for idempotence. Cash has its own offline outbox and can run without opening a triage session. A scan session is unique per volunteer while open; close is retry-safe.
- Book metadata patches lock selected fields. Automatic refresh skips locked fields but keeps fetch/attempt state; failed provider requests and “not found” use separate retry accounting.
- A Books fair is the half-open interval `[OpenAt, CloseAt)` derived from event dates/times in Europe/Paris. Overlap validation rejects ambiguous fairs; do not guess when legacy events overlap.
- Alert rows are created transactionally with session close, grouped per member, delayed by association settings, and protected by sent-history cooldowns. Undated fairs do not enqueue alerts.
- Rare-book price is display data only: sales do not persist a price, basket total, or revenue. Rare-book photos use the dedicated blob container and deletion must remove blobs before database rows.

## Statistics and shared conventions

- Private volunteer statistics derive the member from the authenticated `oid`; client-supplied volunteer IDs are not accepted. Duration, cadence, reader linkage, and revenue share reconstructed from timestamps/ledger are estimates and must be labeled.
- Keep commands/queries and validators beside their feature. Review both Angular clients and MAUI when a shared contract changes.
