# Plan d'implémentation — Performance de la recherche publique et du portail d'administration

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**Objectif.** Ramener la recherche publique sous 1 s quel que soit le genre (`RG-155`). Rendre
le portail d'administration utilisable en moins de 2 s (`RG-159` à `RG-167`) et retirer quatre
indicateurs du tableau de bord (`RG-168`), sans changer aucun autre chiffre (`RG-165`).

**Architecture.** Le backend corrige le plan SQL instable de la recherche (genre en constante,
page en deux temps, exclusion des sessions de scan précalculée, index couvrants). Il supprime
les calculs lourds du tableau de bord ou les déplace vers le Worker, expose un endpoint unique
de compteurs et projette les statistiques au lieu de matérialiser des tables entières. Le
front Catalog charge chaque section à la demande, en parallèle et par bloc (squelettes,
erreurs locales), garde les résultats affichés pendant un rafraîchissement et prépare la page
suivante de la recherche.

**Pile.** .NET 10, EF Core 10 SQL Server (tests applicatifs sur SQLite), MediatR et ErrorOr
(`cqrs-feature`), Azure Functions isolées (Worker), Angular (Catalog et BackOffice),
HybridCache déjà enregistré (`Infrastructure/DependencyInjection.cs:238`).

**Mesure de référence.** `perf/README.md` : seed local, scripts de plans, chiffres avant
correctif. Chaque critère de performance se vérifie avec ces scripts.

## Règles transverses

- Lire avant d'écrire : `.github/skills/cqrs-feature`, `dotnet-patterns`, `tdd-workflow`,
  `xunit-unit-testing`, `angular-patterns`, `ui-ux-front-saas`, `local-auth-testing`.
- **Mêmes chiffres** (`RG-165`) : toute réécriture de requête garde ou ajoute un test qui
  compare la sortie à celle d'avant sur un jeu figé. Les tests existants des handlers sont
  la référence ; ne pas changer leurs valeurs attendues, sauf pour les champs supprimés par
  `RG-168`.
- **Tests sur SQLite** : rien de spécifique à SQL Server dans un chemin commun. Une astuce
  propre à SQL Server (indice de requête, `OPTION`) n'est appliquée que si
  `Database.IsSqlServer()`.
- **Contrats** : le BackOffice consomme les mêmes endpoints admin. On n'y retire que les
  champs de `RG-168` ; tout autre champ reste présent et de même type.
- Variables CSS existantes uniquement (`--admin-*` dans
  `catalog-administration-page.component.scss`, jetons du catalogue public) ; aucun
  hexadécimal des maquettes en dur.

---

## Tâche 0 — État initial

- `dotnet build src/Backend/Vole_Papillon_Damour.slnx` puis
  `dotnet test src/Backend/Vole_Papillon_Damour.slnx`.
- `npm ci`, `npm test -- --watch=false` puis `npm run build` dans `src/Catalog`, puis dans
  `src/BackOffice`.
- Noter dans le compte rendu les échecs déjà présents, sans les corriger. `NEXT.md` cite des
  avertissements préexistants de budget Angular et CommonJS.

Pas de commit.

---

## Lot A — Recherche publique

### Tâche 1 — Index couvrants et index de jointure

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/BookConfiguration.cs`
- `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/BookMovementConfiguration.cs`
- Nouvelle migration `OptimizeCatalogAndAdminReadIndexes` (`dotnet ef migrations add`, projet
  Infrastructure)
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Persistence/ProjectDbContextModelTests.cs`

**Étapes :**
1. Test d'abord : le modèle expose sur `Books` :
   - `IX_Books_PublicCatalog_FirstSeenAt` et `IX_Books_PublicCatalog_UpdatedAt`, avec les
     colonnes incluses `QuantityAvailable`, `MetadataStatus`, `IsFavoriteReference` ;
   - `IX_Books_IsbndbSourceFlags` avec `IsbndbCompletedAt` inclus.

   Il expose sur `BookMovements` : `IX_BookMovements_ScanSessionId` avec `Isbn13` inclus.
2. Ajouter `IncludeProperties` à ces quatre index, sans changer clés, ordres ni filtres
   existants.
3. Générer la migration et relire le SQL : uniquement `DROP INDEX` / `CREATE INDEX` des
   quatre index.

**Commit :** `perf(db): cover public catalog and scan-session lookups with included columns`

