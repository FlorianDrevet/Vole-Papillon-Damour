# Dream State

## Gates

| Gate | Value |
|------|-------|
| `lastDreamDate` | 2026-09-03 |
| `sessionsSinceLastDream` | 3 |

## Config

| Key | Value |
|-----|-------|
| `codeGraphEngine` | graphify |

## Rules

- Time gate: at least 24h since `lastDreamDate`
- Session gate: `sessionsSinceLastDream` >= 5
- Both gates must pass to trigger a dream
