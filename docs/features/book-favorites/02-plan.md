# Coups de cœur et suggestions personnalisées — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**But.** Livrer les coups de cœur (liste locale ou synchronisée), les suggestions notées en
cœurs à partir des coups de cœur, l'e-mail avant chaque bourse et le bandeau « Ce livre a
déjà plu » de la scanette, conformément à `01-fonctionnel.md` (`RG-130` à `RG-159`).

**Architecture.** On réutilise le moteur de recommandations existant
(`BookSimilarityProfiles`, `BookNeighbors`, `BookRecommendationJobs`, file
`IBookRecommendationWorkQueue`). Ce qui change :

- la **base de calcul** personnelle : les coups de cœur au lieu des achats ;
- une **fiche de référence** : un `Book` sans stock, invisible au catalogue public, qui sert
  de point de départ aux suggestions mais n'est jamais proposé comme voisin ;
- un **moteur de suggestions** commun (membre, anonyme, e-mail) qui calcule les cœurs ;
- un **e-mail planifié** par bourse, avec une table d'envoi qui garantit l'unicité ;
- un **compteur** de coups de cœur par livre, synchronisé sur la scanette.

**Stack.** .NET (CQRS MediatR + ErrorOr, EF Core SQL Server, Azure Functions isolées), Angular
(Catalog, Scan), Jasmine/Karma, xUnit + FluentAssertions + NSubstitute.

**Skills du repo à suivre** : `.github/skills/cqrs-feature`, `dotnet-patterns`,
`xunit-unit-testing`, `tdd-workflow`, `angular-patterns`, `ui-ux-front-saas`,
`delivery-workflow` (sauf ouverture de PR, gérée par `implement-handoff`).

## Vocabulaire technique retenu

| Spec | Code |
|---|---|
| Coup de cœur d'un membre | `MemberFavoriteBook` (`UserId`, `Isbn13`, `AddedAt`) |
| Compteur anonyme | `AnonymousFavoriteCount` (`Isbn13`, `Count`, `UpdatedAt`) |
| Fiche de référence | `Book.IsFavoriteReference` (bool) |
| Exigence | `MinimumHearts` (1 à 4, défaut 3) |
| E-mail avant la bourse | `FairDigest` (campagne `FairDigestCampaign`, envoi `FairDigestDelivery`) |
| Consentement e-mail | `MemberRecommendationPreference.FairDigestConsentedAt` (null = non) |

## Configuration ajoutée

| Clé | Défaut | Règle |
|---|---|---|
| `Recommendations:HeartThresholds` | `0.50,0.58,0.66,0.74,0.82` | `RG-138` (score de 1 à 5 cœurs, à calibrer) |
| `Recommendations:DefaultMinimumHearts` | `3` | `RG-138` |
| `Favorites:AccountMax` | `200` | `RG-131` |
| `Favorites:LocalMax` | `50` | `RG-131` (renvoyé au front par l'API de configuration publique, ou constante front alignée) |
| `Favorites:ReferencesPerVisitorPerDay` | `20` | `RG-135` |
| `Favorites:ReferenceDailyIsbndbCredits` | `300` | `RG-135` |
| `FairDigest:Enabled` | `false` | `RG-159` |
| `FairDigest:DaysBefore` | `3` | `RG-145` |
| `FairDigest:SendHourParis` | `10` | `RG-145` |
| `FairDigest:Schedule` | `0 5 * * * *` | minuteur horaire du Worker |
| `FairDigest:NewsMax` / `BackMax` / `BackMinCount` | `8` / `6` / `3` | `RG-147`, `RG-148`, `RG-151` |
| `FairDigest:SoldOutMinSales` / `TopSellersCount` | `3` / `20` | `RG-149` |

Toutes les clés sont lues par des classes d'options validées au démarrage, comme
`IRecommendationSettings` (`src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Services/IRecommendationSettings.cs`)
et `IsbndbPublicSearchOptions`.

---

## Tâche 0 — État initial

1. `dotnet build src/Backend/Vole_Papillon_Damour.slnx`, puis `dotnet test` sur
   `Vole_Papillon_Damour.Domain.tests`, `Application.tests`, `Infrastructure.tests`,
   `Api.tests`, `Worker.tests`.
2. `npm test -- --watch=false` puis `npm run build` dans `src/Catalog` et `src/Scan`.
3. Noter les échecs déjà présents dans le compte rendu, **sans les corriger**.

Pas de commit.

---

## Partie A — Domaine et persistance

### Tâche 1 — Coup de cœur, compteur anonyme, fiche de référence

**Fichiers.**
- Créer `src/Backend/Vole_Papillon_Damour.Domain/FavoriteBookAggregate/MemberFavoriteBook.cs`
  et `AnonymousFavoriteCount.cs` (même style que
  `Domain/MemberSelectionAggregate/` : constructeur privé, fabrique `Create`, propriétés
  `private set`).
- Modifier `src/Backend/Vole_Papillon_Damour.Domain/BookAggregate/Book.cs` :
  - propriété `bool IsFavoriteReference { get; private set; }` ;
  - fabrique `Book.CreateFavoriteReference(Isbn13 isbn13, DateTime firstSeenAt)` =
    `Create` + `IsFavoriteReference = true` ;
  - `RecordAvailableEntry`, `RecordAnnouncementEntry` et toute méthode qui crée un
    mouvement de stock remettent `IsFavoriteReference = false` (`RG-132` : la fiche
    devient ordinaire à son arrivée).
- `AnonymousFavoriteCount.Increment(now)` / `Decrement(now)` : jamais sous 0.

**Tests d'abord** (`src/Backend/Vole_Papillon_Damour.Domain.tests/`, nouveau dossier
`FavoriteBookAggregateTests/`, et `BookAggregateTests/`) :
- une fiche créée par `CreateFavoriteReference` a `IsFavoriteReference`, aucune quantité ;
- `RecordAvailableEntry` puis `RecordAnnouncementEntry` la rendent ordinaire ;
- `Decrement` à 0 reste à 0.

**Commit** : `feat(domain): add favorite books and favorite reference notices`

### Tâche 2 — Préférences et e-mail avant la bourse (domaine)

**Fichiers.**
- `src/Backend/Vole_Papillon_Damour.Domain/RecommendationAggregate/MemberRecommendationPreference.cs` :
  ajouter `int? MinimumHearts` (1 à 4, null = défaut), `DateTime? FairDigestConsentedAt`.
  Méthodes `SetMinimumHearts(int?)`, `GrantFairDigestConsent(now)`,
  `RevokeFairDigestConsent()`. La ligne reste créée seulement si le membre change une
  valeur (`07` §3.3).
- Créer `src/Backend/Vole_Papillon_Damour.Domain/FairDigestAggregate/FairDigestCampaign.cs` :
  `AssoEventsId`, `PlannedAtUtc`, `SuspendedAt?`, `SuspendedBy?`, `StartedAt?`,
  `CompletedAt?`, `SentCount`, `SkippedCount`, `FailedCount`. Méthodes `Suspend`,
  `Resume` (refusé après `StartedAt`), `Start`, `Complete`.
