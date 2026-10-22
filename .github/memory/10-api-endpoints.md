# 10 - API Endpoints

## Transport Style

- The API uses static extension classes with `endpoints.MapGet/MapPost/MapPut/MapDelete`, not MVC controllers deriving from `ControllerBase`.
- Several write endpoints accept `multipart/form-data` through `[FromForm]` for image or file uploads.

## Auth Endpoints

- `POST /auth/register` - public registration route
- `POST /auth/login` - public login route with rate limiting

## Actuality Endpoints

- `GET /actuality/all`, `GET /actuality/latest`, `GET /actuality/{id}` - public reads;
  all three expose only `Published` actualities, and a draft by id is treated as not found
- `GET /actuality/all?includeDrafts=true` - admin-authorized complete actuality list
- `GET /actuality/drafts`, `POST /actuality/{id}/publish` - `IsAdmin` review/publish flow
- `POST /actuality`, `PUT /actuality/{id}`, `DELETE /actuality/{id}` - admin-protected writes

## Event Endpoints

- `GET /asso-events`, `GET /asso-events/{id}` - public event reads; the Catalog home uses the
  collection response for the upcoming calendar and filters the typed `Books` event kind.
- `GET /asso-events/next-bingo`, `GET /asso-events/next-books`, `GET /asso-events/next-other-event` - public next-event projections
- `GET /asso-events/today` - public compact projection for the current Paris calendar day; returns `204` when no non-cancelled event is happening, otherwise prefers a Bingo event and then the earliest start. Output caching is 10 seconds. The typed response includes event identity/name/type, dates, opening/closing times, city, and loto-only `liveStatus` / `lastNumero`.
- `POST /asso-events`, `PUT /asso-events/{id}` - admin-protected event writes
- `POST /asso-events/{id}/numeros`, `DELETE /asso-events/{id}/numeros`, `POST /asso-events/{id}/win-partie`, `PUT /asso-events/{id}/bingo-win` - admin-protected live bingo mutations
- `GET /asso-events/{id}/tableau/sse` - public SSE stream of full `EventResponse` snapshots. The SSE `id` is `LiveRevision`; the initial snapshot includes a 3-second `retry`, and idle streams emit `ping` events every 15 seconds. Unknown event IDs return `404`.

Each successful live bingo mutation increments the event's `LiveRevision`, saves it as an EF concurrency token, then publishes the updated snapshot to subscribers for that event. A concurrent write conflict maps to `409 Conflict`. The endpoint subscribes before loading its initial snapshot to avoid missing a change in that interval, and disposes the subscription when the request ends. `LiveTableauSyncService` checks revisions for locally watched events every second and loads/publishes a fresh snapshot when another API replica advanced one; clients connected to any replica therefore receive the same live state.

## Nested Event Subresources

- `POST/PUT/DELETE /asso-events/{id}/parties...` - admin-protected party management
- `POST/DELETE /asso-events/{assoId}/parties/{partieId}/partie-lines...` - admin-protected line management
- `POST/PUT/DELETE /asso-events/{assoId}/parties/{partieId}/partie-lines/{partieLineId}/lots...` - admin-protected lot management

## Product And Order Endpoints

- `GET /product` - full product listing; consumed by the MAUI Refit client and BackOffice
- `GET /product/public` - public product listing filtered by `Product.Available` and `Product.VisibleOnWebsite`; consumed by Website prices
- `POST /product`, `PUT /product/{id}`, `POST /product/promotion`, `DELETE /product/promotion` - admin-protected product writes
- `POST /orders`, `GET /orders` - admin-protected order routes

## Bibliographic Probe Endpoints

- `GET /books/{isbn13}/metadata` - anonymous consultation-only ISBN lookup; the route
  accepts a valid ISBN-10 or ISBN-13, returns the canonical ISBN-13 and typed title,
  authors, publisher, publication year, cover URL, source, and optional WorkId. The
  Application handler delegates to Infrastructure's BnF SRU client first and Open
  Library second; no book is persisted at this palier. A valid ISBN with no notice is
  `404`; a transient failure of both providers is mapped by the API exception middleware
  to `503 Service Unavailable`, while the resolver keeps the failure visible to the Worker
  for retry rather than recording a negative cache entry.

