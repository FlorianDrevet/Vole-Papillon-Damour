---
name: angular-patterns
description: "Use when generating, reviewing, refactoring, or testing Angular code in BackOffice or Website."
---

# Skill : angular-patterns

Charger ce skill pour toute tache Angular dans `src/BackOffice/` ou `src/Website/`.

## App Boundaries

- `BackOffice` et `Website` sont deux applications distinctes ; ne pas melanger leurs conventions ou assets sans raison explicite.
- Reprendre le pattern de donnees deja present dans l'application cible avant d'ajouter un nouveau service transverse.

## Angular Guidance

- Garder les composants centres sur l'affichage et les interactions locales.
- Garder les services centres sur l'acces donnees ou la logique transverse.
- Preferer des modeles types explicites pour les contrats HTTP.
- Eviter d'introduire une bibliotheque de state globale sans besoin demontre.
- Limiter les subscriptions manuelles lorsqu'un flux template ou une composition RxJS suffit.

## UI Stack

- Angular Material et Tailwind sont deja dans la stack ; les reutiliser au lieu d'introduire un second systeme visuel.
- Preserver la coherence typographique et spatiale de l'application cible.

## Testing

- Utiliser `ng test` pour les specs Angular.
- Ajouter des tests cibles quand un comportement de composant, service, guard, ou pipe change.

## Change Discipline

- Une modification de modele partage ou de service commun doit etre precedee d'une analyse d'impact structurelle.
- Toute modification de contrat backend doit etre reverifiee cote Angular.
