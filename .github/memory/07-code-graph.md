# 07 - Code Graph

## Selected engine and artifacts

- `codeGraphEngine`: `graphify`, used for repository architecture and corpus-level orientation.
- Generated local artifacts: `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`, optional `graphify-out/wiki/index.md`; `.graphifyignore` controls ingestion. These may be absent in a clean worktree and are ignored by Git.
- Snapshot on 2026-10-31: 1,913 files, 9,398 nodes, 20,521 edges, 832 detected communities. Extraction was 59% explicit and 41% inferred; inferred edges are hypotheses. Token cost was zero.
- The graph extractor refreshes successfully, but its HTML visualization exceeds the renderer's size limit at this corpus size. Use the text report and graph query/path tools instead of treating missing HTML as an extraction failure.

## Usage rules

- Read `graphify-out/GRAPH_REPORT.md` for god nodes and community structure before repository-wide architecture answers.
- Use `graphify query`, `path`, or `explain`, or Graphify MCP tools when active. Confirm conclusions against source files before changing shared handlers, repositories, API endpoint registration, or Angular shared services.
- Run `graphify update .` after meaningful source changes when the graph is enabled; do not update for memory-only edits.
- The MCP server is configured in `.vscode/mcp.json` and uses `python -m graphify.serve`.

## Refresh commands

```powershell
python -c "from pathlib import Path; from graphify.watch import _rebuild_code; import sys; ok = _rebuild_code(Path('.')); sys.exit(0 if ok else 1)"
python -m graphify update .
```

Main structural targets are backend feature slices under `src/Backend/Vole_Papillon_Damour.Application/`, repositories in Infrastructure, and shared Angular services/routing in BackOffice and Website.