- `GET /books/admin/dead-stock` - administration-only dead-stock candidates, with optional
  `minAgeMonths` and `minQuantity` query parameters (defaults: 6 and 3). The response contains
  the canonical ISBN, bibliographic fields, current available quantity, and first positive
  availability instant; results are ordered by quantity descending.

## Public catalog endpoints

- `GET /catalog/search` - anonymous typed search over visible canonical books, with title,
  author, publisher and ISBN matching, accent normalization, genre/availability/rare
  filters, relevance or recent sorting, and paging. `includeExhausted=true` is an explicit
  opt-in; without it, books with neither available quantity nor an active announcement are
  excluded from every availability scope. When enabled, exhausted books are added to the
  selected scope rather than replacing it.
- `GET /catalog/books/{isbn13}` - anonymous canonical book projection with available and
  announced quantities kept separate, next-fair date, freshness fields, work identifier and
  an optional `summary`. The detail query reads a cleaned BnF edition summary first, then an
  Open Library description, from the stored `BookSimilarityProfile.NoticeJson`; it does not
  fetch a notice on demand.
- `GET /catalog/fairs/next` - anonymous next non-cancelled Books event with schedule and
  public address.
- `GET /catalog/works/{workId}` - anonymous work projection containing its visible editions.
- `GET /catalog/sitemap.xml` - anonymous XML sitemap containing visible canonical book URLs.

The routes are consumed by the separate SSR Angular application in `src/Catalog/`. Its
`/sitemap.xml` server route proxies the API sitemap so the public host has its own crawler
entry point. Account/watchlist/alert routes are part of the P3 member slice and must remain
client-only/private.

## Scanette endpoints

- `GET /scan/catalog/delta` - `ScanVolunteer` read for the compact catalog projection;
  both `Tri` and `Caisse` can use it. The response includes association settings and the
  next non-cancelled Books fair schedule so the Scan PWA can remain useful offline.
- `POST /scan/sessions` - `Tri` opens or replays a scan session by `ClientSessionId`.
- `POST /scan/sessions/{scanSessionId}/scans` and
  `POST /scan/sessions/{scanSessionId}/close` - `Tri` records idempotent decisions and
  closes a session with a typed reason.
- `POST /scan/sales` - `Caisse` records an idempotent sale with `ClientGestureId` and
  returns the reconciled local-stock projection; cash sales do not open a triage session.
- `GET /scan/me/statistics` - `ScanVolunteer` read for the authenticated volunteer's private
  triage/cash contribution. The API derives the volunteer from `oid`, returns typed `scan`
  and `cash` blocks, and marks timestamp-derived duration, cadence, overlap, reader linkage
  and revenue share as estimates.

## Books P2/P3 member and administration endpoints

- `GET /catalog/me/selection` reads the authenticated member's typed selection with
  current catalogue availability. `POST /catalog/me/selection` adds an edition or rare
  book, `PATCH /catalog/me/selection/{id}` returns `410 Gone` for legacy clients,
  `DELETE /catalog/me/selection/{id}` removes it, and
  `POST /catalog/me/selection/merge` adds local entries idempotently. All routes require
  member authorization and derive identity from token claims.
- `POST /catalog/me/selection/{id}/not-found-report` reports an available selection item;
  its optional request body is `{ comment }` with no search-location choice, and a replay
  returns the existing open report. `DELETE /catalog/me/not-found-reports/{reportId}`
  cancels the authenticated member's own open report.
- `GET /catalog/reference/search` - anonymous external bibliographic search with `q`, `page`,
  and `pageSize`; the Open Library adapter normalizes/deduplicates ISBN-10/ISBN-13 results.
  Publisher arrays aggregated across editions are omitted when their joined text exceeds the
  200-character watchlist metadata limit; other result fields are unchanged.
