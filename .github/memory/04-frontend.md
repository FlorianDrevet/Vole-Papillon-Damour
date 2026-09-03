# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 21 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, with MSAL Angular (`@azure/msal-angular` 5.3.1,
  `@azure/msal-browser` 5.20.0) and `@dhutaryan/ngx-mat-timepicker`
- `src/Website/` - public UI for the association website, now built with Angular SSR and hydration support
- `src/Scan/` - Angular 21 consultation-only feasibility probe for ISBN capture and
  bibliographic metadata; it has no session, IndexedDB, authentication, or write flow

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
- As of 2026-09-03, `npm ci` followed by `npm test -- --watch=false --browsers=ChromeHeadless`
  passes with 5 BackOffice tests, and production/development `npm run build` both pass in
  `src/BackOffice/`; the builds still emit existing signal-diagnostic, bundle-budget, CSS,
  and CommonJS warnings. The same `npm ci`/build validation passes in `src/Website/`.
- As of 2026-09-03, Scan passes 24 ChromeHeadless tests and the Angular production and
  development builds; the production build has only the expected initial bundle budget
  warning after adding the ZXing decoder and photo preprocessing. The CI workflow installs
  its lockfile and builds the app after the existing BackOffice and Website steps. Cover
  fallback and unavailable-cover rendering are covered by the scanner component specs.
