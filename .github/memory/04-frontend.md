# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 21 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, includes `@auth0/angular-jwt`, `ngx-cookie-service`, and `@dhutaryan/ngx-mat-timepicker`
- `src/Website/` - public UI for the association website, now built with Angular SSR and hydration support

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
- Validate responsive behavior on desktop and mobile when UI changes.
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
- The Website legal slice documents the current Microsoft Clarity usage seen in `src/index.html` and keeps explicit placeholders for unresolved association identifiers, publication director, retention windows, and accessibility remediation details until the association validates them.

## Data Access And Live Updates

- Both Angular apps centralize HTTP base URL setup through `shared/services/axios.service.ts` with `axios.defaults.baseURL = environment.api_url`.
- `BackOffice` uses an `AuthenticationGuard` to protect its routed admin screens.
- `Website` has an `sse-client.service` that subscribes to `/asso-events/{id}/tableau/sse` for live event updates and now guards `EventSource` usage behind `isPlatformBrowser()` for SSR safety.
- The Website home SSR path now tolerates missing `next-bingo`, `next-books`, `next-other-event`, and `latest actuality` payloads by keeping default empty state instead of surfacing unhandled promise rejections during server rendering.

## MAUI Client

`src/MauiCashApp/` is a .NET MAUI 9 application with:

- MVVM Toolkit (`CommunityToolkit.Mvvm`)
- Refit for API access
- SQLite for local storage
- embedded `appsettings.json` to configure the backend base URL
- a currently narrow Refit surface: `IVpdApi.GetProductsAsync()` calls `GET /product`

## Client Risk Zones

- Contract drift between backend responses and client models.
- Base URL and auth assumptions in MAUI and web apps.
- SSE changes on `/asso-events/{id}/tableau/sse` can break the public website live table view.
- UI inconsistencies between `BackOffice` and `Website` when shared behaviors change.

## Validation Commands

- `npm run start`, `npm run build`, and `npm test` in each Angular app
- `npm run serve:ssr:vole_papillon_damour_website` in `src/Website/` for SSR smoke validation
- `dotnet build .\src\MauiCashApp\ShopAppVpd.sln` for the MAUI client
- For Website shell changes, prefer `npm run build` plus a local SSR smoke check on `/accueil` and at least one internal route with sub-navigation.
- As of 2026-05-18, `npm run build` in `src/BackOffice/` still fails on baseline Angular module/declaration issues around `SharedModule`, `FeatureModule`, and `DesignSystemModule`; OCR removal did not resolve or introduce that pre-existing build state.