# 20 - Scan And Cash Offline Reliability

## Internal-code favorite-count failure — 2026-11-22

- App Insights recorded 91 distinct HTTP 500 responses from `GET /scan/catalog/delta`. The exception was `System.InvalidOperationException: Favorite count ISBNs must be valid ISBN-13 values.` Catalog delta started returning generated internal codes for books without an ISBN; the shared favorite-count reader treated every book code as an ISBN and attempted to parse it. A related-books lookup could also include internal codes sharing a WorkId with an ISBN book.
- `FavoriteCountSeed` now retains the typed `BookCode`. Only actual ISBNs are passed to favorite aggregation or ISBN parsing; internal-code books are returned with favorite count zero, and internal-code rows are excluded from related ISBN candidates. Both the catalog delta and scan-book favorite-count callers use the typed code.
- Regression coverage includes an internal code by itself, an internal code sharing a WorkId with a favorited ISBN book, separate counts for ISBNs without WorkIds, and a single seeded edition inheriting favorites from a related edition. Application tests pass (881; 70 build warnings). API Release build passes with 0 errors and 71 existing warnings, using installed SDK 10.0.203 because the repository pins unavailable SDK 10.0.404. Stryker reported 32 killed, 10 survived, 17 ignored, 7 without coverage, and 11 compile-error mutants; it also warned that coverage capture failed. That report predates the last direct-reader regression test. No deployment was made; see the current PR for final validation and rollout state.

## Read-only App Insights review — 2026-11-11

- `POST /scan/sales` had two requests with the same client gesture about 19 ms apart; one hit unique index `IX_BookMovements_ClientGestureId` and surfaced as a database error. The handler previously checked idempotency before saving but did not recover when two concurrent requests both passed that lookup. The losing request now clears tracked state and returns the committed sale when the winning gesture is visible.
- `GET /scan/catalog/delta` had one SQL timeout after about 194 seconds in the reviewed window. The catalog handler loaded every active book fair and selected the next fair in memory. Successful observations were much shorter (roughly p95 100 ms, p99 300 ms, maximum about 33.9 seconds), so this was a long-tail outlier rather than a stable baseline. The handler now filters, orders, and takes the next fair in SQL.
- These are dated observations from a read-only Azure query. They do not establish how frequently a weak network causes client timeouts, and no Azure data or configuration was changed.

## Scan/Caisse code paths

- `syncAll()` now sends durable scan/cash outbox work before awaiting the catalog delta. Catalog reads time out after 45 seconds; cash writes and voids time out after 30 seconds. Silent MSAL token acquisition times out after 10 seconds and moves the client into its existing degraded/cached authorization state so local work can continue.
- Ordinary cashier sales use an atomic IndexedDB send/cancel state transition. A still-pending sale can be removed locally and its optimistic stock restored. A sale being transmitted or already accepted by the API retains a correction record and stable void gesture; `POST /scan/sales/{saleMovementId}/void` uses the existing ledger correction command and requires `Caisse`.
- Multiple same-book cash operations remain composable: cancellation adds only the cancelled quantities to the current local projection, while a sale/void response reapplies any still-pending sales and voids. Regression tests cover cancelling one of several sales, completing one while another remains queued, and receiving one response while another void remains queued.
- The corrected fair delta query has a provider-backed SQLite regression test with comparable date converters. The sale idempotency race has an Application regression test that simulates the concurrent unique-index winner.

## Validation and limits

- Full Scan suite: 329/329; Application tests: 710/710; API tests: 93/93; API build and Scan production build pass. The directly targeted auth/API/store/sync/workflow subset passes 113/113.
- The Scan build reports the initial JS bundle 11.41 kB over its 1.50 MB budget.
- The component suite's outdated spies were updated to the current workflow contract, and the camera preview assertion now verifies it remains inside the visible reserved slot while allowing the expected clipping. All 105 component specs pass.
- A real local Scan → Caisse E2E was not possible in this session. Aspire found no running AppHost because Docker Desktop crashed during startup on an inaccessible stale `dockerInference` IPC reparse point; a normal app relaunch left `com.docker.service` stopped. The automatic review blocked deleting that IPC link (`blocked by policy`), so no workaround was attempted. Factory reset was not attempted. No test book was written to Azure or another deployed database.
- Graphify refreshed this worktree without an LLM: 2,220 code files, 19,074 nodes, 56,906 edges, and 642 communities. Its HTML output uses the aggregated community view.
- No deployment was made. The local test data for an actual book-in/checkout needs to be run after Docker Desktop is repaired and the isolated Aspire stack can start.
