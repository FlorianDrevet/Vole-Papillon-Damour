# Handoff Codex — <Titre de la feature>

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

<2 à 4 phrases : ce que l'utilisateur final pourra faire, sur quelles surfaces.>

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-<slug>`
- Branche : `feat/<slug>`, qui contient déjà la conception (spec, maquettes, plan). La PR
  livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/<slug>/01-fonctionnel.md` : **la spec, elle fait foi**
2. `docs/features/<slug>/02-plan.md` : **le plan à exécuter** (<N> tâches)
3. `docs/features/<slug>/maquettes/README.md`, puis `maquettes/preview/index.html`
4. Skills du repo concernées : <ex. cqrs-feature, dotnet-patterns, angular-patterns, ui-ux-front-saas>
5. <autres fichiers de référence utiles, avec la raison>

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| <n> | `<Page>.html` | `E1-…`, `E1-…` |

Largeurs de vérification : <390 et 1440 px, ou autres si la spec le précise>.

## Spécifique à cette feature

- **Constantes non négociables** : <valeurs, seuils>
- **Interdits** : <technos, ressources, fichiers à ne pas toucher>
- **Flags / configuration** : <ex. désactivé par défaut>
- **Azure / infra** : <ce qui est permis ; par défaut aucun déploiement ni `az` en écriture>

## PR

- Titre : `<type>(<scope>): <résumé en anglais>`
- « Risks and follow-up » : <points connus à reporter>
