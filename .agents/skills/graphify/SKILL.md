---
name: graphify
description: "Use for repository-wide architecture, code/documentation relationships, corpus orientation, community exploration, and Graphify graph maintenance."
---

# Graphify repository skill

This repository uses Graphify as its code and corpus graph. Use the detailed
repository workflow in `.github/skills/graphify-corpus/SKILL.md` together with the
local graph artifacts under `graphify-out/`.

## Before exploration

1. Read `graphify-out/GRAPH_REPORT.md` when it exists.
2. If `graphify-out/wiki/index.md` exists, use it for corpus navigation.
3. Use Graphify before broad raw-file searches:

```powershell
python -m graphify query "concept"
python -m graphify path "A" "B"
python -m graphify explain "node"
```

When the workspace MCP server is active, use `query_graph`, `get_node`,
`get_neighbors`, `get_community`, `god_nodes`, `graph_stats`, and
`shortest_path`.

## After source changes

Run the code-only refresh and then the relevant tests/build:

```powershell
python -m graphify update .
```

The stdio server is configured in `.vscode/mcp.json` and runs with:

```powershell
python -m graphify.serve graphify-out/graph.json
```

If the local graph is absent or stale, build it with the code-only bootstrap
command documented in `.github/skills/graphify-corpus/SKILL.md`, then continue with
direct source inspection. Graphify informs exploration; it does not replace tests,
review, or compilation.
