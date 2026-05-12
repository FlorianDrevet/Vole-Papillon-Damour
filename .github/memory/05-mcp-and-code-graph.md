# 05 — MCP and Code Graph Engines

## Two Engines, One Interface

| Engine | Scope | Transport | Install | Best For |
|--------|-------|-----------|---------|----------|
| GitNexus | Code structure, call graph, impact | stdio via `npx gitnexus mcp` | `npx gitnexus analyze` | Open-source, code-first projects |
| Graphify | Corpus (code+docs+audits+diagrams) | stdio via `python -m graphify.serve` | `pip install graphifyy` + `graphify build` | Enterprise, docs-heavy projects |

## Separation of Concerns

- **GitNexus** → code impact, symbol context, rename, detect changes
- **Graphify** → doc-to-code traceability, god nodes, community detection, onboarding
- Never use Graphify for blast radius or rename
- Never use GitNexus for doc-to-code traceability

## MCP Server Configuration

Both engines expose MCP servers that get registered in `.vscode/mcp.json`:

```json
{
  "gitnexus": {
    "type": "stdio",
    "command": "npx",
    "args": ["-y", "gitnexus@latest", "mcp"]
  },
  "graphify": {
    "type": "stdio",
    "command": "python",
    "args": ["-m", "graphify.serve", "graphify-out/graph.json"]
  }
}
```

## Bootstrap Initialization

- Phase 0 asks the user which engine(s) to use
- Phase 7 runs the initial analysis (`gitnexus analyze` or `graphify build`)
- Phase 6 generates the MCP config with only the chosen engine(s)
- The `dev` agent's freshness check adapts based on `dream-state.md:codeGraphEngine`

## Index Freshness

- GitNexus: re-analyze after 7 days or after significant commits
- Graphify: re-build after adding/removing documentation or diagrams
- Both: `@dev` checks freshness at session start (step 1ter)

## Other MCP Servers (optional)

| Server | Purpose |
|--------|---------|
| GitHub MCP | Issue/PR management, code search |
| Azure DevOps MCP | Work items, pipelines, repos |
| Aspire MCP | Runtime diagnostics for .NET Aspire |
| Playwright MCP | Browser-based testing and exploration |
