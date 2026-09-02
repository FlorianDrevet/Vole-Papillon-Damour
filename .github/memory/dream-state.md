# Dream State

## Gates

| Gate | Value |
|------|-------|
| `lastDreamDate` | 2026-05-19 |
| `sessionsSinceLastDream` | 1 |

## Config

| Key | Value |
|-----|-------|
| `codeGraphEngine` | graphify |

## Rules

- Time gate: at least 24h since `lastDreamDate`
- Session gate: `sessionsSinceLastDream` >= 5
- Both gates must pass to trigger a dream