### Tâche 2 — Exclusion des fiches en cours de scan précalculée

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Common/PublicCatalogBookVisibility.cs`
- Appelants : `Books/Queries/SearchCatalog/SearchCatalogQueryHandler.cs`,
  `Books/Queries/GetPublicBook/GetPublicBookQueryHandler.cs`,
  `Books/Queries/GetPublicWork/GetPublicWorkQueryHandler.cs`,
  `Books/Queries/GetPublicCatalogSitemap/GetPublicCatalogSitemapQueryHandler.cs`,
  `Recommendations/Queries/GetSimilarBooks/GetSimilarBooksQueryHandler.cs`,
  `Isbndb/Queries/SearchIsbndbEditions/SearchIsbndbEditionsQueryHandler.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Application.tests/Books/Queries/PublicCatalogQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - une fiche dont un mouvement appartient à une session `InProgress` reste invisible de
     la recherche, de la fiche publique et du sitemap ;
   - elle redevient visible dès que la session est `Completed` ;
   - sans session en cours, la commande SQL de la page ne contient plus de sous-requête
     sur `BookMovements` (vérifier avec l'intercepteur de commandes déjà utilisé par ces
     tests).
2. Ajouter `GetInProgressScanIsbnsAsync(IProjectDbContext, CancellationToken)`. Elle lit
   `ScanSessions` où `Status == InProgress`, puis les `Isbn13` distincts de leurs mouvements,
   en deux requêtes simples sans corrélation par fiche.
3. Remplacer le `NOT EXISTS` corrélé de `FilterPublicCatalogBooks` par une surcharge qui
   reçoit ce tableau : pas de prédicat s'il est vide, `!isbns.Contains(book.Id)` sinon.
   Migrer les six appelants. La règle reste stricte et sans délai (spec §9) : aucun cache.

**Commit :** `perf(catalog): resolve in-progress scan exclusions once per request`

### Tâche 3 — Facettes de genre en cache, genre en constante

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/SearchCatalog/SearchCatalogQueryHandler.cs`
- Nouveau : `src/Backend/Vole_Papillon_Damour.Application/Books/Common/PublicCatalogGenreFacets.cs`
- Test : `PublicCatalogQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - `genre=Roman` produit une commande SQL où le genre est un **littéral**
     (`N'Roman'`), pas un paramètre ;
   - un genre absent des facettes (`genre=Inconnu`) renvoie une page vide, `totalCount`
     cohérent, sans requête de page ;
   - les facettes renvoyées sont identiques avant et après, à deux appels successifs ;
   - la casse et les accents suivent la collation actuelle (`roman` donne les mêmes
     résultats que `Roman`).
2. Extraire le calcul des facettes (lignes 142 à 155 actuelles) dans
   `PublicCatalogGenreFacets`. Le mettre en cache avec `HybridCache` (clé
   `catalog:genre-facets`, 5 min). Le comportement fonctionnel ne change pas : les
   facettes ne dépendent d'aucun filtre.
3. Résoudre le genre demandé contre les noms de facettes (comparaison insensible à la
   casse et aux accents, comme la collation `Latin1_General_100_CI_AI`). Inconnu : page
   vide immédiate. Connu : filtrer avec `EF.Constant(nomCanonique)`. Le jeu de genres est
   borné (28 en production), donc le nombre de plans en cache aussi, et chaque genre a
   son propre plan, compilé pour sa propre cardinalité (`RG-155`).

**Commit :** `perf(catalog): stabilize genre filter plans and cache genre facets`

### Tâche 4 — Page en deux temps, sans colonnes lourdes

