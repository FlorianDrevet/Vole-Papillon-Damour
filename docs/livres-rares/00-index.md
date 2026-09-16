# Module « Livres rares » — plan d'implémentation

Ce dossier est le plan d'implémentation complet du module *Livres rares*. Il est écrit
pour être exécuté par une session repartant de zéro : chaque document se suffit à
lui-même et renvoie aux fichiers réels du dépôt, jamais à une conversation.

## Origine

- **Demande initiale** : un onglet dédié aux livres rares dans le portail
  d'administration et dans l'application de scan, avec prix, photos multiples, saisie
  possible sans ISBN, et un nouveau rôle bénévole.
- **Maquettes** : projet Claude Design « Catalogue Livres »
  (`e18d5045-653e-4cc5-b2c0-f29ab5ba1bd2`), artboards `LivresRares`,
  `LivresRaresMobile`, `FicheRare`, `AdminFicheRare`, `CaisseRare`.
- **Base de code** : `origin/main` à `62a0b4e`
  (*refactor(catalog-admin): remove inventory workspace (#195)*). L'onglet Inventaire
  n'existe plus ; le plan en tient compte.

## Documents

| Fichier | Contenu |
|---|---|
| [`01-decisions-et-portee.md`](01-decisions-et-portee.md) | Les 10 décisions structurantes, les règles métier renversées, les 5 questions encore ouvertes, ce qui est hors périmètre |
| [`02-modele-de-donnees.md`](02-modele-de-donnees.md) | Agrégat `RareBook`, entité `RareBookPhoto`, value objects, configuration EF, migration, reprise des données existantes |
| [`03-application-et-api.md`](03-application-et-api.md) | Commandes et requêtes CQRS, endpoints, contrats, stockage des photos |
| [`04-role-livres-rares.md`](04-role-livres-rares.md) | Le rôle `LivresRares` : Entra, politiques backend, trois frontends |
| [`05-catalogue-public.md`](05-catalogue-public.md) | Page liste, fiche publique, intégration accueil et recherche, alertes |
| [`06-portail-administration.md`](06-portail-administration.md) | Section admin, liste, fiche d'édition, gestion des photos, vue restreinte |
| [`07-scanette.md`](07-scanette.md) | Onglet de gestion, caisse enrichie, recherche hors ligne, synchronisation |
| [`08-lots-et-sequencement.md`](08-lots-et-sequencement.md) | L'ordre d'exécution : 11 lots, leurs dépendances, leurs critères de sortie |

## Comment l'exécuter

1. Lire `01` en entier. Il contient des décisions qui **renversent des règles métier
   écrites** (`RG-50`, `RG-51`) : ne pas commencer sans les avoir comprises.
2. Trancher les questions ouvertes de `01` §4 avec l'association. La question `Q1`
   (prix des livres ordinaires) change le modèle de données : elle bloque le lot 6.
3. Suivre `08`. Les lots sont ordonnés par dépendance, pas par surface.
4. Chaque lot suit le cycle TDD du dépôt
   (`.github/skills/tdd-workflow/SKILL.md`) et le workflow de livraison
   (`.github/skills/delivery-workflow/SKILL.md`) : worktree dédié, branche dédiée,
   validation locale, PR.

## Conventions du dépôt à respecter

- Backend .NET 10, CQRS par couches : `Domain` → `Application` → `Api`, contrats dans
  `Contracts`. Voir `.github/skills/cqrs-feature/SKILL.md`.
- Endpoints en *minimal API*, déclarés dans
  `src/Backend/Vole_Papillon_Damour.Api/Controllers/*Controller.cs` via des méthodes
  d'extension `UseXxxController`.
- Tests xUnit + FluentAssertions + NSubstitute + AutoFixture.
  Voir `.github/skills/xunit-unit-testing/SKILL.md`.
- Frontends Angular 21. Le catalogue public et le portail d'administration sont la
  **même application** (`src/Catalog`) ; `src/BackOffice` est une application
  distincte qui n'est pas concernée par ce module.
- Après toute modification de code : `graphify update .`
