# 08 - Runtime And Orchestration

## Azure environment naming — user-confirmed 2026-10-20

- In the user's current Azure subscription, resources named with `dev` are the user's actual production environment, not a development/test environment. The known production targets include resource group `rg-vpd-dev`, SQL server `vpd-sql-dev`, database `vole-papillon-damour-db`, and Blob Storage account `vpdstdevfc`.
- Treat those resources as production for backup, access, deployment, reset, and data-migration decisions. Never infer that a destructive or test-only operation is safe because a resource name includes `dev`; confirm before any production data replacement or deletion.

## Azure regional migration — plan 2026-10-14

- User-confirmed approach: keep rg-vpd-dev and the existing SQL server/database in place; create the West Europe regional replacements in France Central side by side, then retire the old resources after cutover and a rollback window.
- The planned replacements are the shared ACR, Blob Storage, Key Vault, Container Apps environment/apps, Log Analytics and Application Insights. Bicep must separate resource-group metadata location from resource location and accept the shared ACR name and resource group.
- Existing ACA public hostnames stay on their corresponding new apps: apex/www Website, backoffice BackOffice, livres Catalog, and scan Scan. The six app pipelines, infra pipeline, storage workflow, and ACS configuration workflow need target names/configuration updated.
- Keep SQL/ACS/Foundry and GitHub OIDC resources; do not run EF migrations or copy the SQL database for this regional move. The storage workflow may rewrite only absolute Blob account URLs in the same database if references require it.
- New and old Workers must not run scheduled work at the same time. Move the ACS Event Grid webhook once during cutover. OVH CDN origin and ACA certificate renewal need a tested DNS/TLS path before changing public routing.
- Detailed phase order and gates: [docs/infra/migrations/migration-west-europe-france-central.md](../../docs/infra/migrations/migration-west-europe-france-central.md). This is a plan; no Azure resources were changed.

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
Instagram authentication and quota failures. A successful social import is published
immediately; an empty title or article is rejected before the transaction is committed.
`InstagramFeedClient` and `MediaDownloader` are typed HTTP clients registered by
Infrastructure; title generation is optional and uses `Microsoft.Extensions.AI` with
an Azure managed identity when its Foundry endpoint/deployment are configured.
`InstagramFeedClient` removes copy/paste formatting at the edges and inside the
configured access token before creating the Bearer header; visible non-ASCII
characters still fail with a safe authentication exception.

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

## ISBN metadata import and enrichment

- `ImportAdminBooksCommandHandler` creates new book/inventory rows in a serializable transaction, commits, then enqueues only ISBNs created by that batch through `IBookMetadataEnrichmentQueue`. Existing books remain unchanged.
- The API registers `BookMetadataEnrichmentBackgroundService`; it consumes the in-memory queue with one reader, starts enrichment immediately, and waits one second after each attempt before the next queued ISBN. The queue is not durable, so the hourly `BookEnrichmentFunction` remains the recovery path for pending records after a process restart.
- The Worker’s bulk `EnrichPendingBooksCommand` path also waits one second between candidate ISBNs. These limits serialize work within each host; they are not a distributed rate limiter across separate API and Worker processes.
- The Worker applies `Bibliographic:WorkerBnfTimeoutMilliseconds` to the BnF SRU client, defaulting to 2,000 ms; the API continues to use `Bibliographic:BnfTimeoutMilliseconds` (800 ms by default). This worker-only override was added after live BnF failures timed out at the existing 800 ms threshold; PR #338 is open and has not been deployed.

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
- The DEV SQL deployment parameter now selects the fixed `S0` Standard tier (10 DTUs, 250 GB, no automatic pause). The user reported that the Azure resource was already scaled to 10 DTUs in the portal on 2026-09-22; this worktree aligns the IaC, but has not deployed it.
- DEV API, Website, and Catalog keep `minReplicas: 1`; BackOffice and Scan use HTTP ingress with `minReplicas: 0`, `maxReplicas: 2`, and a three-hour (`10,800` seconds) cooldown before the final replica scales to zero. A request wakes them from zero, while continued activity resets the idle window. `infra/modules/ContainerApp/containerApp.module.bicep` must use API `2025-07-01` or newer for `template.scale.cooldownPeriod`; `2024-03-01` does not include that property in its ARM schema.
- All six DEV apps (API, Website, BackOffice, Scan, Catalog, Worker) use the minimum Consumption allocation: `0.25` vCPU and `0.5Gi` per replica in `infra/parameters/main.dev.bicepparam`. The Worker is a Functions-on-Container-Apps resource and uses the same sizing pair. This setting remains IaC only until `Infra - deploy` is run.
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

