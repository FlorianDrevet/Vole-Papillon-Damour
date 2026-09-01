# Copilot Instructions

This repository has been bootstrapped for the actual `Vole-Papillon-Damour` application stack.

## Solution Snapshot

- Open-source repo using GitNexus for code graph intelligence
- Backend: .NET 8 ASP.NET Core API with layered CQRS
- Web frontends: Angular 18 apps in `src/BackOffice/` and `src/Website/`
- Native client: .NET MAUI 9 app in `src/MauiCashApp/`
- Tests: xUnit domain tests exist; broader automated coverage still needs to grow

## Code Graph Intelligence

GitNexus is the configured engine for this repository.

### Standard Workflow

1. `gitnexus_query()` to locate symbols and flows
2. `gitnexus_context()` to understand a target in context
3. `gitnexus_impact()` before cross-cutting edits
4. `gitnexus_detect_changes()` after significant modifications
5. `npx gitnexus analyze` to refresh the local index when stale

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
| `gitnexus-workflow` | Structure-aware exploration, impact analysis, and change validation | `.github/skills/gitnexus-workflow/SKILL.md` |
| `tdd-workflow` | Mandatory TDD cycle | `.github/skills/tdd-workflow/SKILL.md` |
| `audit-workflow` | Audit report format and findings lifecycle | `.github/skills/audit-workflow/SKILL.md` |
| `cqrs-feature` | CQRS workflow for backend features | `.github/skills/cqrs-feature/SKILL.md` |
| `dotnet-patterns` | .NET 8 backend conventions used in this repo | `.github/skills/dotnet-patterns/SKILL.md` |
| `xunit-unit-testing` | xUnit, FluentAssertions, NSubstitute, AutoFixture conventions | `.github/skills/xunit-unit-testing/SKILL.md` |
| `angular-patterns` | Angular 18 application patterns | `.github/skills/angular-patterns/SKILL.md` |
| `ui-ux-front-saas` | UI guardrails for visible frontend work | `.github/skills/ui-ux-front-saas/SKILL.md` |

## MCP Resources

- `gitnexus` via `.vscode/mcp.json`
- `github` via `.vscode/mcp.json` with token input

## Always Do

- Use GitNexus before modifying shared handlers, repositories, route extensions, or frontend shared services.
- Keep backend work inside the existing layer boundaries.
- Write tests before executable production changes.
- Update thematic memory after non-trivial work.
- Validate the touched application surface locally because no CI pipeline file is currently present.

## Never Do

- Never edit a shared symbol without checking impact first.
- Never introduce a second backend technology path that is not already present.
- Never bypass typed contracts with weakly typed payloads when the schema is known.
- Never commit visible UI changes without checking responsive behavior.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **Vole-Papillon-Damour** (4581 symbols, 10072 relationships, 100 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/Vole-Papillon-Damour/context` | Codebase overview, check index freshness |
| `gitnexus://repo/Vole-Papillon-Damour/clusters` | All functional areas |
| `gitnexus://repo/Vole-Papillon-Damour/processes` | All execution flows |
| `gitnexus://repo/Vole-Papillon-Damour/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
