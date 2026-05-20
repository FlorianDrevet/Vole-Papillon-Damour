---
name: dotnet-patterns
description: "Use when generating, reviewing, refactoring, or testing .NET backend or MAUI client code in this repository."
---

# Skill : dotnet-patterns

Charger ce skill pour toute tache `.NET` dans ce depot.

## Solution-Specific Boundaries

- `Api` : HTTP, middleware, endpoint wiring, JSON and auth pipeline
- `Application` : commands, queries, handlers, validators, interfaces, behaviors
- `Domain` : aggregates and business invariants
- `Infrastructure` : EF Core, repositories, JWT, Azure services, OCR, email, storage
- `Contracts` : transport DTOs
- `MauiCashApp` : MVVM, Refit, SQLite, client-side settings

## Backend Conventions

- Respecter les limites de couche ; ne pas faire remonter Infrastructure dans Domain.
- Utiliser MediatR + FluentValidation dans `Application` quand la tache touche un use case.
- Garder les endpoints et le mapping HTTP dans `Api`.
- Garder les details EF Core et Azure dans `Infrastructure`.
- Garder les DTOs reutilisables dans `Contracts`.

## Client MAUI Conventions

- Reutiliser MVVM Toolkit pour les view models.
- Reutiliser Refit pour l'acces API.
- Eviter de dupliquer la logique backend dans le client.
- Garder les modeles locaux et les acces SQLite clairement separes des appels reseau.

## General .NET Guardrails

- Pas de magic strings pour les claims, policies, config keys, ou noms de conteneurs.
- Un type public top-level par fichier.
- Propager `CancellationToken` dans les chemins async existants.
- Preferer des contrats types aux payloads faibles.

## Validation

- `dotnet build` sur la solution ou le projet touche
- `dotnet test` sur le projet de tests touche quand il existe