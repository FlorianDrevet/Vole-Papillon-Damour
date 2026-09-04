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
  `AzureAd__ClientId`, and `AzureAd__Audience`. For v2 access tokens, the audience is the
  bare API application ID; the `api://<id>/access_as_user` form remains the delegated MSAL
  scope. The development API settings and Bicep deployment use the bare ID so BackOffice
  write requests validate against the `aud` claim issued by Entra.
- Entra `JwtBearerOptions` disable inbound claim mapping and use `roles` as the role claim
  type. This keeps app roles such as `Administration` visible to ASP.NET Core's
  `RequireRole` policies; leaving the default mapping enabled makes those policies return
  `403 Forbidden` even when the token contains the role.
- `POST /auth/login` is public but explicitly rate-limited with the `Login` limiter.
- `POST /auth/register` is public and still contains a commented-out `RequireAuthorization("IsAdmin")` line in code.

## Frontend And Client Auth Touchpoints

- `BackOffice` uses `@azure/msal-angular` 5.3.1 with `@azure/msal-browser` 5.20.0,
  compatible with the repository's Angular 21 line. `shared/auth/msal-config.ts` configures
  the Entra External ID authority, SPA redirect URIs, local storage cache, and the
  `access_as_user` login scope. The CIAM custom-domain authority is tenant-scoped
  (`https://volepapillondamour.ciamlogin.com/<tenantId>/`); `knownAuthorities` remains the
  custom-domain host.
- Protected BackOffice routes use `MsalGuard`; `MsalRedirectComponent` handles the redirect
  response at application bootstrap, and `AppComponent` restores/selects the active cached
  account. Because both root components are bootstrapped, `src/index.html` must declare both
  `<app-root>` and `<app-redirect>`; omitting the latter raises Angular `NG05104` before the
  MSAL redirect handler can initialize. `LoginComponent` starts `loginRedirect` instead of
  posting local credentials.
- `AppModule` provides an awaited `provideAppInitializer` that calls
  `MsalService.initialize()` before either root component is created. Without this barrier,
  a normal refresh can construct `AuthSessionService` before MSAL is initialized and leave
  the BackOffice blank with `uninitialized_public_client_application`; a hard refresh only
  masks the race.
- `ApiAccessTokenService` calls `MsalService.acquireTokenSilent` for the API scope and
  `AxiosService` adds the resulting bearer token to every API request. `MsalInterceptor` is
  intentionally not registered because BackOffice uses Axios rather than Angular `HttpClient`.
- The former BackOffice cookie/JWT authentication service, login facade, guard, token
  interface, role enum, `@auth0/angular-jwt`, and `ngx-cookie-service` were removed in the
  MSAL migration. Authorization remains enforced by the API's Entra role policies.
- `Website` does not show the same auth guard pattern in its top-level routing and now runs through Angular SSR with client hydration.
- `MauiCashApp` targets `net10.0-android` and uses MSAL.NET 4.88.0. `MsalAuthService` acquires
  `api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user` silently first and falls back to
  interactive sign-in; `AuthHandler` attaches the resulting bearer token to the Refit client.
  The Android callback is handled by `MainActivity` and `MsalActivity` for
  `msal427c90de-bf59-4b01-af63-dc0799248496://auth`.

## Configuration Sources

- Backend runtime config lives in `appsettings.json`, `appsettings.Development.json`, and local secrets/connection strings.
- The Aspire AppHost adds local launch settings for dashboard/resource service endpoints and injects backend connection strings for SQL Server and Azurite.
- The AppHost uses the Aspire storage connection expression for Blob clients and leaves
  `AzureWebJobsStorage` to the Azure Functions Aspire integration (or an explicit
  `WithHostStorage` resource), rather than forcing the default Azurite port.
- The AppHost now also owns a local user-secret-backed SQL password parameter under `Parameters:sql-server-password` so the persisted SQL Server volume keeps matching credentials across Aspire launches.
- The backend README points to `dotnet user-secrets` for local secret storage.
- `BackOffice` environment config includes `api_url`, `url_vpd_web_site`, `time_numero_modal`,
  and the public Entra settings (`tenantId`, client ID, authority, redirect URIs, and API scope).
- `Website` environment config includes `api_url`.
- `Scan` development config derives its API host from the browser hostname on port `5257`
  for LAN testing; the production environment points to the configured API deployment.
- Scan production bundles use `@zxing/browser` directly instead of the optional native
  `BarcodeDetector` path. The camera scans the full video frame with `TRY_HARDER` and
  supports EAN-13/EAN-8 ISBN barcodes plus QR codes; the scanner also accepts an image
  selected from the phone as a fallback. Photo decoding retries cropped, resized, and
  thresholded canvas variants, and the zoneless Scan component explicitly marks the view
  after asynchronous scan/API state changes. The public ACA deployment is HTTPS, which
  satisfies the secure-context requirement for camera access.
- `MauiCashApp/appsettings.json` contains `VpdSettings.BaseUrl`; the MSAL client ID, authority,
  API scope, and Android redirect are application configuration constants in `MsalAuthService`.
