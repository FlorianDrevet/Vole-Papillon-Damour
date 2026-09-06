# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 21 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, with MSAL Angular (`@azure/msal-angular` 5.3.1,
  `@azure/msal-browser` 5.20.0) and `@dhutaryan/ngx-mat-timepicker`
- `src/Website/` - public UI for the association website, now built with Angular SSR and hydration support
- `src/Catalog/` - separate public books catalog, built with Angular SSR and hydration support
- `src/Scan/` - Angular 21 Scanette PWA for ISBN capture, offline triage, local decisions,
  catalog consultation, IndexedDB persistence, and volunteer authentication/synchronization

The public catalog is intentionally separate from the association Website. It uses typed
`CatalogApiService`/models and the `/catalog/*` API reads for search, book details, works,
the next books fair, and the dynamic sitemap. Its public routes are `/`, `/recherche`,
`/catalogue`, `/livres/:slug`, `/oeuvre/:workId`, and the two legal pages. The UI keeps
available quantities separate from future announcements, leaves exhausted books visible,
and does not include audience trackers. The `/compte` member route uses a dynamic,
SSR-safe MSAL Browser loader, reads/removes watchlist items through bearer-protected API
calls, exposes alert suspension/reactivation and the durable account-deletion request.
`/desinscription` is a client-only authenticated opt-out route. The `/administration`
route now consumes the typed admin APIs for overview, catalogue, sessions, dead stock,
fairs, alerts, members and settings; `Administration` role assignment remains Entra-owned.
The external bibliographic result block is kept separate from local results, while live
ACS email delivery remains deferred until the sending domain is verified.

As of 2026-09-06, the Catalog P2/P3 integration is implemented in the V2 visual shell:
the search page calls local and external reference endpoints independently; reference
items can be followed as an edition or work after Entra login; the account page manages
watchlist items and alert preference; `/desinscription` confirms an authenticated alert
opt-out; and `/administration` exposes every currently available admin workspace with
typed filters, details, corrections, confirmations and truthful empty/error states. The
Catalog client keeps private data client-rendered and marks `/compte`, `/administration`
and `/desinscription` `noindex, nofollow`. It deliberately does not add role-editing or
physical-carton controls because those contracts do not exist.

## Planned Books Scan client decisions

As of 2026-09-05, the P1-5 Scan foundation is implemented in `src/Scan` and deployed to
the DEV ACA: local verdicts, IndexedDB session/catalog/outbox persistence, MSAL `Tri`
authentication, and sequential gesture replay are present. P1-2 selected the existing
Jasmine/Karma/ChromeHeadless toolchain: browser integration tests use real IndexedDB and
a fake transport simulates delays, failures, mid-flight disconnects, and duplicate
responses. The local outbox states are `Pending`, `Kept`, `Rejected`, and
`CancelledLocal`; only final decisions reach the API, while a transmitted cancellation
becomes a new inverse gesture.

The Scanette redesign shown in `docs/bourse-aux-livres/maquettes/scanette/` is implemented
and deployed in the same PWA: home and session-mode selection, distinct verdict surfaces,
session summary, cash register, consultation, manual ISBN keypad, and offline variants.
Consultation uses the local catalog without creating an outbox gesture; the cash screen
currently keeps a local visual list only, because durable sale persistence is outside this
visual tranche. The root auth gate shows a dedicated login surface until an Entra account
with the `Tri` role is available; token-renewal failures return to that surface. The tri
scan view starts the ZXing camera automatically, keeps manual/photo fallback, and no longer
renders the former top toast stack. Both Scan environments use the tenant-scoped CIAM
authority; the login request carries an explicit root return page and surfaces redirect
failures inline. The MSAL interceptor protects `/scan/*` with the API bearer token; the
wildcard is required for nested delta/session endpoints. CI validation covers 79
ChromeHeadless tests, four bootstrap tests, the production build, and deployment workflow
`33924618301`; the public HTTP smoke is green, while the interactive Tri retest still
requires a signed-in browser/device.

The 2026-09-04 Scan follow-up keeps the live camera open on the cash and consultation
surfaces: it starts on entry and restarts after each decoded book, while the cash list is
kept below the compact camera panel. Each cash item now has its own removal action. The
local workflow marks the same ISBN as `Déjà scanné à l’instant` when it reappears within
five seconds in one session (`RG-04`), and the verdict card is intentionally smaller so
the catalog facts remain visible. Ending a session synchronizes and closes its remote
session before clearing the active IndexedDB snapshot; a failed close keeps the local
gestures rather than silently losing them. Validation for this follow-up passes with 74
ChromeHeadless tests, the bootstrap contract, and the production build; it is included in
the deployed Scan image. The subsequent nested-endpoint authentication regression is
covered by the 79-test CI run described above.

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
  when that image fails, and renders the shared `VpdBookCoverPlaceholderComponent` when both
  sources fail. As of 2026-09-06, the API/Worker resolves direct BnF/Open Library/Google Books
  URLs before the result reaches Scan; the browser-side Open Library retry remains defensive.
