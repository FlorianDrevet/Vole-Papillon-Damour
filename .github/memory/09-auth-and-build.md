# 09 - Auth And Build

## Backend Auth Model

- L0-11 deployment 1 adds `Microsoft.Identity.Web` 4.14.2 and a composite `Bearer`
  policy scheme. It forwards Entra External ID tokens (the `*.ciamlogin.com` issuer) to
  the `Entra` scheme and existing tokens from `/auth/login` to `LegacyJwt`.
- Entra role claims are read from `roles`; the staged policies are `Tri`, `Caisse`, and
  `Administration`. `IsAdmin` remains a compatibility alias accepting `Administration`
  and the legacy `Admin` role until deployment 3.
- `JwtSettings`, `IJwtGenerator`, and `/auth/login` intentionally remain during deployment
  1 so the deployed BackOffice and unredistributed MAUI devices continue to work.
- Entra runtime values are supplied by `AzureAd__Instance`, `AzureAd__TenantId`,
  `AzureAd__ClientId`, and `AzureAd__Audience`; the API client ID is populated only after
  `Configure-EntraApps.ps1` has created the registration.
- `POST /auth/login` is public but explicitly rate-limited with the `Login` limiter.
- `POST /auth/register` is public and still contains a commented-out `RequireAuthorization("IsAdmin")` line in code.

## Frontend And Client Auth Touchpoints

- `BackOffice` uses `@azure/msal-angular` 5.3.1 with `@azure/msal-browser` 5.20.0,
  compatible with the repository's Angular 21 line. `shared/auth/msal-config.ts` configures
  the Entra External ID authority, SPA redirect URIs, local storage cache, and the
  `access_as_user` login scope.
- Protected BackOffice routes use `MsalGuard`; `MsalRedirectComponent` handles the redirect
  response at application bootstrap, and `AppComponent` restores/selects the active cached
  account. `LoginComponent` starts `loginRedirect` instead of posting local credentials.
- `ApiAccessTokenService` calls `MsalService.acquireTokenSilent` for the API scope and
  `AxiosService` adds the resulting bearer token to every API request. `MsalInterceptor` is
  intentionally not registered because BackOffice uses Axios rather than Angular `HttpClient`.
- The former BackOffice cookie/JWT authentication service, login facade, guard, token
  interface, role enum, `@auth0/angular-jwt`, and `ngx-cookie-service` were removed in the
  MSAL migration. Authorization remains enforced by the API's Entra role policies.
- `Website` does not show the same auth guard pattern in its top-level routing and now runs through Angular SSR with client hydration.
- `MauiCashApp` currently configures a base API URL but its visible Refit surface is limited to product retrieval.

## Configuration Sources

- Backend runtime config lives in `appsettings.json`, `appsettings.Development.json`, and local secrets/connection strings.
- The Aspire AppHost adds local launch settings for dashboard/resource service endpoints and injects backend connection strings for SQL Server and Azurite.
- The AppHost now also owns a local user-secret-backed SQL password parameter under `Parameters:sql-server-password` so the persisted SQL Server volume keeps matching credentials across Aspire launches.
- The backend README points to `dotnet user-secrets` for local secret storage.
- `BackOffice` environment config includes `api_url`, `url_vpd_web_site`, `time_numero_modal`,
  and the public Entra settings (`tenantId`, client ID, authority, redirect URIs, and API scope).
- `Website` environment config includes `api_url`.
- `MauiCashApp/appsettings.json` contains `VpdSettings.BaseUrl`.
- Dockerized deployment config now lives in `src/BackOffice/Dockerfile`, `src/Website/Dockerfile`, and `infra/aca/`.
- The frontend Dockerfiles patch the production Angular environment files at image-build time through `API_URL` and `WEBSITE_URL` build args instead of introducing runtime templating.
- API health probes are configured in `infra/parameters/main.dev.bicepparam` as readiness, liveness, and startup checks for `/health` on port `8080`; Azure deployment remains a separate operational step.
- The dev SQL parameter uses Azure SQL Database `S1` (`Standard`, 20 DTUs, 250 GB) with `autoPauseDelayMinutes: 0`; `DatabaseSkuConfig` keeps DTU tiers' family optional.

## Build And Test Commands

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend orchestration: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- BackOffice: `npm install`; `npm run start`; `npm run build`; `npm test`
- Website: `npm install`; `npm run start`; `npm run build`; `npm test`; `npm run serve:ssr:vole_papillon_damour_website`
- BackOffice Docker image: `docker build -f .\src\BackOffice\Dockerfile --build-arg API_URL=<url> --build-arg WEBSITE_URL=<url> .\src`
- Website Docker image: `docker build -f .\src\Website\Dockerfile --build-arg API_URL=<url> .\src`
- Subscription-scope ACA deploy: `az deployment sub create --location FranceCentral --template-file .\infra\aca\main.bicep --parameters .\infra\aca\parameters\main.dev.bicepparam`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net9.0-android`

## Practical Warnings

- Do not store secrets in memory files or commit local connection strings.
- The Angular README files still look template-oriented; prefer `package.json`, environment files, and actual routing/services over README TODOs when you need the truth.
- `BackOffice` now has focused Angular tests for the MSAL login redirect and API token adapter;
  `npm test -- --watch=false --browsers=ChromeHeadless` passes locally with 5 tests. The CI
  workflow currently builds the frontends but does not yet run frontend unit tests.
- `npm run build` in `src/BackOffice/` exits successfully, but keeps pre-existing Angular
  signal-diagnostic, bundle-budget, CSS-budget, and CommonJS warnings unrelated to this
  authentication migration.
- `Website` SSR route ownership lives in `src/app/app.routes.server.ts`; update that file when adding public static, SEO, or live routes.
- The current frontend Dockerfiles intentionally use `npm install` instead of `npm ci` because the repository lockfiles are not accepted by `npm ci` inside the Linux container context.
- The current frontend Dockerfiles must be built from the `src/` context, not the app subfolder, because both Angular apps resolve `@vpd/ui` through `../SharedUi` TS path mappings.
- The Infra Flow Sculptor project was created with placeholder subscription IDs (`00000000-0000-0000-0000-000000000000`) and those must be replaced in the project settings before real deployment.
- Rider build-with-surface-heuristics can create generated C# files under `src/Backend/Vole_Papillon_Damour.Domain/artifacts/validation/obj/`; the Domain project now excludes `artifacts/**` from SDK default items so those generated assembly attribute files do not get compiled alongside the normal `obj/` output.
- Repeated `18456` login failures from the local SQL Server container during Aspire startup usually mean the persisted SQL volume still has an older `sa` password than the one the AppHost is currently using; stabilize the AppHost secret instead of relying on the default generated password.
- `.github/workflows/ci.yml` is the push/pull-request gate for the backend solution, its three test projects, the Android MAUI target, and both Angular builds. It deliberately does not run frontend unit tests yet; the BackOffice tests are currently validated locally.
