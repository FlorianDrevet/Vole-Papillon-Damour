# 01 — Solution Overview

## Purpose

`socle-agents` is a bootstrap template repository. It provides a complete agentic workflow foundation (agents, skills, memory, code graph, MCP servers) that can be applied to any project via the `@memory-bootstrap` agent.

## What This Repo IS

- A collection of `.agent.md`, `SKILL.md`, and configuration files
- A blueprint for initializing agentic infrastructure on new/existing repos
- A reference for memory architecture, Dream consolidation, and review workflows

## What This Repo IS NOT

- Not a runtime application (no build targets, no deployable code)
- Not a monorepo of projects (single purpose: bootstrap)
- Not opinionated about tech stack (adapts to what it discovers)

## Target Projects

Any .NET, Angular, Python, or mixed project that wants:
- Structured multi-agent orchestration via VS Code Copilot
- Code graph intelligence (GitNexus or Graphify)
- Thematic memory with periodic Dream consolidation
- Built-in review and audit workflows
- TDD enforcement via skill

## Key Design Decisions

- Agent files are self-contained and composable
- Skills are lazy-loaded knowledge, not actors
- Memory is thematic (multiple small files), not monolithic
- Code graph choice is deferred to bootstrap time (not hardcoded)
- MCP server config is generated per-project