- `FairDigestDelivery.cs` : `CampaignId`, `UserId`, `Status`
  (`Pending|Sent|SkippedNoContent|Failed`), `SentAt?`, `Error?`. Unicité
  `(CampaignId, UserId)` (`RG-145` : un seul envoi par bourse et par membre).

**Tests d'abord** : `Resume` après `Start` est refusé ; consentement accordé puis retiré ;
`MinimumHearts` hors 1–4 refusé.

**Commit** : `feat(domain): add fair digest campaign and favorite preferences`

### Tâche 3 — EF Core, migration, suppression de compte

**Fichiers.**
- `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Persistence/IProjectDbContext.cs` :
  `DbSet<MemberFavoriteBook> MemberFavoriteBooks`, `DbSet<AnonymousFavoriteCount> AnonymousFavoriteCounts`,
  `DbSet<FairDigestCampaign> FairDigestCampaigns`, `DbSet<FairDigestDelivery> FairDigestDeliveries`.
- Configurations dans `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/`
  (modèle : `MemberSelectionItem` et `MemberRecommendationPreference`) :
  - `MemberFavoriteBooks` : clé `(UserId, Isbn13)`, index `Isbn13` (compteur scanette),
    FK utilisateur en cascade ;
  - `AnonymousFavoriteCounts` : clé `Isbn13` ;
  - `Books.IsFavoriteReference` : `bit not null default 0`, index filtré ;
  - `MemberRecommendationPreferences` : deux colonnes nullable ;
  - `FairDigestCampaigns` : index unique `AssoEventsId` ; `FairDigestDeliveries` : index
    unique `(CampaignId, UserId)`.
- Migration `AddBookFavorites` via `dotnet ef migrations add` (jamais à la main). **Ajout
  seulement** : aucune colonne existante de `Books` modifiée.
- Suppression de compte : `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/AccountDeletionStore.cs`
  supprime `MemberFavoriteBooks` et `FairDigestDeliveries` du membre (`RG-158`, CA-16).
- Export RGPD : la requête qui produit la copie des données du membre (`09` §4) inclut
  les coups de cœur et la date de consentement.

**Tests d'abord** : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/AccountDeletion/AccountDeletionStoreTests.cs`
— un membre avec coups de cœur et envois est supprimé sans reste ; test de la migration
dans la suite existante des migrations (si présente).

**Commit** : `feat(persistence): store favorite books and fair digests`

---

## Partie B — Coups de cœur (application et API)

### Tâche 4 — Fiche de référence : création une fois, plafonds

**Fichiers.**
- Créer `src/Backend/Vole_Papillon_Damour.Application/Favorites/Common/FavoriteReferenceService.cs` :
  `EnsureAsync(Isbn13 isbn13, FavoriteOrigin origin, CancellationToken)` →
  `ErrorOr<FavoriteReferenceResult>`.
  1. Fiche existante (suivre `RedirectedToIsbn13` comme `ScanBookCommandHandler`) :
     rien à créer, aucune requête ISBNdb (`RG-132`, `RG-104`).
  2. Sinon : `Book.CreateFavoriteReference`, puis mise en file d'enrichissement par
     `IBookMetadataEnrichmentQueue` (même chemin que le scan : ISBNdb d'abord, secours
     ensuite, `RG-104` à `RG-107`). L'usage ISBNdb est compté sous un nouvel usage
     `Favorites` dans le décompte qu'affiche la carte ISBNdb (`RG-125`, à côté de
     `PublicSearch`).
  3. Au-delà de `Favorites:ReferenceDailyIsbndbCredits` crédits « Favorites » du jour, ou
     quota épuisé (`IIsbndbQuotaGuard`) : l'enrichissement de cette fiche passe
     directement par le secours (`RG-135`).
  4. Crée ou réarme le `BookRecommendationJob` de la fiche et l'envoie dans
     `IBookRecommendationWorkQueue` dès que les métadonnées sont résolues (même code que
     `CloseScanSessionCommandHandler`, lignes 60-95).
  5. Si l'enrichissement aboutit à `MetadataStatus.Failed` sans titre : erreur
     `Favorites.NoticeNotFound` → message E11 « Nous ne trouvons pas assez
     d'informations sur ce livre pour vous proposer des suggestions. » (zone
     `E11-livre-introuvable`).
- Plafond par visiteur (`RG-135`, 20 fiches par jour) : politique `FavoriteReference` dans
  `src/Backend/Vole_Papillon_Damour.Api/Common/RateLimiting/RateLimiting.cs`
  (fenêtre glissante de 24 h, partition par client comme `IsbndbSearch`). Elle ne
  s'applique qu'aux **créations** : un ISBN déjà en base ne consomme rien. Au-delà, la
  fiche est créée sans ISBNdb (secours) plutôt que refusée.

**Tests d'abord** (`src/Backend/Vole_Papillon_Damour.Application.tests/Favorites/FavoriteReferenceServiceTests.cs`) :
- ISBN déjà en base → aucune mise en file, aucune création ;
- ISBN redirigé → la fiche canonique est utilisée ;
- ISBN inconnu → fiche `IsFavoriteReference`, enrichissement en file, usage `Favorites` ;
- crédits « Favorites » du jour atteints → enrichissement forcé en secours (CA-10).

**Commit** : `feat(favorites): create favorite reference notices once`

### Tâche 5 — Coups de cœur d'un membre

