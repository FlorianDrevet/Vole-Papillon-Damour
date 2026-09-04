# Project Memory - Vole-Papillon-Damour

> Index file. Detailed knowledge lives in thematic files under `.github/memory/`.

## Stack Snapshot

- Open-source repository for the association application
- Backend: ASP.NET Core Web API on .NET 10 under `src/Backend/`
- Local orchestration: .NET Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/` for API, Website, BackOffice, Catalog, SQL Server, and Azurite
- Architecture: layered CQRS with `Domain`, `Application`, `Infrastructure`, `Api`, and `Contracts`
- Web frontends: Angular 21 applications in `src/BackOffice/`, `src/Website/`, the public SSR catalog in `src/Catalog/`, and the consultation-only `src/Scan/` probe
- Native and background clients: .NET MAUI Android cash app in `src/MauiCashApp/` and the .NET isolated account-deletion Worker under `src/Backend/`
- Tests: xUnit domain tests exist under `src/Backend/Vole_Papillon_Damour.Domain.tests/`
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
| `.github/memory/08-runtime-and-orchestration.md` | Entry points, runtime surfaces, and local execution paths |
| `.github/memory/09-auth-and-build.md` | Auth behavior, config sources, and verified build/test commands |
| `.github/memory/10-api-endpoints.md` | Grouped HTTP endpoints, public/admin split, and live routes |
| `.github/memory/11-frontend-design-system.md` | Angular Material, Tailwind, theme tokens, and UI guardrails |
| `.github/memory/changelog.md` | Memory changelog |
| `.github/memory/dream-state.md` | Dream gate state and code graph choice |

## Quick Reference

1. Start from `@dev` for day-to-day work; use `@memory-bootstrap` only when the stack or agent foundation changes materially.
2. Use Graphify (`GRAPH_REPORT.md`, `query`, `path`, `explain`, or MCP) for structural exploration before changing shared handlers, repositories, controllers, route extensions, and Angular shared services.
3. The API layer uses endpoint-mapping extension classes under `Api/Controllers/`, not MVC `ControllerBase` classes.
4. Apply TDD for executable code; record temporary exceptions in `.github/test-debt.md`.
5. Keep backend changes inside the existing boundaries: API wiring in `Api`, MediatR handlers and validators in `Application`, persistence and adapters in `Infrastructure`, invariants in `Domain`, DTOs in `Contracts`.
6. Preserve the current frontend split: `BackOffice` for admin surfaces, `Website` for association content, `Catalog` for the public books catalog, and `MauiCashApp` for the cashier client.
7. Do not assume every mutating HTTP route is admin-protected; check `10-api-endpoints.md` before touching auth-sensitive behavior.

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