**Fichiers :**
- `SearchCatalogQueryHandler.cs`
- `src/Backend/Vole_Papillon_Damour.Application/Books/Common/PublicCatalogProjector.cs`
- Test : `PublicCatalogQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - pour chaque tri (récent, pertinence avec et sans recherche) et chaque disponibilité,
     les ISBN, leur ordre, `hasMore` et `totalCount` sont identiques à ceux d'avant
     (`RG-165`) ;
   - la commande SQL de la page ne sélectionne ni `RawPayload`, ni `Summary`, ni
     `ManuallyEditedFields`.
2. Temps 1 : `orderedBooksQuery.Select(book => book.Id).Skip(offset).Take(pageSize + 1)`.
3. Temps 2 : charger pour ces ISBN une projection limitée aux champs de
   `PublicCatalogBookResult`, genres compris, puis réordonner en mémoire selon le temps 1.
4. Adapter `PublicCatalogProjector.Project` à cette projection. Conserver le filtre
   `IsVisible`, que la projection doit pouvoir évaluer.

**Commit :** `perf(catalog): page public search by key first, then project display fields`

### Tâche 5 — Recherche publique : rafraîchissement sans clignotement, page suivante préparée

**Maquette :** preview/E5-Desktop.html — zones E5-filtre-genre, E5-indicateur-rechargement, E5-resultats-estompes, E5-pagination, E5-grille-squelette

**Fichiers :**
- `src/Catalog/src/app/features/search/catalog-search-page.component.ts`, `.html`, `.scss`
- `src/Catalog/src/app/core/catalog-api.service.ts`
- Nouveau : `src/Catalog/src/app/core/catalog-search-cache.service.ts`
- Tests : `catalog-search-page.component.spec.ts` et un spec pour le cache

**Étapes :**
1. Tests d'abord :
   - **`RG-157`** : deux changements de filtre rapprochés n'affichent que la seconde réponse,
     et la première requête HTTP est **annulée** (`HttpTestingController`, `cancelled`) ;
   - **`RG-156`** : pendant le chargement d'une nouvelle page, les livres précédents restent
     dans le DOM avec la classe estompée et l'indicateur est visible. Le squelette
     n'apparaît qu'au premier chargement, quand il n'y a encore aucune réponse ;
   - **`RG-158`** : après affichage de la page 1 avec `hasMore`, la page 2 est demandée en
     arrière-plan. Un clic sur « → » l'affiche sans nouvelle requête. Un retour arrière
     dans la minute ne relance rien.
2. Remplacer `loadLocalResults()` et `localLoadVersion` par un `Subject` de paramètres et un
   `switchMap`. L'annulation HTTP remonte jusqu'au `CancellationToken` de l'API.
3. `CatalogSearchCacheService` : réponses par clé de paramètres normalisés, 60 s, 20 entrées
   au plus. Préchargement de `page + 1` quand `hasMore`.
4. Gabarit : garder `vpd-loader` pour le premier chargement (`E5-grille-squelette`). Pour un
   rafraîchissement, garder la liste et appliquer l'estompage (`E5-resultats-estompes`,
   opacité, `transition` courte neutralisée par `prefers-reduced-motion`). Ajouter un
   indicateur `role="status"` (`E5-indicateur-rechargement`) : « Filtre « Roman » en
   cours… » pour un genre, « Page N… » pour une page, « Recherche en cours… » sinon.
   Pagination (`E5-pagination`) et filtres (`E5-filtre-genre`) restent actifs.
5. Vérifier à 390 px avec `preview/E5-Mobile.html` : mêmes comportements, indicateur
   « Page 2… » compact.

**Commit :** `perf(catalog-front): keep results during refresh, cancel stale searches and prefetch next page`

---

## Lot B — Portail d'administration, backend

### Tâche 6 — Retrait des quatre indicateurs (`RG-168`)

**Maquette :** preview/E1-Charge-Desktop.html — zones E1-carte-disponibles, E1-carte-tries, E1-carte-vendus, E1-carte-jamais-vendus

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/Admin/GetCatalogAdminOverviewQueryHandler.cs`
- `src/Backend/Vole_Papillon_Damour.Application/Books/Common/AdministrationResults.cs`
- `src/Backend/Vole_Papillon_Damour.Contracts/Books/Responses/AdminResponses.cs`
- `src/Backend/Vole_Papillon_Damour.Api/Controllers/BookAdministrationController.cs` (`ToResponse`, vers la ligne 1350)
- `src/Catalog/src/app/core/catalog.models.ts`, `features/administration/catalog-administration-page.component.html` (cartes, lignes 191 à 238)
- `src/BackOffice/src/app/shared/models/catalog-admin.model.ts`, `src/BackOffice/src/app/feature/catalog-administration/catalog-administration.component.html` (lignes 43 à 48)
- Tests : `Application.tests/Books/Queries/Admin/GetCatalogAdminOverviewQueryHandlerTests.cs`, les deux specs de composants

