# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 18 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, includes `@auth0/angular-jwt` and `ngx-cookie-service`
- `src/Website/` - public UI for the association website

## App Structure

Both Angular apps follow the same high-level split:

- `core/` for shell, layouts, login, and cross-app wiring
- `feature/` for routed screens and business-facing UI
- `shared/` for interfaces, guards, services, and shared components

Verified feature roots:

- `BackOffice`: `actualities`, `actuality-detail`, `caisse`, `dashboard-vpd`, `event-detail`, `vpd-events`
- `Website`: `actuality-detail`, `actuality-page`, `association`, `contact`, `event-detail`, `home`, `maxence`, `tableau`, `vpd-all-events`, `vpd-events`

## Frontend Conventions

- Preserve the split between admin and public concerns.
- Reuse the HTTP/data access pattern already present in the targeted app instead of introducing a second style in the same slice.
- Keep shared models typed and aligned with backend contracts.
- Validate responsive behavior on desktop and mobile when UI changes.

## Data Access And Live Updates

- Both Angular apps centralize HTTP base URL setup through `shared/services/axios.service.ts` with `axios.defaults.baseURL = environment.api_url`.
- `BackOffice` uses an `AuthenticationGuard` to protect its routed admin screens.
- `Website` has an `sse-client.service` that subscribes to `/asso-events/{id}/tableau/sse` for live event updates.

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
- `dotnet build .\src\MauiCashApp\ShopAppVpd.sln` for the MAUI client