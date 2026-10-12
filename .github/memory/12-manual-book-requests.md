# Manual book requests (F-12)

## Delivered behavior

Administration can register a requester who has no account, record oral consent, and add
work- or edition-scoped book requests. A matching scan holds one copy for that request;
the held copy is not added to stock and does not notify member watchers. Administration
can mark it given or refused. Refusal returns the copy to stock and queues watcher alerts
as immediately due; the API attempts delivery and the hourly Sweep remains the fallback.

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

The generated migration is `20261012084826_AddManualBookRequests`. It has not been applied
to a remote database. The next rollout needs `Books runtime - deploy` with
`run_migrations: true`, followed by Catalog and Scan deployment. No Azure service or
remote database was changed during implementation.

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
