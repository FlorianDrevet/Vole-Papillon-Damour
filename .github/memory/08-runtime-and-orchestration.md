# 08 - Runtime And Orchestration

## Production and regional resources

- Resources named `dev` are the user's production environment. Important data targets include `rg-vpd-dev`, SQL server `vpd-sql-dev`, database `vole-papillon-damour-db`, and Blob account `vpdstdevfc`. Treat deployments, backups, data edits, and resets accordingly.
- Azure replacements are in France Central with `-dev-fc` names; SQL, ACS, and Azure OpenAI remain existing resources. The website hosts route directly to the France Central ACA environment. Shared ACR `vpdacrshared` is in `rg-core-florian` and is also used by a second project.
- Keep old West Europe resources/registries until the owner explicitly confirms deletion after cutover and rollback window. Do not run old and new scheduled Workers simultaneously; move the ACS Event Grid endpoint once during cutover.
- Cloudflare Free is authoritative DNS. The apex, `www`, `backoffice`, `livres`, and `scan` website records point to France Central but remain DNS-only. Mail/auth records remain DNS-only. The Origin CA certificate/PFX has not been transferred to Azure; keep proxying off until certificate import and infra deployment are explicitly completed.
- Detailed sequence and resource map: `docs/infra/migrations/migration-west-europe-france-central.md`.

## Runtime surfaces

- API: `src/Backend/Vole_Papillon_Damour.Api/`; local orchestration: `Vole_Papillon_Damour.AppHost/`; background jobs: isolated .NET Worker. Angular delivery surfaces are BackOffice, Website, Catalog, and Scan; `MauiCashApp` is Android-only.
- Aspire AppHost starts API at 5257, BackOffice at 4200, Website at 4201, Scan at 4202, and Catalog at 4203, plus local SQL Server and Azurite. Its persisted SQL data volume requires a stable secret-backed `Parameters:sql-server-password`.
- Keep the backend free of `Aspire.*`; orchestration belongs to AppHost. Let Aspire provide dynamic Azurite connection strings; do not force `UseDevelopmentStorage=true` or override the Functions host-storage connection.
- API `/health` checks database connectivity and ACA probes use port 8080. The Worker is a native Functions-on-Container-Apps resource for account deletion, Books Sweep/Enrich, and alert delivery. Deployment workflows own migrations before API/Worker rollout.
- Website SSR's current repair branch raises only Website from the earlier `.25 vCPU / .5 GiB` minimum to `.5 vCPU / 1 GiB`; it has not been deployed. ACA's default HTTP scaling rule targets 10 concurrent requests when no custom rule is set. See `.github/memory/19-website-event-ssr.md` for the separate tested SSR HTTP and prior SSE loads.
- The DEV SQL IaC target is Standard S0 (10 DTU, 250 GB, no auto-pause). Bicep changes do not take effect until `Infra - deploy` runs.

## Worker and Books processing

- The Worker handles account-deletion outbox, Books Sweep/Enrich, email alerts, social actuality import, and ISBNdb catch-up. API metadata enqueue is an in-memory single-reader queue with a one-second delay; the hourly Worker job is the restart recovery path. Per-process pacing is not a distributed rate limiter.
- Worker BnF timeout defaults to 2 seconds; API BnF timeout defaults to 800 ms. ISBNdb catch-up is a Durable `202 Accepted` flow, not a long synchronous request. Its invocation key is provisioned in a dedicated Key Vault; use the scripts in `infra/scripts/` and never print credentials or Durable management URLs.
- API and Worker role filters/telemetry are configured separately. The Worker uses its managed identity for Blob, ACS, and Key Vault; avoid adding API authentication dependencies to its host.

## Deployment and observability

- `infra/main.bicep` owns ACA, managed identities, Key Vault references, diagnostics, domains, and the ACS Event Grid path. Build frontend images from `src/` so the `src/SharedUi/` path alias is available.
- Catalog and Website are SSR Container Apps on port 8080; Catalog keeps public routes indexable and private account/admin routes `noindex,nofollow`. The Website memory diagnostic records contain route without query, duration/status, active SSR count, and Node RSS/heap/external/array-buffer snapshots. Never add headers, IPs, or query strings.
- Application Insights custom usage spans and workbook cost values are estimates; compare with Azure Cost Management. Low app-level request telemetry does not measure server-side renders unless SSR logs are emitted.
- Cloudflare Images Free transformations allow only the configured BnF/Open Library/Google Books hosts plus the zone; the monthly cap is 5,000 unique variants. Direct source fallback must remain available.

## Verified local commands

- `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular: `npm install`, `npm run start`, `npm run build`, `npm test`; run from the target app.
- Bicep: `az bicep build --file .\infra\main.bicep`; `az bicep build-params --file .\infra\parameters\main.dev.bicepparam`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`.