- `GET/POST /catalog/me/watchlist` and `DELETE /catalog/me/watchlist/{itemId}` - Entra member
  watchlist reads and edition/work mutations; the API derives the local member from the `oid`
  claim and email, never from a client-provided user id.
- `PATCH /catalog/me/alerts` - Entra member preference `{ enabled: boolean }`; returns
  `alertStatus`, `bounceCount`, and `changed`. A member cannot reactivate a `Blocked` list.
- `GET /books/admin/overview` - Administration-policy KPIs for stock, scan/sale periods,
  last fair, dead stock, rare/metadata/undated queues, inventory drift, and pending alerts.
- `POST /books/admin/books/import` - Administration-only batch creation of 1–100 canonical
  ISBN-13 values. The handler validates before writing and uses one transaction per batch;
  missing books receive pending metadata, one available copy, and a `DirectEntry` movement
  attributed to the authenticated administrator. Existing or duplicate values are reported
  as already present and unchanged so a full-list retry does not add stock twice.
- `GET /books/admin/books/metadata-queue?page=&pageSize=` returns the paged ISBN analysis
  queue with status, bounded failure reason, attempt count, and last-attempt time.
  `POST /books/admin/books/{isbn13}/metadata/retry` resets the candidate to `Pending` and
  enqueues enrichment immediately. Both routes require the `Administration` policy.
- `GET/POST/PATCH/DELETE /books/admin/books...` - Administration-policy book list/detail,
  manual add, metadata/quantity corrections, withdrawals, rare/visibility flags, merges and
  guarded deletion. `DELETE /books/admin/books/{isbn13}/permanent` is the catalogue's
  separate hard-delete route: it removes an unused erroneous fiche and associated stock,
  selection, watchlist, report, alert-history and recommendation rows, but refuses sale or
  checkout, announcement, scan/fair, redirect, and fulfilled manual-request history. The
  plain `DELETE /books/admin/books/{isbn13}` remains the ISBN-queue discard path, which
  archives ledger-backed candidates and records a withdrawal. Announcement quantity
  correction is under
  `PATCH /books/admin/announcements/{announcementId}/quantity`.
- `GET /books/admin/fairs`, `GET /books/admin/fairs/{fairId}/stats`, and
  `PUT /books/admin/fairs/{fairId}/revenue` - fair list, sales analysis and optional nullable
  revenue entry. The fair list and previous-fair comparison keep the Books type predicate
  compatible with the configured EF value converter and perform provider-sensitive date
  ordering/filtering after materialization, so the admin endpoints do not fail on SQLite
  translation.
- `GET /books/admin/volunteers/stats?from=&to=&fairId=` - Administration-only, one-shot
  volunteer team aggregation for Catalog. It returns typed team totals, per-volunteer
  contribution, inferred activity roles, monthly activity, renewal buckets and dominant
  genres; sale corrections are netted by their reversal link and no client volunteer id is
  accepted. Till duration and waiting-stock values are explicitly reconstructed indicators.
- `GET /books/admin/sessions` and `/books/admin/sessions/{scanSessionId}` - paged session
  monitoring; `status=InProgress&olderThan24Hours=true` is the stale-session view. Movement
  removal, session reassign/cancel, alert cancel/force, and
  `POST /books/admin/sessions/{scanSessionId}/force-close` are Administration-protected
  actions under the same resource. Force-close reuses the close transaction with the
  `AdminForced` reason and queues the normal delayed alerts.
- `GET /books/admin/alerts`, plus per-message `POST .../{messageId}/cancel|force` - outbox
  diagnosis and pending-message control.
- `GET /books/admin/members` and detail, block/unblock, and deletion routes - member support
  and compliance operations.