- Dockerized deployment config now lives in `src/BackOffice/Dockerfile`, `src/Website/Dockerfile`, and `infra/aca/`.
- Dockerized deployment config also lives in `src/Scan/Dockerfile` and
  `src/Backend/Vole_Papillon_Damour.Worker/Dockerfile`; `.github/workflows/scan-deploy.yml`
  and `.github/workflows/worker-deploy.yml` build/push immutable commit-tagged images and
  update the corresponding Container Apps through GitHub OIDC.
- The frontend Dockerfiles patch the production Angular environment files at image-build time through `API_URL` and `WEBSITE_URL` build args instead of introducing runtime templating.
- API health probes are configured in `infra/parameters/main.dev.bicepparam` as readiness, liveness, and startup checks for `/health` on port `8080`; Azure deployment remains a separate operational step.
- The dev SQL parameter uses Azure SQL Database `S1` (`Standard`, 20 DTUs, 250 GB) with `autoPauseDelayMinutes: 0`; `DatabaseSkuConfig` keeps DTU tiers' family optional.

## Build And Test Commands

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend orchestration: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- BackOffice: `npm install`; `npm run start`; `npm run build`; `npm test`
- Website: `npm install`; `npm run start`; `npm run build`; `npm test`; `npm run serve:ssr:vole_papillon_damour_website`
- Scan: `npm ci`; `npm run start`; `npm run build`; `npm test -- --watch=false --browsers=ChromeHeadless`
- BackOffice Docker image: `docker build -f .\src\BackOffice\Dockerfile --build-arg API_URL=<url> --build-arg WEBSITE_URL=<url> .\src`
- Website Docker image: `docker build -f .\src\Website\Dockerfile --build-arg API_URL=<url> .\src`
- Subscription-scope ACA deploy: `az deployment sub create --location FranceCentral --template-file .\infra\aca\main.bicep --parameters .\infra\aca\parameters\main.dev.bicepparam`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`

## Practical Warnings

- Do not store secrets in memory files or commit local connection strings.
- The Angular README files still look template-oriented; prefer `package.json`, environment files, and actual routing/services over README TODOs when you need the truth.
- `BackOffice` now has focused tests for the MSAL bootstrap/login redirect and API token adapter;
  `npm test -- --watch=false --browsers=ChromeHeadless` passes locally with 2 bootstrap
  contract tests followed by 5 Angular/Karma tests. The CI workflow currently builds the
  frontends but does not yet run frontend unit tests.
- `npm run build` in `src/BackOffice/` exits successfully, but keeps pre-existing Angular
  signal-diagnostic, bundle-budget, CSS-budget, and CommonJS warnings unrelated to this
  authentication migration.
- `Website` SSR route ownership lives in `src/app/app.routes.server.ts`; update that file when adding public static, SEO, or live routes.
- The current frontend Dockerfiles intentionally use `npm install` instead of `npm ci` because the repository lockfiles are not accepted by `npm ci` inside the Linux container context.
- The current frontend Dockerfiles must be built from the `src/` context, not the app subfolder, because both Angular apps resolve `@vpd/ui` through `../SharedUi` TS path mappings.
- The Infra Flow Sculptor project was created with placeholder subscription IDs (`00000000-0000-0000-0000-000000000000`) and those must be replaced in the project settings before real deployment.
- Rider build-with-surface-heuristics can create generated C# files under `src/Backend/Vole_Papillon_Damour.Domain/artifacts/validation/obj/`; the Domain project now excludes `artifacts/**` from SDK default items so those generated assembly attribute files do not get compiled alongside the normal `obj/` output.
- Repeated `18456` login failures from the local SQL Server container during Aspire startup usually mean the persisted SQL volume still has an older `sa` password than the one the AppHost is currently using; stabilize the AppHost secret instead of relying on the default generated password.
- `.github/workflows/ci.yml` is the push/pull-request gate for the backend solution, its three test projects, the Android MAUI target, and the BackOffice, Website, and Scan Angular builds. It deliberately does not run frontend unit tests yet; the Angular tests are currently validated locally. The MAUI workflow target is aligned with the project at `net10.0-android`; the complete CI runs for PR #39 passed.
- `dotnet test .\src\MauiCashApp.Tests\ShopAppVpd.Tests.csproj` covers the platform-independent
  authorization handler. The MAUI Android build remains environment-dependent and currently
  fails locally with `XA5300` when no Android SDK is configured.

## Books runtime update — 2026-09-04

- The API startup migration policy is explicit: `DatabaseMigrationPolicy.ShouldRunOnStartup` returns true only for `Development`; deployed migration is performed before rollout by `Books runtime - deploy`.
- The new workflow builds the API and Worker from the same checkout and image tag, can apply EF migrations through a temporary SQL firewall rule, and always attempts to remove that rule before finishing.
- The Worker is intentionally configured without API authentication registration. It uses the Application/Infrastructure layers for `Sweep` and `Enrich`, with ACS email delivery disabled by default until domain verification and a real delivery test are complete.
- The backend solution validation after the runtime slice includes the account-deletion outbox kind-isolation regression: an `AlertEmail` row is not claimable by `AccountDeletionStore`.
