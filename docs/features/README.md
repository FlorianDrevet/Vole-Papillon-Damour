# Features — conception Claude, implémentation Codex

Un dossier par feature, créé par la skill Claude `/feature`
(`.claude/skills/feature/SKILL.md`) dans la branche `feat/<slug>` :

| Fichier | Phase | Auteur |
|---|---|---|
| `README.md` | statut des phases, liens | Claude |
| `01-fonctionnel.md` | spécification fonctionnelle, validée par l'utilisateur | Claude |
| `maquettes/preview/*.html` | écrans autonomes, zones `data-zone="E<n>-…"` | Claude Design (export) |
| `maquettes/sources/` | sources du canvas (`*.dc.html`, `canvas.json`) | Claude Design |
| `02-plan.md` | plan d'implémentation (chaque tâche d'interface cite sa page et ses zones) | Claude |
| `HANDOFF.md` | ce que Codex exécute | Claude |

Codex l'exécute avec la skill `.agents/skills/implement-handoff/SKILL.md` puis ouvre la
PR, qui livre conception et implémentation ensemble.

Avant le passage à Codex, `python .claude/skills/feature/scripts/check_zones.py docs/features/<slug>`
vérifie que chaque zone de maquette est couverte par le plan.
