# Catalog request and statistics latency

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