**Étapes :**
1. Tests d'abord :
   - le résultat d'overview n'a plus `RareQueueCount`, `MetadataMissingCount`,
     `InventoryDriftTitleCount`, `InventoryDriftQuantity` ni `PendingAlerts`, et aucune
     requête sur `BookMovements` agrégée par fiche ni sur la file d'alertes n'est émise
     (`CA-11`) ;
   - les autres champs gardent leurs valeurs (`RG-165`) ;
   - côté Catalog, le tableau de bord affiche exactement quatre cartes, dans l'ordre :
     « Disponibles et annoncés », « Livres triés », « Livres vendus », « Titres jamais
     vendus ».
2. Supprimer le calcul `inventoryDifferences`, l'appel `bookAlertOutbox.GetAdminPageAsync`,
   et les agrégats `RareTitleCount` et `MetadataToReviewCount` (le paramètre
   `IBookAlertOutbox` du handler disparaît s'il n'a plus d'usage).
3. Supprimer les champs du résultat, du contrat, de `ToResponse` et des deux modèles front.
4. Catalog : retirer les cartes « E-mails en attente », « Rares à expertiser », « Fiches
   sans métadonnées » et « Écart d'inventaire » ; la grille `stat-grid` à 4 colonnes se
   recompose sur une seule rangée.
5. BackOffice : retirer la carte « Alertes en attente » et le panneau « Contrôles de
   cohérence ». La carte « À traiter » devient `undatedAnnouncementCount + deadStockCount`,
   avec le sous-titre « dates · dormant ».
6. La liste « Alertes en attente d'envoi » du tableau de bord Catalog **reste** : elle
   provient des sessions, pas de l'overview.

**Commit :** `feat(admin): drop inventory drift, pending e-mails, rare queue and missing-metadata cards`

### Tâche 7 — « Titres jamais vendus » calculé par le Worker (`RG-163`)

**Maquette :** preview/E1-Charge-Desktop.html — zones E1-heure-calcul ; preview/E1-Etats.html — zones E1-pas-encore-calcule

**Fichiers :**
- Nouveau domaine/persistance : entité singleton `AdminDashboardSnapshot` (`Id`,
  `DeadStockCount`, `DeadStockMinAgeDays`, `DeadStockMinQuantity`, `ComputedAt`), table
  `AdminDashboardSnapshots`, configuration EF et `DbSet` dans `ProjectDbContext` /
  `IProjectDbContext`, migration `AddAdminDashboardSnapshot` (additive)
- Nouveau : `src/Backend/Vole_Papillon_Damour.Application/Books/Commands/Admin/RefreshAdminDashboardSnapshotCommand.cs` et son handler
- Nouveau : `src/Backend/Vole_Papillon_Damour.Worker/AdminDashboardSnapshotFunction.cs`, sur le modèle de `BookSweepFunction.cs`, minuterie `0 */10 * * * *`
- `GetCatalogAdminOverviewQueryHandler.cs`, résultat, contrat, `ToResponse`, modèles front
- Tests : nouveau `Application.tests/Books/Commands/Admin/RefreshAdminDashboardSnapshotCommandHandlerTests.cs`, `GetCatalogAdminOverviewQueryHandlerTests.cs`, spec Catalog

**Étapes :**
1. Tests d'abord :
   - le handler de rafraîchissement produit **le même nombre** que le calcul actuel de
     `deadStockCount` (lignes 122 à 136), sur les jeux de test existants (`RG-165`) ;
   - l'overview lit l'instantané et expose `DeadStockCount` (`int?`) et
     `DeadStockComputedAt` (`DateTimeOffset?`) ;
   - sans instantané, les deux valent `null` ;
   - si les seuils des paramètres ont changé depuis le calcul, l'overview renvoie quand
     même la valeur et son heure, et le prochain passage du Worker recalcule.
2. Déplacer le calcul du stock dormant de l'overview vers le handler de rafraîchissement
   (upsert du singleton).
3. Worker : nouvelle fonction de minuterie toutes les 10 minutes, journal d'une ligne
   (valeur, durée). Un calcul supplémentaire au démarrage de la fonction n'est pas requis.
4. Contrat : `DeadStockCount` devient nullable et `DeadStockComputedAt` s'ajoute. Le
   BackOffice traite `null` comme 0 dans « À traiter ».
5. Catalog :
   - carte « Titres jamais vendus », pied « Calculé à HH:MM · file de retrait »
     (`E1-heure-calcul`), heure de Paris, au format de `formatIsbndbParisTime` ;
   - sans calcul : valeur « — », sous-titre « Premier calcul en cours, disponible dans
     quelques minutes », pied « Pas encore calculé » (`E1-pas-encore-calcule`). Jamais
     « 0 ».

**Commit :** `perf(admin): precompute never-sold titles in the worker and show computation time`

### Tâche 8 — Overview : moins de requêtes, cache court

**Fichiers :** `GetCatalogAdminOverviewQueryHandler.cs`, test associé.

**Étapes :**
1. Tests d'abord :
   - valeurs identiques (`RG-165`) ;
   - au plus 6 commandes SQL pour un appel (compter avec un intercepteur) ;
   - deux appels successifs dans les 30 s n'émettent qu'une seule série de requêtes.
2. Fusionner en un seul agrégat les comptes sur `Books` restants (quantité et titres
   disponibles, hors ISBNdb à compléter) et, dans un autre, les trois agrégats d'annonces.
3. Mettre le résultat en cache `HybridCache` pendant 30 s, clé incluant `from`, `to` et
   `allTime` (`RG-164` admet une minute de retard).

**Commit :** `perf(admin): aggregate overview counters and cache them briefly`

### Tâche 9 — Endpoint unique des compteurs de navigation (`RG-162`)

**Maquette :** preview/Main.html — zones E2-pastille-squelette ; preview/E1-Charge-Desktop.html — zones E2-pastille, E1-tuiles-attention

**Fichiers :**
- Nouveau : `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/Admin/GetAdminNavigationCountsQuery.cs` et son handler
- `BookAdministrationController.cs` : `GET /books/admin/navigation-counts`, `RequireAuthorization("RareBooks")`
- Contrat dans `src/Backend/Vole_Papillon_Damour.Contracts/Books/Responses/AdminResponses.cs`
- Tests : nouveau `Application.tests/Books/Queries/Admin/GetAdminNavigationCountsQueryHandlerTests.cs`, `Api.tests/EndpointAuthorizationTests.cs`

**Étapes :**
1. Tests d'abord. Chaque nombre est égal à celui que produit aujourd'hui la source
   correspondante :

   | Champ | Source actuelle |
   |---|---|
   | `openScanSessions` | sessions `InProgress` |
   | `notFoundOpenTargets`, `notFoundOverdueTargets` | `GetNotFoundReportSummaryQueryHandler` |
   | `manualRequestsFound`, `manualRequestsFoundOverdue` | `GetManualRequestSummaryQueryHandler` |
   | `catalogueBooks` | `totalCount` de `GetAdminBooksQueryHandler` sans filtre |
   | `isbnQueue` | `totalCount` de `GetBookMetadataQueueQueryHandler` sans filtre |
   | `isbndbAbsentToComplete` | même règle que `GetIsbndbStatusQueryHandler` |
   | `publishedRareBooks` | `totalCount` de `GetAdminRareBooksQueryHandler` en `Published` |

   Ensuite :
   - un bénévole livres rares sans rôle Administration ne reçoit que `notFound*` et
     `publishedRareBooks`, les autres valent `null` (même contrôle de rôle que
     `NotFoundReportAdministrationController.cs`, ligne 264) ;
   - un membre reçoit 403.
2. Handler : un `COUNT` direct par compteur, sans charger aucune ligne de liste et sans
   page. Réutiliser les prédicats des handlers sources (les extraire en expressions
   partagées si besoin) pour garantir l'égalité.
3. Pas de cache serveur : chaque compte est indexé, et le front rafraîchit après ses
   propres actions (Tâche 13).

**Commit :** `perf(admin): serve all navigation badge counts from one endpoint`

### Tâche 10 — État ISBNdb sans relecture complète

**Maquette :** preview/Main.html — zones E1-carte-isbndb-squelette

**Fichiers :** `src/Backend/Vole_Papillon_Damour.Application/Isbndb/Queries/GetIsbndbStatus/GetIsbndbStatusQueryHandler.cs`, `Application.tests/Isbndb/GetIsbndbStatusQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - résultat identique (`RG-165`) ;
   - les quatre comptes (`pendingTotal`, `pendingInStock`, en attente hors ISBNdb,
     `absentToComplete`) viennent d'une seule requête agrégée ;
   - un second appel dans les 10 min ne relit pas `BookIsbndbRecords`.
2. Fusionner les quatre `CountAsync` en un agrégat (`GroupBy(_ => 1)`, comme l'overview).
3. Mettre en cache `HybridCache` le calcul des 10 sujets non mappés (clé
   `isbndb:unmatched-subjects`, 10 min).

**Commit :** `perf(isbndb): aggregate status counts and cache unmatched subjects`

### Tâche 11 — Listes d'administration en projection

**Maquette :** preview/E3-Desktop.html — zones E3-recherche, E3-pagination

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/Admin/GetAdminBooksQueryHandler.cs`, `AdminQueryProjection.cs`
- `GetBookMetadataQueueQueryHandler.cs`, `Isbndb/Queries/GetIsbndbAbsentBooks/GetIsbndbAbsentBooksQueryHandler.cs`
- Tests : `GetAdminBooksQueryHandlerTests.cs`, `GetBookMetadataQueueQueryHandlerTests.cs`, `Application.tests/Isbndb/GetIsbndbAbsentBooksQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - mêmes pages, même ordre, même `totalCount` pour chaque filtre (`RG-165`) ;
   - la commande SQL de page ne contient ni `RawPayload`, ni `Summary`, ni
     `ManuallyEditedFields` ;
   - un filtre absent n'apparaît pas dans le SQL (plus de `@x IS NULL OR …`) ;
   - `metadataStatus=Pending` est comparé à la valeur d'énumération, pas à une chaîne
     construite en SQL.
2. `GetAdminBooksQueryHandler` :
   - ajouter chaque filtre (`Rare`, `Hidden`, `MetadataStatus`, `Undated`, recherche) par
     un `if`, sans prédicat « attrape-tout » ;
   - analyser `MetadataStatus` en énumération avant la requête (valeur inconnue :
     erreur de validation existante, sinon liste vide) ;
   - page en deux temps (ISBN triés, puis projection des champs de
     `AdminQueryProjection.ToBookResult`).
3. Vérifier `metadata-queue` et `isbndb-absent` sur le seed local. S'ils dépassent
   300 ms, appliquer le même traitement ; sinon, ne rien changer et le noter.

**Commit :** `perf(admin): project admin book lists and drop catch-all filters`

### Tâche 12 — Statistiques sans matérialisation de tables entières (`RG-166`)

**Maquette :** preview/E4-Desktop.html — zones E4-onglets

**Fichiers :**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/GetVolunteerStatistics/GetVolunteerStatisticsQueryHandler.cs` (`/scan/me/statistics`)
- `Books/Queries/Admin/GetAdminVolunteerStatisticsQueryHandler.cs`, `GetAdminCatalogueFlowStatsQueryHandler.cs`, `GetAdminFairStatsQueryHandler.cs`
- Tests : `VolunteerStatistics/GetVolunteerStatisticsQueryHandlerTests.cs`, `Admin/GetAdminVolunteerStatisticsQueryHandlerTests.cs`, `Admin/GetAdminCatalogueFlowStatsQueryHandlerTests.cs`, `Admin/GetAdminFairStatsQueryHandlerTests.cs`

**Étapes :**
1. Tests d'abord :
   - résultats identiques pour toutes les périodes et bourses des jeux existants
     (`RG-165`) ;
   - aucune commande ne lit `Books` sans filtre d'ISBN, ni `RawPayload` ou `Summary`.
2. Remplacer chaque `dbContext.Books…ToDictionaryAsync(book => book.Id…)` par une
   projection `(Isbn13, Title, Genre)` des seuls ISBN utilisés.
3. Remplacer les mouvements matérialisés en entités par une projection des colonnes lues
   par le calcul (`Id`, `Isbn13`, `Type`, `Quantity`, `OccurredAt`, `VolunteerId`,
   `AssoEventsId`, `ScanSessionId`, `ReversalOfMovementId`).
4. `GetVolunteerStatisticsQueryHandler` :
   - calculer `firstEntryByIsbn` en SQL (premier mouvement d'entrée par ISBN, restreint
     aux ISBN des mouvements gardés du bénévole) ;
   - restreindre `soldIsbns` à ces mêmes ISBN ;
   - charger `allSessions` en projection (`KeptCount`, `ScannedCount`) ;
   - garder les calculs d'équipe (cadences, part de recette) en projection légère.
5. Mesurer sur le seed local : moins de 500 ms par appel.

**Commit :** `perf(stats): project statistics inputs instead of loading whole tables`

---

## Lot C — Portail d'administration, front Catalog

### Tâche 13 — Route unique, chargement à la demande, une seule initialisation (`RG-159`, `RG-160`)

**Maquette :** preview/Main.html — zones E1-entete ; preview/E1-Mobile.html — zones E2-selecteur-mobile

**Fichiers :**
- `src/Catalog/src/app/app-routing.module.ts`, `app.module.ts`, `app.routes.server.ts`
- Nouveau : `src/Catalog/src/app/features/administration/catalog-administration.module.ts` (routes enfants, déclarations déplacées depuis `AppModule`)
- `catalog-administration-page.component.ts`
- Tests : `catalog-administration-page.component.spec.ts`, `core/catalog-administration-route.spec.ts`

**Étapes :**
1. Tests d'abord :
   - **`CA-03`** : ouvrir `/administration` puis passer de `overview` à `catalogue`
     n'appelle `initialize` qu'une fois et ne refait aucun appel de l'overview ;
   - le chargement initial de `overview` émet au plus 4 appels (`navigation-counts`,
     `overview`, `isbndb/status`, `sessions`) ;
   - le chargement de `catalogue` n'émet que `books` ;
   - aucun appel n'est fait en double.
2. Routes : `administration` redirige vers `administration/overview` ; une seule route
   `administration/:section` charge le module paresseux (`loadChildren`). Garder
   `RenderMode.Client`.
3. `initialize()` :
   - authentification ;
   - appel des compteurs en parallèle du chargement de la section active, et **seulement**
     de celle-ci (`Promise.all`, sans `await` en série) ;
   - plus d'appel à `loadSessions`, `loadDeadStock`, `loadNotFoundSummary` ni
     `loadManualRequestSummary` hors de leur section.
4. Section `overview` : `overview`, `isbndb/status` et `sessions` partent en parallèle.
   `loadIsbndbStatus` n'attend plus l'overview.
5. Pastilles et tuiles d'attention (`E1-tuiles-attention`) : lues depuis
   `navigation-counts`. `navBadge()` n'utilise plus `sessionsPage`, `notFoundSummary`,
   `manualRequestSummary` ni les pages `pageSize=1` ; `loadNavigationBadgeCounts` est
   remplacé. Après une action qui modifie un compteur (correction de notice, ajout,
   retrait, publication rare, clôture de signalement, décision de demande, clôture de
   session), relancer `navigation-counts`.
6. Cache de section de 60 s : rouvrir une section dans la minute réaffiche ses données
   immédiatement, puis les rafraîchit en arrière-plan.
7. Mobile (`E2-selecteur-mobile`) : les options du sélecteur portent le nombre seulement
   quand il est connu (« Livres introuvables (6) »), jamais « 0 » ni « … ».

**Commit :** `perf(admin-front): lazy-load the portal, load only the visible section in parallel`

### Tâche 14 — États par bloc, squelettes, erreurs locales (`RG-161`)

**Maquette :** preview/Main.html — zones E1-carte-squelette, E1-alertes-squelette, E1-carte-isbndb-squelette, E2-pastille-squelette ; preview/E1-Etats.html — zones E1-bloc-erreur, E1-bouton-reessayer, E1-rafraichissement ; preview/E3-Desktop.html — zones E3-ligne-squelette, E3-liste-estompee, E3-indicateur-rechargement ; preview/E3-Mobile.html — zones E3-bloc-erreur ; preview/E4-Desktop.html — zones E4-bloc-squelette, E4-graphique-squelette

**Fichiers :**
- `catalog-administration-page.component.ts`, `.html`, `.scss`
- `features/administration/isbndb/isbndb-status-card.component.ts`
- `features/administration/volunteer-statistics-view.ts`, `fairs-evolution-view.ts`
- Tests : `catalog-administration-page.component.spec.ts`

**Étapes :**
1. Tests d'abord :
   - pendant le chargement, chaque carte, la carte ISBNdb, la liste des alertes et chaque
     pastille affichent leur squelette (`aria-hidden`), avec un `role="status"` « Chargement
     des indicateurs… » masqué visuellement ;
   - une erreur de `isbndb/status` affiche le bloc d'erreur (`role="alert"`, « Ce chiffre
     n’a pas pu être chargé. ») et « Réessayer » sur ce seul bloc ; les cartes restent
     affichées ;
   - « Réessayer » ne relance que ce bloc ;
   - un bouton « Corriger » reste actif pendant le chargement de l'état ISBNdb (`CA-10`) ;
   - catalogue : page 2 demandée, les lignes de la page 1 restent estompées ;
     l'indicateur dit « Chargement de la page 2… » et la pagination reste active ; une
     recherche affiche « Recherche en cours… » ;
   - une liste en erreur garde la dernière liste affichée et propose « Réessayer ».
