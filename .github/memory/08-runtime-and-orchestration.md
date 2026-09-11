# 08 - Runtime And Orchestration

## Deployable Surfaces

- `src/Backend/Vole_Papillon_Damour.Api/` - ASP.NET Core HTTP API
- `src/Backend/Vole_Papillon_Damour.AppHost/` - .NET Aspire AppHost for local orchestration
- `src/BackOffice/` - Angular admin SPA
- `src/Website/` - Angular public SPA
- `src/Catalog/` - Angular SSR public books catalog
- `src/Scan/` - Angular Scanette PWA for offline triage, consultation, and cash sales, deployable as a public HTTPS Container App
- `src/Backend/Vole_Papillon_Damour.Worker/` - .NET isolated Azure Functions worker for account deletion, Books Sweep/Enrich, and alert delivery
- `src/MauiCashApp/` - .NET MAUI cashier client

### Worker social import

`src/Backend/Vole_Papillon_Damour.Worker/SocialImportFunction.cs` adds the
`ImportSocialActualities` timer trigger using `%SocialImport:Schedule%`. It creates a
scope, dispatches `ImportSocialActualitiesCommand`, logs counters, and distinguishes
Instagram authentication and quota failures. `InstagramFeedClient` and
`MediaDownloader` are typed HTTP clients registered by Infrastructure; title generation
is optional and uses `Microsoft.Extensions.AI` with an Azure managed identity when its
Foundry endpoint/deployment are configured.

## Entry Points

- Backend entry point: `src/Backend/Vole_Papillon_Damour.Api/Program.cs`
- Aspire AppHost entry point: `src/Backend/Vole_Papillon_Damour.AppHost/Program.cs`
- BackOffice entry path: `src/BackOffice/src/main.ts` -> `app.module.ts`
- Website entry path: `src/Website/src/main.ts` -> `app.module.ts`
- Catalog entry path: `src/Catalog/src/main.ts` -> `app.module.ts`; SSR entry is `src/Catalog/src/server.ts`
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
- `MauiCashApp` targets only `net10.0-android`; its current local distribution remains the direct app build, without a durable signing keystore.
- The repository now includes a verified Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/`.
- The AppHost orchestrates the API on port `5257`, Scan on `4202`, BackOffice on `4200`, Website on `4201`, Catalog on `4203`, plus local SQL Server and Azurite.
- The AppHost passes the Aspire-generated Blob Storage connection to the API and worker. The Functions worker uses the host-storage connection supplied by `AddAzureFunctionsProject`; it must not be overridden with `UseDevelopmentStorage=true`, because Aspire publishes Azurite on dynamic host ports.
- The Functions worker registers account deletion, Books Sweep/Enrich, and alert delivery plus Infrastructure, with API authentication disabled in that host. This keeps Microsoft Identity Web out of the generic Functions dependency graph and avoids resolving ASP.NET endpoint services that do not exist in the worker host.
- The AppHost SQL Server resource uses `WithDataVolume()`, so it must keep a stable password across launches through the AppHost secret key `Parameters:sql-server-password`; otherwise SQL Server starts but later rejects `sa` logins with `18456` because the persisted master database still expects the older password.
- The AppHost `AddJavaScriptApp(...).WithRunScript("start")` calls pass the `--` separator
  followed by frontend CLI arguments such as `--host` and `--port`; this is required by the
  current Aspire/Angular startup wiring and must be validated if the hosting package changes.
- The backend itself still stays free of `Aspire.*` packages; orchestration concerns live in the AppHost only.
- The API health endpoint is `/health`; local Azure Container Apps probe parameters target it on port `8080` for readiness, liveness, and startup. Website and BackOffice probes remain disabled until their plan specifies health endpoints.
- The Scan image is built from the `src/` context with nginx on port `8080`; `Scan - deploy` injects the public API URL, Application Insights connection string, and canonical browser origin at build time, then rolls `vpd-scan-ca-dev` onto the image. Its secured public hostname is `https://scan.volepapillondamour.fr`; the deployed ACA HTTPS FQDN remains a technical fallback.
- The worker is deployed as a native Functions-on-Container-Apps resource (`Microsoft.App/containerApps`, `kind=functionapp`) with a dedicated managed identity, ACR pull, Key Vault secret references, Application Insights, and a `P1-1` measurement target of `minReplicas: 0`/`maxReplicas: 1`. It is intentionally private (no ingress); the timer was previously verified in Azure with a successful `AccountDeletionSweepFunction` invocation, and the zero-replica behavior still needs the two-hour observation.
- The SQL deployment parameter is now the fixed `S1` Standard tier (20 DTUs, 250 GB, no automatic pause); the Azure resource has not been changed from this workspace.
- Deployment IaC for Azure Container Apps now lives under `infra/` and targets the API, BackOffice, Website, Scan, Catalog, and Worker surfaces, including SNI bindings for the catalog and Scan custom domains when their managed certificate names are supplied.
- An Infra Flow Sculptor project named `Vole-Papillon-Damour` was created on 2026-05-18 with `dev` and `prod` environments in `FranceCentral`, a shared `rg-vpd-common`, and a separate `VpdApplications` infrastructure config.
- The Infra Flow Sculptor run created ACR and Log Analytics in the project, but ACA environment and Container App auto-creation failed server-side with a compile exception, so the repository-local Bicep template completes that missing part.

