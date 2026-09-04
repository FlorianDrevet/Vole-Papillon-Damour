# 01 - Solution Overview

## Scope

`Vole-Papillon-Damour` is the application repository itself, not a bootstrap template. It ships several delivery and runtime surfaces around one shared business domain:

- an ASP.NET Core backend API
- three Angular 21 web applications (`BackOffice`, `Website`, and the public books `Catalog`)
- one Angular 21 consultation-only ISBN probe (`Scan`)
- one .NET isolated account-deletion Worker
- one .NET MAUI cashier client (`MauiCashApp`)

## Runtime Surfaces

- `src/Backend/` hosts the main .NET 10 solution and the layered backend projects.
- `src/Backend/Vole_Papillon_Damour.AppHost/` hosts the Aspire AppHost used for local orchestration of the API and web apps.
- `src/BackOffice/` is the Angular admin application.
- `src/Website/` is the Angular public website.
- `src/Catalog/` is the separate Angular SSR public books catalog, released at the planned
  `livres.volepapillondamour.fr` hostname once its DNS binding exists.
- `src/Scan/` is the public consultation-only ISBN metadata probe.
- `src/Backend/Vole_Papillon_Damour.Worker/` hosts the private account-deletion timer worker.
- `src/MauiCashApp/` is the MAUI client that calls the deployed backend through Refit.

## Verified Functional Areas

The backend and contracts expose features around:

- authentication
- account deletion and external identity coordination
- actuality content
- association events
- bibliographic ISBN metadata lookup
- products
- orders

## Critical Zones

- Authentication and JWT configuration in the API and Infrastructure layers.
- Persistence and repository wiring through `ProjectDbContext` and `Infrastructure.Persistence`.
- Azure-backed integrations for Blob storage and monitoring.
- Cross-app contract drift between `Contracts`, Angular apps, and the MAUI client.
- Event, order, and product flows that touch both storage and user-facing clients.

## Current Constraints

- The backend currently enables a permissive CORS policy for all origins.
- Domain tests exist, but cross-layer automated coverage is still thin.
- Residual `MailingList` folders still exist in `Application` and `Contracts`, but the API runtime no longer maps mailing-list endpoints.
- The OCR bingo-card analysis slice was removed from the backend and BackOffice admin UI because automatic loto-card reading is no longer allowed by the business rules.
- The backend projects remain free of `Aspire.*` packages; local orchestration lives only in the AppHost project.
- `.github/workflows/ci.yml` is configured for backend, MAUI, and frontend builds; frontend unit tests are still validated locally.
- The MAUI Android build remains dependent on an Android SDK being available in the environment.
- A Graphify knowledge graph is available for documentation and corpus-level orientation.
