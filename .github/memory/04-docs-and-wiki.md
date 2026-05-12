# 04 — Docs and Wiki

## Documentation Structure

This repository has its own docs explaining the agentic concepts:

```
docs/
├── README.md              — Entry point for docs
├── agents/                — Individual agent documentation
├── skills/                — Skill concept and catalog
├── memory/                — Memory architecture explanation
├── bootstrap/             — Bootstrap workflow guide
└── code-graph/            — GitNexus vs Graphify comparison
```

## Wiki

The `wiki/` folder mirrors `docs/` for GitHub Wiki compatibility.
It is auto-synced from `docs/` and should not be edited directly.

## Documentation Principles

- Documentation is pedagogical (explain why, not just what)
- Use real examples from the socle, not abstract theory
- Link back to agent/skill files as primary source of truth
- Keep docs in sync with actual agent behavior
