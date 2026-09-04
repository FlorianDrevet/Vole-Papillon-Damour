# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 21 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, with MSAL Angular (`@azure/msal-angular` 5.3.1,
  `@azure/msal-browser` 5.20.0) and `@dhutaryan/ngx-mat-timepicker`
- `src/Website/` - public UI for the association website, now built with Angular SSR and hydration support
- `src/Scan/` - Angular 21 Scanette PWA for ISBN capture, offline triage, local decisions,
  catalog consultation, IndexedDB persistence, and volunteer authentication/synchronization

## Planned Books Scan client decisions

As of 2026-09-04, the P1-5 Scan foundation is implemented in `src/Scan` and deployed to
the DEV ACA: local verdicts, IndexedDB session/catalog/outbox persistence, MSAL `Tri`
authentication, and sequential gesture replay are present. P1-2 selected the existing
Jasmine/Karma/ChromeHeadless toolchain: browser integration tests use real IndexedDB and
a fake transport simulates delays, failures, mid-flight disconnects, and duplicate
responses. The local outbox states are `Pending`, `Kept`, `Rejected`, and
`CancelledLocal`; only final decisions reach the API, while a transmitted cancellation
becomes a new inverse gesture.

The Scanette redesign shown in `docs/bourse-aux-livres/maquettes/scanette/` is now
implemented locally in the same PWA: home and session-mode selection, distinct verdict
surfaces, session summary, cash register, consultation, manual ISBN keypad, and offline
variants. Consultation uses the local catalog without creating an outbox gesture; the
cash screen currently keeps a local visual list only, because durable sale persistence is
outside this visual tranche. The new UI has not been deployed yet. Its local validation
passes with 53 ChromeHeadless tests, the production build, and browser checks at 390 px
and 1280 px.

## App Structure

Both Angular apps follow the same high-level split:

- `core/` for shell, layouts, login, and cross-app wiring
- `feature/` for routed screens and business-facing UI
- `shared/` for interfaces, guards, services, and shared components

Verified feature roots:

- `BackOffice`: `actualities`, `actuality-detail`, `caisse`, `dashboard-vpd`, `event-detail`, `vpd-events`
- `Website`: `actuality-detail`, `actuality-page`, `association`, `contact`, `event-detail`, `home`, `legal`, `maxence`, `tableau`, `vpd-all-events`, `vpd-events`

## Frontend Conventions

- Preserve the split between admin and public concerns.
- Reuse the HTTP/data access pattern already present in the targeted app instead of introducing a second style in the same slice.
- Keep shared models typed and aligned with backend contracts.
- Both apps currently keep zoneless change detection via `provideZonelessChangeDetection()`.
- The Scan app follows the same zoneless Angular setup and reuses `@vpd/ui` through the
  `SharedUi` TypeScript path alias. Its typed `BookMetadataService` calls the backend
  metadata endpoint; `CameraScannerService` uses `@zxing/browser` with `TRY_HARDER` for
  EAN-13/EAN-8 and QR decoding, while `ScannerComponent` also accepts keyboard-wedge
  scanners, photo capture, and manual ISBNs. Because the app is zoneless, the component
  explicitly marks the view after asynchronous camera/photo/API callbacks; photo decoding
  also retries cropped, resized, and thresholded canvas variants for difficult images. The
  result card first uses the source-provided cover, retries an ISBN-based Open Library cover
  when that image fails, and renders an explicit unavailable-cover placeholder when both
  sources fail.
- Validate responsive behavior on desktop and mobile when UI changes.
- `src/SharedUi/scripts/link-shared-ui.mjs` is the shared npm linker. Both apps invoke it
  from `prebuild` and `prestart` through `node ../SharedUi/scripts/link-shared-ui.mjs`; it
  uses the calling application's `process.cwd()` so `SharedUi/node_modules` resolves to
  the caller rather than always to Website. BackOffice can therefore build after its own
  `npm ci`, without installing Website first.
- The Website public shell now keeps one shared visual language between the home overlay navigation and internal pages; the first modernization wave lives mainly in `src/styles.scss` plus the `navigation`, `navigation-mobile`, and `home` templates.
- The BackOffice OCR scan dialog, `bingo-card` shared component, and `/bingo-card` facade were removed in May 2026; no equivalent OCR surface exists in `Website`.

