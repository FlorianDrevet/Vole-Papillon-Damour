# 20 - Scan And Cash Offline Reliability

## Internal-code favorite-count failure — 2026-11-22

- App Insights recorded 91 distinct HTTP 500 responses from `GET /scan/catalog/delta`. The exception was `System.InvalidOperationException: Favorite count ISBNs must be valid ISBN-13 values.` Catalog delta started returning generated internal codes for books without an ISBN; the shared favorite-count reader treated every book code as an ISBN and attempted to parse it. A related-books lookup could also include internal codes sharing a WorkId with an ISBN book.
- `FavoriteCountSeed` now retains the typed `BookCode`. Only actual ISBNs are passed to favorite aggregation or ISBN parsing; internal-code books are returned with favorite count zero, and internal-code rows are excluded from related ISBN candidates. Both the catalog delta and scan-book favorite-count callers use the typed code.
- Regression coverage includes an internal code by itself, an internal code sharing a WorkId with a favorited ISBN book, separate counts for ISBNs without WorkIds, and a single seeded edition inheriting favorites from a related edition. Application tests pass (881; 70 build warnings). API Release build passes with 0 errors and 71 existing warnings, using installed SDK 10.0.203 because the repository pins unavailable SDK 10.0.404. Stryker reported 32 killed, 10 survived, 17 ignored, 7 without coverage, and 11 compile-error mutants; it also warned that coverage capture failed. That report predates the last direct-reader regression test. No deployment was made; [PR #425](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/425) is open and unmerged.
## PWA build identification and update

- The Scan home screen shows the short Git commit ID embedded by `.github/workflows/scan-deploy.yml` through the `SCAN_APP_VERSION` Docker build argument. This identifies the exact source build independently of the manually selected image tag.
- `ScanUpdateService` checks Angular's service worker on first opening the home screen and exposes a manual recheck. A ready version prompts with a `Mettre à jour` action that reloads the page; it deliberately does not call `activateUpdate()`, which could mix an old shell with new lazy-loaded chunks.
- The update status is only confirmed after an online service-worker check. In development the service worker is disabled, so the control reports that production PWA updates are unavailable there.
- `rtk npm run build` passes; the initial JS bundle is 177.41 kB over its 1.50 MB warning budget. Automated specs and authenticated visual review were not run; the local Docker image build was unavailable because Docker is not installed. There was no deployment.

## Read-only App Insights review — 2026-11-11

- `POST /scan/sales` had two requests with the same client gesture about 19 ms apart; one hit unique index `IX_BookMovements_ClientGestureId` and surfaced as a database error. The handler previously checked idempotency before saving but did not recover when two concurrent requests both passed that lookup. The losing request now clears tracked state and returns the committed sale when the winning gesture is visible.
- `GET /scan/catalog/delta` had one SQL timeout after about 194 seconds in the reviewed window. The catalog handler loaded every active book fair and selected the next fair in memory. Successful observations were much shorter (roughly p95 100 ms, p99 300 ms, maximum about 33.9 seconds), so this was a long-tail outlier rather than a stable baseline. The handler now filters, orders, and takes the next fair in SQL.
- These are dated observations from a read-only Azure query. They do not establish how frequently a weak network causes client timeouts, and no Azure data or configuration was changed.

## Scan/Caisse code paths

- `syncAll()` now sends durable scan/cash outbox work before awaiting the catalog delta. Catalog reads time out after 45 seconds; cash writes and voids time out after 30 seconds. Silent MSAL token acquisition times out after 10 seconds and moves the client into its existing degraded/cached authorization state so local work can continue.
- Ordinary cashier sales use an atomic IndexedDB send/cancel state transition. A still-pending sale can be removed locally and its optimistic stock restored. A sale being transmitted or already accepted by the API retains a correction record and stable void gesture; `POST /scan/sales/{saleMovementId}/void` uses the existing ledger correction command and requires `Caisse`.
- Multiple same-book cash operations remain composable: cancellation adds only the cancelled quantities to the current local projection, while a sale/void response reapplies any still-pending sales and voids. Regression tests cover cancelling one of several sales, completing one while another remains queued, and receiving one response while another void remains queued.
- The corrected fair delta query has a provider-backed SQLite regression test with comparable date converters. The sale idempotency race has an Application regression test that simulates the concurrent unique-index winner.

## Physical-format signal in Scan triage — 2026-11-23

- `BookMetadataResponse` already exposes `PhysicalFormat`; the scan catalog delta now carries the same scalar from `Book` through `ScanCatalogBookResult` and `ScanCatalogBookResponse` into the offline `ScanCatalogBook` cache.
- Scan IndexedDB version 8 invalidates the catalog watermark for version-7 clients so the delta reloads physical formats, while keeping the existing catalog usable offline during that refresh. Older upgrades still use the existing metadata migration that clears stale catalog rows.
- `ScannerComponent.newFormatNotice` adds the orange « Nouveau format » callout only when the scanned format differs from a strongly linked edition with an available or announced copy. It supplements the current tri verdict; unknown formats and weak title/author links do not trigger it.
- The catalog delta keeps its existing query count, filters and ordering; the response adds one projected scalar per book.

## Validation and limits

- Full Scan suite: 329/329; Application tests: 710/710; API tests: 93/93; API build and Scan production build pass. The directly targeted auth/API/store/sync/workflow subset passes 113/113.
- The Scan build reports the initial JS bundle 11.41 kB over its 1.50 MB budget.
- The component suite's outdated spies were updated to the current workflow contract, and the camera preview assertion now verifies it remains inside the visible reserved slot while allowing the expected clipping. All 105 component specs pass.
- A real local Scan → Caisse E2E was not possible in this session. Aspire found no running AppHost because Docker Desktop crashed during startup on an inaccessible stale `dockerInference` IPC reparse point; a normal app relaunch left `com.docker.service` stopped. The automatic review blocked deleting that IPC link (`blocked by policy`), so no workaround was attempted. Factory reset was not attempted. No test book was written to Azure or another deployed database.
- Graphify refreshed this worktree without an LLM: 2,220 code files, 19,074 nodes, 56,906 edges, and 642 communities. Its HTML output uses the aggregated community view.
- No deployment was made. The local test data for an actual book-in/checkout needs to be run after Docker Desktop is repaired and the isolated Aspire stack can start.
