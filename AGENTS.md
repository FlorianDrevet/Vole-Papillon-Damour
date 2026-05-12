# Copilot instructions

This repository is set up as a bootstrap template for initializing agentic workflows on new or existing projects.

## General recommendations

1. Always run `@memory-bootstrap` when applying this socle to a new project.
2. The bootstrap will ask whether to use GitNexus (open-source) or Graphify (enterprise) as code graph engine.
3. Make changes incrementally and validate after each step.
4. Use the code graph tools to understand code before modifying it.

## Running the bootstrap

To initialize a project with this socle:

1. Copy the `.github/` folder structure to the target project
2. Run `@memory-bootstrap` in the target project
3. The bootstrap agent will explore, detect the stack, and generate adapted agents/skills/memory

## Code Graph Engine Choice

| Engine | Best for | Command |
|--------|----------|---------|
| GitNexus | Open-source, code-first projects | `npx gitnexus analyze` then `npx gitnexus mcp` |
| Graphify | Enterprise, corpus-rich projects | `pip install graphifyy` then `python -m graphify.serve graphify-out/graph.json` |
| Both | Large projects with rich docs + complex code | Both commands above |

## Agents

| Agent | Purpose | File |
|-------|---------|------|
| `dev` | Main entry point — reads memory, routes to specialists, loads skills | `.github/agents/dev.agent.md` |
| `memory-bootstrap` | Explores project and initializes the full agentic foundation | `.github/agents/memory-bootstrap.agent.md` |
| `architect` | Architecture review and implementation planning | `.github/agents/architect.agent.md` |
| `dotnet-dev` | Backend .NET specialist, generated only for .NET backends | `.github/agents/dotnet-dev.agent.md` |
| `python-dev` | Backend Python specialist, generated only for Python backends | `.github/agents/python-dev.agent.md` |
| `documentation-professor` | Technical documentation, onboarding, pedagogy | `.github/agents/documentation-professor.agent.md` |
| `review-expert` | Pre-merge code review, quality gate | `.github/agents/review-expert.agent.md` |
| `vibe-coding-refractaire` | Anti-vibe coding review, smell detection | `.github/agents/vibe-coding-refractaire.agent.md` |
| `review-remediator` | Apply review backlog corrections | `.github/agents/review-remediator.agent.md` |
| `audit-expert` | Technical audits with GitHub sync | `.github/agents/audit-expert.agent.md` |
| `dream` | Memory consolidation pass | `.github/agents/dream.agent.md` |
| `merge-main` | Merge main with semantic conflict resolution | `.github/agents/merge-main.agent.md` |
| `pr-manager` | PR conventions | `.github/agents/pr-manager.agent.md` |

## Skills

| Skill | Purpose | File |
|-------|---------|------|
| `memory-management` | Memory update rules and routing | `.github/skills/memory-management/SKILL.md` |
| `gitnexus-workflow` | Structure-aware exploration, impact analysis, change validation | `.github/skills/gitnexus-workflow/SKILL.md` |
| `graphify-corpus` | Corpus graph, docs-to-code traceability, community detection | `.github/skills/graphify-corpus/SKILL.md` |
| `tdd-workflow` | Mandatory TDD Red-Green-Refactor cycle | `.github/skills/tdd-workflow/SKILL.md` |
| `audit-workflow` | Audit report format, findings lifecycle, GitHub sync | `.github/skills/audit-workflow/SKILL.md` |

## Conditional Skills

| Skill | Purpose | File |
|-------|---------|------|
| `python-patterns` | Pragmatic Python backend conventions for FastAPI, Django, Flask, typing, tests, and maintainable structure | `.github/skills/python-patterns/SKILL.md` |

## Always Do

- Run `@memory-bootstrap` on any new project before starting work
- Let `@memory-bootstrap` generate only the backend specialist that matches the detected stack (`dotnet-dev` or `python-dev`)
- Use the code graph engine for exploration and impact analysis before modifying shared symbols
- Update memory after every non-trivial task
- Use `@dream` periodically to consolidate memory
- Write tests BEFORE production code (TDD workflow)
- Run code reviews (`review-expert` + `vibe-coding-refractaire`) before merging

## Never Do

- Never edit a shared symbol without running impact analysis first
- Never ignore HIGH or CRITICAL risk warnings
- Never commit without validating the change scope
- Never write production code without tests
- Never keep both backend agents in a target project unless the repository truly has multiple backends
- Never create dump files with multiple unrelated types
