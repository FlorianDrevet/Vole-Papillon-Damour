# 06 - Agents And Skills

## Generated Agents

- `dev` - main orchestrator
- `memory-bootstrap` - rebootstrap agent for future structural changes
- `architect` - architecture analysis and implementation planning
- `dotnet-dev` - backend .NET and MAUI specialist for this repository
- `angular-front` - Angular web specialist for `BackOffice` and `Website`
- `documentation-professor` - docs and onboarding
- `review-expert` - pre-merge review
- `vibe-coding-refractaire` - anti-slop technical review
- `review-remediator` - review backlog remediation
- `audit-expert` - technical audits
- `dream` - memory consolidation
- `merge-main` - semantic merge helper
- `pr-manager` - PR conventions
- `memory` - deprecated redirect to `dev`

## Backend Routing Rule

- This repository has a .NET backend and no verified Python backend.
- Backend and MAUI tasks route to `dotnet-dev`.
- Angular web tasks route to `angular-front`.

## Source Of Truth Note

- Treat this file and `MEMORY.md` as the source of truth for the active project stack.
- Some generic bootstrap-oriented agent files may still mention optional Graphify, Python, or Aspire branches; those are not the active runtime stack of this repository.

## Active Skills

- Base: `memory-management`, `graphify-corpus`, `tdd-workflow`, `audit-workflow`
- Backend: `cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`
- Frontend: `angular-patterns`, `ui-ux-front-saas`

## Skills Not Generated

- `python-patterns` is not part of the active project stack.
- No CI/CD-specific skill was generated; the repository CI entry point is `.github/workflows/ci.yml`.

## Routing Heuristics

- Shared backend symbol change -> perform structural impact review first, then use `dotnet-dev`
- Angular UI or state change -> `angular-front`, plus `ui-ux-front-saas` for visible UI work
- Cross-surface contract change -> backend first, then Angular and MAUI follow-up validation
