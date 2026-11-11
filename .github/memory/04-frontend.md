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
- Website photo albums are catalog-driven by `src/app/feature/association/pictures/photo-album-catalog.ts`; event assets live under `public/images/Evenements/<album-slug>/`. The album list is prerendered and `/association/photos/:albumSlug` is server-rendered. Loto Halloween 2026 now publishes 95 resized JPEGs (maximum 2,200 px); photos 018, 026, and 081 are excluded at the owner's request.
- The top Halloween banner announces the Loto Halloween 2026 photo album from Nov 2 through Nov 8 in Paris time, links directly to `/association/photos/loto-halloween-2026`, and has its own persistent dismissal so it does not overwrite the next-event preference.

## Catalog

- Catalog is a separate SSR site. Public search/detail/work routes are crawlable; `/compte`, `/administration`, and `/desinscription` are client-only/private and `noindex,nofollow`.
- Use typed `CatalogApiService`/admin APIs. Search, pagination, filters, and reports belong on the API; never fetch every record for client-side filtering. Keep local catalogue results distinct from external reference results.
- `Ma sélection` is distinct from `Livres suivis` (work/edition watchlist). Availability, future announcements, and rare-book prices remain truthful and separate; no UI should invent reservations, book prices, role assignment, or physical-carton data.
- Catalog V2 uses a pale-paper/navy editorial shell; the shared design details are in `.github/memory/11-frontend-design-system.md`. Member/admin pages use MSAL lazily and read API roles from the API access token, not the ID token.
- Public cover URLs go through the shared allow-listed transformation helper with a direct-source fallback. Rare-book routes use UUIDs; the API retains legacy slug reads.

## Scan PWA

- Scan is an offline-first Angular PWA. Local decisions, catalog projection, session state, and cash sales are persisted in IndexedDB; final scan gestures and sales replay sequentially with client-generated idempotency IDs.
- The triage session keeps decided scan history (including already-synced gestures) in IndexedDB until session close. The latest-book strip and session list can request an idempotent removal; pending removals stay queued offline, and each list row groups ISBN copies while removing one scan at a time. Cached cover URLs come from bibliographic metadata.
- A scan result is local-first; metadata resolves independently and must not replace a keep/discard decision already shown. A valid result pauses the camera until a volunteer decides; an invalid/no-local-result decode resumes the existing camera stream. If the browser ends the video track unexpectedly, Scan reacquires the camera once while preserving the current decode pause; the cashier exposes a retry action if reacquisition fails.
- `Tri` can triage, `Caisse` can sell, both can consult. Cached local work remains usable during token/network loss, but synchronization needs a current API token. Do not put requester contact details in the catalog delta, IndexedDB diagnostics, or logs.
- The PWA retains explicit retry/error/recovery states; browser storage persistence is a best-effort capability, not a server-sync guarantee.
- Ordinary cashier sales retain a `Synced` IndexedDB record for the 30-second correction window. Cancellation restores the local projection atomically; a sale already sent to the API queues an idempotent void. `syncAll()` flushes the durable outbox before waiting for the catalog delta, and slow catalog/cash/token requests are bounded. Details and dated telemetry are in `.github/memory/20-scan-cash-offline-reliability.md`.
- `/caisse` puts the scanned account name at the left of the header (or “Anonyme”), omits a “Client :” prefix, and truncates long names with CSS ellipsis while retaining the full text for assistive technology. Buyer-card scanning and Home remain on the right. The cashier camera slot spans the screen width and reserves 160–190 px; the video covers that slot, with the default refocus prompt hidden. Tap-to-refocus and its status feedback remain available.

## MAUI cashier

- `src/MauiCashApp/` is Android-only .NET MAUI. Its public API/SQLite cache and UI expose only available `Bar`, `Bingo` and `Book` products; the Livres tab is a price calculator and records no sale or book exit (exception to `RG-50` for the tablette only). Website visibility is a separate filter.
- BackOffice distributes a versioned APK at `/apks/caisse-app-v{ApplicationDisplayVersion}.apk`; update both links and filename together to avoid a stale CDN asset.

## Recent validation

- 2026-11-11 — Scan now reacquires an unexpectedly ended camera track once and keeps decode paused until an in-flight scan completes; the cashier shows « Réessayer la caméra » if reacquisition fails. The full Scan suite passes 336/336, the production build succeeds with the initial bundle budget exceeded by 13.62 kB, and bootstrap contracts pass 6/6. Graphify update completed at 19,082 nodes. No deployment.
- 2026-11-11 — Cash reliability follow-up: Scan suite passes 329/329; backend Application tests pass 710/710; API tests pass 93/93; API build passes. Scan production build passes with its existing initial-bundle budget exceeded by 11.41 kB. The live camera preview test checks containment within the reserved slot because the preview correctly clips to the visible slot area. New store regression tests cover multiple pending sales and void corrections so one server response cannot overwrite other local quantity changes. Local Scan → Caisse E2E could not run because Docker Desktop crashed during startup and `com.docker.service` remained stopped; no reset, Azure write, or deployment was performed. See `.github/memory/20-scan-cash-offline-reliability.md`.
- 2026-11-11 — Scan’s shared manual ISBN keypad now places `0` before `Vider`, keeps `Valider` and `Retour` side by side, and hides the unreadable-barcode notice during manual entry. The code-recovery hint remains. The local Scan build passes; the unchanged `scanner.component.scss` exceeds its style budget by 498 bytes. Automated tests were not run, and authenticated mobile visual verification remains pending. Graphify extracted 1,913 files but could not render its 9,404-node report. No deployment.
- The 2026-11-11 Scan cashier layout change passed the full Scan suite (317/317), production build, and bootstrap contract tests (6/6); the user later confirmed PR #381 was merged and deployed. The local authenticated device preview still requires MSAL.
- The cashier follow-up passes the full Scan suite (318/318), production build, and bootstrap contract tests (6/6). Component assertions cover the header name/ellipsis, camera slot and preview geometry, and the hidden default focus prompt. Graphify extracted 1,913 files but could not regenerate its HTML report because the graph has 9,402 nodes. [PR #382](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/382) is open and not merged; no deployment.
- The Loto Halloween photo announcement passes its focused Website tests (20/20) and the SSR production build. The full Website suite is 202/203 because the pre-existing event-detail test still expects “Demain” for a Nov 1 event while the test clock uses Nov 2; no deployment. Desktop and mobile SSR screenshots were reviewed. `graphify update . --no-viz` completed AST extraction but could not rebuild the report because the existing 9,404-node graph exceeded its visualization limit.
- The 2026-10-31 event SSR correction passes Website 197/197, SEO HTTP checks 14/14, and the SSR production build (existing Angular budget/CommonJS warnings). Its local concurrent request evidence and limits are in `.github/memory/19-website-event-ssr.md`.
- The Halloween loto board and E6 banner validation is in `.github/memory/18-halloween-loto-board.md`; deployment still requires the `AddEventLiveTheme` migration first.
