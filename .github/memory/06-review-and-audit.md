# 06 — Review and Audit Workflows

## Review Pipeline (Pre-Merge)

The review pipeline is a two-pass gate before any merge:

1. **`review-expert`** — Strict merge-readiness review
   - Severity: BLOCKING / HIGH / MEDIUM / LOW / NIT
   - Produces a corrective backlog (JSON)
   - Focus: security, architecture, correctness, maintainability

2. **`vibe-coding-refractaire`** — Anti-vibe coding pass
   - Focus: unnecessary abstractions, copy-paste, weak tests, architecture drift
   - Hater lens for generated/AI code specifically
   - Catches "looks correct but is actually lazy" patterns

3. **`review-remediator`** — Applies accepted fixes
   - Consumes the backlog from steps 1+2
   - Delegates fixes to the expert agents that match the detected stack (`dotnet-dev`, `python-dev`, frontend agent)
   - Validates and traces what was resolved vs deferred

## Audit Workflow

`audit-expert` performs periodic technical audits:

- Coverage: security, performance, scalability, code quality, patterns, debt
- Output: Markdown report in `audits/` folder
- Findings format: `[CAT-NNN]` identifiers (e.g., `SEC-001`, `PERF-003`)
- GitHub sync: creates/updates/closes issues with audit labels
- Reconciliation: compares successive audits to track resolution

## Quality Gates

| Gate | When | Tools |
|------|------|-------|
| Code graph impact | Before editing shared symbols | `gitnexus_impact` / graphify |
| TDD cycle | During implementation | `tdd-workflow` skill |
| Pre-merge review | Before PR merge | `review-expert` + `vibe-coding-refractaire` |
| Change scope validation | After implementation | `gitnexus_detect_changes` |
| Periodic audit | Monthly or on-demand | `audit-expert` |
