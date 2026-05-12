---
description: "Skill for updating MEMORY.md and .github/memory thematic files. Defines routing, formatting, validation, and Dream-aware memory maintenance."
applyTo: "MEMORY.md,.github/memory/**/*.md"
---

# Skill : memory-management

Charger ce skill pour toute mise a jour de `MEMORY.md` ou de `.github/memory/`.

## Regles de base

- `MEMORY.md` reste un index leger (~80 lignes max).
- Les details vont dans les fichiers thematiques sous `.github/memory/`.
- `changelog.md` recoit une ligne pour toute tache non triviale.
- `dream-state.md` ne sert qu'a gerer les gates Dream et stocker le choix de code graph engine.
- Ne jamais ecrire de secrets ou de speculation.

## Routage

| Information | Fichier |
|-------------|---------|
| Vision rapide, table des thematiques | `MEMORY.md` |
| Structure du projet | `01-solution-overview.md`, `02-project-structure.md` |
| Runtime, API, domain model | `03-*` selon le projet |
| Frontend | `04-frontend.md` |
| Donnees, persistance | `05-data-and-storage.md` |
| Agents et skills | `06-agents-skills.md` |
| Code graph (GitNexus ou Graphify) | `07-code-graph.md` |
| Historique des mises a jour memoire | `changelog.md` |
| Gates Dream + choix code graph | `dream-state.md` |

## Format de dream-state.md

```markdown
# Dream State

## Gates

| Gate | Value |
|------|-------|
| `lastDreamDate` | YYYY-MM-DD |
| `sessionsSinceLastDream` | N |

## Config

| Key | Value |
|-----|-------|
| `codeGraphEngine` | gitnexus / graphify / both |

## Rules

- Time gate: at least 24h since `lastDreamDate`
- Session gate: `sessionsSinceLastDream` >= 5
- Both gates must pass to trigger a dream
```

## Dream-aware maintenance

- `@dev` incremente `sessionsSinceLastDream` a chaque session.
- `@dream` consolide, deduplique et prune.
- L'index doit rester court et scannable.
- Les fichiers thematiques ne doivent pas depasser ~150 lignes.
- Le changelog ne garde que les 60 derniers jours.
