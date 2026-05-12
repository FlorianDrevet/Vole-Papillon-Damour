# 09 - Auth And Build

## Backend Auth Model

- JWT bearer auth is configured from `JwtSettings` in `Infrastructure`.
- The API defines an `IsAdmin` policy requiring the `Admin` role.
- `POST /auth/login` is public but explicitly rate-limited with the `Login` limiter.
- `POST /auth/register` is public and still contains a commented-out `RequireAuthorization("IsAdmin")` line in code.

## Frontend And Client Auth Touchpoints

- `BackOffice` includes `@auth0/angular-jwt`, `ngx-cookie-service`, and an `AuthenticationGuard` for admin routes.
- `Website` does not show the same auth guard pattern in its top-level routing.
- `MauiCashApp` currently configures a base API URL but its visible Refit surface is limited to product retrieval.

## Configuration Sources

- Backend runtime config lives in `appsettings.json`, `appsettings.Development.json`, and local secrets/connection strings.
- The Aspire AppHost adds local launch settings for dashboard/resource service endpoints and injects backend connection strings for SQL Server, Azurite, Email, and OCR.
- The backend README points to `dotnet user-secrets` for local secret storage.
- `BackOffice` environment config includes `api_url`, `url_vpd_web_site`, and `time_numero_modal`.
- `Website` environment config includes `api_url`.
- `MauiCashApp/appsettings.json` contains `VpdSettings.BaseUrl`.

## Build And Test Commands

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.sln`; `dotnet test .\src\Backend\Vole_Papillon_Damour.sln`
- Backend orchestration: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- BackOffice: `npm install`; `npm run start`; `npm run build`; `npm test`
- Website: `npm install`; `npm run start`; `npm run build`; `npm test`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.sln`

## Practical Warnings

- Do not store secrets in memory files or commit local connection strings.
- The Angular README files still look template-oriented; prefer `package.json`, environment files, and actual routing/services over README TODOs when you need the truth.
- No CI pipeline file was detected, so local validation remains part of normal workflow.