## Website Rendering Modes

- `src/Website/` uses Angular SSR with `provideClientHydration(withEventReplay())` in `AppModule` and render-mode mapping in `src/app/app.routes.server.ts`.
- Static association and Maxence leaf pages are prerendered.
- Static legal pages `mentions-legales`, `politique-de-confidentialite`, `politique-de-cookies`, and `accessibilite` are also prerendered through one shared legal-page component driven by route data.
- Content-driven routes such as `accueil`, `toute-l-actualite`, `actualite/:id`, `evenement`, `evenement/all`, and `evenement/:id` are server-rendered.
- The live route `evenement/:id/tableau` stays client-rendered because it depends on `EventSource` updates.

## Website Shell Notes

- The public footer now derives legal link labels and paths from Angular router config data instead of hard-coded placeholder paragraphs.
- Header sub-navigation is data-driven: `core/layouts/navigation/nav-items.ts` gives each `SiteNavItem` an optional `children`/`hint`, the desktop header renders them as a pure-CSS dropdown (`group-hover` + `group-focus-within`, collapsed state uses `invisible` so folded links stay out of the tab order), and the mobile overlay lists the same children indented under their parent. Only "L'association" declares children today; adding a submenu elsewhere is a data change, not a template change.
- The Website legal slice documents the current Microsoft Clarity usage seen in `src/index.html` and keeps explicit placeholders for unresolved association identifiers, publication director, retention windows, and accessibility remediation details until the association validates them.
- Website audience measurement is centralized in `shared/services/google-analytics.service.ts`: GA4 loads only after the cookie consent service grants the audience-measurement category, initializes Consent Mode with denied defaults, queues commands with the standard `arguments` shape expected by `gtag.js`, sends explicit pageviews on `NavigationEnd`, disables collection when consent is revoked, and stays inactive when `environment.google_analytics_measurement_id` is empty or still a build placeholder.
- The Website Docker build and `.github/workflows/website-deploy.yml` inject the public GA4 measurement ID from the GitHub `development` environment variable `GOOGLE_ANALYTICS_MEASUREMENT_ID`; `public/robots.txt` points crawlers to the real-domain `public/sitemap.xml`, which lists the static routes.

## Data Access And Live Updates

- Both Angular apps centralize HTTP base URL setup through `shared/services/axios.service.ts` with `axios.defaults.baseURL = environment.api_url`.
- Scan deliberately uses Angular `HttpClient` rather than the existing Axios setup. Its
  development API URL follows the browser host on port `5257`, so a phone opening the
  dev server on the LAN reaches the laptop's API; production keeps the configured Render
  API URL.
- `BackOffice` uses `MsalGuard` to protect its routed admin screens. `shared/auth/msal-config.ts`
  owns the Entra External ID client configuration; `shared/services/api-access-token.service.ts`
  acquires the API scope silently for the existing Axios transport. The Angular `MsalInterceptor`
  is not registered because the app does not use Angular `HttpClient` for API calls.
- `BackOffice` bootstraps both `AppComponent` and `MsalRedirectComponent`; its
  `src/index.html` must therefore contain both `<app-root>` and `<app-redirect>`. The
  tenant-scoped CIAM authority is configured in both environment files so development and
  production builds resolve the same Entra tenant.
- `Website` has an `sse-client.service` that subscribes to `/asso-events/{id}/tableau/sse` for live event updates and now guards `EventSource` usage behind `isPlatformBrowser()` for SSR safety.
- The Website SSE client closes the previous `EventSource` before opening a new event, ignores malformed payloads without dropping the last good state, and reconnects with bounded backoff from 250ms to 5s.
- The Website home SSR path now tolerates missing `next-bingo`, `next-books`, `next-other-event`, and `latest actuality` payloads by keeping default empty state instead of surfacing unhandled promise rejections during server rendering.
- The Website event detail places its location card in the hero (map, address, and itinerary) and uses `event-detail/components/general-infos` for the description plus editorial event photos; the standalone `shared/components/event-locations` block remains reserved for `/evenement`.
- Website prices now use a dedicated responsive card/grid presentation under `shared/components/prices`; API-provided product images, unit prices, and promotions remain unchanged, while BackOffice keeps the shared design-system list.
- Website prices load `ProductFacadeService.getPublicProducts()` from `GET /product/public` and apply a defensive `visibleOnWebsite` filter. BackOffice continues to load `GET /product` and the product dialog exposes the independent `VisibleOnWebsite` toggle for cash-only prices.

