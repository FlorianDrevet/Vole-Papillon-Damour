# 08 - Runtime And Orchestration

## Deployable Surfaces

- `src/Backend/Vole_Papillon_Damour.Api/` - ASP.NET Core HTTP API
- `src/Backend/Vole_Papillon_Damour.AppHost/` - .NET Aspire AppHost for local orchestration
- `src/BackOffice/` - Angular admin SPA
- `src/Website/` - Angular public SPA
- `src/Scan/` - Angular feasibility-probe SPA, deployable as a public HTTPS Container App
- `src/Backend/Vole_Papillon_Damour.Worker/` - .NET isolated Azure Functions account-deletion worker
- `src/MauiCashApp/` - .NET MAUI cashier client

## Entry Points

- Backend entry point: `src/Backend/Vole_Papillon_Damour.Api/Program.cs`
- Aspire AppHost entry point: `src/Backend/Vole_Papillon_Damour.AppHost/Program.cs`
- BackOffice entry path: `src/BackOffice/src/main.ts` -> `app.module.ts`
- Website entry path: `src/Website/src/main.ts` -> `app.module.ts`
- Scan entry path: `src/Scan/src/main.ts` -> `app.module.ts`
- MAUI entry point: `src/MauiCashApp/MauiProgram.cs` and `App.xaml`

## Backend Runtime Pipeline

The API startup wires:

- Swagger only in development
- CORS policy `CorsPolicy`
- custom error handling middleware
- HTTPS redirection
- routing, rate limiting, authentication, authorization
- WebSockets support
- `GET /health`, backed by an Infrastructure database connectivity check, for API liveness and readiness
- endpoint registration through `UseAuthenticationController()`, `UseActualityController()`, `UseProductController()`, `UseOrdersController()`, and `UseEventsController()`

## Multi-Runtime Notes

- The Website consumes the backend SSE stream for event table updates.
- The MAUI client loads its backend base URL from embedded configuration and does not share Angular environment files.
- `MauiCashApp` targets only `net9.0-android`; its current local distribution remains the direct app build, without a durable signing keystore.
- The repository now includes a verified Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/`.
- The AppHost orchestrates the API on port `5257`, Scan on `4202`, BackOffice on `4200`, Website on `4201`, plus local SQL Server and Azurite.
- The AppHost passes the Aspire-generated Blob Storage connection to the API and worker. The Functions worker uses the host-storage connection supplied by `AddAzureFunctionsProject`; it must not be overridden with `UseDevelopmentStorage=true`, because Aspire publishes Azurite on dynamic host ports.
- The Functions worker registers only account-deletion processing plus Infrastructure, with API authentication disabled in that host. This keeps Microsoft Identity Web out of the generic Functions dependency graph and avoids resolving ASP.NET endpoint services that do not exist in the worker host.
- The AppHost SQL Server resource uses `WithDataVolume()`, so it must keep a stable password across launches through the AppHost secret key `Parameters:sql-server-password`; otherwise SQL Server starts but later rejects `sa` logins with `18456` because the persisted master database still expects the older password.
- The AppHost `AddJavaScriptApp(...).WithRunScript("start")` calls pass the `--` separator
  followed by frontend CLI arguments such as `--host` and `--port`; this is required by the
  current Aspire/Angular startup wiring and must be validated if the hosting package changes.
- The backend itself still stays free of `Aspire.*` packages; orchestration concerns live in the AppHost only.
- The API health endpoint is `/health`; local Azure Container Apps probe parameters target it on port `8080` for readiness, liveness, and startup. Website and BackOffice probes remain disabled until their plan specifies health endpoints.
- The Scan image is built from the `src/` context with nginx on port `8080`; `Scan - deploy` injects the public API URL and Application Insights connection string at build time, then rolls `vpd-scan-ca-dev` onto the image. Its deployed ACA HTTPS FQDN is `https://vpd-scan-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io` for the iPhone test.
- The worker is deployed as a native Functions-on-Container-Apps resource (`Microsoft.App/containerApps`, `kind=functionapp`) with a dedicated managed identity, ACR pull, Key Vault secret references, Application Insights, and fixed `minReplicas: 1`/`maxReplicas: 1` until timer scaling is measured. It is intentionally private (no ingress); the timer was verified in Azure with a successful `AccountDeletionSweepFunction` invocation.
- The SQL deployment parameter is now the fixed `S1` Standard tier (20 DTUs, 250 GB, no automatic pause); the Azure resource has not been changed from this workspace.
- Deployment IaC for Azure Container Apps now lives under `infra/` and targets the API, BackOffice, Website, Scan, and Worker surfaces.
- An Infra Flow Sculptor project named `Vole-Papillon-Damour` was created on 2026-05-18 with `dev` and `prod` environments in `FranceCentral`, a shared `rg-vpd-common`, and a separate `VpdApplications` infrastructure config.
- The Infra Flow Sculptor run created ACR and Log Analytics in the project, but ACA environment and Container App auto-creation failed server-side with a compile exception, so the repository-local Bicep template completes that missing part.

## Verified Local Commands

- Backend build: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend tests: `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`. The Scan app
  also needs its `src/SharedUi` link and exposes the LAN-oriented development server on
  port `4202` when started through AppHost.
- ACA Bicep compile: `az bicep build --file .\infra\aca\main.bicep`
- ACA image build/push helper: `.\infra\aca\build-and-push.ps1 -EnvironmentName <dev|prod> -RegistryName <acr> -ApiUrl <url> -WebsiteUrl <url>`
- MAUI build: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net9.0-android`

## Runtime Risks

- Cross-surface changes require validating the API plus at least one client.
- SSE, WebSockets, and rate limiting live in the API startup path and can affect website live views and login behavior.
- The permissive CORS policy means frontend/runtime changes should be reviewed with deployment assumptions in mind.
- Frontend Docker validation now depends on using the `src/` folder as build context so `src/SharedUi/` stays available to both Angular applications during compilation.
