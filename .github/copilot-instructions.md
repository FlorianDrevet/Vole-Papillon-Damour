# Copilot Instructions

## Getting Started

Utiliser `@dev` comme point d'entree normal. Revenir a `@memory-bootstrap` seulement si la stack, la memoire thematique, ou les agents/skills doivent etre regenerees.

La connaissance detaillee vit dans `MEMORY.md`, dans `.github/memory/`, et dans les agents/skills projet.

## Development Environment

L'utilisateur travaille sur **Windows**. Toutes les commandes terminal doivent utiliser **PowerShell** (`pwsh`) avec `\.\` pour les chemins relatifs, `;` comme separateur, et `$env:` pour les variables d'environnement.

## Project Snapshot

- Open-source repository for the association application
- Backend: ASP.NET Core Web API on **.NET 10**
- Architecture: `Domain` / `Application` / `Infrastructure` / `Api` / `Contracts` with CQRS via MediatR + FluentValidation
- Web UIs: **Angular 21** apps in `src/BackOffice/` and `src/Website/`
- Native client: .NET MAUI app in `src/MauiCashApp/` (`net9.0-*`, **outside the solution**)
- Tests: xUnit domain tests exist; add focused tests before production edits

## Agents

| Agent | Role | File |
|-------|------|------|
| `dev` | Main orchestrator for research, routing, memory, and validation | `.github/agents/dev.agent.md` |
| `memory-bootstrap` | Rebootstrap the agentic foundation when the stack changes materially | `.github/agents/memory-bootstrap.agent.md` |
| `architect` | Architecture analysis and implementation planning | `.github/agents/architect.agent.md` |
| `dotnet-dev` | Backend .NET and MAUI specialist | `.github/agents/dotnet-dev.agent.md` |
| `angular-front` | Angular specialist for `BackOffice` and `Website` | `.github/agents/angular-front.agent.md` |
| `documentation-professor` | Technical documentation and onboarding | `.github/agents/documentation-professor.agent.md` |
| `review-expert` | Pre-merge review and quality gate | `.github/agents/review-expert.agent.md` |
| `vibe-coding-refractaire` | Anti-slop technical review | `.github/agents/vibe-coding-refractaire.agent.md` |
| `review-remediator` | Review backlog remediation | `.github/agents/review-remediator.agent.md` |
| `audit-expert` | Periodic technical audits | `.github/agents/audit-expert.agent.md` |
| `dream` | Memory consolidation | `.github/agents/dream.agent.md` |
| `merge-main` | Merge helper | `.github/agents/merge-main.agent.md` |
| `pr-manager` | Pull request conventions | `.github/agents/pr-manager.agent.md` |
| `memory` | Deprecated redirect to `dev` | `.github/agents/memory.agent.md` |

## Skills

| Skill | Role | File |
|-------|------|------|
| `memory-management` | Maintain `MEMORY.md` and thematic memory files | `.github/skills/memory-management/SKILL.md` |
| `graphify-corpus` | Corpus-level architecture and documentation exploration | `.github/skills/graphify-corpus/SKILL.md` |
| `tdd-workflow` | Mandatory TDD cycle for executable code | `.github/skills/tdd-workflow/SKILL.md` |
| `audit-workflow` | Audit workflow and findings lifecycle | `.github/skills/audit-workflow/SKILL.md` |
| `cqrs-feature` | Backend CQRS feature workflow for this solution | `.github/skills/cqrs-feature/SKILL.md` |
| `dotnet-patterns` | .NET backend patterns used in this repository | `.github/skills/dotnet-patterns/SKILL.md` |
| `xunit-unit-testing` | xUnit + FluentAssertions + NSubstitute conventions | `.github/skills/xunit-unit-testing/SKILL.md` |
| `angular-patterns` | Angular patterns for `BackOffice` and `Website` | `.github/skills/angular-patterns/SKILL.md` |
| `ui-ux-front-saas` | UI guardrails for visible frontend work | `.github/skills/ui-ux-front-saas/SKILL.md` |

## graphify

Graphify is the repository knowledge graph for corpus-level architecture and documentation exploration.

- Read `graphify-out/GRAPH_REPORT.md` for architecture, god-node, and community context.
- Use `python -m graphify query "concept"`, `path`, and `explain` for conceptual searches when the graph is enabled.
- Use the `graphify` MCP server from `.vscode/mcp.json` when it is active.
- Run `python -m graphify update .` after meaningful source changes when the graph is enabled.
- Type `/graphify` in Copilot Chat for a full corpus build or refresh when the installed Graphify skill is available.

## Project Guardrails

- TDD is mandatory for executable code; use `.github/test-debt.md` only for explicit, tracked exceptions.
- No magic strings for claims, policies, config keys, route names, or status values.
- One public top-level type per file.
- Prefer typed contracts over `object`, `Dictionary<,>`, or `any` when the schema is known.
- Keep backend logic inside the existing layer boundaries.
- Preserve Angular Material + Tailwind conventions and validate responsive behavior on UI changes.

## Pull Requests

Any PR prepared by an agent must follow `.github/agents/pr-manager.agent.md`.
