# Project Memory — socle-agents

> Index file. Detailed knowledge lives in thematic files under `.github/memory/`.

## Memory Architecture

| File | Content |
|------|---------|
| `.github/memory/01-solution-overview.md` | Purpose of the bootstrap repository and scope |
| `.github/memory/02-bootstrap-system.md` | What `memory-bootstrap` must initialize in target projects |
| `.github/memory/03-agents-skills.md` | Base agents, base skills, conditional generation rules |
| `.github/memory/04-docs-and-wiki.md` | Documentation and wiki surfaces |
| `.github/memory/05-mcp-and-code-graph.md` | MCP baseline, GitNexus, Graphify, and workspace config |
| `.github/memory/06-review-and-audit.md` | Review workflow, audit workflow, quality gates |
| `.github/memory/changelog.md` | Memory changelog |
| `.github/memory/dream-state.md` | Dream trigger state + code graph engine choice |

## Quick Reference — Bootstrap Requirements

1. `memory-bootstrap` must ask the user to choose a code graph engine (GitNexus / Graphify / both).
2. `memory-bootstrap` must initialize a thematic memory system, not a monolithic memory file.
3. `gitnexus-workflow`, `graphify-corpus`, `memory-management`, `tdd-workflow`, `audit-workflow` are base skills.
4. `dev` must support Dream gate checks and code-graph-first research.
5. Bootstrap output must update `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, and `.vscode/mcp.json`.
6. MCP configuration must include the chosen code graph server as a first-class entry.
7. Review agents (`review-expert`, `vibe-coding-refractaire`, `review-remediator`) are always generated.
8. `audit-expert` is always generated for periodic code audits.
9. The backend specialist is exclusive by detected stack: `dotnet-dev` for `.NET`, `python-dev` for `Python`, or both only for real multi-backend repos.
10. Python backends also receive `python-patterns`, with framework-specific guidance for FastAPI, Django, or Flask.

## How Memory Works

- This repository uses its own thematic memory under `.github/memory/`.
- A bootstrapped target project should receive the same structure.
- `@dream` consolidates memory periodically when the configured gates pass.