2. Introduire un état par bloc : `{status: 'loading' | 'ready' | 'error', data, refreshing}`
   (petit utilitaire typé, signaux). Supprimer l'usage de `loading()` global pour les
   chargements. Le garder, renommé par action, pour les seuls boutons de mutation : un
   bouton n'est désactivé que pendant **sa** propre action.
3. Squelettes : classe partagée dans le SCSS du portail (fond `--admin-line`, scintillement
   lent, aucun mouvement avec `prefers-reduced-motion`). Mêmes dimensions que le bloc
   chargé, pour éviter tout saut de mise en page. La carte foncée « Titres jamais vendus »
   a son squelette sur fond `--admin-ink`.
4. Rafraîchissement (`E1-rafraichissement`) : pastille discrète « Actualisation… » quand une
   section en cache se recharge.
5. Listes (catalogue, file ISBN, hors ISBNdb, sessions) :
   - premier chargement : 3 lignes squelettes (`E3-ligne-squelette`) ;
   - rechargement : liste estompée (`E3-liste-estompee`) et indicateur
     (`E3-indicateur-rechargement`) ;
   - seule la dernière demande compte : ignorer la réponse d'une demande remplacée et,
     si possible, l'annuler.
6. Statistiques : onglets (`E4-onglets`) et choix de bourse toujours actifs ; squelettes des
   cartes chiffrées (`E4-bloc-squelette`) et des graphiques (`E4-graphique-squelette`) ;
   changer d'onglet abandonne la demande en cours.
