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

- Account administration keeps Entra app roles as the source of truth. The BackOffice
  uses the Administration-protected `/accounts/admin` endpoints; the API Graph adapter
  creates External ID local identities with `passwordPolicies=DisablePasswordExpiration`
  and synchronizes `Tri`, `Caisse`, and `Administration` assignments. The app-only Graph
  registration needs `User.ReadWrite.All`, `Application.Read.All`, and
  `AppRoleAssignment.ReadWrite.All`; the API receives its tenant domain and API client ID
  through `EntraGraph__TenantDomain` and `EntraGraph__ApiClientId`, plus the app-only
  credential through `EntraGraph__ClientId` and `EntraGraph__ClientSecret`. If the Graph
  directory cannot authenticate or lacks permissions, Catalog now keeps the accounts
  workspace readable with a contextual dependency notice instead of the generic red error;
  the runtime secret/permission fix remains an external deployment check.

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
- `Catalog` uses a dynamic SSR-safe MSAL Browser loader. Its member/admin pages acquire the
  API scope silently and read `roles` from that API access token (not from the cached ID
  token); `Administration` and the compatibility `Admin` role expose the administration
  navigation affordance, while API policies still enforce every admin request. When silent
  acquisition needs user interaction, `acquireTokenRedirect` keeps the current private URL
  as `redirectStartPage` and the pages render a specific renewal state.
- Public Catalog registration keeps the MSAL `prompt=create` request and `/compte` return
  URL. The External ID form is provisioned separately by
  `infra/entra/Configure-EntraUserFlow.ps1` through Graph v1.0 and is associated only with
  `vpd-catalog-<environment>`; the flow body uses a portable 0–256-character
  `displayName` validation regex, so normal names such as `Florian Drevet` are accepted.
  Scan, BackOffice and Cash have no self-service signup flow.
- Catalog browser-delegated requests add `ui_locales=fr-FR` and `mkt=fr-FR` to sign-in,
  registration, and interactive API-token renewal. `infra/entra/Configure-EntraBranding.ps1`
  creates or updates the tenant's `fr-FR` branding, updates the External ID default
  localization `0`, and uploads the Catalog CSS to both localization streams through Graph
  `OrganizationalBranding.ReadWrite.All`. A missing branding localization is treated as an
  empty collection only when Graph reports the expected fresh-tenant `ResourceNotFound`, and
  every Graph write is explicitly terminating so a partial application cannot be reported as
  successful. This changes the hosted page's visual language but does not move password entry
  into the Catalog. A pixel-perfect custom form would require a separate Native Authentication
  decision and a CORS proxy.
- Because the production redirect URI is the Catalog origin, the root shell checks for a
  pending MSAL `code`/`error` response and initializes `CatalogAuthService` before the
  saved `redirectStartPage` is restored. Normal anonymous shell bootstraps still leave MSAL
  lazy, while sign-in and registration return to `/compte` instead of remaining on `/`.
 - `Scan` gates the entire PWA through `ScanAuthService.authState$`: an Entra account with
   `Tri` or `Caisse` renders the PWA (`Tri` triages; `Caisse` sells), while unauthenticated,
   unauthorized, and token-renewal-failure states render `ScanLoginComponent`. `AppModule` awaits
  `MsalService.initialize()` and `src/index.html` declares `<app-redirect>` before MSAL
  roots are bootstrapped, preventing the refresh-time `NG05104`/uninitialized-cache race.
  `msalInterceptorConfig` protects `${environment.apiUrl}/scan/*`; the wildcard is required
  because MSAL Angular 5.3.1 uses strict path matching by default, and `/scan` alone does not
  match nested endpoints such as `/scan/catalog/delta` or `/scan/sessions`.
  The role gate acquires the API-scoped access token silently and reads its `roles` claim;
  `AccountInfo.idTokenClaims` is not sufficient for app roles defined on the API resource
  in this two-registration SPA/API flow. The API remains the authoritative authorization
  boundary.
  Initial session restoration waits for `MsalBroadcastService.inProgress$` to reach
  `InteractionStatus.None` after MSAL initialization and redirect handling. The `checking`
  state then covers only the silent cached-token/role validation, so the Scan login action
  remains available during that check. An active interactive MSAL operation still disables the
  action, and `ScanAuthService.login()` guards against starting a concurrent redirect.
  Both Scan environment files declare the tenant ID and tenant-scoped CIAM authority;
  `ScanAuthService.login()` sends an explicit root `redirectStartPage` through a deferred
  observable so MSAL redirect-start failures are rendered inline instead of being swallowed.
  The production environment uses `https://scan.volepapillondamour.fr` for both redirect and
  post-logout URLs; the deployment workflow and Dockerfile inject the same canonical origin.
