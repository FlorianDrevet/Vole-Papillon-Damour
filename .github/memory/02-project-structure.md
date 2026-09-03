# 02 - Project Structure

## Top-Level Layout

- `src/Backend/` - main backend solution and class libraries
- `src/BackOffice/` - Angular 21 admin UI
- `src/Website/` - Angular 21 public website
- `src/Scan/` - Angular 21 consultation-only ISBN metadata probe
- `src/Backend/Vole_Papillon_Damour.Worker/` - .NET 10 isolated account-deletion worker
- `src/MauiCashApp/` - .NET MAUI 10 Android client app

## Backend Structure

- `Vole_Papillon_Damour.Api/` - ASP.NET Core entry point, controller wiring, middleware, HTTP errors
- `Vole_Papillon_Damour.Application/` - CQRS commands/queries, handlers, validators, behaviors, interfaces
- `Vole_Papillon_Damour.Contracts/` - DTOs exchanged across layers and clients
- `Vole_Papillon_Damour.Domain/` - aggregates, domain rules, value logic
- `Vole_Papillon_Damour.Infrastructure/` - EF Core persistence, repositories, auth, Azure services, and storage adapters
- `Vole_Papillon_Damour.Domain.tests/` - xUnit domain-focused tests

## Backend Ownership Boundaries

- API project owns HTTP entry points and middleware registration.
- Application project owns use cases and orchestration through MediatR.
- Domain project stays free of infrastructure concerns.
- Infrastructure project owns external systems and repository implementations.
- Contracts project owns transport-friendly request/response models.

## Frontend And Client Split

- `BackOffice` is the admin surface and carries auth-related client dependencies.
- `Website` is the public-facing web surface.
- `MauiCashApp` is a separate client runtime and should not leak web-only assumptions.

## Structural Notes

- The backend is the shared source of truth for business behavior.
- The Angular apps and MAUI client consume the backend and contracts indirectly, so contract changes have multi-app impact.
- No dedicated `tests/` root exists yet outside the domain test project.
