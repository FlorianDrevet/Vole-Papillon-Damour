# 08 - Runtime And Orchestration

## Deployable Surfaces

- `src/Backend/Vole_Papillon_Damour.Api/` - ASP.NET Core HTTP API
- `src/Backend/Vole_Papillon_Damour.AppHost/` - .NET Aspire AppHost for local orchestration
- `src/BackOffice/` - Angular admin SPA
- `src/Website/` - Angular public SPA
- `src/Catalog/` - Angular SSR public books catalog
- `src/Scan/` - Angular feasibility-probe SPA, deployable as a public HTTPS Container App
- `src/Backend/Vole_Papillon_Damour.Worker/` - .NET isolated Azure Functions account-deletion worker
- `src/MauiCashApp/` - .NET MAUI cashier client

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
- The Functions worker registers only account-deletion processing plus Infrastructure, with API authentication disabled in that host. This keeps Microsoft Identity Web out of the generic Functions dependency graph and avoids resolving ASP.NET endpoint services that do not exist in the worker host.
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

## Public catalog runtime — 2026-09-04

- `src/Catalog/` is a distinct Angular SSR application. Its Docker image is built from
  the `src/` context and serves the compiled browser/server bundles with Node on port
  `8080`; `API_URL` and `DEPLOY_HOST` are build arguments.
- The public catalog Container App is represented in `infra/main.bicep` as
  `vpd-catalog-ca-<environment>`, with its own ACR-pull identity and Application Insights
  resource. In the dev parameter file, `livres.volepapillondamour.fr` and its managed
  certificate are bound through the ACA environment's SNI custom-domain configuration.
- `.github/workflows/catalog-deploy.yml` is manual-only. It resolves the API and catalog
  FQDNs, builds/pushes `vpd-catalog`, and rolls the Container App without changing DNS.

## Public domain runtime — 2026-09-04

- OVH publishes CNAMEs for `livres` and `scan` to their ACA FQDNs, plus the `asuid.livres`
  and `asuid.scan` TXT validation records. DNS resolution was checked from Windows.
- Azure Container Apps reports managed certificates as `Succeeded` and both custom hostnames
  as `Secured`/`SNI SSL`. Both public hostnames are live; the post-merge Scan image contains
  its canonical origin and the public login redirect reaches the role gate.

## Post-merge public rollout — 2026-09-04

- `Catalog - deploy` run `33906639354`, `Scan - deploy` run `33906624368`, and the retried
  `Infra - deploy` run `33906654599` succeeded from `main` commit `9f8ec55`. The first Infra
  attempt failed only because Scan was still provisioning; the retry completed without any
  resource deletion.
- Public smoke checks return `200` for the catalog root, robots, sitemap, and Scan root. A
  Scan login from the canonical host returns to the application and enforces the `Tri` role;
  the current test account is intentionally denied because it lacks that role.
- The full API/Worker runtime was rolled by `Books runtime - deploy` `33908408641` from
  `main` commit `585a0ac`, with the explicit EF migration gate enabled. The migration
  `20260902223842_MigrateUsersToEntraIdentity` intentionally removes legacy `Users` rows and
  was applied after the previously verified backup; the temporary SQL firewall rule was
  removed before the workflow completed.

## Books runtime update — 2026-09-05

- The Worker now exposes `Sweep` every five minutes and `Enrich` hourly. A sweep closes idle scan sessions, attaches undated announcements to the next active Books fair, releases due announcements, delivers due alert outbox messages, and runs account deletion; enrichment resolves pending/not-found bibliographic records with retry and direct HTTPS cover URLs.
- The API now enqueues a newly committed canonical ISBN when its book remains `Pending`.
  A deduplicated in-process service resolves that ISBN asynchronously with a fresh scope,
  while the hourly Worker remains the restart/provider-failure fallback.
