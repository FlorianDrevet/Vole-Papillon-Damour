# Catalog request and statistics latency

The implementation is in [PR #346](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/346),
open and not merged. No deployment was made.

## Deployed telemetry (2026-10-18 to 2026-10-25)

Read-only Application Insights queries against `vpd-catalog-appi-dev-fc` and
`vpd-api-appi-dev-fc` show:

| Request | Count | p50 | p95 | Max | Failed |
|---|---:|---:|---:|---:|---:|
| Catalog SSR `GET /` | 38 | 0.911 s | 14.794 s | 17.891 s | 0 |
| Catalog SSR `GET /recherche` | 423 | 0.272 s | 1.330 s | 13.133 s | 0 |
| API `GET /catalog/search` | 2,701 | 0.021 s | 0.471 s | 10.861 s | 45 |
| API volunteer statistics | 5 | 2.213 s | 5.821 s | 5.821 s | 0 |
| API catalogue-flow statistics | 7 | 1.413 s | 2.435 s | 2.435 s | 0 |
| API fairs evolution | 2 | 0.094 s | 0.113 s | 0.113 s | 0 |
| API admin overview | 115 | 1.353 s | 3.937 s | 5.619 s | 1 |

Two slow Catalog `GET /` traces awaited `/catalog/search`, `/catalog/rare-books` and
`/asso-events` calls before returning. The home page's initial `forkJoin` included all
four API calls (two search queries plus rare books and fairs), so the optional sections
could delay server rendering of the main books.

The slowest `/catalog/search` trace on 2026-10-21 took 10.861 s and contained five SQL
dependencies, including sequential calls of 5.385 s and 5.432 s. Their `data` field is
empty in Application Insights. The handler first queries all public genre facets, then
the filtered/sorted page, followed by a count when requested; these are candidates for
the two slow calls, but telemetry does not identify which statements they were. Do not
attribute the SQL long tail to a specific LINQ expression without Query Store or query
stage instrumentation. The endpoint's seven-day p95 is 0.471 s despite the outliers.

The API volunteer-statistics trace at 5.821 s contained four SQL dependencies totaling
about 183 ms. The handler previously materialized historical scan sessions, movements,
all books and all users, then applied the requested time/fair filters and aggregation in
memory. This was avoidable work even though the precise non-SQL time is not identified by
the trace.

The deployed SQL database is Standard S0 (10 DTU). Over the same seven-day window,
hourly-average DTU use averaged 0.6%, with an hourly maximum of 82%; this does not show
sustained saturation or prove a correlation with the slow queries.

## Changes in `fix/catalog-book-statistics-latency`

- Volunteer and catalogue-flow statistics now apply UTC period and selected-fair
  predicates in SQL before materializing rows. They load only books and users referenced
  by retained movements and skip those lookups when there are no matching rows.
- Catalog home SSR now waits for the two primary book searches only. Rare books and fairs
  load after the first browser render and have independent loading states.
- Catalog administration starts each statistics request while fair options load,
  removing the frontend's serial wait.

## Validation and remaining work

- Application tests: 690/690 pass. Targeted statistics handler tests cover SQL date
  predicates and avoiding whole-table book/user reads when there are no matching rows.
- Catalog targeted specs pass 101/101; production browser/server build passes with the
  existing bundle/style budget and CommonJS warnings.
- Full Catalog suite: 576/580 pass. The same four failures reproduce on `origin/main`:
  three AccountCard assertions and one book-detail copy assertion.
- No Azure resource, database or deployment was changed. Graphify extracted all 1,854
  code files but could not render its 9,073-node HTML visualization.
- API `/catalog/search` and admin `/books/admin/overview` still have slow outliers. The
  next useful diagnostic is to correlate SQL Query Store statements/plans with a slow
  request, or add query tags and timings around facet, page, count and availability reads.
  Then decide whether the public genre-facet aggregation should be cached or computed
  separately, and whether the SQL tier needs more capacity. Do not scale production based
  on one hourly maximum alone.

## BackOffice administration overview latency — 2026-10-26

Read-only Application Insights queries against `vpd-api-appi-dev-fc` observed five
`GET /books/admin/overview` requests on 2026-10-26: p50 4.274 s, p95/max 7.191 s,
with no failures. This is a small one-day sample. Four correlated requests each had 21
SQL dependencies; their total dependency duration was 2.94–4.26 s within 2.96–4.65 s
HTTP requests. The Application Insights browser component for BackOffice returned no
page views or browser dependencies in the prior 30 days, so end-to-end screen readiness
is not measured and the reason for missing client telemetry remains unconfirmed.

The default admin Actualités page calls `GET /actuality/all`; two observed API calls
had a 6 ms p95 and no failures. The slow data path is the separate Catalog
administration overview. Its S0 SQL database (10 DTU) was online; during two five-minute
intervals around the observed requests, average DTU was 6.5% and 9.5%, with maxima of
47% and 46%. This does not show sustained database saturation.

The targeted optimization groups four book counters into one aggregate query and groups
each current/previous period summary from five SQL commands into two. The SQLite command
counter regression measured 22 commands before and 13 after (41% fewer); Application
tests pass 698/698. No Azure resource, database or deployment was changed. Recheck
production latency after merging and deploying the API.

## Public catalogue and admin portal optimization — implementation in progress — 2026-11-15

- Tasks 0–13 of `docs/features/perf-catalogue-admin/02-plan.md` are committed on the
  dedicated `fix/perf-catalogue-admin` branch. Task 13 lazily loads the administration
  module, fetches only the active parent-owned workspace, overlaps navigation counts
  with workspace loading, uses the navigation-count endpoint for badges and attention
  tiles, and refreshes visited sections in the background during a 60-second cache window.
- Task 14 adds typed per-block read state, keeps prior list data during refresh, ignores
  stale responses, adds local retry/error UI and shared reduced-motion skeleton styles.
  The statistics request runs no longer wait for fair options and ignore results after a
  tab change.
- Catalog administration target specs pass 95/95. After synchronizing with the latest
  `origin/main`, the full Catalog suite is 659/663. Its four failing tests (three
  AccountCard assertions and one CatalogAccountPage assertion) are in component/test files
  unchanged from `origin/main`. Catalog production build passes with the initial bundle at
  1.10 MB and the administration component stylesheet at 59.95 kB (the configured error
  limit is 60 kB).
- Task 15 validation: the .NET solution passes 1,164/1,164; Catalog production build
  and BackOffice production builds pass. BackOffice bootstrap checks pass 4/6; the two
  failing script assertions expect a static bootstrap array and direct MSAL initializer,
  which differ from the current conditional bootstrap in files unchanged from `origin/main`.
  Task 14 did not modify BackOffice files.
- Final validation could not rerun the local SQL seed, plans, endpoint timings, or admin
  network count because neither Podman nor Docker could reach a daemon. The earlier
  five-request local measurements and 14 November pre-fix App Insights values remain the
  only available numbers; no post-fix production measurement is claimed.
- `graphify update .` completed. The authenticated responsive visual check is still pending:
  Windows Computer Use stopped because it could not determine the browser's current URL
  with enough confidence to proceed. No Azure resource, database or deployment changed.
