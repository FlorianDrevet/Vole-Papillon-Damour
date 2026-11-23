# Décisions de performance (recommandations payantes ou infra)

Journal des recommandations du skill [`perf-guardrails`](../../.github/skills/perf-guardrails/SKILL.md)
§B. L'agent y consigne chaque proposition et la réponse de l'utilisateur, pour ne pas
reposer la même question. Une reco refusée ne se représente que si son déclencheur change.

Statuts : `acceptée`, `refusée`, `reportée`.

| Date | Reco | Déclencheur | Gain / coût annoncés | Décision | Remarques |
|------|------|-------------|----------------------|----------|-----------|
| 2026-11-23 | Augmentation du SKU Azure SQL | Demande d'optimisation des performances ; l'utilisateur interdit toute hausse de SKU pendant cette implémentation. | Aucun palier supérieur proposé ni chiffrage ; la télémétrie du SSR lent ne montrait pas de saturation DTU soutenue. | refusée | Ne pas représenter cette hausse tant que les optimisations gratuites ne sont pas déployées et que les mesures ne montrent pas une saturation soutenue. |
