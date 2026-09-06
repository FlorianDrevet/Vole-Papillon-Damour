# 10 - API Endpoints

## Transport Style

- The API uses static extension classes with `endpoints.MapGet/MapPost/MapPut/MapDelete`, not MVC controllers deriving from `ControllerBase`.
- Several write endpoints accept `multipart/form-data` through `[FromForm]` for image or file uploads.

## Auth Endpoints

- `POST /auth/register` - public registration route
- `POST /auth/login` - public login route with rate limiting

## Actuality Endpoints

- `GET /actuality/all`, `GET /actuality/latest`, `GET /actuality/{id}` - public reads
- `POST /actuality`, `PUT /actuality/{id}`, `DELETE /actuality/{id}` - admin-protected writes

## Event Endpoints

- `GET /asso-events`, `GET /asso-events/{id}` - public event reads
- `GET /asso-events/next-bingo`, `GET /asso-events/next-books`, `GET /asso-events/next-other-event` - public next-event projections
- `POST /asso-events`, `PUT /asso-events/{id}` - admin-protected event writes
- `POST /asso-events/{id}/numeros`, `DELETE /asso-events/{id}/numeros`, `POST /asso-events/{id}/win-partie`, `PUT /asso-events/{id}/bingo-win` - admin-protected live bingo mutations
- `GET /asso-events/{id}/tableau/sse` - public SSE stream used by the website live tableau

Live bingo mutations broadcast the updated `EventResponse` only to SSE clients registered for the same `{id}`. The SSE endpoint sends an initial snapshot after registration and removes the client when the request is aborted.

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
  filters, relevance or recent sorting, and paging. Exhausted books remain in `all`.
- `GET /catalog/books/{isbn13}` - anonymous canonical book projection with available and
  announced quantities kept separate, next-fair date, freshness fields and work identifier.
- `GET /catalog/fairs/next` - anonymous next non-cancelled Books event with schedule and
  public address.
- `GET /catalog/works/{workId}` - anonymous work projection containing its visible editions.
- `GET /catalog/sitemap.xml` - anonymous XML sitemap containing visible canonical book URLs.

The routes are consumed by the separate SSR Angular application in `src/Catalog/`. Its
`/sitemap.xml` server route proxies the API sitemap so the public host has its own crawler
entry point. Account/watchlist/alert routes are part of the P3 member slice and must remain
client-only/private.

## Books P2/P3 member and administration endpoints

- `GET /catalog/reference/search` - anonymous external bibliographic search with `q`, `page`,
  and `pageSize`; the Open Library adapter normalizes/deduplicates ISBN-10/ISBN-13 results.
- `GET/POST /catalog/me/watchlist` and `DELETE /catalog/me/watchlist/{itemId}` - Entra member
  watchlist reads and edition/work mutations; the API derives the local member from the `oid`
  claim and email, never from a client-provided user id.
- `PATCH /catalog/me/alerts` - Entra member preference `{ enabled: boolean }`; returns
  `alertStatus`, `bounceCount`, and `changed`. A member cannot reactivate a `Blocked` list.
- `GET /books/admin/overview` - Administration-policy KPIs for stock, scan/sale periods,
  last fair, dead stock, rare/metadata/undated queues, inventory drift, and pending alerts.
- `GET/POST/PATCH/DELETE /books/admin/books...` - Administration-policy book list/detail,
  manual add, metadata/quantity corrections, withdrawals, rare/visibility flags, merges and
  guarded deletion; announcement quantity correction is under
  `PATCH /books/admin/announcements/{announcementId}/quantity`.
- `GET /books/admin/fairs`, `GET /books/admin/fairs/{fairId}/stats`, and
  `PUT /books/admin/fairs/{fairId}/revenue` - fair list, sales analysis and optional nullable
  revenue entry.
- `GET /books/admin/sessions` and `/books/admin/sessions/{scanSessionId}` - paged session
  monitoring; movement removal, session reassign/cancel, and alert cancel/force are POST
  actions under the same resource.
- `GET /books/admin/alerts`, plus per-message `POST .../{messageId}/cancel|force` - outbox
  diagnosis and pending-message control.
- `GET /books/admin/members` and detail, block/unblock, and deletion routes - member support
  and compliance operations.
- `GET/PUT /books/admin/settings` - typed association thresholds and alert/session timing.

All `/books/admin/*` routes require the `Administration` policy (`Administration` or `Admin`
app role). All admin mutation responses expose an explicit `changed` flag where an operation
is idempotent. Details and exact request/response fields are in
`docs/bourse-aux-livres/06-reprise-front-catalogue-p2-p3.md`.

No dedicated OCR or automatic loto-card analysis endpoint remains in the active API runtime.

## Current Auth Asymmetries To Recheck Before Editing

- `DELETE /asso-events/{id}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `DELETE /product/{productId}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `POST /auth/register` remains public even though a commented admin requirement is present in code.
