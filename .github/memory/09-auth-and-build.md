# 09 - Auth And Build

## API authentication

- The API uses a composite Bearer scheme: Entra External ID tokens go to `Entra`; legacy `/auth/login` JWTs go to `LegacyJwt` while old clients are retired. `IsAdmin` remains a compatibility policy.
- Entra API app-role claims are named `roles`; inbound claim mapping is disabled so `RequireRole` sees `Tri`, `Caisse`, `Administration`, and `LivresRares`. `RareBooks` accepts `LivresRares`, `Administration`, or legacy `Admin`; Scan accepts `Tri`/`Caisse` as appropriate.
- For Entra v2 access tokens, `AzureAd__Audience` is the bare API application ID. The delegated scope remains `api://<id>/access_as_user`.
- `POST /auth/login` is public and rate-limited. `POST /auth/register` is currently public despite a commented authorization line; inspect before changing auth assumptions.
- `/accounts/admin` uses the app-only Microsoft Graph adapter. Account enable/disable and password reset need delegated app permissions plus the appropriate manually assigned Entra directory role; provisioning scripts do not grant elevated directory roles implicitly.

## Local Keycloak personas

- Aspire AppHost imports `src/Backend/Vole_Papillon_Damour.AppHost/Realms/vpd-realm.json` into a persistent Keycloak volume. Realm import runs only on an empty volume; to reload fixture changes, reset only `keycloak-data`, never SQL.
- API Development uses `Authentication:Provider=Keycloak`, realm `vpd`, audience `vpd-api`, and the live HTTPS endpoint passed by AppHost. Aspire may randomize ports; resolve them through `aspire describe` rather than assuming port 8080.
- `tools/Get-LocalApiToken.ps1 -Persona admin|tri-volunteer|cashier|member` uses the local-only `vpd-local-test` direct-grant client. Do not use the fixture client outside local API tests.
- The local `vpd-api-access` client scope maps email and first/last-name claims needed by member endpoints. Four personas cover `Administration`, `LivresRares`, `Tri`, and `Caisse`; local role smoke should assert anonymous 401, member 403 on admin routes, and admin success.
- The AppHost Keycloak admin password is a .NET User Secret. In Azure Bicep explicitly selects Entra; local Keycloak does not change production identity.
- BackOffice local browser sign-in uses public client `vpd-backoffice-local` with Authorization Code + PKCE S256. Website, Catalog, and Scan continue to use Entra locally.

## Angular/MSAL behavior

- BackOffice, Catalog, and Scan use API access tokens for authorization roles; never trust the ID token's role claims. The backend remains the authorization boundary.
- BackOffice uses MSAL Angular and Axios, not `MsalInterceptor`. `AppModule` waits for MSAL initialization; `<app-root>` and `<app-redirect>` must both be in `index.html`. `ApiAccessTokenService` waits for `InteractionStatus.None`, selects a sole cached account only when unambiguous, retries one 401 with a forced token refresh, and does not retry 403.
- Never bootstrap the BackOffice Angular root inside an MSAL hidden iframe: the root URL doubles as silent redirect URI and could start a second router/MSAL flow before Axios reaches the API.
- Catalog and Scan use the same active-account rule and should request `prompt: 'select_account'` when several accounts are cached with none active. Interaction-required errors can cross bundled module boundaries; classify by MSAL error codes as well as class identity.
- Scan uses the tenant-scoped CIAM authority; its MSAL interceptor must protect `/scan/*` and `/rare-books/*`, including nested routes. Its role gate reads `roles` from an API-scoped token.
- Production UI authentication remains Entra External ID. Local BackOffice Keycloak configuration belongs only in development environment/realm files.

## Configuration and commands

- API config comes from appsettings, environment variables, and local secrets. Aspire injects SQL/Azurite; keep SQL's persisted-volume password stable via its User Secret and let the Functions integration provide its own host-storage connection.
- Frontend API/site URLs and Entra values are environment/build inputs. Docker builds use `src/` as context for `@vpd/ui`; never commit local connection strings or tokens.
- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`.
- AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`.
- Angular: run `npm run build` or `npm test` from `src/BackOffice`, `src/Website`, `src/Catalog`, or `src/Scan`. Frontend unit tests remain a local validation step.
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`; requires the Android SDK. The CI workflow may target `net9.0-android` stale config—check project TFM when interpreting CI failures.
- Existing Angular bundle/style/CommonJS warnings are known; report new warnings separately. Check `NEXT.md` before operational deployments or data-changing jobs.
