# Copilot Instructions

This repository has been bootstrapped for the actual `Vole-Papillon-Damour` application stack.

## Solution Snapshot

- Open-source repo for the `Vole-Papillon-Damour` application stack
- Backend: **.NET 10** ASP.NET Core API with layered CQRS, in `src/Backend/`
- Web frontends: **Angular 21** apps in `src/BackOffice/` and `src/Website/`, sharing
  `src/SharedUi/` through the `@vpd/ui` tsconfig alias
- Native client: **.NET MAUI** app in `src/MauiCashApp/`, currently `net9.0-*`, **not
  referenced by `Vole_Papillon_Damour.slnx`** — building the solution does not build it
- Tests: xUnit tests in the three `*.tests` projects; `src/BackOffice` has **no** spec
  file, so `npm test` fails there by design until tests exist

> **Work in progress.** A new module — *bourse aux livres* — is specified, designed and
> planned but **not implemented**. Read [`NEXT.md`](NEXT.md) before touching anything: it
> carries the current step and everything git cannot (Azure, DNS, tenant, measurements).

## Agents

| Agent | Purpose | File |
|-------|---------|------|
| `dev` | Main entry point for research, routing, memory, and validation | `.github/agents/dev.agent.md` |
| `memory-bootstrap` | Rebootstrap the project memory, agents, and MCP config after structural changes | `.github/agents/memory-bootstrap.agent.md` |
| `architect` | Architecture review and implementation planning | `.github/agents/architect.agent.md` |
| `dotnet-dev` | Backend .NET and MAUI specialist | `.github/agents/dotnet-dev.agent.md` |
| `angular-front` | Angular specialist for `BackOffice` and `Website` | `.github/agents/angular-front.agent.md` |
| `documentation-professor` | Technical documentation and onboarding | `.github/agents/documentation-professor.agent.md` |
| `review-expert` | Pre-merge review and quality gate | `.github/agents/review-expert.agent.md` |
| `vibe-coding-refractaire` | Anti-slop review and maintainability triage | `.github/agents/vibe-coding-refractaire.agent.md` |
| `review-remediator` | Apply the review backlog | `.github/agents/review-remediator.agent.md` |
| `audit-expert` | Technical audits | `.github/agents/audit-expert.agent.md` |
| `dream` | Memory consolidation pass | `.github/agents/dream.agent.md` |
| `merge-main` | Merge helper | `.github/agents/merge-main.agent.md` |
| `pr-manager` | PR conventions | `.github/agents/pr-manager.agent.md` |

## Skills

| Skill | Purpose | File |
|-------|---------|------|
| `memory-management` | Memory update rules and routing | `.github/skills/memory-management/SKILL.md` |
| `graphify-corpus` | Corpus-level architecture and documentation exploration | `.github/skills/graphify-corpus/SKILL.md` |
| `tdd-workflow` | Mandatory TDD cycle | `.github/skills/tdd-workflow/SKILL.md` |
| `audit-workflow` | Audit report format and findings lifecycle | `.github/skills/audit-workflow/SKILL.md` |
| `cqrs-feature` | CQRS workflow for backend features | `.github/skills/cqrs-feature/SKILL.md` |
| `dotnet-patterns` | .NET backend conventions used in this repo | `.github/skills/dotnet-patterns/SKILL.md` |
| `xunit-unit-testing` | xUnit, FluentAssertions, NSubstitute, AutoFixture conventions | `.github/skills/xunit-unit-testing/SKILL.md` |
| `angular-patterns` | Angular application patterns | `.github/skills/angular-patterns/SKILL.md` |
| `ui-ux-front-saas` | UI guardrails for visible frontend work | `.github/skills/ui-ux-front-saas/SKILL.md` |

## MCP Resources

- `graphify` via `.vscode/mcp.json` (stdio, `graphify.serve`)
- `github` via `.vscode/mcp.json` with token input

## Always Do

- Keep backend work inside the existing layer boundaries.
- Write tests before executable production changes.
- Update thematic memory after non-trivial work.
- Validate the touched application surface locally because no CI pipeline file is currently present.

## Never Do

- Never edit a shared symbol without checking impact first.
- Never introduce a second backend technology path that is not already present.
- Never bypass typed contracts with weakly typed payloads when the schema is known.
- Never commit visible UI changes without checking responsive behavior.

## graphify

This project has a Graphify knowledge graph at `graphify-out/`.

Rules:

- Before answering architecture or codebase questions, read `graphify-out/GRAPH_REPORT.md` for god nodes and community structure.
- If `graphify-out/wiki/index.md` exists, navigate it instead of reading raw files.
- If the Graphify MCP server is active, use `query_graph`, `get_node`, `get_neighbors`, `get_community`, `god_nodes`, `graph_stats`, and `shortest_path` for graph exploration.
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost).