- `GET/PUT /books/admin/settings` - typed association thresholds and alert/session timing.
- Not-found administration uses `GET /books/admin/not-found-reports` for the open queue,
  `GET .../closed` for history, and `GET .../summary` for badge, dashboard, and book-detail
  projections. Edition and rare-book targets have separate `POST` actions for `found`,
  `withdrawal`, and `dismissal`. Edition routes use `Administration`; rare-book routes
  accept `LivresRares`, `Administration`, or legacy `Admin`. The summary also identifies
  withdrawal movement IDs so the Catalog can mark « suite à signalement » in the ledger.
  `/books/admin/settings` includes `notFoundReportDailyLimit`.

## Account administration endpoints

- `GET /accounts/admin` - Administration-policy paged Entra account list with optional
  `search`, `page`, and `pageSize` filters. The response includes the current `Tri`,
  `Caisse`, `LivresRares`, and `Administration` app-role assignments, plus
  `canResetPassword`, true only for local email/password identities with at least one
  volunteer role (`Tri`, `Caisse`, or `LivresRares`). Graph transport or malformed-payload
  failures are returned as a typed `Account.DirectoryUnavailable` `503` rather than an
  unhandled `500`; omitted Graph collections are treated as empty.
- `POST /accounts/admin` - Administration-policy account creation with `email`, `firstName`,
  `lastName`, `temporaryPassword`, and at least one selected role. The app-only Graph
  directory writes `givenName`/`surname`, derives the directory `displayName`, creates the
  External ID local identity and assigns the requested API roles.
- `PUT /accounts/admin/{externalId}/roles` - Administration-policy replacement of the
  selected API roles. An administrator cannot remove their own `Administration` role.
- `POST /accounts/admin/{externalId}/password-reset` - Administration-policy password reset
  with no request body. The API rechecks the target's local identity and volunteer role,
  writes a cryptographically generated temporary password through Graph with forced change
  at next sign-in, and returns `{ temporaryPassword }` only to the administrator; ineligible
  targets return a typed `Account.PasswordResetNotAllowed` `409`.

The Graph runtime configuration uses `EntraGraph__TenantDomain` and
`EntraGraph__ApiClientId` alongside the existing app-only client credentials. The
provisioning script must grant `User.ReadWrite.All`, `User.EnableDisableAccount.All`,
`User-PasswordProfile.ReadWrite.All`, `Application.Read.All`, and `AppRoleAssignment.ReadWrite.All`
to that app-only registration. Sensitive `accountEnabled` updates and app-only password resets
also require an Entra directory role authorized for each action. Password resets require the
service principal's manual `User Administrator` role assignment; the provisioning script does
not grant it.

All `/books/admin/*` routes require the `Administration` policy (`Administration` or `Admin`
app role). All admin mutation responses expose an explicit `changed` flag where an operation
is idempotent. Details and exact request/response fields are in
`docs/bourse-aux-livres/06-reprise-front-catalogue-p2-p3.md`.

Rare-book administration routes under `/rare-books/admin*` require the `RareBooks` policy
(`LivresRares`, `Administration`, or `Admin`); public `/catalog/rare-books*` reads remain
anonymous and `/rare-books/cash/search` remains `ScanVolunteer`-protected. The API does
not add a price, total, or revenue field to ordinary book or sale contracts.

The Catalog client mirrors the rare administration routes with typed models and an
`AdminRareBooksFacade`. Its ISBN relation lookup searches the protected rare-book list;
an exact match opens the existing fiche, while a miss creates a draft prefilled only with
ordinary bibliographic metadata. Photo upload uses multipart form data and keeps the
backend's ordering/caption/delete operations separate from fiche publication.

Rare-book endpoints expose bibliographic details, price, condition, publication state, and
photos. Public listing supports search, sold-item inclusion, sorting, and pagination. The
ordinary ISBN-book metadata contract remains separate from the rare-book contract.

No dedicated OCR or automatic loto-card analysis endpoint remains in the active API runtime.

## Current Auth Asymmetries To Recheck Before Editing

- `DELETE /asso-events/{id}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `DELETE /product/{productId}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `POST /auth/register` remains public even though a commented admin requirement is present in code.
