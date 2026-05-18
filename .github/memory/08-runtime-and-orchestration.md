# 08 - Runtime And Orchestration

## Deployable Surfaces

- `src/Backend/Vole_Papillon_Damour.Api/` - ASP.NET Core HTTP API
- `src/Backend/Vole_Papillon_Damour.AppHost/` - .NET Aspire AppHost for local orchestration
- `src/BackOffice/` - Angular admin SPA
- `src/Website/` - Angular public SPA
- `src/MauiCashApp/` - .NET MAUI cashier client

## Entry Points

- Backend entry point: `src/Backend/Vole_Papillon_Damour.Api/Program.cs`
- Aspire AppHost entry point: `src/Backend/Vole_Papillon_Damour.AppHost/Program.cs`
- BackOffice entry path: `src/BackOffice/src/main.ts` -> `app.module.ts`
- Website entry path: `src/Website/src/main.ts` -> `app.module.ts`
- MAUI entry point: `src/MauiCashApp/MauiProgram.cs` and `App.xaml`

## Backend Runtime Pipeline

The API startup wires:

- Swagger only in development
- CORS policy `CorsPolicy`
- custom error handling middleware
- HTTPS redirection
- routing, rate limiting, authentication, authorization
- WebSockets support
- endpoint registration through `UseAuthenticationController()`, `UseActualityController()`, `UseProductController()`, `UseOrdersController()`, and `UseEventsController()`

## Multi-Runtime Notes

- The Website consumes the backend SSE stream for event table updates.
- The MAUI client loads its backend base URL from embedded configuration and does not share Angular environment files.
- The repository now includes a verified Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/`.
- The AppHost orchestrates the API on port `5257`, BackOffice on `4200`, Website on `4201`, plus local SQL Server and Azurite.
- The AppHost `AddNpmApp(..., args)` calls for BackOffice and Website must pass only frontend CLI arguments like `--host` and `--port`; do not include a leading `--` in the args array because Aspire/npm already inserts the separator and Angular CLI fails schema validation on the empty extra argument.
- The backend itself still stays free of `Aspire.*` packages; orchestration concerns live in the AppHost only.
- Deployment IaC for Azure Container Apps now lives under `infra/aca/` and targets only the API, BackOffice, and Website surfaces.
- An Infra Flow Sculptor project named `Vole-Papillon-Damour` was created on 2026-05-18 with `dev` and `prod` environments in `FranceCentral`, a shared `rg-vpd-common`, and a separate `VpdApplications` infrastructure config.
- The Infra Flow Sculptor run created ACR and Log Analytics in the project, but ACA environment and Container App auto-creation failed server-side with a compile exception, so the repository-local Bicep template completes that missing part.

## Verified Local Commands

- Backend build: `dotnet build .\src\Backend\Vole_Papillon_Damour.sln`
- Backend tests: `dotnet test .\src\Backend\Vole_Papillon_Damour.sln`
- Backend AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`
- ACA Bicep compile: `az bicep build --file .\infra\aca\main.bicep`
- ACA image build/push helper: `.\infra\aca\build-and-push.ps1 -EnvironmentName <dev|prod> -RegistryName <acr> -ApiUrl <url> -WebsiteUrl <url>`
- MAUI build: `dotnet build .\src\MauiCashApp\ShopAppVpd.sln`

## Runtime Risks

- Cross-surface changes require validating the API plus at least one client.
- SSE, WebSockets, and rate limiting live in the API startup path and can affect website live views and login behavior.
- The permissive CORS policy means frontend/runtime changes should be reviewed with deployment assumptions in mind.
- Frontend Docker validation now depends on using the `src/` folder as build context so `src/SharedUi/` stays available to both Angular applications during compilation.