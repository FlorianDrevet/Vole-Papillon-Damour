# Code Graph Intelligence

This repository uses **GitNexus** as its code graph engine.

## Recommended Workflow

1. `gitnexus_query()` to locate the right symbols and flows
2. `gitnexus_context()` to understand the target in context
3. `gitnexus_impact()` before cross-cutting changes
4. `gitnexus_detect_changes()` after significant modifications
5. `gitnexus_rename()` for safe symbol renames

## Always Do

- Use the code graph first for structural exploration.
- Run impact analysis before modifying shared handlers, repositories, route extensions, or frontend shared services.
- Validate the real change scope after implementation.
- Update `.github/memory/` after non-trivial tasks.

## Never Do

- Never edit a shared symbol without impact analysis.
- Never ignore HIGH or CRITICAL GitNexus findings.
- Never rename with plain find-and-replace.
- Never commit without checking the effective scope of the change.

## Local Refresh

- Refresh the local index with `npx gitnexus analyze`.
- The MCP server is declared in `.vscode/mcp.json`.

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

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)