- `MauiCashApp` targets `net10.0-android` and uses MSAL.NET 4.88.0. `MsalAuthService` acquires
  `api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user` silently first and falls back to
  interactive sign-in; `AuthHandler` attaches the resulting bearer token to the Refit client.
  The Android callback is handled by `MainActivity` and `MsalActivity` for
  `msal427c90de-bf59-4b01-af63-dc0799248496://auth`.

## Configuration Sources

- Backend config comes from `appsettings.json`, environment-specific settings, and local
  secrets; the AppHost injects SQL Server/Azurite connections and a stable secret-backed
  `Parameters:sql-server-password` for its persisted SQL volume.
- The AppHost keeps Blob storage on Aspire's dynamic connection and leaves
  `AzureWebJobsStorage` to the Functions integration; forcing `UseDevelopmentStorage=true`
  breaks the dynamic Azurite ports.
- `BackOffice` config contains API/site URLs and Entra tenant, client, authority, redirect, and
  scope values. `Website` contains its API URL. Scan derives its LAN API host from port `5257`
  in development and uses the tenant-scoped CIAM authority in both environments.
- `Configure-EntraApps.ps1` merges SPA redirect URIs. The public Catalog and Scan redirects are
  registered while existing local/technical URIs remain; the public Scan role gate was verified.
- Scan uses `@zxing/browser` with `TRY_HARDER` for ISBN/QR camera and photo fallback decoding;
  the HTTPS ACA deployment satisfies the secure-context camera requirement.
- MAUI keeps `VpdSettings.BaseUrl` in `appsettings.json`; its MSAL values are constants in
  `MsalAuthService`. Dockerfiles and OIDC deployment workflows live under each app and `infra/`.
- Frontend images patch production environment values at build time through `API_URL` and
  `WEBSITE_URL`; Docker builds use the `src/` context so `src/SharedUi/` resolves.
- ACA probes check `/health` on port `8080`; the dev SQL parameter is Azure SQL `S1`, 20 DTUs,
  250 GB, with no automatic pause. Azure deployment remains an operational step.

## Build And Test Commands

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`.
- AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`.
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`; Scan also uses
  `npm test -- --watch=false --browsers=ChromeHeadless` and its SSR app has a serve command.
- Docker images build from `src/` with the relevant `Dockerfile` and `API_URL`/`WEBSITE_URL` args;
  ACA Bicep uses `az bicep build` and the subscription deployment command under `infra/aca/`.
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`.

## Practical Warnings

- Never store secrets in memory files or commit local connection strings. Prefer actual config,
  routing, and services over template-oriented Angular README TODOs.
- CI builds the backend, MAUI target, and Angular apps but does not run frontend unit tests;
  those remain local validation. BackOffice retains known Angular signal, bundle, CSS, and
  CommonJS warnings. The CI MAUI request still says `net9.0-android` while the project targets
  `net10.0-android`; a missing SDK can produce local `XA5300`.
- Website SSR route ownership is `src/app/app.routes.server.ts`. Dockerfiles use `npm install`
  in the Linux container context and must retain the `src/` build context for `@vpd/ui`.
- Replace the Infra Flow Sculptor placeholder subscription IDs before a real deployment. A
  persisted Aspire SQL volume can yield `18456` until its stable AppHost password is restored.
- Rider validation may generate `src/Backend/Vole_Papillon_Damour.Domain/artifacts/validation/obj/`;
  the Domain project excludes that tree from SDK default items.
