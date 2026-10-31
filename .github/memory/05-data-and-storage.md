# 05 - Data And Storage

## SQL persistence

- `Infrastructure.DependencyInjection` configures `ProjectDbContext` with SQL Server and `ProjectDatabase`; `IProjectDbContext` exposes it to Application. Persistence interfaces live in Application and EF/repository implementations in Infrastructure.
- EF configuration owns strong-ID/ISBN conversions, UTC `datetime2` conversion, row versions, filtered unique indexes, and database-specific collation behavior. `Book.Id` maps to persisted `Books.Isbn13`; raw SQL must join on `Isbn13`, not `Id`.
- Deployment workflows apply EF migrations before rolling out API/Worker revisions. API startup migrations run only in Development; do not assume a code change has updated a deployed schema.
- Books ledger history is append-only. Corrections, reversals, and reassignment use signed linked movements. Hard delete is limited to unused erroneous catalogue records and removes dependent projections transactionally.
- Books metadata separates `Pending`, `Failed`, `NotFound`, `Resolved`, `Manual`, and `Discarded`. `MetadataFailureReason` is bounded; discarded rows with movement history remain hidden and get a compensating withdrawal. Manual field locks survive enrichment.
- SQLite-backed Application tests use a real in-memory EF transaction and are test-only; validate SQL Server-specific scripts/queries with the SQL Server provider before relying on translation or collation.

## Books and member records

- Ordinary `Book` rows and `RareBook` rows have separate persistence and contracts. `RareBooks`/`RareBookPhotos` use row-version concurrency, unique UUID/slug/ISBN constraints, and ordered unique photo positions. Rare-book firm prices are display-only; sale lines persist no price or total.
- Rare-book photos accept JPEG, WebP, or PNG up to 8 MiB in `rare-book-photos`. Delete the blob before deleting its row; a failed blob delete must retain the database record.
- `BookMovement` is the inventory source of truth. The dead-stock query combines current `Books` state with historical first-positive and sale movements; the `Isbn13 + Type + OccurredAt` index supports those checks.
- Member selection, signed member cards, purchase passages, and not-found reports are separate member projections. Account deletion removes selection/cards, anonymizes passage identity, and detaches report identity/comment while retaining audit rows and purchase snapshots.
- `BookNotFoundReports` has a filtered unique constraint for one open report per member/target. Its request has no location field; the default daily limit is 10 (configurable 1–100).
- Manual request contact data is isolated from Scan catalog sync and diagnostics, and is erased 90 days after the last request closes; closed history remains anonymous. See `.github/memory/12-manual-book-requests.md`.

## Alerts and external services

- Book alerts are inserted transactionally at scan-session close, grouped per member, delayed by `AssociationSettings`, and suppressed by recent delivery history. Cancellations affect only pending messages; force-send resets the lease/due time.
- ACS Event Grid delivery reports enter through the typed `/integrations/acs/email-delivery-reports` contract. Provider event IDs are unique, bounce updates are transactional/idempotent, and recipient data must not be copied into ordinary app logs. ACS `202` means accepted for processing, not mailbox delivery.
- Azure Blob connection configuration is centralized in Infrastructure. Named containers include `loto-images`, `actuality-images`, `event-images`, `product-images`, and `rare-book-photos`.
- Bibliographic resolution currently falls back BnF SRU → Open Library → Google Books and returns validated HTTPS image URLs. BnF queries `bib.fuzzyISBN` so ISBN-13 searches can match older ISBN-10 notices; see `.github/memory/14-scan-metadata-latency.md` and `.github/memory/15-isbndb-migration.md` for current provider observations and planned ISBNdb-first rollout.

## Client storage and operational data

- Scan uses IndexedDB `vpd-scan` for catalog, session, and outbox state. Requesting persistent browser storage is best-effort; a denied request means local retention is not guaranteed and is distinct from failed server synchronization.
- Cash sales have a dedicated durable `sales` store and replay through `POST /scan/sales` with `ClientGestureId`; do not merge them into the triage session outbox.
- As of 2026-10-20, the application CSV has 7,748 valid unique canonical ISBN-13 values; `Nombre_de_photos` is only source annotation. Each newly imported ISBN would add one copy, idempotently. No full-file import was recorded; check `NEXT.md` before any import.
- Use `.agents/skills/book-data-reset/SKILL.md` for an explicitly requested deployed reset. `rg-vpd-dev`, `vpd-sql-dev`, `vole-papillon-damour-db`, and `vpdstdevfc` are production resources despite `dev` names. The reset protects active users/the named keeper and preserves events, news, Entra identities, and blobs.
- Never place credentials, access tokens, or connection strings in memory. Use the production backup skill only for an explicitly requested backup; see `.agents/skills/vpd-prod-backup/SKILL.md`.