- `Books runtime - deploy` is a manual GitHub Actions workflow that builds API and Worker images from one commit, optionally opens the SQL firewall and applies all EF migrations, closes the firewall in cleanup, then updates both Container Apps. The workflow deliberately deploys migrations before the application rollout.
- The API now runs startup migrations only for `Development`. Production and deployed development use the explicit migration step, preventing concurrent API replicas from migrating the database.
- `main.bicep` declares the `book-covers` blob container, `Cors:AllowedOrigins`, Application Insights daily caps, and Azure Monitor alerts for missing Worker heartbeat, late announcements, and a late alert queue. The contact group must still be confirmed by Azure after infrastructure deployment.
- The dev Worker is private, running at `minReplicas: 0`/`maxReplicas: 1`; the old account-deletion timer was observed successfully in the correct subscription tenant. The new `Sweep`/`Enrich` heartbeat must now be verified after a few timer cycles from runtime run `33926622823`.
- The DEV infra what-if `33822673986` reported 5 creates, 24 modifies, 17 no-change, 9 unsupported and 10 ignored changes, with no deletes; the real infra run `33822751659` succeeded. The Azure Monitor contact group still needs a delivery test.
- The independent API smoke after runtime rollout returned `200 Healthy` for `/health`, `200` for catalog search and the catalog sitemap, and `200` for the public catalog/Scan roots. The first historical `/books/9783140464079/metadata` call returned `500` because both bibliographic providers were temporarily unavailable; the API error middleware correction is included in the shared `585a0ac` runtime tag and maps that `HttpRequestException` to `503 Service Unavailable`, while the resolver still throws so the Worker retries instead of recording a negative-cache miss.
- PR #59 adds the domain `RecordMetadataProviderFailure` transition and a one-hour `Pending` retry cooldown. `EnrichPendingBooksCommandHandler` now records provider, cover, invalid-source, and invalid-payload failures, orders never-attempted rows before cooled-down rows, and preserves the `ResolveAttempts` budget reserved for negative-cache misses. Full backend validation passes with 288 tests; runtime `33926622823` deploys the API and Worker from shared tag `dfd8e69` without migrations, and public API/catalog/Scan smoke is green.

## Books runtime update — PR #61 — 2026-09-05

- PR #61 (`dcc0c23`) fixes the provider-specific pending account-deletion lookup for the
  SQLite/Aspire test provider and removes member-only watchlist, alert-history, bounce and
  `AlertEmail` outbox data during local account finalization. The backend suite passes with
  291 tests, including retained-history and delete-without-history regressions.
- `Books runtime - deploy` `33929828651` built and rolled `vpd-api:dcc0c23` and
  `vpd-worker:dcc0c23` from the same commit, with `run_migrations=false`; the job completed
  successfully and its migration/firewall steps were skipped because schema migrations were
  already applied by `33922677695`.
- Post-rollout read-only smoke returned `200` for API health, next fair, metadata, public
  catalog and Scan; anonymous watchlist access returned `401`, private catalog pages carried
  `X-Robots-Tag: noindex, nofollow`, and DNS CNAME/TXT validation records still resolved.
  `Sweep`/`Enrich` heartbeat observation, ACS verification and physical acceptance remain open.

## Catalog SEO correction and rollout — PR #63 — 2026-09-05

- The Catalog SSR shell now defaults to `noindex, nofollow` and `AppComponent` updates the
  robots meta tag on initial load and every navigation: public routes use `index, follow`,
  while `/compte` and `/administration` remain `noindex, nofollow`.
- The route-level regression suite covers private routes, query strings, trailing slashes,
  and public routes. Catalog validation passed with 34 ChromeHeadless tests, a production
  build, and local SSR smoke checks.
- PR #63 (`3a6e887`) passed CI checks `33931556397` and `33931558967`. `Catalog - deploy`
  `33932087193` rolled `vpdacrdev.azurecr.io/vpd-catalog:3a6e887` successfully.
- Live read-only smoke confirms `index, follow` on `/`, both HTML and
  `X-Robots-Tag: noindex, nofollow` on `/compte` and `/administration`, and `200` for
  `/robots.txt` and `/sitemap.xml`. DNS and Entra public redirect configuration were
  unchanged.

## Books cover URL update — 2026-09-06

- The API and Worker no longer register a dedicated book-cover Blob container or upload
  service. Bibliographic enrichment validates direct HTTPS image URLs from BnF, Open Library,
  and Google Books; the resolver requires an exact ISBN-13 match before accepting Google
  metadata/images and treats the known BnF cover HTTP 500 as a provider miss.
- `Bibliographic:GoogleBooksApiKey` is optional and is exposed by `infra/main.bicep` through
  the `google-books-api-key` Key Vault secret. The migration
  `20260906101426_ReplaceBookCoverBlobWithDirectCoverUrl` must run before the API/Worker
  rollout; no Azure deployment has been made from this worktree.
- Catalog and Scan consume the URL and use the shared `VpdBookCoverPlaceholderComponent` when
  the provider chain has no usable image or the browser reports an image error. Local backend
  build and targeted bibliographic/domain/application tests pass; Angular and full-suite
  validation remain the next worktree checks.
