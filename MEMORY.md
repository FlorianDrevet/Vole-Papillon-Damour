# Project Memory - Vole-Papillon-Damour

> Index file. Detailed knowledge lives in thematic files under `.github/memory/`.

## Stack Snapshot

- Open-source repository for the association application
- Backend: ASP.NET Core Web API on .NET 10 under `src/Backend/`
- Local orchestration: .NET Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/` for API, Website, BackOffice, Catalog, SQL Server, and Azurite
- Architecture: layered CQRS with `Domain`, `Application`, `Infrastructure`, `Api`, and `Contracts`
- Web frontends: Angular 21 applications in `src/BackOffice/`, `src/Website/`, the public SSR catalog in `src/Catalog/`, and the offline scan/consultation/cash-sale PWA in `src/Scan/`
- Native and background clients: .NET MAUI Android cash app in `src/MauiCashApp/` and the .NET isolated Worker under `src/Backend/` for account deletion, Books `Sweep`/`Enrich`, and alert delivery
- Tests: xUnit coverage spans the backend test projects; frontend ChromeHeadless suites remain a local validation step
- CI/CD: `.github/workflows/ci.yml` is configured for backend, MAUI, and frontend builds; frontend unit tests remain a local validation step. Catalog release is manual through `.github/workflows/catalog-deploy.yml`.

## Thematic Memory

| File | Content |
|------|---------|
| `.github/memory/01-solution-overview.md` | Scope, runtime surfaces, critical flows |
| `.github/memory/02-project-structure.md` | Folder map and ownership boundaries |
| `.github/memory/03-domain-model.md` | CQRS slices, aggregates, contracts, API flow |
| `.github/memory/04-frontend.md` | Angular and MAUI client conventions |
| `.github/memory/05-data-and-storage.md` | Persistence, auth, external services |
| `.github/memory/06-agents-skills.md` | Generated agents, skills, and routing rules |
| `.github/memory/07-code-graph.md` | Graphify workflow and code graph notes |
| `.github/memory/08-runtime-and-orchestration.md` | Entry points, runtime surfaces, Azure hosting, and regional migration decisions |
| `.github/memory/09-auth-and-build.md` | Auth behavior, config sources, and verified build/test commands |
| `.github/memory/10-api-endpoints.md` | Grouped HTTP endpoints, public/admin split, and live routes |
| `.github/memory/11-frontend-design-system.md` | Angular Material, Tailwind, theme tokens, and UI guardrails |
| `.github/memory/12-manual-book-requests.md` | F-12 domain, privacy, rollout, and validation notes |
| `.github/memory/13-book-recommendations.md` | Similar-book recommendation defaults, API filtering, and Catalog carousel behavior |
| `.github/memory/changelog.md` | Memory changelog |
| `.github/memory/dream-state.md` | Dream gate state and code graph choice |
| `.github/memory/14-scan-metadata-latency.md` | Dated provider telemetry, ISBN error triage, and the Google Books key issue |
| `.github/memory/15-isbndb-migration.md` | ISBNdb-first book notices, quota, catch-up, Catalog/Scan flows, infra and validation |
| `.github/memory/16-catalog-performance.md` | Deployed Catalog request latency, admin statistics optimization, and remaining SQL investigation |
| `.github/memory/17-worktree-dependency-sharing.md` | Clone-local Git hook for reusing frontend dependencies across worktrees |
| `.github/memory/18-halloween-loto-board.md` | Halloween live-board API contract, Website theme wiring, motion behavior, validation, and rollout |

## Quick Reference

1. Start from `@dev` for day-to-day work; use `@memory-bootstrap` only when the stack or agent foundation changes materially.
2. Use Graphify (`GRAPH_REPORT.md`, `query`, `path`, `explain`, or MCP) for structural exploration before changing shared handlers, repositories, controllers, route extensions, and Angular shared services.
3. The API layer uses endpoint-mapping extension classes under `Api/Controllers/`, not MVC `ControllerBase` classes. Mark JSON DTO bodies on `DELETE` routes with `[FromBody]` so endpoint discovery does not fail at startup; see `.github/memory/10-api-endpoints.md`.
4. Apply TDD for executable code; record temporary exceptions in `.github/test-debt.md`.
5. Keep backend changes inside the existing boundaries: API wiring in `Api`, MediatR handlers and validators in `Application`, persistence and adapters in `Infrastructure`, invariants in `Domain`, DTOs in `Contracts`.
6. Preserve the current frontend split: `BackOffice` for admin surfaces, `Website` for association content, `Catalog` for the public books catalog, and `MauiCashApp` for the cashier client.
7. Do not assume every mutating HTTP route is admin-protected; check `10-api-endpoints.md` before touching auth-sensitive behavior.
8. Every paginated or searchable list must query the API: send the filters, page and page size to the backend and render only the returned page; never load the full collection into the browser for client-side pagination or search.
9. Raw SQL against `Books` must use persisted column `Isbn13`; `BookConfiguration` maps the domain key `Book.Id` to that column.
10. Use `.agents/skills/book-data-reset/SKILL.md` for an explicitly requested deployed books-data reset; it preserves events/news and requires a verified PITR restore path.
11. The France Central migration is planned in `docs/infra/migrations/migration-west-europe-france-central.md`; keep rg-vpd-dev and SQL in place, deploy regional replacements in parallel, and do not remove West Europe resources before the rollback window ends.
12. A completed loto sets `CurrentPartieIndex` to the party count; `RemoveLastNumeroCommandHandler` rewinds to the last party before undoing the final draw. See `.github/memory/03-domain-model.md`.
13. Use `.agents/skills/vpd-prod-backup/SKILL.md` for an explicitly requested local backup. The Azure resources named `rg-vpd-dev`, `vpd-sql-dev`, and `vpdstdevfc` are the user's production environment; do not treat them as disposable development resources.
14. Open Library reference search can aggregate publishers across editions; values over the 200-character watchlist limit are omitted before follow. See `.github/memory/10-api-endpoints.md`.
18. Local Aspire authentication uses the imported Keycloak `vpd` realm and test personas; Azure deployments continue to use Entra. See `.github/memory/09-auth-and-build.md`.

15. Seven-day App Insights data showed metadata API p95 10.9 s; the Open Library latency fix is in [PR #345](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/345) (merged 2026-10-26). Google Books returned `API_KEY_INVALID` on 2026-10-23 and still needs key validation. PR #338 (merged 2026-10-23) raised the Worker BnF timeout default to 2 s, but the 2026-10-26 catch-up still recorded BnF timeouts. See `.github/memory/14-scan-metadata-latency.md` for dated evidence and follow-up.
16. ISBNdb is the primary notice source with BnF/Open Library/Google Books fallback. Manual catch-up is a Durable `202 Accepted` flow; after deploying the infrastructure and Worker, run `infra/scripts/Sync-WorkerFunctionKey.ps1`, then `infra/scripts/Invoke-WorkerIsbndbCatchUp.ps1`. Recommendation SQL persistence must not use the reserved T-SQL alias `current`. See `.github/memory/15-isbndb-migration.md`.
17. The 2026-10-25 Catalog latency investigation and fixes are recorded in `.github/memory/16-catalog-performance.md`; the deployed search endpoint still has an unattributed SQL long tail because Application Insights omits command text.
19. The Website Halloween live board and E6 banner are wired through `liveTheme` and share `vpd.tableau.calm`; apply `AddEventLiveTheme` before deploying the API. See `.github/memory/18-halloween-loto-board.md`.

## Commands To Remember

- Backend build/test: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- Backend AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`
- Website SSR: `npm run serve:ssr:vole_papillon_damour_website` from `src/Website/`
- Catalog SSR: `npm run build` then `npm run serve:ssr:vole_papillon_damour_catalog` from `src/Catalog/`
- MAUI app: `dotnet build .\src\MauiCashApp\ShopAppVpd.slnx`

## Memory Rules

- `MEMORY.md` stays short and scannable.
- Detailed facts belong in the thematic files, not in chat answers.
- Do not copy secrets or connection string values into memory.