- The Scan root is access-gated by `ScanAuthService.authState$`: only the Entra `Tri` app
  role renders `ScannerComponent`; unauthenticated and unauthorized accounts render
  `ScanLoginComponent`. `src/index.html` includes `<app-redirect>` and `AppModule` awaits
  `MsalService.initialize()` before the auth cache is read, which keeps refresh and redirect
  bootstrapping reliable. The environment authority includes the tenant path, and
  `ScanAuthService.login()` uses `redirectStartPage` plus a deferred observable so synchronous
  MSAL startup failures reach the login component. The mobile shell uses a centered fixed
  viewport with no document scroll, and the tri view starts/stops the live camera around each
  lookup.
- Validate responsive behavior on desktop and mobile when UI changes.
- `src/SharedUi/scripts/link-shared-ui.mjs` is the shared npm linker. Both apps invoke it
  from `prebuild` and `prestart` through `node ../SharedUi/scripts/link-shared-ui.mjs`; it
  uses the calling application's `process.cwd()` so `SharedUi/node_modules` resolves to
  the caller rather than always to Website. BackOffice can therefore build after its own
  `npm ci`, without installing Website first.
- The Website public shell now keeps one shared visual language between the home overlay navigation and internal pages; the first modernization wave lives mainly in `src/styles.scss` plus the `navigation`, `navigation-mobile`, and `home` templates.
- The BackOffice OCR scan dialog, `bingo-card` shared component, and `/bingo-card` facade were removed in May 2026; no equivalent OCR surface exists in `Website`.
- Legacy event date/time values are encoded as UTC wall-clock components: the BackOffice
  `shared/extensions/MyDate.ts` helpers convert API values into local-field `Date` values
  before Angular Material date/time pickers display them, and `MyDate.toISOUtcString()`
  converts edited picker values back without a timezone drift. Website event date/time pipes
  use `UTC`, and the event detail selects `hourOpenDoors` for Books events rather than
  treating a non-midnight `dateStart` as the opening time.

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
- In `src/Catalog/`, use `npm ci`, `npm run build`, and
  `npm test -- --watch=false --browsers=ChromeHeadless`; the SSR smoke server is
  `npm run serve:ssr:vole_papillon_damour_catalog` after a build.
- `npm run serve:ssr:vole_papillon_damour_website` in `src/Website/` for SSR smoke validation
- `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android` for the MAUI client
- For Website shell changes, prefer `npm run build` plus a local SSR smoke check on `/accueil` and at least one internal route with sub-navigation.
- As of 2026-09-03, BackOffice `npm ci`, its 2 bootstrap contract tests and 5 Angular tests,
  and `npm run build` pass; existing signal-diagnostic, bundle-budget, CSS, and CommonJS
  warnings remain.
- As of 2026-09-03, Website `npm ci`, 58 ChromeHeadless tests, the production build, SSR
  smoke checks, and responsive browser checks pass; its existing bundle/CSS/CommonJS
  warnings remain.
- As of 2026-09-04, BackOffice passes its 12 ChromeHeadless tests and production build, and
  Website passes 67 ChromeHeadless tests and its production build after the event date/time
  correction; the existing bundle/CSS/CommonJS warnings remain.
- As of 2026-09-04, the Website home "Prochains rendez-vous" cards use the same event-time
  rule as the detail page: Books events use `hourOpenDoors`, while Bingo and Other events
  use `dateStart`; a regression test covers all three event types.
- As of 2026-09-05, Scan's merged auth follow-up passes 79 ChromeHeadless tests, four
  bootstrap tests, and the production build; the production bundle retains the expected
  initial-size warning. The redesigned Scanette and the nested-endpoint bearer fix are
  deployed by `Scan - deploy` `33924618301`. CI still builds the backend, MAUI, and frontend
  surfaces but does not run frontend unit tests; the PR ran them explicitly.
- As of 2026-09-06, Catalog passes 55 ChromeHeadless tests, the production Angular build,
  and a local SSR smoke of public/private routes after the P2/P3 member and administration
  integration. BackOffice passes 15 ChromeHeadless tests and its bootstrap contract. The
  mobile check covers 390px for the account and administration surfaces; the private routes
  keep the HTTP `noindex, nofollow` header. The API was unavailable during the smoke, so
  empty/error fallback states were verified without inventing catalogue data.
- As of 2026-09-05, Catalog passes 29 ChromeHeadless tests, the production Angular build,
  the SSR container build, and public smoke checks. The image is built from the `src/`
  context so the `SharedUi` linker remains available. The public domain is
  `https://livres.volepapillondamour.fr`; `/compte` and `/administration` are client-only
  protected routes with an HTTP `X-Robots-Tag: noindex, nofollow` header.

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