## Observability coverage — worktree 2026-09-15

- Sampling traps found and fixed: `Azure.Monitor.OpenTelemetry.AspNetCore` 1.5 defaults to a
  5 traces/s rate limiter (API now calls `AzureMonitorTelemetry.ConfigureSampling`), the worker
  `host.json` had adaptive sampling on, and the Node distro needs `tracesPerSecond: 0`.
- API `appsettings.json` was `Default: Error` in production (no `Information` facts ingested);
  it is now `Information` with EF Core, ASP.NET Core, Identity and HttpClient at `Warning`.
- Worker: `host.json` `telemetryMode: OpenTelemetry` plus `Microsoft.Azure.Functions.Worker.OpenTelemetry`,
  `Azure.Monitor.OpenTelemetry.Exporter`, HttpClient and SqlClient instrumentation (not the ASP.NET
  Core distro, which would duplicate host requests); Bicep sets `OTEL_SERVICE_NAME=vpd-worker`.
- Catalog SSR: `src/Catalog/instrumentation.mjs` loaded with `node --import` (Dockerfile CMD) uses
  `@azure/monitor-opentelemetry` + `@opentelemetry/instrumentation-undici`; server-side only (ENF-14),
  names incoming spans by path and ignores SDK/IMDS hosts; Bicep sets `OTEL_SERVICE_NAME=vpd-catalog`.
  The Azure core pipeline refuses non-HTTPS ingestion endpoints, so local smoke tests need an HTTPS fake.
- Scan, Website and BackOffice register `ApplicationInsightsErrorHandler` (Angular swallows errors
  before `window.onerror`) and restrict W3C correlation headers to the API host.
- Bicep adds alerts (API 5xx, API P95 per operation, slow SQL, repeated backend exceptions, failed
  alert e-mails), Failure Anomalies rules for API/worker/catalog on the project action group, four
  standard availability tests (`availabilityTestsEnabled`), and the shared workbook
  `VPD - Performance et santé` generated by `infra/modules/Monitor/workbooks/generate-performance-workbook.mjs`.
- `monitoringAlertEmail` is `afdrevet@outlook.com`; no Azure infrastructure notification may target
  `volepapillondamour@sfr.fr` (public association contact). Subscription-level notifications
  (billing, Service Health, Defender) are outside Bicep and must be checked in the portal.
- The Worker project resolves its unversioned `Azure.Functions.Sdk` through the repository-root
  `global.json`. Both the manual Books runtime and CI workflows use `src/Backend` as Docker context,
  so they copy the root file into that context and the Worker Dockerfile copies it to `/src` before
  restore. Keep both workflow copies and the Dockerfile `COPY` together if the context or SDK mapping changes.

## Monitoring workbook expansion — worktree 2026-10-20

- The shared Bicep workbook now has Embeddings and costs plus Infrastructure tabs. Bicep injects
  resource IDs for the six Container Apps and the existing SQL database; no Azure deployment was
  performed as part of the PR.
- `Vpd.Recommendations` emits one `books.recommendations.embedding.generate` dependency span per
  Azure OpenAI batch with outcome, input count, dimensions, deployment model, provider-reported input
  token count, and duration. It deliberately excludes text, ISBNs, and book identifiers. API and
  Worker export this ActivitySource through their existing Azure Monitor OpenTelemetry setup.
- The workbook estimates embedding input cost using `embeddingCostUsdPerMillionTokens` (string,
  default `0.02` USD/million) and counts batches without token usage separately. This is not invoice
  data; adjust the default for the tenant/model/region and compare with Azure Cost Management.
- Infrastructure charts query ACA CPU/memory percentages, replicas/restarts and SQL DTU maximum;
  CPU/memory are per replica and may be absent when an app is scaled to zero. The workbook is
  generated from `infra/modules/Monitor/workbooks/generate-performance-workbook.mjs`; after Bicep
  deploy, verify charts/queries against live telemetry and the actual metric series in the portal.
- Azure Monitor `AppRoleName` values can include the Container Apps environment and app resource
  name (for example, `[vpd-cae-dev-fc]/vpd-api-ca-dev-fc`), so workbook role filters must use
  substring matching rather than equality with `OTEL_SERVICE_NAME`. Keep literal
  `{OriginalFormat}` out of workbook KQL; construct the dynamic property key with `strcat` so the
  workbook parameter parser does not treat it as a parameter. Empty embedding panels only mean
  that no matching custom embedding spans were ingested in the selected period; recommendation
  recomputation can reuse stored vectors and therefore skip `EmbedAsync`. Azure OpenAI token
  metrics are aggregate usage and do not attribute tokens/cost to a specific recommendation run.

