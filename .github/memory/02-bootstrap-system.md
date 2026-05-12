# 02 — Bootstrap System

## Entry Point

`@memory-bootstrap` (`.github/agents/memory-bootstrap.agent.md`) is the single entry point for initializing a target project with the full agentic foundation.

## Bootstrap Phases

| Phase | Purpose |
|-------|---------|
| 0 — Choice | Ask user: GitNexus / Graphify / both |
| 1 — Stack detection | Explore codebase to detect tech stack |
| 2 — Discovery | Identify project structure, patterns, conventions |
| 3 — Memory init | Create `MEMORY.md` + `.github/memory/` thematic files |
| 4 — Agents | Generate adapted agent files |
| 5 — Skills | Generate applicable skills |
| 6 — MCP config | Generate `.vscode/mcp.json` |
| 7 — Code graph init | Run `gitnexus analyze` or `graphify build` |
| 8 — Docs surface | Generate `AGENTS.md`, `CLAUDE.md` |
| 9 — Validation | Verify all generated files are consistent |

## What Gets Generated on Target

- `MEMORY.md` (index)
- `.github/memory/` (thematic files)
- `.github/agents/` (at minimum: dev, dream, architect, review-expert, vibe-coding-refractaire)
- `.github/skills/` (at minimum: memory-management, tdd-workflow, one code graph skill)
- `.github/copilot-instructions.md` (main instructions referencing agents/skills)
- `.vscode/mcp.json` (MCP servers based on code graph choice)
- `AGENTS.md` (public agents listing for tools like Claude/Copilot)
- `CLAUDE.md` (Claude Code compatibility)

## Adaptation Rules

- If .NET detected → add `dotnet-dev` agent, `dotnet-patterns` skill placeholder, and omit `python-dev`
- If Angular detected → add `angular-front` agent, `angular-patterns` skill placeholder
- If Python detected → add `python-dev` agent, `python-patterns` skill, and omit `dotnet-dev`
- If Aspire detected → add `aspire-debug` agent, aspire MCP server
- If monorepo → split memory by package/project

## Python Framework Detection

- `FastAPI` target: detect `fastapi`, `FastAPI(`, `APIRouter`, `Depends`, `pydantic`, `uvicorn`
- `Django` target: detect `manage.py`, `settings.py`, `INSTALLED_APPS`, imports `django.`
- `Flask` target: detect `flask`, `Flask(`, `Blueprint`, `current_app`, app factory patterns
- The detected Python framework must be recorded in memory and used to tailor `python-dev` guidance.

## Backend Exclusivity

- The bootstrap must generate only the backend agent that matches the detected stack.
- `.NET` target → `dotnet-dev`
- `Python` target → `python-dev`
- Multi-backend target → one backend agent per real backend, clearly documented in memory.
