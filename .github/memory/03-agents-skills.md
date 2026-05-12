# 03 — Agents and Skills

## Base Agents (always generated)

| Agent | Role |
|-------|------|
| `dev` | Orchestrator: memory, dream gates, research, delegation |
| `dream` | Periodic memory consolidation (4 phases) |
| `architect` | Architecture analysis and planning (never codes) |
| `review-expert` | Pre-merge quality gate |
| `vibe-coding-refractaire` | Second-pass anti-vibe reviewer |
| `review-remediator` | Applies accepted review findings |
| `audit-expert` | Technical audit with GitHub issue sync |
| `merge-main` | Semantic merge resolution |
| `pr-manager` | PR title/description conventions |
| `documentation-professor` | Pedagogical documentation |

## Conditional Agents (generated if stack detected)

| Agent | Condition |
|-------|-----------|
| `dotnet-dev` | .NET/C# project detected |
| `python-dev` | Python backend project detected |
| `angular-front` | Angular project detected |
| `aspire-debug` | .NET Aspire AppHost detected |

## Base Skills (always generated)

| Skill | Purpose |
|-------|---------|
| `memory-management` | Memory routing and formatting |
| `tdd-workflow` | RED→GREEN→REFACTOR→VERIFY |
| `audit-workflow` | Audit report format and lifecycle |

## Conditional Skills (based on code graph choice)

| Skill | Condition |
|-------|-----------|
| `gitnexus-workflow` | GitNexus chosen (or both) |
| `graphify-corpus` | Graphify chosen (or both) |

## Conditional Skills (based on stack)

| Skill | Condition |
|-------|-----------|
| `dotnet-patterns` | .NET detected |
| `python-patterns` | Python backend detected |
| `xunit-unit-testing` | .NET with tests detected |
| `angular-patterns` | Angular detected |
| `ui-ux-front-saas` | Frontend with UI work |

## Agent Design Principles

- Agents are actors with tool access
- Skills are passive knowledge, loaded on demand
- One responsibility per agent
- Delegation over duplication
- Memory is the coordination layer between agents
- Backend agents are generated exclusively by detected backend stack unless the target is truly multi-backend
