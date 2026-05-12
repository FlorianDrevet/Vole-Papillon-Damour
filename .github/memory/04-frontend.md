# 04 - Frontend And Client Surfaces

## Angular Applications

Both web apps are Angular 18 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, includes `@auth0/angular-jwt` and `ngx-cookie-service`
- `src/Website/` - public UI for the association website

## Frontend Conventions

- Preserve the split between admin and public concerns.
- Reuse the HTTP/data access pattern already present in the targeted app instead of introducing a second style in the same slice.
- Keep shared models typed and aligned with backend contracts.
- Validate responsive behavior on desktop and mobile when UI changes.

## MAUI Client

`src/MauiCashApp/` is a .NET MAUI 9 application with:

- MVVM Toolkit (`CommunityToolkit.Mvvm`)
- Refit for API access
- SQLite for local storage
- embedded `appsettings.json` to configure the backend base URL

## Client Risk Zones

- Contract drift between backend responses and client models.
- Base URL and auth assumptions in MAUI and web apps.
- UI inconsistencies between `BackOffice` and `Website` when shared behaviors change.

## Validation Commands

- `npm run build` and `npm test` in each Angular app
- `dotnet build .\src\MauiCashApp\ShopAppVpd.sln` for the MAUI client