# 07 - Code Graph

## Selected Engine

- `codeGraphEngine`: `graphify`
- Reason: the repository benefits from corpus-level links between code, documentation, and audits.

## Graphify Artifacts

- `graphify-out/graph.json` - knowledge graph data (local, ignored by Git)
- `graphify-out/GRAPH_REPORT.md` - god nodes, communities, and notable connections
- `graphify-out/wiki/index.md` - optional crawlable corpus wiki
- `.graphifyignore` - repository exclusions for Graphify ingestion
- Latest code refresh (2026-09-03): 2,256 nodes, 3,121 edges, 354 communities.

## Usage Rules

- Read `graphify-out/GRAPH_REPORT.md` for architecture and corpus-level orientation.
- Use `python -m graphify query`, `path`, or `explain`, or the MCP tools when the server is active.
- Run `python -m graphify update .` after meaningful source changes when the graph is enabled.
- The workspace MCP server is declared in `.vscode/mcp.json` and runs `python -m graphify.serve`.

## Bootstrap and refresh

```powershell
python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; ok = _rebuild_code(Path('.')); sys.exit(0 if ok else 1)"
python -m graphify update .
```

The first command creates the local code graph without an LLM. The second keeps
code nodes and edges current after source changes. A full corpus build is
available through `/graphify .` from the installed platform skill.

## Practical Targets In This Repo

- Backend feature slices under `src/Backend/Vole_Papillon_Damour.Application/`
- Repository implementations under `src/Backend/Vole_Papillon_Damour.Infrastructure/`
- Angular shared services and routing under `src/BackOffice/src/` and `src/Website/src/`
