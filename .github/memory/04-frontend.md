# 04 - Frontend And Client Surfaces

## Applications and conventions

- Angular 21 apps are `src/BackOffice/` (admin), `src/Website/` (association site), `src/Catalog/` (public book catalogue), and `src/Scan/` (offline triage/cash PWA). `src/SharedUi/` is consumed through the `@vpd/ui` TypeScript path alias.
- Preserve typed API models and the separation between app-specific `core/`, `feature/`, and `shared/` code. Apps use zoneless change detection; async UI callbacks must notify the view through the app's established pattern.
- Keep BackOffice for association administration, Website for association/public events, Catalog for books, and Scan for volunteer triage, consultation, and cash. The API remains the authorization and business-rule boundary.
- For visual rules and component conventions see `.github/memory/11-frontend-design-system.md`.

## BackOffice

- Entra/MSAL is production authentication; local development can use the `vpd-backoffice-local` Keycloak Authorization Code + PKCE client. Protected routes use `MsalGuard`; the API still enforces roles.
- The loto operator screen supports keyboard draws and a projector palette. `G` toggles the pre-game QR/empty grid without drawing; `P` cycles palettes. Current desktop projection removes repeated previous-draw cells, while the Website phone tableau keeps its own compact summary.
- BackOffice shares typed event, product, and book-admin contracts. Do not infer API authorization from a hidden or visible navigation link.

## Website routes and rendering

- Website uses Angular SSR plus client hydration. Static association, Maxence, and legal content is prerendered; data-driven event pages `/evenement` and `/evenement/:id` are server-rendered; `/evenement/:id/tableau` stays browser-rendered for `EventSource`.
- Public event agenda and detail share event identity/date helpers. Calendar counts use Paris dates; event theme and Halloween motion respect the `vpd.tableau.calm` preference and `prefers-reduced-motion`.
- The live tableau uses the shared `buildLotoTableauView`; SSE reconnects with bounded backoff and ignores malformed events. Keep `EventSource` out of SSR execution.
- Website event SSR now registers initial list/detail API promises as bounded Angular server `PendingTasks` and caches calendar/date-animation projections. The local 20-list + 20-detail SSR run is recorded separately from the prior deployed 300-client SSE run in `.github/memory/19-website-event-ssr.md`.
- Website canonical links use `https://volepapillondamour.fr`, remove query/hash/trailing slash, and map `/` to `/accueil`; public SEO checks cover redirect and sitemap behavior.

## Catalog

- Catalog is a separate SSR site. Public search/detail/work routes are crawlable; `/compte`, `/administration`, and `/desinscription` are client-only/private and `noindex,nofollow`.
- Use typed `CatalogApiService`/admin APIs. Search, pagination, filters, and reports belong on the API; never fetch every record for client-side filtering. Keep local catalogue results distinct from external reference results.
- `Ma sélection` is distinct from `Livres suivis` (work/edition watchlist). Availability, future announcements, and rare-book prices remain truthful and separate; no UI should invent reservations, book prices, role assignment, or physical-carton data.
- Catalog V2 uses a pale-paper/navy editorial shell; the shared design details are in `.github/memory/11-frontend-design-system.md`. Member/admin pages use MSAL lazily and read API roles from the API access token, not the ID token.
- Public cover URLs go through the shared allow-listed transformation helper with a direct-source fallback. Rare-book routes use UUIDs; the API retains legacy slug reads.

## Scan PWA

- Scan is an offline-first Angular PWA. Local decisions, catalog projection, session state, and cash sales are persisted in IndexedDB; final scan gestures and sales replay sequentially with client-generated idempotency IDs.
- A scan result is local-first; metadata resolves independently and must not replace a keep/discard decision already shown. A valid result pauses the camera until a volunteer decides; an invalid/no-local-result decode resumes the existing camera stream.
- `Tri` can triage, `Caisse` can sell, both can consult. Cached local work remains usable during token/network loss, but synchronization needs a current API token. Do not put requester contact details in the catalog delta, IndexedDB diagnostics, or logs.
- The PWA retains explicit retry/error/recovery states; browser storage persistence is a best-effort capability, not a server-sync guarantee.

## MAUI cashier

- `src/MauiCashApp/` is Android-only .NET MAUI. Its public API/SQLite cache and UI expose only available `Bar` and `Bingo` products; books are hidden. Website visibility is a separate filter.
- BackOffice distributes a versioned APK at `/apks/caisse-app-v{ApplicationDisplayVersion}.apk`; update both links and filename together to avoid a stale CDN asset.

## Recent validation

- The 2026-10-31 event SSR correction passes Website 197/197, SEO HTTP checks 14/14, and the SSR production build (existing Angular budget/CommonJS warnings). Its local concurrent request evidence and limits are in `.github/memory/19-website-event-ssr.md`.
- The Halloween loto board and E6 banner validation is in `.github/memory/18-halloween-loto-board.md`; deployment still requires the `AddEventLiveTheme` migration first.
