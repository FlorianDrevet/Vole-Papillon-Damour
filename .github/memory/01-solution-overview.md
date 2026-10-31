# 01 - Solution Overview

## Scope

`Vole-Papillon-Damour` is the application repository itself, not a bootstrap template. It ships several delivery and runtime surfaces around one shared business domain:

- an ASP.NET Core backend API
- three Angular 21 web applications (`BackOffice`, `Website`, and the public books `Catalog`)
- one Angular 21 Scanette PWA for ISBN capture, offline triage, consultation, and cash sales (`Scan`)
- one .NET isolated Worker for account deletion, Books `Sweep`/`Enrich`, and alert delivery
- one .NET MAUI cashier client (`MauiCashApp`)

## Runtime Surfaces

- `src/Backend/` hosts the main .NET 10 solution and the layered backend projects.
- `src/Backend/Vole_Papillon_Damour.AppHost/` hosts the Aspire AppHost used for local orchestration of the API and web apps.
- `src/BackOffice/` is the Angular admin application.
- `src/Website/` is the Angular public website.
- `src/Catalog/` is the separate Angular SSR public books catalog, released at
  `https://livres.volepapillondamour.fr`.
- `src/Scan/` is the public Scanette PWA for authenticated triage, consultation, and cash sales,
  released at `https://scan.volepapillondamour.fr` with the ACA FQDN as a technical fallback.
- `src/Backend/Vole_Papillon_Damour.Worker/` hosts the private account-deletion, Books sweep/enrichment, and alert-delivery worker.
- `src/MauiCashApp/` is the MAUI client that calls the deployed backend through Refit.

## Verified Functional Areas

The backend and contracts expose features around:

- authentication
- account deletion and external identity coordination
- actuality content
- association events
- books catalogue, scanning, sales, alerts, and bibliographic metadata lookup
- member book-not-found reports and Catalog administration queue
- products
- orders

## Critical Zones

- Authentication and JWT configuration in the API and Infrastructure layers.
- Persistence and repository wiring through `ProjectDbContext` and `Infrastructure.Persistence`.
- Azure-backed integrations for Blob storage and monitoring.
- Cross-app contract drift between `Contracts`, Angular apps, and the MAUI client.
- Event, order, and product flows that touch both storage and user-facing clients.

## Current Constraints

- Treat Azure resources whose names include `dev` as production; see `.github/memory/08-runtime-and-orchestration.md` before any deployment or data operation.
- The backend currently enables permissive CORS; inspect this when changing public/API deployment assumptions.
- Frontend unit suites remain a local validation step even though CI builds the applications. The MAUI Android build requires an Android SDK.
- Residual `MailingList` folders remain in `Application` and `Contracts`, but the API runtime no longer maps mailing-list endpoints. Automatic Bingo-card OCR was removed.
- Backend projects remain free of `Aspire.*`; orchestration lives only in the AppHost. Graphify artifacts are local/ignored and may be absent in a clean worktree.
- Member-account and not-found-report rollout details live in `.github/memory/03-domain-model.md`, `.github/memory/05-data-and-storage.md`, and `.github/memory/12-manual-book-requests.md`; do not rely on older branch/migration status copied here.
