# 03 - Domain Model And Runtime Flow

## Backend Runtime

The API boots from `Vole_Papillon_Damour.Api/Program.cs` and wires:

- Swagger in development
- controllers with camelCase JSON output
- authorization policy `IsAdmin`
- Azure Monitor OpenTelemetry
- custom error handling middleware
- rate limiting
- authentication and authorization

## CQRS Flow

Application setup registers:

- MediatR handlers from the Application assembly
- FluentValidation validators from the Application assembly
- a `ValidationBehavior<,>` pipeline behavior

The usual change path is:

1. API endpoint or controller extension receives HTTP input
2. request maps to a command or query in `Application`
3. MediatR dispatches to a handler
4. handler uses repositories or services from `Infrastructure`
5. contracts and results flow back to clients

## Feature Slices

Verified slices in `Application` and `Contracts` include:

- `Actuality`
- `Authentication`
- `BingoCard`
- `Events`
- `MailingList`
- `Orders`
- `Products`

## Domain Aggregates

Verified aggregate folders in `Domain` include:

- `ActualityAggregate`
- `AssoEventsAggregate`
- `OrderAggregate`
- `ProductAggregate`
- `UserAggregate`

## Conventions To Preserve

- Keep commands and queries in their feature folders under `Application`.
- Keep validators and MediatR handlers close to the feature they serve.
- Keep domain rules in aggregates and domain types, not in controllers.
- Keep transport DTOs in `Contracts`, not inside Angular or MAUI code.
- When a shared contract changes, review both Angular apps and the MAUI client.