---
name: cqrs-feature
description: "Use when touching backend feature slices in the .NET CQRS stack of this repository."
---

# Skill : cqrs-feature

Charger ce skill pour toute tache backend qui touche `Api`, `Application`, `Contracts`, `Infrastructure`, ou `Domain` autour d'une feature fonctionnelle.

## Workflow attendu

1. Mettre a jour le contrat dans `Vole_Papillon_Damour.Contracts/` si la forme HTTP change.
2. Mettre a jour l'entree HTTP dans `Vole_Papillon_Damour.Api/Controllers/` ou dans la methode `Use...Controller()` appropriee.
3. Creer ou modifier la commande/requete dans `Vole_Papillon_Damour.Application/<Feature>/`.
4. Garder le handler et le validator au plus pres de la feature.
5. Modifier le domaine uniquement si une regle metier ou un invariant change vraiment.
6. Modifier `Infrastructure` seulement pour les frontieres techniques : repositories, auth, email, OCR, blob, table storage.

## Regles CQRS

- Une commande ou requete par intention claire.
- Les commandes mutent ; les queries lisent.
- Les validators vivent pres de la requete ou de la commande.
- Les handlers restent minces : orchestration, pas de logique d'infrastructure inline.
- Les erreurs et resultats doivent rester coherents avec les conventions deja presentes dans le slice cible.

## Impact multi-surface

- Toute modification de `Contracts` doit etre revue cote `BackOffice`, `Website`, et `MauiCashApp` si le contrat est consomme.
- Toute modification d'une interface de repository ou de service partage demande une analyse d'impact structurelle avant edition.

## Tests

- Tester d'abord le comportement du handler, du validator, ou de la regle de domaine modifiee.
- Si le test project n'existe pas pour la couche cible, documenter la dette dans `.github/test-debt.md` avant de continuer sans TDD strict.
