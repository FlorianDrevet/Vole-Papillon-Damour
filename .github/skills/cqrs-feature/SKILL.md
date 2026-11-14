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
- Pour une nouvelle feature, verifier que les tests rouges echouent pour la bonne raison avant l'implementation, puis relancer les tests cibles apres chaque couche modifiee.
- Apres les suites de tests vertes, executer Stryker.NET sur les fichiers métier modifies (Domain, Application, ou Worker), avec un filtre `-m` par fichier. Depuis `src/Backend/` :
  ```powershell
  dotnet tool restore
  dotnet stryker -s Vole_Papillon_Damour.slnx -r json -r html -m '**/<Feature>Handler.cs' -m '**/<Feature>Validator.cs'
  ```
  Remplacer les motifs par les fichiers reels du changement. Cette solution utilise xUnit v2 avec VSTest : conserver le runner par defaut et ne pas reprendre `-t mtp` du template. Ne pas muter le cablage DI, `Program.cs`, les configurations EF ou les migrations. Examiner `StrykerOutput/*/reports/mutation-report.json` : ajouter des tests comportementaux pour les survivants utiles, ou justifier les mutants equivalents et `NoCoverage`; ne pas viser un score arbitraire. Les bornes FluentValidation (`MaximumLength`, `GreaterThan`, etc.) restent couvertes par des tests explicites.