## DEV observability cost tuning — 2026-09-22

- The DEV Bicep parameters disable the four synthetic availability tests without removing
  their resources: `availabilityTestsEnabled = false` is passed into both the web test and
  its metric alert so an incremental deployment also disables resources created earlier.
- DEV scheduled-query alerts use `PT6H`, with a six-hour KQL lookback where a query embeds
  `ago()`: four evaluations per day instead of the default `PT5M`. Failure Anomalies uses a
  fixed `PT1M` cadence, matching Microsoft's Bicep sample for `FailureAnomaliesDetector`;
  deployment `35858128356` rejected `PT5M` for all three detector rules.
- Application Insights ingestion sampling is 25% for Website, BackOffice and Catalog in
  DEV; API, Worker and Scan remain at 100% because their telemetry feeds operational
  diagnosis and alerts. Runtime OpenTelemetry sampling remains explicitly at 100%.
- This is prepared in Bicep only; Azure deployment and a post-deployment cost check remain
  pending. Platform/container diagnostic logs are not reduced by this Application Insights
  sampling setting.

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
- 2026-09-12 incident: API deploy runs `34717607127` and `34718378378` rolled out commit
  `0443c5d` while skipping the explicit EF migration step, leaving the Catalog watchlist API
  incompatible with the newly added `WatchlistItem` metadata columns and returning `500`.
  Both backend deployment workflows now default `run_migrations` to `true`; API deploy
  `34721709446` subsequently applied `20260912214304_AddWatchlistItemCoverUrl` before
  rolling out `357eadc`, so the known schema incident is resolved. A later authenticated
  `/compte` reproduction generated no `/catalog/me/watchlist` HTTP log at all, identifying
  the remaining failure as pre-API MSAL token acquisition rather than another server `500`.
  The guardrail is tracked in [PR #157](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/157).
- ACS delivery is enabled in DEV after domain verification; the domain is verified but DMARC is
  `NotStarted`, and a real authorized-recipient delivery test remains open.
- ACS runtime wiring is now declared by `infra/main.bicep`, not only by the manual ACS
  workflow. Keep `communicationEmailCreateResources=false` after the customer-managed
  domain is verified, provide `ACS_EMAIL_WEBHOOK_SECRET` to `Infra - deploy`, and enable
  `bookAlertsEmailEnabled` only after a real API image is running. The deployment script
  waits for the API's Key Vault secret reference before ARM creates the Event Grid webhook,
  avoiding the 503 validation race from run `35098468030`.
- Bibliographic covers found by enrichment continue to use validated direct HTTPS provider URLs.
  Catalog administrators can upload a manual override to the public `book-covers` Blob container;
  the same `Books.CoverUrl` field stores its URL and the manual metadata lock protects it from
  later enrichment. The container is provisioned by `infra/main.bicep`; after merge, deploy Infra,
  then API, then Catalog. No EF migration is needed. Migration
  `20260906101426_ReplaceBookCoverBlobWithDirectCoverUrl` is present in the deployed main line.
- Catalog SSR keeps public routes indexable and private account/admin routes `noindex, nofollow`;
  this behavior and the robots/sitemap smoke checks are retained as deployment invariants.

## Azure regional migration

- `infra/main.bicep` keeps the existing `rg-vpd-dev` resource group and provisions parallel regional resources with `-dev-fc` names in France Central. SQL server/database, ACS Communication Service, and the Azure OpenAI account are declared as existing resources so their configuration is not reapplied.
- Shared ACR `vpdacrshared` is in `rg-core-florian`, France Central, Basic, public network enabled, and admin authentication disabled. Its name, resource group, subscription, and CI principal are parameterized; six new ACA identities receive `AcrPull`, and the GitHub deployment identity receives `Owner` on the registry.
- GitHub application workflows target the `-dev-fc` Container Apps and shared ACR. `ACR - migrate images` imports and compares all tagged repositories from `vpdacrdev`; the old registry remains available for rollback.
- `Storage - migrate blobs` copies from `vpdstdev` to `vpdstdevfc`, preserves each source container ACL, copies only missing or source-newer blobs, leaves newer/extra target blobs intact, validates that every source blob name exists at the target, and defaults database URL rewriting to `skip`. Its `preview` mode reports old Blob URL references before the separate `apply` mode can change them.
- The OVH zone now routes the apex A record and `www`, `backoffice`, `livres`, and `scan` CNAMEs directly to the France Central ACA environment. All five domains have secured managed certificates and returned HTTP 200 with TLS verification successful. No active OVH CDN service was identified in the account; the former apex IP belonged to the old West Europe ACA environment.
- Shared registry `vpdacrshared` in `rg-core-florian` now serves this repo and `mariage-edwige-henri`. The wedding project's 99 existing tags were imported with digests verified, and its backend/frontend GitHub workflows succeeded with OIDC. Keep the old `acrweh` and `vpdacrdev` registries and all other West Europe resources until the owner explicitly confirms deletion.

## Cloudflare proxy preparation — 2026-10-20

The `volepapillondamour.fr` zone is active on Cloudflare Free. OVH accepted the nameserver
change, and the `.fr` parent now publishes `david.ns.cloudflare.com` and
`monroe.ns.cloudflare.com`; recursive resolvers may still have different cached answers during
propagation. Cloudflare has the 27 non-NS records copied from OVH. The five website records —
apex, `www`, `backoffice`, `livres`, and `scan` — still point to the France Central ACA and
remain DNS-only. Mail and authentication records also remain DNS-only.

A Cloudflare Origin CA certificate for the apex and `*.volepapillondamour.fr` was issued with a
15-year validity. Its certificate and matching private key were used locally to create and
validate a password-protected PFX. The PFX has not been transferred to Azure. Importing it into
the `vpd-cae-dev-fc` Container Apps environment (`rg-vpd-dev`) requires explicit action-time
confirmation because the PFX contains the private key. The feature branch adds an optional
`cloudflareOriginCertificateName` binding in Bicep for the five ACA custom hostnames; DEV expects
`cf-origin-volepapillondamour`.

Cloudflare Images Transformations are enabled on the Free plan and show 0/5,000 unique
transformations used. The permitted external image origins are `openapi.bnf.fr`,
`covers.openlibrary.org`, and `books.google.com`, in addition to the zone's own origin. Images
Paid and Polish are not enabled. Do not add `robots.txt` bot-preference synchronization; the AI
bot setting remains off. The Catalog feature branch builds transformation URLs for these sources
and falls back to the direct image URL after a transformation error.

GitHub Actions run 39482297824 completed successfully as an Azure `what-if` with
`recommendations_enabled=true`; the deployment step was skipped. Azure reported 32 resources to
modify, 40 unchanged, 12 unsupported, and 21 ignored, with no creates or deletes. The preview
keeps `Recommendations__Enabled=True` in the API and Worker configurations. No Azure certificate
import, infrastructure deployment, application deployment, or DNS proxying has happened yet.
Keep all five website records DNS-only until the certificate is imported and the Bicep deployment
has completed. After that, proxy only those five website records, keep mail/authentication
DNS-only, deploy Catalog, and smoke-test all five hostnames and image fallback. Keep the zone
and Images on Free; do not enable paid products.

## Book enrichment SQL collision and resume checkpoint — 2026-10-23

- The Worker revision `vpd-worker-ca-dev-fc--0000021` is healthy with one replica, but there is no
  running `EnrichCatalogOrchestration` instance. Instance
  `391b981d91f542f79a54a4360eefcf98` failed at 2026-10-23 10:31:43 UTC after batch 75 completed
  (3,750 books). Its failure was `PK_BookGenres` inserting the same ISBN/genre twice, with SQL
  reporting `(9782266214827, Littérature française)`.
- Open Library returned equivalent spellings including `Littérature française` and
  `Litterature francʹaise`, plus `Science-fiction`/`Science fiction` variants. The old
  `CompareInfo` equivalence differed between Windows NLS and Linux ICU, so the worker could retain
  both genre names before SQL's case/accent-insensitive primary key check.
- The repair uses deterministic Unicode normalization and deduplicates accents, whitespace,
  dashes, ordinary apostrophes, and transliteration modifier marks while retaining meaningful
  symbols and other punctuation. Resume endpoint `POST /api/Enrich/resume` takes the original
  `FullCatalogRefreshStartedAt` and `LastCompletedBatchNumber`; restarting with the original
  timestamp excludes books already refreshed after the run began.
- Exact restart input: `FullCatalogRefreshStartedAt = 2026-10-23T07:18:40.030384Z` and
  `LastCompletedBatchNumber = 75`. The code is prepared on a task branch only; no deployment or
  resumed orchestration has happened. PR [#337](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/337)
  is open with six checks pending. `rg-vpd-dev` is the user's production environment despite its
  name; after merge, run the manual Worker deployment and verify the new instance after rollout.
