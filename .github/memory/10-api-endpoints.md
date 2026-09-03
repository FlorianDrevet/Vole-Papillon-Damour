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
  Library second; no book is persisted at this palier.

No dedicated OCR or automatic loto-card analysis endpoint remains in the active API runtime.

## Current Auth Asymmetries To Recheck Before Editing

- `DELETE /asso-events/{id}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `DELETE /product/{productId}` currently has no explicit `RequireAuthorization("IsAdmin")`.
- `POST /auth/register` remains public even though a commented admin requirement is present in code.
