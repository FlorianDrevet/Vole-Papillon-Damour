# Handoff Codex — ISBNdb, source principale des fiches livres

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Les fiches livres sont remplies d'abord par ISBNdb. La BnF, Open Library et Google Books
deviennent un secours temporaire, remplacé chaque nuit par un rattrapage.

Les livres absents d'ISBNdb sont listés dans l'administration pour y être complétés. Les
recommandations ne portent que sur des fiches définitives. L'administration voit le
quota. Sur le site public, le visiteur peut demander d'autres éditions à ISBNdb sous la
dernière page des références externes. La scanette signale qu'une autre édition de la
même œuvre est déjà en stock.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-isbndb-migration`
- Branche : `feat/isbndb-migration`. Elle contient déjà la conception : spec et plan. La
  PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/isbndb-migration/01-fonctionnel.md` : **la spec, elle fait foi**. Les
   décisions de la §11 sont acquises. Les mesures réelles de l'API sont en §10.
2. `docs/features/isbndb-migration/02-plan.md` : **le plan à exécuter** (tâches 0 à 19).
3. `docs/features/isbndb-migration/README.md` : il n'y a **pas de maquettes**.
4. Skills du repo : `cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `tdd-workflow`, `angular-patterns`, `ui-ux-front-saas`, `delivery-workflow`.
5. `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` : la charte visuelle
   du Catalog.
6. `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/OpenLibraryClientTests.cs` :
   le modèle `StubHttpMessageHandler` pour tester le client ISBNdb.

## Interfaces sans maquettes

L'utilisateur a abandonné les maquettes : ne dessine pas de nouveau style. Chaque écran
reprend l'écran existant de référence cité dans le plan, avec ses composants, ses
classes, ses variables CSS (`--catalog-*`, `--scan-*`) et son ton de libellés. Les
libellés donnés dans le plan sont à reprendre mot pour mot.

| Tâche | Écran | Référence existante |
|---|---|---|
| 14 | E4 recherche publique | `catalog-search-page`, section `external-block` |
| 15 | E1 carte ISBNdb | tuiles de la section `overview` de l'administration |
| 15 | E2 Livres hors ISBNdb | section `isbn-queue`, composant séparé comme `manual-requests-view` |
| 15 | E3 fiche | bloc métadonnées et `side-card` de la fiche du catalogue admin |
| 16 | E5 scanette | `tri-result` : `verdict-card`, `book-card`, `book-facts` |

Vérifier à 390 et 1440 px (E5 : 390 px seulement), sans défilement horizontal. Pour la
PR, capturer chaque écran à côté de son écran de référence.

## Spécifique à cette feature

- **Constantes non négociables.**
  - Requêtes groupées de 100 ISBN au plus.
  - Recherche publique en `pageSize=100`, `language=fr`, cache de 60 min.
  - Plafonds de la recherche publique : 10 par heure et par visiteur, 300 crédits par
    jour au total.
  - 3 nouvelles tentatives au plus sur un 429.
  - Pas de réserve de crédits pour le rattrapage (`RG-109`).
  - Le seuil « inutile d'en garder » ne compte l'œuvre que sur un lien fort : `workId`
    ou `otherIsbns` (`Q-01`).
  - Une fiche complétée n'est jamais écrasée (`Q-04`).
- **Interdits.**
  - Appeler `/stats`.
  - Stocker ou journaliser `image_original`.
  - Exposer un prix ISBNdb dans un contrat d'API ou une page.
  - Mettre la clé ISBNdb dans un fichier suivi, un log, une URL ou le navigateur.
  - Modifier les colonnes existantes de `Books` autrement que par ajout.
  - Supprimer les clients BnF, Open Library ou Google Books : ils restent le secours.
- **Clé de test.** L'utilisateur l'a fournie dans la conversation de conception. Si tu
  en as besoin pour le contrôle manuel de la tâche 19, demande-la-lui. Mets-la en
  `dotnet user-secrets` ou en variable d'environnement `Isbndb__ApiKey`, **jamais** dans
  un fichier du dépôt. Pas plus de 20 crédits pour toute la validation. Les tests
  automatisés n'appellent **jamais** l'API réelle.
- **Configuration.** Section `Isbndb` (tableau du plan). Clé absente : tout passe par le
  secours, sans erreur.
- **Azure et infra.**
  - Modifier `infra/` et `.github/workflows/infra-deploy.yml` (tâche 18).
  - `az bicep build` seulement.
  - Aucun déploiement, aucune commande `az` en écriture, aucune migration appliquée à
    une base distante.

## PR

- Titre : `feat(books): make ISBNdb the primary book notice source`
- « Risks and follow-up » :
  - créer le secret GitHub `ISBNDB_API_KEY`, puis lancer `Infra - deploy` (what-if puis
    deploy), puis `Books runtime - deploy` avec les migrations, puis Catalog et Scan ;
  - le premier rattrapage reprend toutes les fiches existantes : plusieurs nuits, un
    crédit par livre trouvé ;
  - le timer est en UTC (`0 30 2 * * *`), soit 3 h 30 ou 4 h 30 à Paris ;
  - l'état du quota peut retarder d'une requête ;
  - la table sujets → genres est à enrichir à partir du rapport E1 ;
  - les écrans sont sans maquettes : joindre les captures comparées aux écrans de
    référence ;
  - lister les échecs de la tâche 0.
