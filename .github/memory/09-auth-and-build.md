# 09 - Auth And Build

## Backend Auth Model

- JWT bearer auth is configured from `JwtSettings` in `Infrastructure`.
- The API defines an `IsAdmin` policy requiring the `Admin` role.
- `POST /auth/login` is public but explicitly rate-limited with the `Login` limiter.
- `POST /auth/register` is public and still contains a commented-out `RequireAuthorization("IsAdmin")` line in code.

## Frontend And Client Auth Touchpoints

- `BackOffice` includes `@auth0/angular-jwt`, `ngx-cookie-service`, and an `AuthenticationGuard` for admin routes.
- `Website` does not show the same auth guard pattern in its top-level routing and now runs through Angular SSR with client hydration.
- `MauiCashApp` currently configures a base API URL but its visible Refit surface is limited to product retrieval.

## Configuration Sources

- Backend runtime config lives in `appsettings.json`, `appsettings.Development.json`, and local secrets/connection strings.
- The Aspire AppHost adds local launch settings for dashboard/resource service endpoints and injects backend connection strings for SQL Server and Azurite.
- The backend README points to `dotnet user-secrets` for local secret storage.
- `BackOffice` environment config includes `api_url`, `url_vpd_web_site`, and `time_numero_modal`.
- `Website` environment config includes `api_url`.
- `MauiCashApp/appsettings.json` contains `VpdSettings.BaseUrl`.
- Dockerized deployment config now lives in `src/BackOffice/Dockerfile`, `src/Website/Dockerfile`, and `infra/aca/`.
- The frontend Dockerfiles patch the production Angular environment files at image-build time through `API_URL` and `WEBSITE_URL` build args instead of introducing runtime templating.

## Build And Test Commands

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.sln`; `dotnet test .\src\Backend\Vole_Papillon_Damour.sln`
- Backend orchestration: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- BackOffice: `npm install`; `npm run start`; `npm run build`; `npm test`
- Website: `npm install`; `npm run start`; `npm run build`; `npm test`; `npm run serve:ssr:vole_papillon_damour_website`
- BackOffice Docker image: `docker build -f .\src\BackOffice\Dockerfile --build-arg API_URL=<url> --build-arg WEBSITE_URL=<url> .\src`
- Website Docker image: `docker build -f .\src\Website\Dockerfile --build-arg API_URL=<url> .\src`
- Subscription-scope ACA deploy: `az deployment sub create --location FranceCentral --template-file .\infra\aca\main.bicep --parameters .\infra\aca\parameters\main.dev.bicepparam`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.sln`

## Practical Warnings

- Do not store secrets in memory files or commit local connection strings.
- The Angular README files still look template-oriented; prefer `package.json`, environment files, and actual routing/services over README TODOs when you need the truth.
- `BackOffice` currently has no Angular `*.spec.ts` files, so `npm test` fails at tsconfig discovery and build validation is the effective safety net until tests are added.
- `npm run build` in `src/BackOffice/` is also not fully green as of 2026-05-18 because of pre-existing Angular module/declaration issues unrelated to the removed OCR slice.
- `Website` SSR route ownership lives in `src/app/app.routes.server.ts`; update that file when adding public static, SEO, or live routes.
- The current frontend Dockerfiles intentionally use `npm install` instead of `npm ci` because the repository lockfiles are not accepted by `npm ci` inside the Linux container context.
- The current frontend Dockerfiles must be built from the `src/` context, not the app subfolder, because both Angular apps resolve `@vpd/ui` through `../SharedUi` TS path mappings.
- The Infra Flow Sculptor project was created with placeholder subscription IDs (`00000000-0000-0000-0000-000000000000`) and those must be replaced in the project settings before real deployment.
- No CI pipeline file was detected, so local validation remains part of normal workflow.