## MAUI Client

`src/MauiCashApp/` is an Android-only .NET MAUI 10 application targeting `net10.0-android` with:

- MVVM Toolkit (`CommunityToolkit.Mvvm`)
- Refit for API access
- SQLite for local storage
- embedded `appsettings.json` to configure the backend base URL
- MSAL.NET 4.88.0 with silent-first token acquisition and the Android redirect
  `msal427c90de-bf59-4b01-af63-dc0799248496://auth`
- an `AuthHandler` that adds the acquired Entra API token to Refit requests
- a currently narrow Refit surface: `IVpdApi.GetProductsAsync()` calls `GET /product`

The MAUI cash surface intentionally continues to use the full `/product` projection, so products hidden from Website remain available at the till.

## Client Risk Zones

- Contract drift between backend responses and client models.
- Base URL and auth assumptions in MAUI and web apps.
- SSE changes on `/asso-events/{id}/tableau/sse` can break the public website live table view.
- UI inconsistencies between `BackOffice` and `Website` when shared behaviors change.

## Validation Commands

- `npm run start`, `npm run build`, and `npm test` in each Angular app
- In `src/Scan/`, use `npm run start`, `npm run build`, and
  `npm test -- --watch=false --browsers=ChromeHeadless`; production and development
  builds are both part of the local validation because the environment file replacement
  is intentionally different between them.
- `npm run serve:ssr:vole_papillon_damour_website` in `src/Website/` for SSR smoke validation
- `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android` for the MAUI client
- For Website shell changes, prefer `npm run build` plus a local SSR smoke check on `/accueil` and at least one internal route with sub-navigation.
- As of 2026-09-03, BackOffice `npm ci`, its 2 bootstrap contract tests and 5 Angular tests,
  and `npm run build` pass; existing signal-diagnostic, bundle-budget, CSS, and CommonJS
  warnings remain.
- As of 2026-09-03, Website `npm ci`, 58 ChromeHeadless tests, the production build, SSR
  smoke checks, and responsive browser checks pass; its existing bundle/CSS/CommonJS
  warnings remain.
- As of 2026-09-04, Scan passes 53 ChromeHeadless tests and its production build; the
  production bundle retains the expected initial-size warning. Its redesigned Scanette
  surface was also checked in a local browser at 390 px and 1280 px. CI is configured to
  build the backend, MAUI, and frontend surfaces but does not run frontend unit tests.

## 2026-09-03 — Website editorial update

- Famille Drevet content added the FSHD sheet at `/maxence/maladies/fshd` and the memories
  route `/maxence/souvenirs`, both exposed through Maxence navigation and breadcrumbs.
- `/maxence/vie-quotidienne` is an editorial landing page with four chapters (daily care,
  hospital care, school, and transplant); the school detail remains directly reachable.
- The four Maxence daily-life detail routes share `DailyLifeChapterHeaderComponent`, with a
  consistent dark chapter banner, a return link to the four-entry landing page, and a common
  `max-w-[880px]` reading column; the landing page no longer repeats its chapter index in a
  right-hand hero card.
- Association copy now says “loi 1901”, asks for “un peu de votre temps”, removes former market
  activity, clarifies “cartons de livres”, and omits “Le bureau” and “Tout est public”.
- The presentation hero uses `public/images/Association/asso.jpg` with a wider desktop image
  zone while preserving the compact responsive mobile flow.
- Maxence daily-life pages preserve their first-person narratives in the shared editorial shell;
  daily care orders enteral nutrition, gastrostomy, digestive stoma, left-eye care, then antibiotics.
- Website prices hide exact `10c` and `50c` denomination labels while the authenticated cash
  surface keeps the full product projection. Scan cover fallback and unavailable-cover rendering
  are covered by component specs.
