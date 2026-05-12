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
- endpoint registration through `UseAuthenticationController()`, `UseActualityController()`, `UseProductController()`, `UseOrdersController()`, `UseEventsController()`, `UseBingoCardController()`, `UseMailingListController()`

## Multi-Runtime Notes

- The Website consumes the backend SSE stream for event table updates.
- The MAUI client loads its backend base URL from embedded configuration and does not share Angular environment files.
- The repository now includes a verified Aspire AppHost under `src/Backend/Vole_Papillon_Damour.AppHost/`.
- The AppHost orchestrates the API on port `5257`, BackOffice on `4200`, Website on `4201`, plus local SQL Server and Azurite.
- The backend itself still stays free of `Aspire.*` packages; orchestration concerns live in the AppHost only.

## Verified Local Commands

- Backend build: `dotnet build .\src\Backend\Vole_Papillon_Damour.sln`
- Backend tests: `dotnet test .\src\Backend\Vole_Papillon_Damour.sln`
- Backend AppHost: `dotnet run --project .\src\Backend\Vole_Papillon_Damour.AppHost\Vole_Papillon_Damour.AppHost.csproj`
- Angular apps: `npm install`; `npm run start`; `npm run build`; `npm test`
- MAUI build: `dotnet build .\src\MauiCashApp\ShopAppVpd.sln`

## Runtime Risks

- Cross-surface changes require validating the API plus at least one client.
- SSE, WebSockets, and rate limiting live in the API startup path and can affect website live views and login behavior.
- The permissive CORS policy means frontend/runtime changes should be reviewed with deployment assumptions in mind.