**Fichiers.** Dossier `src/Backend/Vole_Papillon_Damour.Application/Favorites/`, sur le
modèle de `MemberSelection/` (`MemberIdentityService.EnsureAsync` pour l'identité) :
- `Commands/AddMyFavorite` : `FavoriteReferenceService.EnsureAsync`, puis ajout
  idempotent (même ISBN = rien) ; plafond `Favorites:AccountMax` → erreur
  `Favorites.LimitReached` (`RG-131`).
- `Commands/RemoveMyFavorite` : idempotent.
- `Commands/MergeMyFavorites` : union **sans confirmation** (`RG-141`), dédoublonnage par
  ISBN, plafond 200 : les entrées locales les plus anciennes au-delà sont ignorées.
  Résultat `{ added, skipped, total }`. Chaque ISBN ajouté passe par `EnsureAsync`.
  Les ISBN déjà comptés comme ajouts anonymes sont **décrémentés** dans
  `AnonymousFavoriteCounts` (`RG-156` : pas de double comptage).
- `Queries/GetMyFavorites` : liste triée du plus récent au plus ancien (`RG-134`), regroupée
  par œuvre sur lien fiable (`WorkId` ou `other_isbns`, `RG-130`) avec le nombre
  d'éditions, la disponibilité (`PublicCatalogProjector`), l'état du calcul des
  suggestions (`BookRecommendationJobs` : `Computing` tant que le job n'est pas
  `Ready`/`NoNotice`), le compteur et le maximum.

**Tests d'abord** (`Application.tests/Favorites/`) : ajout idempotent ; 201ᵉ ajout refusé ;
fusion de 3 locaux dont 1 déjà présent → `added = 2` ; fusion au-delà de 200 → `skipped` ;
deux éditions de la même œuvre (même `WorkId`) → un seul élément « 2 éditions » ;
décrément anonyme à la fusion.

**Commit** : `feat(favorites): manage member favorite books`

### Tâche 6 — Coups de cœur anonymes et compteur

**Fichiers.**
- `src/Backend/Vole_Papillon_Damour.Application/Favorites/Commands/DeclareAnonymousFavorite` :
  `EnsureAsync` puis `AnonymousFavoriteCount.Increment`. Aucun identifiant du visiteur
  n'est stocké (`RG-158`).
- `Commands/WithdrawAnonymousFavorite` : `Decrement`.
- Chaque variation du nombre de coups de cœur d'un livre (membre ou anonyme) met à jour le
  marqueur de changement qu'utilise la synchronisation delta de la scanette (voir tâche 10)
  pour que le compteur se propage.

**Tests d'abord** : déclaration d'un ISBN inconnu → fiche de référence + compteur à 1 ;
retrait → 0 ; retrait à 0 → reste 0.

**Commit** : `feat(favorites): count anonymous favorite declarations`

### Tâche 7 — Recommandations : fiches de référence et calcul immédiat

**Fichiers.**
- `src/Backend/Vole_Papillon_Damour.Application/Recommendations/Commands/ProcessBookRecommendations/ProcessBookRecommendationsCommandHandler.cs`
  (lignes 74-79) : une fiche **de secours** (`!IsIsbndbSourced && !IsAbsentFromIsbndb`)
  dont `MetadataStatus` est `Resolved` est traitée tout de suite si elle est
  `IsFavoriteReference` **ou** présente dans `MemberFavoriteBooks` /
  `AnonymousFavoriteCounts` (exception de `RG-139` à `RG-112`). Le rattrapage ISBNdb la
  recalculera ensuite (déjà fait par `CatchUpIsbndbBooksCommandHandler.RequestRecommendationAsync`).
- `src/Backend/Vole_Papillon_Damour.Application/Recommendations/Commands/RecomputeBookNeighbors/RecomputeBookNeighborsCommandHandler.cs`
  et `Recommendations/Similarity/NeighborComputer.cs` : les fiches `IsFavoriteReference`
  gardent leur vecteur et **leurs propres voisins**, mais ne sont **jamais candidates**
  comme voisin d'un autre livre. Ajouter à `Compute`, `ComputeIncremental` et
  `ComputeForBooks` un ensemble `candidateEligible` (ISBN autorisés comme voisins).
  Une fiche qui perd `IsFavoriteReference` (arrivée au tri) redevient candidate au
  prochain calcul incrémental de cette fiche.
- `Recommendations/Queries/GetSimilarBooks/GetSimilarBooksQueryHandler.cs` et toutes les
  requêtes publiques qui filtrent `!IsHiddenFromCatalog` (au minimum
  `Books/Queries/SearchCatalog/SearchCatalogQueryHandler.cs`, y compris la branche
  `IncludeExhausted`, `Books/Queries/GetPublicBook/`, `Books/Queries/GetPublicWork/`,
  `Books/Queries/GetPublicCatalogSitemap/`) ajoutent `&& !book.IsFavoriteReference`
  (`RG-132`, CA-01). La fiche publique `/livres/:slug` d'une fiche de référence répond
  404.

**Tests d'abord** :
- `Application.tests/Recommendations/` : une fiche de secours de référence est traitée
  sans attendre ; une fiche de secours ordinaire attend toujours (régression `RG-112`) ;
- `NeighborComputerTests` : une fiche de référence n'apparaît dans les voisins d'aucun
  livre, mais a ses propres voisins ;
- `Application.tests/Books/Queries/` : recherche publique avec `IncludeExhausted` ne
  renvoie pas de fiche de référence ; `GetPublicBook` → not found.

**Commit** : `feat(recommendations): treat favorite references as seeds only`

### Tâche 8 — Moteur de suggestions et cœurs

**Fichiers.**
- Créer `src/Backend/Vole_Papillon_Damour.Application/Recommendations/Common/PersonalSuggestionEngine.cs`
  extrait de `GetMyRecommendationsQueryHandler` :
  - entrée : `Seeds` (ISBN + titre), `Exclusions` (ISBN et `WorkId` achetés, ISBN et
    `WorkId` des coups de cœur, ISBN de *Ma sélection*), `MinimumHearts`, `Limit`,
    filtre optionnel « arrivé après » (pour l'e-mail) ;
  - **poids égal** pour chaque coup de cœur (`RG-136`, fin de la décroissance par date
    d'achat) ;
  - pour chaque candidat : `TotalScore` = somme des scores, `BestScore` = meilleur score
    d'un coup de cœur, `ContributorCount` = coups de cœur dont le score ≥
    `SimilarMinScore`, raison = coup de cœur qui contribue le plus ;
  - **cœurs** (`RG-138`) : nombre de paliers `HeartThresholds` ≤ `BestScore` (1 à 5),
    + 1 si `ContributorCount ≥ 2`, plafonné à 5 ;
  - tri : cœurs décroissants, puis `TotalScore`, puis ISBN ;
  - exclusions de `RG-137` ; seulement disponible ou annoncé (`PublicCatalogProjector`) ;
  - sortie : livre projeté, `Hearts`, `Reason` (`NextTome`… inchangé), `ReasonTitle`,
    `OtherContributorCount` (pour « et 2 autres »).
- `GetMyRecommendationsQueryHandler` : points de départ = coups de cœur du membre (au plus
  200), plus les achats ne servent qu'aux exclusions. Statut `NoFavorites` remplace
  `NoPurchases` (garder la valeur d'énumération ancienne lisible pour compatibilité si
  le front la lit encore, puis la retirer en tâche 18). Paramètre `minimumHearts`
  (préférence du membre si absent).
- Créer `Recommendations/Queries/GetSuggestionsForFavorites/` (anonyme) : entrée = au plus
  50 ISBN + `minimumHearts` ; mêmes règles, sans exclusions d'achats ni de sélection. Aucun
  identifiant n'est journalisé.
- `IRecommendationSettings` : `HeartThresholds`, `DefaultMinimumHearts`.

**Tests d'abord** (`Application.tests/Recommendations/PersonalSuggestionEngineTests.cs`) :
- score 0,83 → 5 cœurs ; 0,60 → 2 cœurs ; 0,60 avec deux contributeurs → 3 cœurs ;
- `MinimumHearts = 4` écarte les 3 cœurs (CA-04) ;
- livre acheté, autre édition d'un coup de cœur, livre de la sélection, livre épuisé non
  annoncé : jamais proposés (CA-03) ;
- raison « et 1 autre » quand deux coups de cœur contribuent.

Adapter `Api.tests/Recommendations/MyRecommendationsEndpointsTests.cs` : un membre avec des
achats mais sans coup de cœur reçoit `NoFavorites` (CA-07).

**Commit** : `feat(recommendations): suggest from favorite books with hearts`

### Tâche 9 — API publique et membre

**Fichiers.**
- Contrats dans `src/Backend/Vole_Papillon_Damour.Contracts/Favorites/` et extension de
  `Contracts/Recommendations/` (`hearts`, `heartsLabel` non : le front compose le libellé ;
  `otherContributorCount`, `minimumHearts`).
- `src/Backend/Vole_Papillon_Damour.Api/Controllers/MemberAccountController.cs` :
  - `GET /catalog/me/favorites`, `PUT /catalog/me/favorites/{isbn13}`,
    `DELETE /catalog/me/favorites/{isbn13}`, `POST /catalog/me/favorites/merge` ;
  - `GET /catalog/me/recommendations?limit=&minimumHearts=` (réponse étendue) ;
  - `GET|PUT /catalog/me/recommendations/preference` : `enabled`, `minimumHearts`,
    `fairDigestConsent` (`true` = accorder maintenant, `false` = retirer) et
    `fairDigestConsentedAt` en lecture.
- `src/Backend/Vole_Papillon_Damour.Api/Controllers/BookController.cs` (anonyme) :
  - `POST /catalog/favorites/declarations` `{isbn13}` → fiche + compteur, politique
    `FavoriteReference` ;
  - `DELETE /catalog/favorites/declarations/{isbn13}` ;
  - `POST /catalog/favorites/suggestions` `{isbn13s[≤50], minimumHearts}` →
    suggestions + état de calcul par ISBN, `Cache-Control: no-store`, politique
    `PublicCatalog`.
- Toutes ces routes renvoient `Disabled` / 404 quand `Recommendations:Enabled = false`
  (`RG-159`).

**Tests d'abord** (`src/Backend/Vole_Papillon_Damour.Api.tests/Favorites/FavoritesEndpointsTests.cs`,
modèle `MemberSelectionEndpointsTests.cs`) : 401 sans jeton sur `/catalog/me/favorites` ;
ajout/retrait/merge ; 51 ISBN sur `/catalog/favorites/suggestions` → 400 ; route anonyme
sans authentification ; politique de débit appliquée.

**Commit** : `feat(api): expose favorite books and suggestions`

### Tâche 10 — Scanette : nombre de coups de cœur (back)

**Fichiers.**
- `src/Backend/Vole_Papillon_Damour.Application/Books/Common/ScanCatalogBookResult.cs` et
  `src/Backend/Vole_Papillon_Damour.Contracts/Books/Responses/ScanCatalogBookResponse.cs` :
  `int FavoriteCount`.
- La requête delta de la scanette (`Books/Queries/GetCatalogDelta/`, testée par
  `Application.tests/Books/Queries/GetCatalogDeltaQueryHandlerTests.cs`) calcule
  `FavoriteCount` = membres ayant ce livre **ou une édition de la même œuvre par lien
  fiable** + `AnonymousFavoriteCounts` des mêmes éditions (`RG-155`, `RG-156`). Le delta
  inclut un livre dès que son compteur change (marqueur de la tâche 6).
- Mapping dans `Api/Controllers/BookController.cs` (bloc qui mappe `IsWanted`, vers la
  ligne 697) et réponse de scan en ligne (`ScanBookResponse`) avec le même champ.
- Le verdict n'est pas modifié (`BookVerdictCalculator`, `RG-15`, `RG-16`).

**Tests d'abord** : `GetCatalogDeltaQueryHandlerTests` — 2 membres + 1 anonyme sur deux
éditions liées par `WorkId` → 3 ; un ajout de coup de cœur fait réapparaître le livre dans
le delta ; `BookVerdictCalculatorTests` inchangés.

**Commit** : `feat(scan): sync favorite counts to the scanner`

---

## Partie C — E-mail avant la bourse

### Tâche 11 — Planification et contenu

**Fichiers.** Dossier `src/Backend/Vole_Papillon_Damour.Application/FairDigest/` :
- `Common/FairDigestSchedule.cs` : prochaine bourse = `AssoEvents` non annulée, type
  `Books`, `DateStart` futur le plus proche. Heure prévue = `DateStart` (jour, heure de
  Paris) − `DaysBefore` jours, à `SendHourParis` h (`RG-145`). Bourse programmée trop tard :
  envoi dès que possible s'il reste ≥ 24 h avant l'ouverture, sinon aucun envoi.
  Bourse précédente = dernière bourse `Books` non annulée antérieure ; sa fin =
  `DateEnd ?? DateStart`, fin de journée à Paris.
- `Common/FairDigestContentBuilder.cs` :
  - **Nouveautés pour vous** (`RG-147`) : `PersonalSuggestionEngine` avec le filtre
    « arrivé après la fin de la bourse précédente » (premier mouvement d'entrée
    `DirectEntry` / `AnnouncementEntry` postérieur), `NewsMax`, exigence du membre ;
    absent si la préférence « Suggestions personnalisées » est désactivée ;
  - **De retour à la bourse** (`RG-148`, `RG-149`) : œuvres arrivées depuis la bourse
    précédente, disponibles ou annoncées, **et** (stock à 0 à la fin de la bourse
    précédente, reconstitué depuis `BookMovements`, avec `SalesCount ≥ SoldOutMinSales`)
    **ou** (dans les `TopSellersCount` œuvres les plus vendues pendant la bourse
    précédente). Tri par ventes, `BackMax`. Liste commune, filtrée par membre ;
  - exclusions `RG-150` (achats, coups de cœur, liste de recherche ; un livre n'apparaît
    qu'une fois, priorité aux nouveautés) ;
  - seuil `RG-151` : nouveautés ≥ 1 **ou** retours ≥ `BackMinCount`, sinon
    `SkippedNoContent`.
- `Commands/PlanFairDigest` : crée la campagne de la prochaine bourse (une seule,
  index unique).
- `Commands/SendDueFairDigests` : si `FairDigest:Enabled`, campagne due, non suspendue,
  non terminée : destinataires = membres avec `FairDigestConsentedAt` non null, au moins un
  coup de cœur, alertes non suspendues (`RG-44`) ; une `FairDigestDelivery` par membre
  (l'index unique empêche un second envoi) ; envoi par lots ; compteurs de campagne.
- `Queries/GetFairDigestStatus`, `Queries/PreviewFairDigest` (coups de cœur de
  l'administrateur connecté, sinon jeu d'exemple), `Commands/SendFairDigestTest`
  (à l'administrateur seulement), `Commands/SuspendFairDigest` / `ResumeFairDigest`
  (`RG-153`).

**Tests d'abord** (`Application.tests/FairDigest/`) :
- bourse le samedi 13 mars 2027 → envoi prévu le **mercredi 10 mars 2027 à 10 h Paris**
  (= 09:00 UTC, heure d'hiver) ; bourse le samedi 10 avril 2027 → mercredi 7 avril à
  10 h Paris (= 08:00 UTC, heure d'été) (CA-11). Les dates des maquettes (« samedi 14
  mars 2027 ») sont des exemples de mise en page, pas des jeux de test ;
- bourse créée 2 jours avant → envoi immédiat ; 20 h avant → aucun envoi ;
- un livre arrivé avant la fin de la bourse précédente n'est pas une nouveauté (CA-12) ;
- livre de la liste de recherche exclu ; livre en double gardé dans « Nouveautés » ;
- 0 nouveauté et 2 retours → `SkippedNoContent` (CA-13) ;
- second passage du minuteur → aucun second envoi ;
- campagne suspendue → aucun envoi ; reprise possible avant `Start`, refusée après.

**Commit** : `feat(fair-digest): plan and build the pre-fair email`

### Tâche 12 — Gabarit HTML et texte, désabonnement

**Maquette :** preview/E8-Email600.html — zones E8-texte-apercu, E8-objet, E8-entete, E8-accroche, E8-encart-bourse, E8-section-nouveautes, E8-carte-livre, E8-coeurs, E8-raison, E8-lien-fiche, E8-section-de-retour, E8-etiquette-tres-demande, E8-non-reservation, E8-bouton-suggestions, E8-pied, E8-raison-envoi, E8-desabonnement

**Maquette :** preview/E8-Email390.html — zones E8-entete, E8-accroche, E8-encart-bourse, E8-section-nouveautes, E8-carte-livre, E8-coeurs, E8-raison, E8-lien-fiche, E8-section-de-retour, E8-etiquette-tres-demande, E8-non-reservation, E8-bouton-suggestions, E8-pied, E8-raison-envoi, E8-desabonnement

**Maquette :** preview/E11-Etats.html — zones E11-email-sans-nouveautes

Sans nouveauté personnalisée (`RG-151`) : titre « 3 livres très demandés sont de retour »,
section « De retour à la bourse » seule, objet « Avant la bourse du samedi 14 mars : 3 livres
de retour ».

**Fichiers.**
- Créer `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/FairDigest/FairDigestEmailHtml.cs`,
  `FairDigestEmailText.cs`, `FairDigestEmailSender.cs`, en **réutilisant**
  `Services/BookAlerts/BookAlertEmailLayout.cs` (en-tête, règle de marque, encart bourse,
  carte livre, non-réservation, pied), `BookAlertEmailFormat.cs` (dates en français, heure
  de Paris) et `BookAlertEmailHeaders.cs` (désabonnement en un clic). Extraire au besoin
  les blocs communs, sans changer le rendu des alertes (leurs tests doivent rester verts).
- Libellés, mot pour mot (`RG-152`) :
  - objet `E8-objet` : « Avant la bourse du samedi 14 mars : 6 livres qui pourraient vous plaire » ;
  - aperçu `E8-texte-apercu` : « 4 nouveautés pour vous · 2 livres de retour » (texte
    caché en tête de corps) ;
  - accroche : « Bonjour Marie, voici ce qui vous attend samedi. » (« Bonjour, » sans
    prénom) puis titre « 6 livres qui pourraient vous plaire » ; rares seuls inexistant ici ;
  - sections « Nouveautés pour vous » (sous-titre « Arrivés depuis la dernière bourse,
    choisis d'après vos coups de cœur. ») et « De retour à la bourse » (sous-titre « Des
    livres très demandés, de nouveau disponibles. », étiquette « Très demandé ») ;
  - cœurs `E8-coeurs` : **texte** `♥` × n et `♡` × (5 − n) en `#c8364e`, suivi de
    « N cœurs sur 5 » ; raison « Parce que vous avez aimé *X* » / « La suite de *X* » /
    « … et N autres » ;
  - mention « Ces livres ne sont pas mis de côté. Premier arrivé, premier servi. » ;
  - bouton « Voir toutes mes suggestions » → `/trouver-mon-prochain-livre` ;
  - pied : « Vous recevez cet e-mail parce que vous avez demandé une sélection avant
    chaque bourse. » + « Se désabonner ».
- Désabonnement : `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Services/IUnsubscribeTokenService.cs`
  prend un **objet** (`UnsubscribePurpose.BookAlerts | FairDigest`). Les jetons
  existants (sans objet) restent valides et valent `BookAlerts`.
  `Api/Integrations/AcsEmail/EmailUnsubscribeController.cs` aiguille : `FairDigest` →
  `RevokeFairDigestConsent` (CA-14). La page `/desinscription` du Catalog affiche « Vous
  ne recevrez plus la sélection avant chaque bourse. » pour cet objet.

**Tests d'abord** (`Infrastructure.tests/FairDigest/FairDigestEmailHtmlTests.cs`, modèle des
tests `BookAlerts`) : contient objet, aperçu, encart, cœurs texte, sections dans l'ordre,
lien de désabonnement ; version texte avec les mêmes informations ; jeton `FairDigest`
refusé pour désabonner des alertes et inversement ; ancien jeton = alertes.

**Commit** : `feat(fair-digest): render and send the pre-fair email`

### Tâche 13 — Minuteur du Worker

**Fichiers.**
- Créer `src/Backend/Vole_Papillon_Damour.Worker/FairDigestFunction.cs`
  (`[TimerTrigger("%FairDigest:Schedule%")]`, modèle `IsbndbCatchUpFunction.cs`) :
  `PlanFairDigestCommand` puis `SendDueFairDigestsCommand`, journalisation des compteurs.
- Paramètres `FairDigest:*` dans la configuration du Worker et dans `infra/` (valeurs par
  défaut, `FairDigest:Enabled = false`).

**Tests d'abord** (`Worker.tests/`) : la fonction ne fait rien quand `Enabled = false`.

**Commit** : `feat(worker): schedule the pre-fair email`

### Tâche 14 — API d'administration de l'e-mail

**Fichiers.** `src/Backend/Vole_Papillon_Damour.Api/Controllers/BookAdministrationController.cs`
(politique `Administration`) :
`GET /books/admin/fair-digest` (prochaine bourse, envoi prévu, destinataires estimés,
interrupteur, dernier compte rendu), `GET /books/admin/fair-digest/preview` (HTML),
`POST /books/admin/fair-digest/test`, `POST /books/admin/fair-digest/suspend`,
`POST /books/admin/fair-digest/resume`.

**Tests d'abord** (`Api.tests/FairDigest/FairDigestAdministrationEndpointsTests.cs`) :
403 pour un membre ; suspendre puis reprendre ; test envoyé à l'adresse de l'administrateur
seulement.

**Commit** : `feat(api): administer the pre-fair email`

---

## Partie D — Catalog (Angular)

Règles communes : composants autonomes comme les composants voisins, variables CSS du
catalogue, nouveaux jetons du tableau de `maquettes/README.md` déclarés une fois
(`--catalog-heart`, `--catalog-heart-ink`, `--catalog-heart-empty`, `--catalog-heart-soft`,
`--catalog-heart-soft-line`) dans la feuille globale du Catalog. Vérifier à 390 et 1440 px
sans défilement horizontal de page (CA-17).

### Tâche 15 — Services : liste locale, liste du compte, fusion

**Fichiers.**
- Créer `src/Catalog/src/app/core/favorites/local-favorites.store.ts` sur le modèle exact de
  `core/selection/local-selection.store.ts` : clé `vpd.catalog.favorites.v1`, entrées
  `{isbn13, title, authors, coverUrl, addedAt}`, plafond 50, `available` faux si le
  stockage est refusé ; clés annexes `minimumHearts`, `bannerCollapsed`,
  `homeEntryDismissed`.
- Créer `core/favorites/catalog-favorites.service.ts` (modèle
  `core/selection/catalog-selection.service.ts`, signaux) : mode `local` / `remote` selon
  `CatalogAuthService.isAuthenticated()` ; ajout, retrait avec « Annuler » pendant 6 s
  (`RG-134`) ; en local, appelle `POST/DELETE /catalog/favorites/declarations` ; à la
  connexion, **fusion automatique** via `POST /catalog/me/favorites/merge` puis vidage
  de la liste locale et message de reprise (`RG-141`) ; état `computing` par livre,
  rafraîchi toutes les 5 s pendant 1 min au plus tant qu'un livre est `Computing` (`RG-139`).
- `core/catalog-member-api.service.ts` et `core/catalog-api.service.ts` : méthodes des
  routes de la tâche 9 ; `core/catalog.models.ts` : types.

**Tests d'abord** (`*.spec.ts` voisins) : stockage refusé → `available = false` sans
exception ; 51ᵉ ajout refusé ; connexion avec 3 locaux → un seul appel merge puis liste
locale vide ; retrait annulé → réapparaît sans appel de suppression définitif.

**Commit** : `feat(catalog): store and sync favorite books`

### Tâche 16 — E3 Ajouter un coup de cœur

**Maquette :** preview/E3-Desktop.html — zones E3-panneau, E3-fermer, E3-recherche, E3-aide-edition, E3-resultats-bourse, E3-resultats-externes, E3-resultat, E3-bouton-ajouter-edition, E3-voir-autres-editions

**Maquette :** preview/E3-Mobile.html — zones E3-panneau, E3-fermer, E3-recherche, E3-aide-edition, E3-resultats-bourse, E3-resultats-externes, E3-resultat, E3-bouton-ajouter-edition, E3-voir-autres-editions

**Maquette :** preview/E11-Etats.html — zones E11-plafond-liste, E11-livre-introuvable

**Fichiers.** Créer `src/Catalog/src/app/features/favorites/add-favorite-panel/` (panneau
latéral desktop 600 px, plein écran mobile, `role="dialog"`, piège du focus, Échap ferme).
La recherche réutilise les appels de `features/search/catalog-search-page.component.ts`
(catalogue + section « Pas encore dans la bourse aux livres ») et l'encart « Voir
d'autres éditions » ISBNdb existant (`RG-117`, `RG-118`). **Aucun appel ISBNdb** avant le
clic sur « Ajouter » ou « Voir d'autres éditions » (`RG-133`).

Libellés : « Mes coups de cœur », « Ajouter un coup de cœur », « Un livre que vous avez
aimé », « Chercher », « Choisissez l'édition que vous avez lue. Elle compte pour toute
l'œuvre : inutile d'ajouter les autres éditions. », « Déjà à la bourse aux livres »,
« Pas encore à la bourse aux livres », « Ajouter » / « Ajouté », « Vous n'avez pas trouvé
l'édition que vous cherchez ? », « Voir d'autres éditions ». Plafond : « Vous avez atteint
50 coups de cœur. » + « Retirez-en un pour en ajouter un autre, ou créez un compte pour
aller jusqu'à 200. » + lien « Créer un compte ». Introuvable : « Ajout impossible » + « Nous
ne trouvons pas assez d'informations sur ce livre pour vous proposer des suggestions. »

**Tests d'abord** : le panneau ne déclenche aucune requête ISBNdb à la saisie ; « Ajouter »
appelle le service une fois et bascule en « Ajouté » (`aria-pressed`) ; plafond affiché.

**Commit** : `feat(catalog): add favorite search panel`

### Tâche 17 — E2 Trouver mon prochain livre

**Maquette :** preview/Main.html — zones E2-entete, E2-lien-menu, E2-bandeau-navigateur, E2-bouton-creer-compte, E2-lien-connexion, E2-replier-bandeau, E2-coups-de-coeur, E2-compteur, E2-bouton-ajouter, E2-coup-de-coeur, E2-pastille-disponibilite, E2-calcul-en-cours, E2-retirer, E2-suggestions, E2-nombre-suggestions, E2-curseur-exigence, E2-carte-suggestion, E2-coeurs, E2-raison

**Maquette :** preview/E2-Mobile.html — zones E2-entete, E2-preference-email, E2-coups-de-coeur, E2-compteur, E2-bouton-ajouter, E2-coup-de-coeur, E2-pastille-disponibilite, E2-retirer, E2-suggestions, E2-curseur-exigence, E2-nombre-suggestions, E2-carte-suggestion, E2-coeurs, E2-raison

**Maquette :** preview/E11-Etats.html — zones E11-e2-vide, E11-calcul-en-cours, E11-aucune-suggestion, E11-suggestions-desactivees, E11-stockage-indisponible, E11-annuler-retrait

**Fichiers.**
- Route `trouver-mon-prochain-livre` dans `src/Catalog/src/app/app-routing.module.ts` et
  `app.routes.server.ts` (rendu côté client : contenu personnel) ; titre « Trouver mon
  prochain livre | Vole Papillon d’Amour ».
- Lien de menu « Trouver mon prochain livre » avec cœur dans
  `src/Catalog/src/app/core/layouts/navigation/catalog-nav-items.ts` et
  `catalog-navigation.component.*`, actif sur la route (`E2-lien-menu`).
- Créer `src/Catalog/src/app/features/favorites/` : page, `favorite-list`,
  `suggestion-grid`, `browser-storage-banner`, `hearts` (composant partagé dans
  `src/Catalog/src/app/shared/components/hearts/` : 5 cœurs SVG, `role="img"`,
  `aria-label="4 cœurs sur 5"`), `minimum-hearts-control` (4 boutons `aria-pressed`
  « 1 cœur et + », « 2 cœurs et + », « 3 cœurs et + », « 4 cœurs et + » ; mobile « 1+ »…
  « 4+ » ; extrémités « Plus de découvertes » / « Plus sûr »).
- Carte de suggestion = `app-book-card` existant (variante `home`) enrichi d'un slot cœurs
  et raison, plutôt qu'une nouvelle carte. Étiquette « Tome suivant » existante.

Libellés, mot pour mot : surtitre « Trouver mon prochain livre », titre « Dites-nous les
livres que vous avez aimés, *on cherche à la bourse.* », chapeau de `Main.html` ; bandeau
`RG-140` (texte de la spec, bouton « Créer un compte pour garder ma liste », lien « J'ai
déjà un compte », « Replier ») ; « Mes coups de cœur », compteur « 3 / 50 » (« 3 / 200 »
connecté), « Ajouter un coup de cœur », « 2 éditions », pastille « 1 dispo. à la
bourse », « Suggestions en cours de calcul… », note « Un coup de cœur ne réserve rien et ne
déclenche pas d'alerte. Pour être prévenu de l'arrivée d'un livre, utilisez la liste de
recherche. » ; « À la bourse aux livres », « Ils pourraient vous plaire. », « 6 suggestions
à 3 cœurs ou plus » ; note « Les suggestions ne comprennent ni vos coups de cœur, ni les
livres de votre sélection. Les livres ne sont pas mis de côté. ». Membre : case « Recevoir
avant chaque bourse une sélection de livres qui pourraient me plaire » + « Un e-mail trois
jours avant l'ouverture, avec les nouveautés qui ressemblent à vos coups de cœur.
Désabonnement en un clic. » à la place du bandeau (`RG-146`). États de `E11-Etats.html`,
textes exacts (vide, calcul, aucune suggestion avec bouton « Voir les suggestions à 2
cœurs et + », suggestions désactivées avec lien « Réactiver dans Compte et données → »,
stockage indisponible, retrait « *Dune* retiré de vos coups de cœur. » + « Annuler »).

**Tests d'abord** : anonyme → bandeau visible dès 1 coup de cœur (CA-05) ; membre → case
e-mail, pas de bandeau ; curseur à 4 → plus aucune carte à 3 cœurs (CA-04) ; liste vide →
`E11-e2-vide` ; calcul en cours affiché pour un livre `Computing`.

**Commit** : `feat(catalog): add the find my next book page`

### Tâche 18 — E1 Accueil

**Maquette :** preview/E1-Desktop.html — zones E1-section-pour-vous, E1-carte, E1-coeurs, E1-raison, E1-lien-toutes, E1-lien-menu

**Maquette :** preview/E1-Mobile.html — zones E1-section-pour-vous, E1-defilement, E1-carte, E1-coeurs, E1-raison, E1-lien-toutes

**Maquette :** preview/E11-Etats.html — zones E11-encart-entree, E11-ne-plus-afficher

**Fichiers.** `src/Catalog/src/app/features/home/for-you/for-you-section.component.*`
(déjà placé juste sous le héros dans `catalog-home-page.component.html`, ligne 286 : garder
cette place, `RG-143`) :
- source = coups de cœur (membre : `/catalog/me/recommendations` ; anonyme avec coups de
  cœur locaux : `/catalog/favorites/suggestions`, D-02) ;
- titre « D'après vos coups de cœur. », surtitre « Pour vous, Camille » (prénom, sinon
  « Pour vous »), lien « Voir toutes mes suggestions → » vers E2, cœurs et raison sur
  chaque carte, 4 cartes desktop, défilement horizontal mobile à 156 px ;
- moins de 3 suggestions, ou aucun coup de cœur : encart d'entrée (`RG-144`) « Trouver un
  livre qui pourrait vous plaire », texte de `E11-Etats.html`, bouton « Ajouter mes coups
  de cœur », « Ne plus afficher » (mémorisé dans le navigateur) ;
- suggestions désactivées : rien.

**Tests d'abord** : membre avec achats sans coup de cœur → encart, pas de rangée (CA-07) ;
3 suggestions → rangée ; « Ne plus afficher » persiste ; anonyme avec coups de cœur locaux
→ rangée.

**Commit** : `feat(catalog): show favorite suggestions first on the home page`

### Tâche 19 — E5 Fiche livre et E6 Mes achats

**Maquette :** preview/E5-Desktop.html — zones E5-actions, E5-bouton-coup-de-coeur, E5-confirmation

**Maquette :** preview/E5-Mobile.html — zones E5-actions, E5-bouton-coup-de-coeur, E5-confirmation

**Maquette :** preview/E6-Desktop.html — zones E6-explication, E6-ligne-achat, E6-bouton-jai-aime

**Maquette :** preview/E6-Mobile.html — zones E6-explication, E6-ligne-achat, E6-bouton-jai-aime

**Fichiers.**
- `src/Catalog/src/app/features/book-detail/catalog-book-detail-page.component.*` :
  bouton bascule « Coup de cœur » / « Dans vos coups de cœur » (`aria-pressed`) entre
  « Ajouter à Ma sélection » et « Suivre ce livre » ; confirmation « Ajouté à vos coups de
  cœur. » + lien « Voir mes suggestions » ; note « Un coup de cœur dit que vous avez aimé
  ce livre : il sert à vous proposer des livres qui lui ressemblent. Il ne réserve
  rien. » ; fonctionne sans compte.
- `src/Catalog/src/app/features/account/purchases/account-purchases.component.*` :
  « J'ai aimé » sur chaque ligne (bascule, mobile icône seule avec `aria-label`) ; texte
  « Vous avez aimé un de ces livres ? Dites-le d'un clic : il rejoint vos coups de cœur et
  sert à vous proposer des livres qui lui ressemblent. » + « Voir mes suggestions » ;
  **retirer** `recommendations-band` de Mes achats et l'invitation « sans achat »
  (`RG-142`). Supprimer `features/account/recommendations-band/` s'il n'est plus utilisé.

**Tests d'abord** : bouton de la fiche bascule l'état et appelle le service ; Mes achats
n'affiche plus la bande de recommandations ; « J'ai aimé » ajoute le livre.

**Commit** : `feat(catalog): add favorite toggles on book page and purchases`

### Tâche 20 — E4 Étape avant la création de compte et reprise

**Maquette :** preview/E4-Desktop.html — zones E4-etape-compte, E4-recap-liste, E4-consentement-email, E4-bouton-continuer, E4-message-reprise, E4-message-plafond

**Maquette :** preview/E4-Mobile.html — zones E4-etape-compte, E4-recap-liste, E4-consentement-email, E4-bouton-continuer, E4-message-reprise, E4-message-plafond

**Fichiers.**
- Créer `src/Catalog/src/app/features/favorites/account-step-dialog/` : dialogue
  « Gardez vos 3 coups de cœur », texte, récapitulatif des titres, case « Recevoir avant
  chaque bourse une sélection de livres qui pourraient me plaire » (**non cochée**,
  `RG-146`) + « Facultatif. Un e-mail trois jours avant l'ouverture. Désabonnement en un
  clic. », bouton « Créer mon compte », lien « J'ai déjà un compte », mention « Vous allez
  être redirigé vers la page sécurisée de création de compte, puis ramené ici. ».
- `src/Catalog/src/app/core/catalog-auth.service.ts` : le choix de la case et la route de
  retour voyagent dans l'état de la redirection Entra (pas dans l'URL en clair). Au
  retour : fusion (tâche 15), puis si la case était cochée `PUT preference
  {fairDigestConsent: true}`.
- Messages de retour sur E2 : « 3 coups de cœur ajoutés à votre compte » + « Ils sont
  maintenant synchronisés. Vos suggestions apparaîtront aussi sur l'accueil. » ; plafond :
  « 48 coups de cœur ajoutés à votre compte, 4 n'ont pas pu l'être » + « Votre compte
  compte désormais 200 coups de cœur, le maximum. Les plus anciens de ce navigateur n'ont
  pas été repris. ».

**Tests d'abord** : case non cochée par défaut ; retour avec case cochée → appel de
consentement après la fusion ; message avec le nombre renvoyé par l'API.

**Commit** : `feat(catalog): keep favorites when creating an account`

### Tâche 21 — E7 Compte et données

**Maquette :** preview/E7-Desktop.html — zones E7-preference-suggestions, E7-explication-suggestions, E7-preference-email, E7-date-consentement, E7-exigence

**Maquette :** preview/E7-Mobile.html — zones E7-preference-suggestions, E7-explication-suggestions, E7-preference-email, E7-date-consentement, E7-exigence

**Fichiers.** `src/Catalog/src/app/features/account/catalog-account-page.component.*`
(bloc préférence existant `07` R7) : section « Suggestions et coups de cœur » ;
interrupteur « Suggestions personnalisées » avec le texte de `RG-154` et le lien « Gérer
mes coups de cœur (3) → » ; interrupteur « E-mail avant chaque bourse » (`role="switch"`)
avec son texte et « Accepté le 13 novembre 2026 » / « Désactivé » ; « Exigence des
suggestions » (même contrôle que E2).

**Tests d'abord** : activer l'e-mail envoie `fairDigestConsent: true` et affiche la date ;
désactiver la retire.

**Commit** : `feat(catalog): manage favorite suggestion preferences`

### Tâche 22 — E9 Administration de l'e-mail

**Maquette :** preview/E9-Desktop.html — zones E9-lien-menu, E9-interrupteur, E9-prochain-envoi, E9-date-envoi, E9-destinataires, E9-bouton-test, E9-bouton-suspendre, E9-apercu, E9-compte-rendu

**Maquette :** preview/E9-Mobile.html — zones E9-lien-menu, E9-interrupteur, E9-prochain-envoi, E9-date-envoi, E9-destinataires, E9-bouton-test, E9-bouton-suspendre, E9-apercu, E9-compte-rendu

**Fichiers.**
- `src/Catalog/src/app/features/administration/catalog-administration-page.component.ts` :
  section `fair-digest`, libellé « E-mail avant la bourse », dans le groupe « Réservé à
  l'administration » (lignes 195-198).
- Créer `features/administration/fair-digest/` (modèle `features/administration/isbndb/`) :
  pastille « Envoi activé » / « Envoi désactivé » ; tuiles « Prochaine bourse »,
  « Envoi prévu » (« Mercredi 11 mars, 10 h », « J-3 · heure de Paris »), « Destinataires
  estimés » (« 142 membres », « consentement donné et au moins un coup de cœur ») ;
  boutons « M'envoyer un test », « Suspendre l'envoi pour cette bourse » (« Reprendre
  l'envoi » quand suspendu) ; aperçu dans une `iframe` `sandbox` sans script ; « Dernier
  envoi » avec « Envoyés », « Non envoyés, rien à proposer », « Erreurs ».
- `core/catalog-admin-api.service.ts` : routes de la tâche 14.

**Tests d'abord** : la section n'apparaît qu'au rôle Administration ; suspendre bascule le
libellé du bouton.

**Commit** : `feat(catalog): administer the pre-fair email`

---

## Partie E — Scanette

### Tâche 23 — E10 Bandeau « Ce livre a déjà plu »

**Maquette :** preview/E10-Garder.html — zones E10-bandeau-a-deja-plu, E10-nombre-lecteurs, E10-ligne-coups-de-coeur

**Maquette :** preview/E10-Trop.html — zones E10-bandeau-a-deja-plu, E10-nombre-lecteurs, E10-ligne-coups-de-coeur

**Maquette :** preview/E10-HorsLigne.html — zones E10-bandeau-a-deja-plu, E10-nombre-lecteurs, E10-fraicheur

**Fichiers.**
- `src/Scan/src/app/offline/scan-offline.model.ts` : `ScanCatalogBook.favoriteCount`,
  `LocalVerdict.favoriteCount` (défaut 0 pour les données déjà en cache).
- `src/Scan/src/app/offline/scan-verdict.service.ts` : additionne `favoriteCount` du livre et
  des autres éditions liées par lien fiable ; le verdict ne change pas.
- `src/Scan/src/app/scanner/scanner.component.html` / `.scss` : sous `.verdict-card`
  (ligne 257), bande blanche + bandeau arrondi `#d6304f` (`--scan-favorite`), cœur blanc,
  « Ce livre a déjà plu à un lecteur de la bourse » / « Ce livre a déjà plu à 3 lecteurs
  de la bourse », `role="status"` ; ligne « Coups de cœur » dans les faits du livre. Modes
  Tri et Consultation seulement, jamais en Caisse (`RG-155`). Hors ligne : la mention de
  fraîcheur existante (« Hors ligne · données du … ») reste en place au-dessus
  (`E10-fraicheur`, `RG-157`).

**Tests d'abord** (`scan-verdict.service.spec.ts`, `scanner.component.spec.ts`) :
`favoriteCount = 3` → bandeau au pluriel ; 1 → « un lecteur » ; 0 → aucun bandeau ;
verdict « Inutile d'en garder » inchangé avec bandeau (CA-15) ; mode Caisse → aucun
bandeau ; données en cache sans le champ → 0.

**Commit** : `feat(scan): show the already-loved banner`

---

## Partie F — Documentation et clôture

### Tâche 24 — RGPD et documentation

**Fichiers.**
- `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` §2 : lignes « Coups de cœur »
  (intérêt légitime, opposition par la préférence) et « E-mail avant la bourse »
  (consentement, retrait en un clic) (`RG-158`).
- Page publique « Vos données et le RGPD » : contenu de `features/legal/` pour
  `donnees-personnelles`.
- `docs/recommandations-livres/07-specification.md` §3.2 et §3.3 : renvoi vers
  `docs/features/book-favorites/01-fonctionnel.md` (base de calcul et texte de préférence
  remplacés).

**Commit** : `docs(book-favorites): document favorite data processing`

### Tâche 25 — Validation complète et PR

1. Build et tests complets (backend, `src/Catalog`, `src/Scan`), comparaison avec la
   tâche 0.
2. Contrôle manuel local (Aspire, `local-auth-testing`) : parcours P1 à P4 et P6 de la
   spec ; captures 390 et 1440 px de E1, E2, E3, E5, E6, E7, E9 et 390 px de E10, à côté
   des pages `maquettes/preview/` correspondantes ; rendu de E8 par l'aperçu de E9.
3. `graphify update .`
4. PR selon `implement-handoff`.

---

## Zones non implémentées

Aucune : chaque zone des maquettes est couverte par une tâche ci-dessus.

## Points d'attention pour Codex

- **Ne pas** changer le verdict de tri ni sa priorité (`RG-15`, `RG-16`).
- **Ne jamais** appeler ISBNdb depuis la recherche de E3 (`RG-133`).
- Les textes envoyés au service de vectorisation restent des notices : aucun identifiant de
  membre (`07` CA-8).
- `FairDigest:Enabled` reste `false` par défaut ; aucun envoi réel pendant la validation.
- Aucun déploiement ni commande `az` en écriture ; migrations jamais appliquées à une base
  distante.