## Verified Local Commands

- Backend build: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend tests: `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`. The Scan app
  also needs its `src/SharedUi` link and exposes the LAN-oriented development server on
  port `4202` when started through AppHost.
- ACA Bicep compile: `az bicep build --file .\infra\main.bicep`; dev parameters: `az bicep build-params --file .\infra\parameters\main.dev.bicepparam`
- ACA image build/push helper: `.\infra\aca\build-and-push.ps1 -EnvironmentName <dev|prod> -RegistryName <acr> -ApiUrl <url> -WebsiteUrl <url>`
- MAUI build: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`

## Runtime Risks

- Cross-surface changes require validating the API plus at least one client.
- SSE, WebSockets, and rate limiting live in the API startup path and can affect website live views and login behavior.
- The permissive CORS policy means frontend/runtime changes should be reviewed with deployment assumptions in mind.
- Frontend Docker validation now depends on using the `src/` folder as build context so `src/SharedUi/` stays available to all Angular applications during compilation.

## Scan observability — worktree 2026-09-07

- The worktree adds the `Vpd.Books` `ActivitySource`/`Meter` with spans for
  `books.metadata.resolve`, `books.metadata.provider`, and `books.scan.persist`, plus duration
  histograms for provider calls, full resolution, and SQL-backed scan persistence.
- Provider/resolution spans carry ISBN, provider/source, and outcome tags; persistence spans
  correlate session, client gesture, ISBN, decision, and persistence outcome. Metric tags stay
  low-cardinality around provider/source and outcome.
- The API exports the source and meter through Azure Monitor OpenTelemetry when its Application
  Insights connection string is configured, and Bicep sets `OTEL_SERVICE_NAME=vpd-api`.
- The Scan browser configuration enables W3C/CORS correlation for cross-origin API calls, and
  `infra/main.bicep` adds a scheduled alert for metadata requests slower than three seconds.
- The Kusto diagnostic procedure is documented in `docs/bourse-aux-livres/technique/11-observabilite.md`;
  local backend/Scan validation and Bicep compilation pass; Azure deployment and live
  workspace verification remain pending.

## Current DEV runtime — 2026-09-07

- `src/Catalog/` is the public Angular SSR catalog. Its image is built from the `src/` context,
  serves browser/server bundles on port `8080`, and receives `API_URL`/`DEPLOY_HOST` at build
  time. `infra/main.bicep` declares its Container App, identity, telemetry, and SNI binding.
- The manual Catalog workflow rolls `vpd-catalog`; OVH CNAME/TXT validation records and the
  managed certificates for `livres.volepapillondamour.fr` and `scan.volepapillondamour.fr` are
  verified, with both custom hostnames secured.
- Main commit `5601c2e` is the recorded DEV deployment baseline. The associated infra, API/Worker,
  Website, BackOffice, Scan, Catalog, and ACS workflow runs completed; read-only smoke returned
  `200` for health and the public Catalog/Scan surfaces. The current operational checks are the
  Worker `Sweep`/`Enrich` heartbeat, an authorized-recipient ACS email, QT-02, and physical QT-08.
- The Worker is a private native Functions-on-Container-Apps resource with managed identity,
  Key Vault, ACR, and Application Insights. `Sweep` runs every five minutes; `Enrich` hourly;
  account deletion and alert delivery are also registered. The target remains `minReplicas: 0`/
  `maxReplicas: 1`; API authentication is intentionally absent from this host.
- `Books runtime - deploy` builds API and Worker from one commit, applies EF migrations before
  rollout when requested through the temporary SQL firewall rule, and cleans up that rule. API
  startup migrations run only in `Development`; deployed environments use the explicit workflow.
- ACS delivery is enabled in DEV after domain verification; the domain is verified but DMARC is
  `NotStarted`, and a real authorized-recipient delivery test remains open.
- Bibliographic covers now use validated direct HTTPS provider URLs; the dedicated Blob cover
  container/upload path is gone. Migration `20260906101426_ReplaceBookCoverBlobWithDirectCoverUrl`
  is present in the deployed main line; rollout did not rerun EF migrations because DEV was current.
- Catalog SSR keeps public routes indexable and private account/admin routes `noindex, nofollow`;
  this behavior and the robots/sitemap smoke checks are retained as deployment invariants.
