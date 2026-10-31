# Project Memory - Vole-Papillon-Damour

> Index file. Detailed knowledge lives in thematic files under `.github/memory/`.

## Stack Snapshot

- Backend: .NET 10 ASP.NET Core API with layered CQRS in `src/Backend/`.
- Local orchestration: .NET Aspire AppHost for API, Angular apps, SQL Server, and Azurite.
- Frontends: Angular 21 BackOffice, Website, public SSR Catalog, and offline Scan PWA; shared UI source is `src/SharedUi/` via `@vpd/ui`.
- Background work: .NET isolated Worker for account deletion, Books processing, alerts, and social imports.
- Native client: Android-only .NET MAUI cashier in `src/MauiCashApp/`.
- Backend and app builds are in CI; frontend unit suites remain local validation.

## Thematic Memory

| File | Content |
|------|---------|
| `.github/memory/01-solution-overview.md` | Runtime scope and critical constraints |
| `.github/memory/02-project-structure.md` | Folder map and ownership boundaries |
| `.github/memory/03-domain-model.md` | CQRS, aggregates, Books and live Loto invariants |
| `.github/memory/04-frontend.md` | Angular and MAUI surfaces, SSR, offline client rules |
| `.github/memory/05-data-and-storage.md` | SQL, Books persistence, blobs, storage and reset rules |
| `.github/memory/06-agents-skills.md` | Agent routing and repository skills |
| `.github/memory/07-code-graph.md` | Graphify workflow and current snapshot |
| `.github/memory/08-runtime-and-orchestration.md` | Azure resources, deployment, Aspire, Worker |
| `.github/memory/09-auth-and-build.md` | Entra/Keycloak auth, MSAL behavior, build commands |
| `.github/memory/10-api-endpoints.md` | Endpoint map, authorization, auth asymmetries |
| `.github/memory/11-frontend-design-system.md` | UI tokens, shared components, design guardrails |
| `.github/memory/12-manual-book-requests.md` | F-12 behavior, privacy, rollout and validation |
| `.github/memory/13-book-recommendations.md` | Similar-book API and Catalog behavior |
| `.github/memory/14-scan-metadata-latency.md` | Dated provider telemetry and unresolved follow-ups |
| `.github/memory/15-isbndb-migration.md` | ISBNdb rollout, catch-up and Scan matching |
| `.github/memory/16-catalog-performance.md` | Catalog API/admin latency evidence and optimization |
| `.github/memory/17-worktree-dependency-sharing.md` | Clone-local hook for frontend dependency reuse |
| `.github/memory/18-halloween-loto-board.md` | Live board theme behavior, motion, validation |
| `.github/memory/19-website-event-ssr.md` | Event SSR fix and separate 40-request/300-client evidence |
| `.github/memory/changelog.md` | Dated memory changes from the last 60 days |
| `.github/memory/dream-state.md` | Dream gate state and Graphify selection |

## Quick Reference

1. Start day-to-day work from `@dev`; use `@memory-bootstrap` after material stack/agent changes.
2. Read `graphify-out/GRAPH_REPORT.md` for architecture questions; inferred edges are hypotheses, not source truth.
3. Keep API, Application, Domain, Infrastructure, and Contracts responsibilities separate; see `.github/memory/03-domain-model.md`.
4. Use TDD for executable changes and keep typed contracts across clients.
5. Check endpoint policies individually; known mutation asymmetries are listed in `.github/memory/10-api-endpoints.md`.
6. Every searchable or paginated list must send filters/page/page size to the API; do not load collections for browser-side filtering.
7. Raw SQL against `Books` uses `Isbn13`; `BookConfiguration` maps domain `Book.Id` to that column.
8. Keep Website event list/detail server-rendered and the live tableau browser-rendered; distinguish the local 20+20 SSR HTTP run from the prior deployed 300-client SSE run in `.github/memory/19-website-event-ssr.md`.
9. `AssoEvents.LiveRevision` is the SSE snapshot concurrency token. Completed Loto undo rewinds to the last party; see `.github/memory/03-domain-model.md`.
10. Local API persona tests use Aspire Keycloak and `.github/skills/local-auth-testing/SKILL.md`; deployed auth remains Entra.
11. Treat `rg-vpd-dev`, `vpd-sql-dev`, `vole-papillon-damour-db`, and `vpdstdevfc` as production despite their names.
12. Keep West Europe resources until the owner confirms deletion after France Central cutover/rollback. Cloudflare website records remain DNS-only until Origin CA installation is complete.
13. Use `.agents/skills/book-data-reset/SKILL.md` for an explicitly requested deployed books reset; it preserves events/news and active identities.
14. Use `.agents/skills/vpd-prod-backup/SKILL.md` for an explicitly requested local production backup.
15. Check `NEXT.md` for current feature step and external-state follow-ups before deployment or data operations.

## Commands To Remember

- Backend: `dotnet build .\src\Backend\Vole_Papillon_Damour.slnx`; `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx`
- AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular: `npm install`, `npm run build`, and `npm test` from the target app directory
- Website SSR: `npm run serve:ssr:vole_papillon_damour_website` from `src/Website/`
- MAUI: `dotnet build .\src\MauiCashApp\ShopAppVpd.csproj --framework net10.0-android`

## Memory Rules

- Keep this index short; details and dated evidence belong in thematic files.
- Do not copy secrets, tokens, or connection strings into memory.
- Preserve observed environment provenance and separate local smoke from deployed load evidence.
