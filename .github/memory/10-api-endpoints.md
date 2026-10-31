# 10 - API Endpoints

## Transport and authorization

- HTTP surfaces are static extension classes mapping `endpoints.MapGet/MapPost/MapPut/MapDelete`, not MVC `ControllerBase` controllers. Multipart uploads use `[FromForm]`.
- JSON DTO bodies on `DELETE` routes must be annotated `[FromBody]`; otherwise endpoint discovery may stop API startup with an inferred-body exception. `DELETE /catalog/me/genre-favorites` has a route-registration regression test.
- Check authorization per endpoint before editing. Public, member, `ScanVolunteer`, `Administration`, and `RareBooks` policies are not interchangeable; hidden frontend controls do not secure an API route.

## Public actualities, events, products

- Actualities: public `GET /actuality/all|latest|{id}` exposes published rows only. Admin draft review uses `GET /actuality/all?includeDrafts=true`, `/actuality/drafts`, `POST /actuality/{id}/publish`; writes require admin policy.
- Events: public `GET /asso-events`, `/{id}`, `/next-bingo`, `/next-books`, `/next-other-event`, and `/today`. `/today` returns 204 when no non-cancelled event is happening, prioritizes Bingo, uses the Paris day, and is cached 10 seconds.
- Admin event writes include `POST/PUT /asso-events`, party/line/lot operations, draw/undo, `win-partie`, and `bingo-win`. Check each registration's policy; `DELETE /asso-events/{id}` is a known authorization asymmetry to recheck.
- Live board: `GET /asso-events/{id}/tableau/sse` streams full snapshots with `LiveRevision` as SSE ID, 3-second retry hint, and 15-second idle pings. It subscribes before its initial DB read; a missing event is 404. Mutations publish after save; revision conflicts map to 409; `LiveTableauSyncService` propagates changes across API replicas.
- `GET /product` is the full cashier/admin projection; Website prices use public `GET /product/public`, filtered by availability and `VisibleOnWebsite`. Product writes are admin routes; `DELETE /product/{id}` is another known authorization asymmetry. Orders use `POST/GET /orders` and are admin-protected.

## Public catalogue and bibliographic reads

- `GET /catalog/search` is anonymous, typed, filtered, and paged. It supports text/ISBN, genre, availability, rare, and ordering filters. Exhausted books require explicit `includeExhausted=true`; results stay in the selected scope.
- Public reads include `GET /catalog/books/{isbn13}`, `/catalog/works/{workId}`, `/catalog/fairs/next`, and `/catalog/sitemap.xml`. Available and announced quantities remain separate. Summary text comes from stored notice data, not a per-view provider request.
- `GET /books/{isbn13}/metadata` is an anonymous, non-persisting ISBN-10/13 probe. It returns a canonical ISBN-13; no notice is 404 and temporary failure of providers is 503. Current resolver/provider follow-ups are in `.github/memory/14-scan-metadata-latency.md` and `.github/memory/15-isbndb-migration.md`.
- Public rare-book reads live under `/catalog/rare-books*`; they are separate from ordinary ISBN book contracts and use UUID route IDs. API legacy slug reads remain for old links.

## Member and Scan routes

- Member routes under `/catalog/me/*` derive identity from token claims, never a client-supplied member ID. They include selection, watchlist, alert preference, member card, purchase passage/history, and not-found reports. Legacy `PATCH /catalog/me/selection/{id}` returns 410; report creation accepts only an optional comment, no shelf location.
- External bibliographic reference search is anonymous at `GET /catalog/reference/search`; edition-only and work-scope follow requests are typed and authenticated.
- Scan uses `GET /scan/catalog/delta`, session open/scan/close routes, `POST /scan/sales`, `GET /scan/manual-requests/candidate`, and `GET /scan/me/statistics`. `Tri` records scans, `Caisse` records sales, and both may consult. Client gesture/session IDs make replay idempotent.
- `GET /scan/me/statistics` derives the volunteer from `oid`; durations, reader links, and revenue-share values are estimates and must be presented as such.

## Books and account administration

- `/books/admin/*` requires `Administration` (or compatibility `Admin`). The surface includes paged catalogue/metadata queue, overview, dead stock, fair/revenue statistics, sessions and movement correction, alerts, members, settings, and not-found reports.
- `POST /books/admin/books/import` accepts 1–100 canonical ISBN-13s in one transaction per batch; new books receive one available copy and an audited `DirectEntry`, while existing ISBNs remain unchanged. `DELETE /books/admin/books/{isbn13}` is ISBN-queue discard/archive; `/permanent` is a distinct guarded deletion for unused erroneous fiches.
- `GET/POST/PATCH/DELETE /rare-books/admin*` uses `RareBooks` policy (`LivresRares`, `Administration`, or `Admin`). Rare-book cash search is `ScanVolunteer`; ordinary sales do not carry prices.
- `/accounts/admin*` uses `Administration` and Graph for paged account reads, create, role/status changes, password reset, and deletion. Password reset eligibility and directory permissions are rechecked server-side; generated credentials are returned only to the requesting administrator.
- ACS Event Grid delivery reports use `POST /integrations/acs/email-delivery-reports` with typed validation and shared-secret auth. Unknown recipients are acknowledged safely; provider event IDs prevent duplicate bounce increments.

## Known auth asymmetries

- Verify the explicit endpoint policy before assuming every mutation is protected. Current known items: `DELETE /asso-events/{id}`, `DELETE /product/{id}`, and public `POST /auth/register`.
- Exact request and response fields belong in the endpoint registration/contracts and feature docs; this memory is a route map, not a substitute for inspecting the route before changing it.
