# Prompts d'exécution — Audit Scanette du 9 septembre 2026

Un fichier par lot. Chaque fichier est le brief complet d'un agent : contexte, périmètre, ce qu'il ne doit **pas** toucher, définition de terminé.

Audit de référence : [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)

---

## Le lanceur

Coller ceci dans Codex, en remplaçant le nom du fichier par le lot voulu :

```
Lis docs/audits/2026-09-09-audit-scanette-reprise-gestes/L0-filet-de-securite.md
et exécute-le intégralement.

Lis d'abord l'audit de référence qu'il cite, puis les fichiers du périmètre.
Ne traite que ce lot : si tu repères une anomalie d'un autre lot, note-la dans
le corps de la PR au lieu de la corriger.
```

Chemins exacts, dans l'ordre d'exécution :

| Lot | Fichier | Prérequis |
|---|---|---|
| **L0** | `L0-filet-de-securite.md` | aucun |
| **L1** | `L1-separer-les-etats.md` | L0 livré |
| **L2** | `L2-ecran-reprise.md` | L1 livré, **et export L0 récupéré depuis un téléphone réel** |
| **L3** | `L3-fiabiliser-la-session.md` | L2 livré |
| **L4** | `L4-navigation-unifiee.md` | L1 livré |
| **L5** | `L5-filet-responsable.md` | aucun — peut partir en parallèle de L1 |

---

## Trois règles qui valent pour tous les lots

1. **Un lot à la fois.** Six lots dans un seul prompt donnent six corrections à moitié faites. Chaque fichier se termine par la liste explicite de ce qu'il ne doit pas toucher : c'est la partie la plus importante du brief.
2. **`.github/skills/delivery-workflow` s'applique intégralement.** Worktree neuf depuis un `origin/main` fraîchement récupéré, branche dédiée, PR vers `main`. Un lot n'est pas terminé tant que l'URL de la PR n'a pas été rendue.
3. **`.github/skills/tdd-workflow` s'applique à tout code exécutable.** Les tests d'abord. La définition de terminé de chaque lot est écrite pour être vérifiable par un test, pas par une lecture.

## Rappel de contexte, à ne pas perdre entre les lots

Cet audit fait **suite** à `docs/audits/2026-09-08-audit-catalogue-scanette.md`. Le correctif de `SCAN-03` y recommandait de mettre en `Orphaned` les gestes d'une session liée à un autre compte. Cette recommandation a été appliquée correctement, mais **sans construire la sortie correspondante** : le statut `Orphaned` n'a aucune transition sortante.

Aucun lot ne doit revenir sur `SCAN-03`. Le refus de transmettre les gestes du bénévole A sous le jeton du bénévole B reste acquis. Ce qui est construit ici, c'est la porte de sortie qui manquait.
