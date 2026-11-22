# Manual book requests (F-12)

## Delivered behavior

Administration can register a requester who has no account, record oral consent, and add
work- or edition-scoped book requests. A matching scan holds one copy for that request;
the held copy is not added to stock and does not notify member watchers. Administration
can mark it given or refused. Refusal returns the copy to stock and queues watcher alerts
as immediately due; the API attempts delivery and the hourly Sweep remains the fallback.

The follow-up administration UX adds a paginated **Personnes** directory searchable by
name, first name, or phone so volunteers can reopen a requester and add books without
recreating the person. Searching is optional: volunteers can create a new requester directly,
and search results remain available for reusing existing matches. In the add-book dialog, **all editions of a work** is the
default scope, selecting an edition switches to edition-only, and only the edition list
scrolls between the fixed search/scope header and stock/actions footer.

## Architecture and privacy

- Domain slice: `ManualRequester` and `ManualBookRequest` under
  `Domain/ManualRequestAggregate/`; CQRS handlers, queries, contracts, endpoint mapping,
  EF configuration, and tests follow the existing layered Books pattern.
- `BookMovementType.ManualRequestHold` is appended as enum value 7.
- Catalog administration owns requester, queue, book-add, decision, and dashboard flows.
  Scan resolves a candidate only for the scanned ISBN through
  `GET /scan/manual-requests/candidate`.
- Candidate contact fields are not placed in the catalog delta, IndexedDB, scan
  diagnostics, or logs. The candidate endpoint is the online, per-ISBN contact path.
- The Worker erases requester contact details 90 days after the last request closes;
  closed history remains anonymous.

## Persistence and rollout

The migration `20261012084826_AddManualBookRequests` was applied by
[Books runtime - deploy #38689858408](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/38689858408)
on 2026-10-15; API/Worker rolled out successfully. Catalog #38689891967 and Scan #38689915502
also deployed successfully on `e26dacc8`. The UX follow-up in
[PR #259](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/259) merged on
2026-10-15. It adds an API query but no schema change; see `NEXT.md` for its deployment
validation state.

## Validation record (2026-10-12)

- Backend build: 14 projects, 0 errors; five backend test projects: 976 passing tests.
- Scan: 300 tests pass and production build succeeds.
- Catalog feature/admin/legal focused suites: 28 + 61 + 6 tests pass. The full headless
  suite completes at 485/487; the two failures are existing `AccountCardComponent`
  recovery-code and accessible-QR assertions.
- Catalog and Scan builds succeed with existing bundle/CSS budget warnings. Responsive
  visual checks covered E1–E5 and E7 at 390/1440 px, and E6 at 390 px; no page-level
  horizontal overflow was observed.
- `check_zones.py` reports all 76 zones in 17 pages; `graphify update .` completes.

## Administration UX follow-up (2026-10-15)

- Branch `feat/manual-request-admin-ux`, PR #259 merged to `main` on 2026-10-15 from a
  fresh `origin/main` worktree; it added no schema migration.
- API build: 5 projects, 0 errors (85 warnings, including existing NuGet advisory warnings).
- Catalog production build: 0 errors; existing initial bundle/CSS budget and CommonJS warnings.
- `git diff --check` and `graphify update .` pass; graph has 11,636 nodes and 16,968 edges.
- No automated tests were run for this follow-up. Local browser reached the Entra login wall;
  API at `localhost:5257` was unavailable, so authenticated visual checks at desktop/mobile
  sizes remain open.

## Requester creation without a prior search (2026-11-22)

- Catalog administration now allows opening the new-requester form before searching by
  name or phone; search remains available to find and reuse existing records.
- The change is on `fix/manual-request-create-person`. Catalog production build and Graphify
  update pass with existing bundle/style-budget and CommonJS warnings. Automated tests were
  not run; the local administration route reached the Microsoft Entra login wall, so an
  authenticated responsive review remains open. No schema change or deployment was made.
