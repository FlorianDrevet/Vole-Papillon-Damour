---
name: tdd-workflow
description: "Use when: any code modification, feature implementation, bug fix, refactoring. Enforces TDD Red-Green-Refactor cycle. Mandatory for all coding agents."
---

# Skill : tdd-workflow - Cycle TDD obligatoire

> Charger ce skill des qu'un agent doit modifier, creer, ou corriger du code executable.
> Le TDD est obligatoire pour tout code de production.

---

## Cycle obligatoire : RED → GREEN → REFACTOR → VERIFY

### 1. RED - Ecrire le test en premier

- Ecrire un test qui echoue pour le comportement attendu
- Le test doit etre precis : un seul comportement par test
- Nommer le test avec la convention `Given_When_Then` ou `MethodName_Scenario_Expected`
- Verifier que le test echoue pour la bonne raison (pas une erreur de compilation)

### 2. GREEN - Implementer le minimum

- Ecrire le code de production minimal pour faire passer le test
- Ne pas anticiper les cas futurs
- Ne pas optimiser prematurement

### 3. REFACTOR - Ameliorer sans casser

- Nettoyer le code de production et les tests
- Supprimer la duplication
- Ameliorer les noms
- Verifier que tous les tests passent encore

### 4. VERIFY - Valider l'ensemble

- Lancer la suite de tests complete du projet ou du module
- Verifier qu'aucun test existant n'a ete casse

---

## Initialisation d'un projet de tests

Si le projet de tests n'existe pas :

1. Creer le projet sous `tests/<AssemblyName>.Tests/`
2. Ajouter les references NuGet de base : xUnit, FluentAssertions, NSubstitute
3. Ajouter une reference au projet cible
4. Verifier que `dotnet test` fonctionne

---

## Conventions de tests

- Un fichier de test par classe/handler/service teste
- Nommage : `<ClasseTestee>Tests.cs`
- Structure AAA : Arrange, Act, Assert
- Un seul assert logique par test (FluentAssertions groupes acceptes)
- Donnees de test deterministes (pas de `DateTime.Now`, pas de `Guid.NewGuid()` dans les assertions)

---

## Dette de tests

Si du code existant n'a pas de tests et que le TDD strict n'est pas applicable (ex: correction urgente, code existant trop couple) :

1. Documenter la dette dans `.github/test-debt.md`
2. Format : `- [ ] [Date] [Fichier/Classe] - Raison de la dette`
3. Ne pas laisser la dette s'accumuler sans trace

---

## Exceptions au TDD strict

Le TDD strict peut etre relache uniquement pour :
- Les fichiers de configuration pure (appsettings, DI registration)
- Les migrations EF Core
- Les fichiers de bootstrapping (`Program.cs`, `main.py`, `app.py`, `asgi.py`, `wsgi.py`)
- Le code genere automatiquement

Meme dans ces cas, un test d'integration ou de smoke est recommande.

---

## Integration avec les agents

- `@dev` doit rappeler le TDD dans toute delegation de code
- `@dotnet-dev` et `@python-dev` doivent charger ce skill avant toute implementation
- `@review-expert` doit verifier la presence de tests pour tout nouveau code
- `@vibe-coding-refractaire` doit signaler les "tests theatre" (tests qui ne testent rien)