7. Vérifier à 390 px et 1440 px avec les pages `preview/` correspondantes, y compris
   `preview/E4-Mobile.html`.

**Commit :** `feat(admin-front): per-block loading, skeletons and local retry`

---

## Tâche 15 — Validation complète et PR

1. `dotnet test` de la solution, `npm test` et `npm run build` (Catalog, BackOffice) ; comparer
   à l'état initial (Tâche 0).
2. Mesures locales (`perf/README.md`) :
   - seed, `02-genres-rares.sql`, scénario de parameter sniffing ;
   - temps de `/catalog/search?genre=Roman…` après `genre=Amour` (`CA-01`) ;
   - temps de chaque endpoint admin modifié ;
   - nombre d'appels à l'ouverture de `/administration` (`CA-03`, onglet Réseau ou trace
     Aspire).
3. Contrôle visuel authentifié (`local-auth-testing`, persona `local-admin` puis
   `local-member` pour le 403), à 390 et 1440 px, avec réseau ralenti (DevTools « Fast
   4G ») pour voir squelettes et estompages.
4. `graphify update .`, `NEXT.md` (entrée datée, mesures avant/après), puis PR selon
   `implement-handoff`.

**Commit :** `docs(perf-catalogue-admin): record measurements and status`

---

## Zones non implémentées

Aucune : chaque zone est couverte par une tâche ci-dessus.
Les pages mobiles des écrans E3, E4 et E5 reprennent les zones de leurs pages desktop.

## Points d'arbitrage

1. **Granularité des cartes de l'overview.** Les quatre cartes viennent d'une seule réponse
   (rapide après les tâches 6 à 8) et s'affichent donc ensemble. « Un bloc » au sens de
   `RG-161` est une demande : overview, état ISBNdb, alertes, pastilles. La maquette mobile
   qui montre « Livres vendus » arrivé seul illustre le principe, pas une demande par carte.
2. **Lazy loading (Tâche 13).** Déplacer les déclarations vers un module paresseux touche
   `AppModule` et les specs. Si la tâche dépasse le raisonnable, la livrer séparément, après
   les autres : le gain de temps perçu vient surtout des étapes 3 à 6.
3. **Infra (spec §10).** Hors code et hors PR : passage de l'API à 0,5 vCPU / 1 Go et
   vérification de l'écart S0 / S1 entre Bicep et Azure. À reporter dans « Risks and
   follow-up ».
