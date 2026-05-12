# Dream State

## Gates

| Gate | Value |
|------|-------|
| `lastDreamDate` | 2025-07-23 |
| `sessionsSinceLastDream` | 0 |

## Config

| Key | Value |
|-----|-------|
| `codeGraphEngine` | both |

## Rules

- Time gate: at least 24h since `lastDreamDate`
- Session gate: `sessionsSinceLastDream` >= 5
- Both gates must pass to trigger a dream
- Dream lock: `$env:TEMP\socle-agents-dream-lock`
- If lock exists and is >30min old, consider stale and retry once
