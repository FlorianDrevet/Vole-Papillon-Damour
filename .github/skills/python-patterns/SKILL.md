---
description: "Use when: generating, reviewing, refactoring, or testing backend Python code. Covers clean Python structure, FastAPI, Django, Flask, typing, validation, persistence boundaries, and pragmatic maintainability without overengineering."
---

# Skill : python-patterns

Charger ce skill pour toute tache backend Python.

## Standard de qualite

- La clarte prime sur la cleverness.
- Le code doit rester simple a lire, a modifier et a tester.
- Ne pas introduire de pattern lourd sans probleme concret a resoudre.
- Preferer des modules petits, des fonctions explicites et des contrats types.

## Regles generales

- Suivre PEP 8 et le style deja present dans le projet.
- Utiliser des **type hints** pour clarifier les contrats publics.
- Preferer `dataclass`, modeles du framework, ou classes explicites a `dict[str, Any]` si le schema est connu.
- Garder les fonctions courtes, a faible profondeur de branchement.
- Eviter les helpers fourre-tout, metaclasses, decorators complexes et sur-abstraction prematuree.
- Laisser le framework a la frontiere ; garder la logique metier testable hors transport HTTP quand cela simplifie le code.

## Organisation par defaut

- Regrouper le code par feature ou responsabilite metier.
- Une responsabilite principale par module.
- N'introduire une couche `services/`, `repositories/`, `adapters/` que si elle clarifie une separation reelle.
- Ne pas recopier une architecture enterprise generique si le projet n'en a pas besoin.

## Validation, erreurs, configuration

- Valider les entrees au plus pres de la frontiere.
- Utiliser des exceptions explicites et limitees a des cas metier ou techniques clairs.
- Centraliser la configuration dans un point unique deja etabli par le projet.
- Eviter les magic strings pour les cles de config, types d'evenements, noms de taches et statuts.

## I/O et persistence

- Separer la logique metier de l'I/O quand cela ameliore la lisibilite.
- Ne pas propager des objets techniques du framework partout dans le code metier.
- Garder les requetes, mappings et transactions comprehensibles.
- Optimiser seulement quand un besoin de performance ou de volume est etabli.

## Async

- Utiliser `async` uniquement lorsqu'il y a un vrai benefice I/O.
- Ne pas melanger sans raison appels sync et async dans la meme chaine.
- Garder les points d'entree async fins et explicites.

## Tests

- Le skill `tdd-workflow` reste obligatoire.
- Preferer `pytest` si le projet l'utilise deja.
- Tester le comportement observable plutot que l'implementation interne.
- Favoriser des fixtures petites et explicites.
- Mocker les frontieres, pas chaque appel interne.

## FastAPI

Charger cette section si le projet expose `FastAPI`, `APIRouter`, `Depends`, `pydantic`, ou `uvicorn`.

- Garder les endpoints fins : parsing, appel service, mapping de reponse.
- Utiliser des modeles de requete/reponse explicites.
- Limiter `Depends()` aux frontieres utiles.
- Eviter de mettre la logique metier directement dans les route handlers.
- Utiliser l'async seulement pour les appels I/O reels.
- Eviter les singletons implicites et l'etat global mutable.

## Django

Charger cette section si le projet contient `manage.py`, `settings.py`, `INSTALLED_APPS`, ou des imports `django.`.

- Respecter la structure en apps du projet si elle existe.
- Garder les vues fines et lisibles.
- Eviter de surcharger les modeles avec de la logique transverse confuse.
- Sortir la logique metier complexe des vues/admin/forms vers des modules explicites si cela clarifie le flux.
- Faire attention aux acces ORM : `select_related`, `prefetch_related`, transactions et contraintes d'integrite.
- Suivre les conventions existantes avant d'introduire une nouvelle organisation.

## Flask

Charger cette section si le projet utilise `Flask`, `Blueprint`, `current_app`, ou une app factory.

- Suivre l'app factory si elle existe deja.
- Organiser les routes via des blueprints par feature ou zone fonctionnelle.
- Eviter de cacher de l'etat metier dans `g`, `current_app`, ou des globals.
- Garder les handlers HTTP fins et pousser la logique dans des modules explicites si besoin.
- Ne pas reconstruire artificiellement une architecture lourde autour d'un service leger.

## Outils de qualite

- Si le projet utilise deja `ruff`, `black`, `isort`, `mypy`, `pyright`, `pytest`, ou `uv`, s'y conformer.
- Ne pas imposer de nouvel outil sans signal clair du projet ou besoin explicite.

## Sortie attendue

Un code backend Python propre, direct, type quand cela aide, simple a maintenir,
respectant le framework detecte et sans architecture decorative.