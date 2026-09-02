# 01 - Solution Overview

## Scope

`Vole-Papillon-Damour` is the application repository itself, not a bootstrap template. It ships three delivery surfaces around one shared business domain:

- an ASP.NET Core backend API
- two Angular 21 web applications (`BackOffice` and `Website`)
- one .NET MAUI cashier client (`MauiCashApp`)

## Runtime Surfaces

- `src/Backend/` hosts the main .NET 10 solution and the layered backend projects.
- `src/Backend/Vole_Papillon_Damour.AppHost/` hosts the Aspire AppHost used for local orchestration of the API and web apps.
- `src/BackOffice/` is the Angular admin application.
- `src/Website/` is the Angular public website.
- `src/MauiCashApp/` is the MAUI client that calls the deployed backend through Refit.

## Verified Functional Areas

The backend and contracts expose features around:

- authentication
- actuality content
- association events
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
- `.github/workflows/ci.yml` runs the backend solution/tests, the Android MAUI target, and both Angular builds on every push and pull request.
- GitNexus is the selected and documented code graph engine for this open-source repository.
