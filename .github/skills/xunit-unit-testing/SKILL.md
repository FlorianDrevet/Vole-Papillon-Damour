---
name: xunit-unit-testing
description: "Use when writing or updating .NET tests in this repository."
---

# Skill : xunit-unit-testing

Ce depot utilise xUnit pour les tests .NET existants.

## Tooling deja present

- `xunit`
- `FluentAssertions`
- `NSubstitute`
- `AutoFixture`
- `AutoFixture.AutoNSubstitute`

## Test Style

- Nommage : `MethodName_Scenario_Expected` ou `Given_When_Then`
- Structure AAA : Arrange, Act, Assert
- Donnees deterministes et lisibles
- Tester le comportement observable, pas l'implementation privee

## Repo-Specific Guidance

- Le projet de tests existant couvre aujourd'hui le domaine.
- Pour une nouvelle couverture applicative, ajouter le projet de test le plus proche de la couche modifiee au lieu d'entasser des tests hors contexte.
- Si un test manque et qu'aucun projet adapte n'existe encore, tracer la dette dans `.github/test-debt.md` si la tache ne peut pas inclure la creation du projet.

## Mocking Guidance

- Mocker les frontieres techniques, pas les objets de valeur ou le domaine pur.
- Utiliser `AutoFixture` pour reduire le bruit d'instanciation.
- Utiliser `FluentAssertions` pour des assertions lisibles.