# ISBNdb, source principale des fiches livres — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : aucune
> (voir README) : chaque écran reprend l'écran existant le plus proche.

**Objectif.** ISBNdb remplit les fiches en premier. La BnF, Open Library et Google Books
deviennent le secours. Une fiche de secours est remplacée chaque nuit par ISBNdb. Les
livres absents d'ISBNdb sont listés pour être complétés à la main. Les recommandations
n'utilisent que des fiches définitives. La recherche publique propose ISBNdb à la
demande, avec un cache. L'administration voit le quota. La scanette signale qu'une autre
édition de la même œuvre est déjà en stock.

**Architecture.**
- Un client `IsbndbClient` en Infrastructure gère l'authentification, le débit, les
  nouvelles tentatives, l'annulation et la lecture des en-têtes de quota.
- Un service applicatif `IsbndbLookupService` met en cache chaque ISBN, partagé entre
  les appels unitaires et les requêtes groupées (`RG-104`).
- Un garde de quota persistant est partagé entre l'API et le Worker (`RG-106`).
- Le domaine `Book` porte les marqueurs ISBNdb et la clé d'œuvre. La notice ISBNdb
  complète est stockée dans une table 1-1 `BookIsbndbRecords`.
- Une commande de rattrapage est déclenchée par un timer du Worker.
- Les endpoints admin et publics sont des minimal API. Côté front : trois écrans dans
  Catalog (admin et public) et un encart dans Scan.

**Stack.** .NET 10, EF Core (SQL Server, SQLite en test), MediatR, ErrorOr, HybridCache,
xUnit + FluentAssertions + NSubstitute ; Angular (Catalog, Scan), Jasmine/Karma.

## Contraintes globales

- **Spec, `RG-100` à `RG-129`.** Les décisions de la §11 sont acquises. Leurs points
  sensibles :
  - `RG-109` : pas de réserve, le rattrapage peut tout consommer.
  - `Q-01` : le seuil compte l'œuvre seulement si le lien est fiable.
  - `Q-04` : une fiche complétée n'est jamais écrasée, seuls ses champs vides sont
    remplis.
- **Clé ISBNdb.** Elle n'apparaît jamais dans le dépôt, les journaux, les réponses d'API
  ou le navigateur. Configuration `Isbndb:ApiKey`, variable d'environnement
  `Isbndb__ApiKey`. Clé absente : ISBNdb est considéré comme indisponible et tout passe
  par le secours, sans erreur.
- **Coût mesuré** (spec §10), à respecter strictement :
  - 404 gratuit ; 400 payant. On valide donc l'ISBN-13 (clé de contrôle) avant tout
    appel.
  - `POST /books` : un crédit par livre trouvé, jusqu'à 100 ISBN par requête.
  - `GET /books/{q}` : un crédit par page.
  - `/stats` est **interdit** (un crédit, aucune utilité).
- **Quota.** Il est lu dans l'en-tête `ratelimit: "daily";r=<n>;t=<s>` de chaque réponse
  (plusieurs en-têtes `ratelimit` sont possibles ; lire celui qui commence par
  `"daily"`). Le renouvellement est à `observedAt + t` secondes, c'est-à-dire minuit
  UTC.
- **Aucune requête n'est émise** pendant que le garde indique « quota épuisé jusqu'à X ».
- **`image_original`** n'est jamais stocké ni journalisé : c'est une URL signée qui
  expire. Seul `image` est enregistré.
- Les **prix** sont stockés dans `BookIsbndbRecords` et ne sont exposés par aucun
  contrat d'API (`RG-103`).
- **Pas de maquettes.** Écrans en français, réutilisation des composants et variables CSS
  existants, aucun hexadécimal en dur. Vérification visuelle à 390 et 1440 px contre
  l'écran existant le plus proche, cité dans chaque tâche.
- **Infra et déploiement.**
  - Aucun déploiement, aucune commande `az` en écriture.
  - La migration est générée, pas appliquée à une base distante.
  - Le Bicep et le workflow sont modifiés (tâche 18), sans exécution.

## Configuration (`IsbndbOptions`, section `Isbndb`)

| Clé | Défaut | Rôle |
|---|---|---|
| `ApiKey` | `""` | secret |
| `BaseUrl` | `https://api2.isbndb.com` | `api.premium.isbndb.com` si upgrade |
| `RequestsPerSecond` | `1` | débit local, passer à `3` sans redéployer de code (`RG-105`) |
| `BulkSize` | `100` | taille des requêtes groupées (`Q-09`) |
| `TimeoutMilliseconds` | `10000` | délai par requête |
| `MaxRetries` | `3` | nouvelles tentatives sur 429 (`RG-105`) |
| `LookupCacheHours` | `24` | cache par ISBN (`RG-104`) |
| `SearchCacheMinutes` | `60` | cache de la recherche publique (`RG-116`) |
| `PublicSearchPerVisitorPerHour` | `10` | `RG-118` |
| `PublicSearchDailyCredits` | `300` | `RG-118` |
| `AbsentRecheckDays` | `30` | `RG-111` |
| `CatchUpSchedule` | `0 30 2 * * *` | Worker, UTC : 3 h 30 en hiver, 4 h 30 en été (`P5`) |

## Points d'attention pour la revue

1. **Double crédit sur un même ISBN.** Pendant un scan, `GET /books/{isbn}/metadata`
   (scanette), `ScanBook` (demandes manuelles) et la file d'enrichissement consultent le
   même ISBN dans la seconde. Tous passent par `IsbndbLookupService`, dont le cache
   HybridCache est **par ISBN**, y compris quand la requête groupée a été faite pour
   d'autres ISBN. Test : tâche 7.
2. **Quota épuisé en pleine bourse.** Le garde court-circuite l'appel HTTP, le secours
   prend le relais et les deux marqueurs restent à faux. Aucune recommandation n'est
   lancée avant le rattrapage. Tests : tâches 6, 8 et 10.
3. **Cohérence API ↔ Worker.** Il n'y a pas de limiteur distribué. Chaque processus a son
   seau de jetons (`RequestsPerSecond`), et les 429 sont rattrapés par `MaxRetries`. La
   fenêtre ISBNdb est de 60 requêtes par 60 s glissantes, et une rafale de 70 n'a pas
   été refusée. L'état « quota épuisé » est partagé par la base (tâche 5).
4. **Fiches d'avant la migration.** Les deux marqueurs sont à faux. Elles sont donc
   reprises par le rattrapage, stock d'abord, et aucune n'est vidée en attendant
   (`RG-129`).
5. **Recommandations différées.** Le job est créé et demandé à la clôture comme
   aujourd'hui. `ProcessBookRecommendations` le laisse en attente tant que la fiche n'est
   pas définitive. Il est redemandé quand la fiche le devient. Test : tâche 10.
6. **Cache de la scanette.** Le delta gagne des champs. Une copie locale ancienne sans
   ces champs les traite comme `null`, et une resynchronisation complète est forcée une
   fois (tâche 16).

## Fichiers

| Couche | Créés | Modifiés |
|---|---|---|
| Domain | `BookAggregate/Entities/BookIsbndbRecord.cs`, `BookAggregate/ValueObjects/WorkKey.cs`, `IsbndbAggregate/IsbndbQuotaState.cs`, `IsbndbAggregate/IsbndbDailyUsage.cs`, `IsbndbAggregate/IsbndbCatchUpRun.cs`, `IsbndbAggregate/IsbndbUsage.cs` | `BookAggregate/Book.cs`, `ValueObjects/BookMetadataSource.cs`, `BookCoverSource.cs`, `BookMetadataField.cs`, `BookMetadataPatch.cs`, `Common/Errors/Errors.Book.cs` |
| Infrastructure | `Services/Isbndb/IsbndbOptions.cs`, `IsbndbClient.cs`, `IsbndbRateLimitHeader.cs`, `IsbndbQuotaGuard.cs`, `Persistence/Configurations/BookIsbndbRecordConfiguration.cs`, `IsbndbConfigurations.cs`, migration `AddIsbndbSource` | `DependencyInjection.cs`, `Persistence/ProjectDbContext.cs`, `Persistence/Configurations/BookConfiguration.cs`, `Services/Bibliographic/BibliographicMetadataResolver.cs`, `CachedBibliographicMetadataResolver.cs`, `BookMetadataEnrichmentQueue.cs`, `BookMetadataEnrichmentBackgroundService.cs`, `Services/Recommendations/BibliographicNoticeReader.cs`, `appsettings*.json` (Api) |
| Application | `Common/Interfaces/Services/IIsbndbClient.cs`, `IIsbndbQuotaGuard.cs`, `Isbndb/IsbndbBook.cs`, `Isbndb/IsbndbLookupService.cs`, `Isbndb/IsbndbNoticeMapper.cs`, `Isbndb/IsbndbGenreMapper.cs`, `Isbndb/Commands/CatchUpIsbndbBooks/*`, `Isbndb/Commands/SetIsbndbCompletion/*`, `Isbndb/Commands/RecheckIsbndbBook/*`, `Isbndb/Queries/GetIsbndbStatus/*`, `Isbndb/Queries/GetIsbndbAbsentBooks/*`, `Isbndb/Queries/SearchIsbndbEditions/*` | `Books/Commands/Background/EnrichPendingBooksCommand*.cs`, `Books/Queries/GetBookMetadata/GetBookMetadataQueryHandler.cs`, `Books/Common/BookMetadataResult.cs`, `ScanCatalogBookResult.cs`, `AdministrationResults.cs`, `Books/Queries/GetCatalogDelta/GetCatalogDeltaQueryHandler.cs`, `Books/Queries/Admin/GetAdminBookQueryHandler.cs`, `GetCatalogAdminOverviewQueryHandler.cs`, `Books/Commands/UpdateBookMetadata/*`, `Recommendations/Commands/ProcessBookRecommendations/ProcessBookRecommendationsCommandHandler.cs`, `RefreshBookSimilarityProfiles/RefreshBookSimilarityProfilesCommandHandler.cs`, `Common/Interfaces/Persistence/IProjectDbContext.cs`, `Common/Interfaces/Services/IBookMetadataEnrichmentQueue.cs` |
| Contracts / Api | `Contracts/Isbndb/*.cs`, `Api/Controllers/IsbndbAdministrationController.cs` | `Api/Controllers/BibliographicReferenceController.cs`, `BookAdministrationController.cs`, `BookController.cs` (métadonnées scan), `Api/Common/RateLimiting/RateLimiting.cs`, `RateLimitingPolicies.cs`, `Api/Program.cs`, `Contracts/Books/Responses/*` concernés |
| Worker | `IsbndbCatchUpFunction.cs` | `Program.cs` (rien si `AddInfrastructure` suffit) |
| Catalog | `features/administration/isbndb/**` | `core/catalog.models.ts`, `core/catalog-admin-api.service.ts`, `core/catalog-api.service.ts`, `core/catalog-administration-route.ts`, `features/administration/catalog-administration-page.component.{ts,html,scss}`, `features/search/catalog-search-page.component.{ts,html,scss}` |
| Scan | — | `offline/scan-offline.model.ts`, `scan-verdict.service.ts`, `scan-sync.service.ts`, `scan-local-store.service.ts`, `scan-local-store-migration.ts`, `scanner/book-metadata.model.ts`, `scanner/scanner.component.{ts,html}`, `src/styles.scss` |
| Infra | — | `infra/main.bicep`, `infra/modules/KeyVault/appSecrets.module.bicep`, `infra/parameters/main.dev.bicepparam`, `.github/workflows/infra-deploy.yml` |

Chemins backend relatifs à `src/Backend/Vole_Papillon_Damour.<Projet>/`, front à
`src/<App>/src/app/`.

---

### Tâche 0 : état initial

- [ ] `rtk git status` propre sur `feat/isbndb-migration`.
- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx`
- [ ] `dotnet test` sur `Domain.tests`, `Application.tests`, `Infrastructure.tests`,
  `Api.tests`, `Worker.tests`.
- [ ] `npm ci && npm test -- --watch=false && npm run build` dans `src/Catalog` puis
  `src/Scan`.
- [ ] Noter les échecs déjà présents (dans `NEXT.md` : deux attentes `pageSize` héritées
  dans la spec admin Catalog, et `AccountCard`) **sans les corriger**. Pas de commit.

---

### Tâche 1 : domaine — marqueurs ISBNdb, notice ISBNdb, nouveaux champs

**Files :**
- Create : `Domain/BookAggregate/Entities/BookIsbndbRecord.cs`
- Modify : `Domain/BookAggregate/Book.cs`, `ValueObjects/BookMetadataSource.cs` (ajouter
  `Isbndb` **en fin d'enum** : valeurs stockées en `byte`), `BookCoverSource.cs` (`Isbndb`
  en fin), `BookMetadataField.cs` (`Pages`, `Edition` en fin), `BookMetadataPatch.cs`
  (paramètres optionnels `int? Pages = null`, `string? Edition = null`)
- Test : `Domain.tests/Books/BookIsbndbTests.cs`

**Modèle.**
- `Book` gagne :
  - `bool IsIsbndbSourced`, `bool IsAbsentFromIsbndb`, `DateTime? IsbndbCheckedAt` ;
  - `DateTime? IsbndbCompletedAt`, `UserId? IsbndbCompletedBy` (même type que les
    autres attributions admin) ;
  - `int? Pages`, `string? Edition` (100), `string? WorkKey` (200, tâche 2).
- `BookIsbndbRecord` est en 1-1 avec `Book`, clé = ISBN-13. Il porte tous les champs de
  `RG-100` :
  - `TitleLong`, `Isbn10`, `Binding`, `DatePublished` (texte brut), `EditionNumber`,
    `Dimensions` (texte) ;
  - les mesures structurées en colonnes : `HeightValue`, `HeightUnit`, `WidthValue`,
    `WidthUnit`, `LengthValue`, `LengthUnit`, `WeightValue`, `WeightUnit` ;
  - `DeweyDecimal` (JSON), `Synopsis`, `Overview`, `Excerpt`, `Subjects` (JSON),
    `Authors` (JSON, forme brute « Nom, Prénom »), `OtherIsbns` (JSON
    `[{isbn,binding}]`), `Related` (JSON), `Msrp`, `ListPriceAmount`,
    `ListPriceCurrency` ;
  - `LanguageCode` (brut, ex. `fra`), `ImageUrl`, `RawJson` (réponse complète **sans**
    `image_original`), `FetchedAt`.

**Comportements (tests d'abord) :**
- [ ] `ApplyIsbndbNotice(BookMetadataPatch patch, DateTime fetchedAt)`.
  - Applique tous les champs du patch **sauf** ceux corrigés à la main (`RG-05`, réutiliser
    `ReadManuallyEditedFields`).
  - **Écrase** les valeurs de secours (`RG-110`), contrairement à `ApplyAutomaticMetadata`,
    qui ne remplit que les vides en rafraîchissement.
  - Positionne `IsIsbndbSourced = true`, `IsAbsentFromIsbndb = false`,
    `IsbndbCheckedAt = fetchedAt`, `MetadataSource = Isbndb` (ou `Manual` s'il existe des
    champs manuels, comme aujourd'hui), `MetadataStatus = Resolved` (ou `Manual`),
    `CoverSource = Isbndb` si la couverture est appliquée, et `MetadataFailureReason =
    null`.
  - Refusé sur une fiche `Discarded`.
- [ ] Fiche complétée (`IsbndbCompletedAt != null`) : `ApplyIsbndbNotice` ne remplit
  **que les champs vides** (`Q-04`), puis passe `IsIsbndbSourced = true` et
  `IsAbsentFromIsbndb = false`.
- [ ] `MarkAbsentFromIsbndb(DateTime checkedAt)` : `IsAbsentFromIsbndb = true`,
  `IsIsbndbSourced = false`, `IsbndbCheckedAt = checkedAt`. Les métadonnées existantes ne
  changent pas.
- [ ] `RecordIsbndbCheck(DateTime at)` : met seulement à jour `IsbndbCheckedAt` (revérif
  mensuelle sans changement).
- [ ] `MarkIsbndbCompleted(UserId by, DateTime at)`.
  - Exige `IsAbsentFromIsbndb`, un `Title` et un `Authors` non vides (`RG-127`), sinon
    `InvalidOperationException`.
  - L'appel est idempotent.
- [ ] `ReopenIsbndbCompletion(DateTime at)` remet `IsbndbCompletedAt` et
  `IsbndbCompletedBy` à `null`.
- [ ] Propriété calculée `IsDefinitiveForRecommendation =>
  IsIsbndbSourced || IsAbsentFromIsbndb` (non mappée).
- [ ] `ApplyManualMetadata` et `ValidatePatch` gèrent `Pages` (1–100 000) et `Edition`
  (≤ 100). `ApplyField` les applique.
- [ ] `ApplyAutomaticMetadata` accepte la source `Isbndb`.

Commit : `feat(domain): add ISBNdb provenance flags and notice record to books`

---

### Tâche 2 : domaine — clé d'œuvre (`RG-119`)

**Files :** Create `Domain/BookAggregate/ValueObjects/WorkKey.cs` ; Modify `Book.cs` ;
Test `Domain.tests/Books/WorkKeyTests.cs`

`WorkKey.Compute(string? title, string? authors) → string?`, puis `Book` recalcule
`WorkKey` à chaque changement de `Title` ou `Authors`, dans `ApplyField`.

**Règles de normalisation :**
- passage en majuscules invariantes et suppression des accents (FormKD sans marques) ;
- ponctuation remplacée par des espaces, espaces compactés ;
- titre coupé avant le premier ` : `, ` - `, ` — `, `(` ou `/` (sous-titre et mention
  entre parenthèses) ;
- articles initiaux retirés : `LE LA LES L UN UNE DES DU DE D THE A AN` ;
- tome : `TOME n`, `T n`, `T. n`, `VOL n`, `VOLUME n`, `LIVRE n` ou chiffres romains
  I–XX en fin de titre → suffixe `#n` ;
- auteur principal = premier auteur. Le nom est la partie avant la virgule (« Hugo,
  Victor ») ou le dernier mot (« Victor Hugo »), normalisé de la même façon.

**Résultat :** `"<TITRE>|<NOM>"` ou `"<TITRE>|<NOM>#<n>"`. Retourne `null` si le titre
normalisé fait moins de 4 caractères, s'il est dans la liste générique (`OEUVRES`,
`OEUVRES COMPLETES`, `POEMES`, `POESIES`, `THEATRE`, `ROMANS`, `NOUVELLES`, `CONTES`,
`CORRESPONDANCE`, `MEMOIRES`, `ANTHOLOGIE`) ou s'il n'y a aucun auteur.

**Cas de test** (entrée → sortie) :

| Titre | Auteurs | Clé |
|---|---|---|
| `Les misérables` | `Hugo, Victor` | `MISERABLES\|HUGO` |
| `Les Misérables` | `Victor Hugo` | `MISERABLES\|HUGO` |
| `Les Misérables ( Les Misérables, Tome 1)` | `Victor Hugo` | `MISERABLES\|HUGO` *(la parenthèse est coupée ; tome non détecté → même clé, assumé)* |
| `Les Misérables - Tome 2` | `Victor Hugo` | `MISERABLES\|HUGO#2` |
| `Germinal` | `Zola, Émile` | `GERMINAL\|ZOLA` |
| `Œuvres complètes` | `Rimbaud` | `null` |
| `Le petit prince` | `Saint-Exupéry, Antoine de` | `PETIT PRINCE\|SAINT EXUPERY` |
| `Astérix T. 3` | `Goscinny` | `ASTERIX\|GOSCINNY#3` |
| `Ça` | `King` | `null` (moins de 4 caractères) |
| `Dune` | *(vide)* | `null` |

Commit : `feat(domain): compute work grouping key from title and main author`

---

### Tâche 3 : domaine — quota, usage, compte rendu de rattrapage

**Files :** Create `Domain/IsbndbAggregate/IsbndbQuotaState.cs` (ligne unique, `Id = 1`),
`IsbndbDailyUsage.cs`, `IsbndbCatchUpRun.cs`, `IsbndbUsage.cs` ; Test
`Domain.tests/Isbndb/IsbndbQuotaStateTests.cs`.

- `IsbndbUsage` : `Scan`, `CatchUp`, `PublicSearch`, `Administration`.
- `IsbndbQuotaState` porte les champs suivants :
  - `DailyLimit`, `DailyRemaining`, `ResetsAt`, `ObservedAt` ;
  - `ExhaustedUntil?`, `LastFailureAt?`, `LastFailureReason?` (≤ 500).
- Méthodes de `IsbndbQuotaState` :
  - `Observe(limit, remaining, resetsAt, at)`. Si `remaining == 0` →
    `ExhaustedUntil = resetsAt`. Une observation après `ExhaustedUntil` efface
    l'épuisement.
  - `MarkExhausted(until, at)`.
  - `RecordFailure(reason, at)`.
  - `IsExhausted(now) => ExhaustedUntil > now`.
  - `Status(now)` : `Operational` | `Exhausted` | `Unavailable` (échec depuis moins de
    15 min sans succès plus récent).
- `IsbndbDailyUsage` : clé (`Day` en date UTC, `Usage`), `Credits` (int),
  `Increment(n)`.
- `IsbndbCatchUpRun` contient :
  - `StartedAt`, `EndedAt?`, `Filled`, `Absent`, `Rechecked`, `Remaining` ;
  - `StopReason` : `Completed` | `QuotaExhausted` | `Unavailable` | `Cancelled`.

Commit : `feat(domain): track ISBNdb quota, daily usage and catch-up runs`

---

### Tâche 4 : persistance — configurations et migration `AddIsbndbSource`

**Files :**
- Create : `Infrastructure/Persistence/Configurations/BookIsbndbRecordConfiguration.cs`,
  `IsbndbConfigurations.cs`
- Modify : `BookConfiguration.cs`, `ProjectDbContext.cs`, `IProjectDbContext.cs`
  (`DbSet` `BookIsbndbRecords`, `IsbndbQuotaStates`, `IsbndbDailyUsages`,
  `IsbndbCatchUpRuns`)
- Test : `Infrastructure.tests/Persistence/IsbndbPersistenceTests.cs` (aller-retour
  SQLite des nouvelles entités et colonnes)

- [ ] Colonnes `Books` :
  - `IsIsbndbSourced` et `IsAbsentFromIsbndb` en **bit non nul, défaut `0`** (`RG-129`) ;
  - `IsbndbCheckedAt`, `IsbndbCompletedAt`, `IsbndbCompletedBy`, `Pages`,
    `Edition nvarchar(100)`, `WorkKey nvarchar(200)` ;
  - index sur `WorkKey` ;
  - index filtré sur `(IsIsbndbSourced, IsAbsentFromIsbndb)` pour le rattrapage et E2.
- [ ] `BookIsbndbRecords` : PK/FK `Isbn13` → `Books` avec suppression en cascade (la
  suppression définitive d'une fiche existe). Textes longs en `nvarchar(max)`.
- [ ] `IsbndbQuotaStates` reçoit une ligne `Id = 1` par `HasData` : limite 5000,
  restant 5000, dates nulles ou minimales.
- [ ] `dotnet ef migrations add AddIsbndbSource` : vérifier que la migration **n'altère
  aucune** colonne existante et ne supprime rien, puis que
  `dotnet ef migrations has-pending-model-changes` répond non.
- [ ] Le calcul initial de `WorkKey` pour l'existant n'est **pas** fait en SQL (règle
  C#). Il est fait par le rattrapage (tâche 11, étape 0).

Commit : `feat(infrastructure): persist ISBNdb provenance, notices and quota`

---

### Tâche 5 : Infrastructure — client ISBNdb et garde de quota

**Files :**
- Create : `Application/Common/Interfaces/Services/IIsbndbClient.cs`,
  `IIsbndbQuotaGuard.cs`, `Application/Isbndb/IsbndbBook.cs`,
  `Infrastructure/Services/Isbndb/IsbndbOptions.cs`, `IsbndbClient.cs`,
  `IsbndbRateLimitHeader.cs`, `IsbndbQuotaGuard.cs`
- Modify : `Infrastructure/DependencyInjection.cs`, `Api/appsettings.json`,
  `appsettings.Development.json` (section `Isbndb` avec `ApiKey: ""`)
- Test : `Infrastructure.tests/Isbndb/IsbndbClientTests.cs`,
  `IsbndbRateLimitHeaderTests.cs`, `IsbndbQuotaGuardTests.cs` (reprendre
  `StubHttpMessageHandler` de `Bibliographic/OpenLibraryClientTests.cs`)

**Contrat :**
```
Task<IsbndbLookup> GetBookAsync(Isbn13, IsbndbUsage, CancellationToken)
Task<IsbndbBulkLookup> GetBooksAsync(IReadOnlyList<Isbn13>, IsbndbUsage, CancellationToken)   // ≤ BulkSize
Task<IsbndbSearch> SearchAsync(string text, IsbndbUsage, CancellationToken)          // page=1, pageSize=100, language=fr
```
- `IsbndbLookup` est `Found(IsbndbBook)` | `NotFound` | `QuotaExhausted(resetsAt)` |
  `Unavailable(reason)`.
- `IsbndbBulkLookup` contient `Found` (dictionnaire ISBN → livre), la liste des ISBN
  `Missing`, ou `QuotaExhausted` / `Unavailable`.
- `IsbndbBook` reprend tous les champs JSON observés (spec §10), sauf `image_original`,
  plus `RawJson` déjà épuré de `image_original`.

**Tests d'abord :**
- [ ] En-tête `Authorization: <clé>` présent. La clé n'est jamais dans l'URL ni dans un
  log : vérifier avec un `FakeLogger` que le message ne contient pas la clé.
- [ ] `GET /book/9782070409228` 200 → `Found`, `image` conservée, `image_original`
  absente de `RawJson`.
- [ ] 404 `{"message":"Book not found"}` → `NotFound`, et aucun crédit compté.
- [ ] `POST /books` avec le corps `{"isbns":[...]}` : la réponse
  `{"total":2,"requested":3,"data":[…]}` donne 2 `Found` et 1 `Missing`. Le lot est
  découpé en morceaux de `BulkSize`.
- [ ] `IsbndbRateLimitHeader.TryParse` :
  - deux en-têtes `ratelimit: "rate";r=59;t=0` et `ratelimit: "daily";r=4997;t=15025` →
    `remaining = 4997`, `resetsAt = now + 15025 s` ;
  - `ratelimit-policy: "daily";q=5000;w=86400` → `limit = 5000` ;
  - un en-tête malformé est ignoré.
- [ ] Chaque réponse qui porte l'en-tête appelle `quotaGuard.ObserveAsync` (limite,
  restant, renouvellement).
- [ ] 429 → nouvelle tentative avec attente croissante (1 s, 2 s, 4 s, ou `Retry-After`
  s'il est présent), au plus `MaxRetries`, puis `Unavailable`. Tester avec un
  `TimeProvider` factice.
- [ ] 429 ou 403 alors que le dernier `daily` vaut `r=0`, ou dont le corps contient
  `limit` ou `quota` → `QuotaExhausted` et `quotaGuard.MarkExhaustedAsync(resetsAt)`.
  Aucune nouvelle tentative.
- [ ] 5xx, délai dépassé ou JSON invalide → `Unavailable`, `RecordFailure`, sans nouvelle
  tentative (le secours prend le relais).
- [ ] Garde épuisé (`IsExhausted(now)`) → `QuotaExhausted` **sans aucun appel HTTP**
  (le stub échoue s'il est appelé).
- [ ] `ApiKey` vide → `Unavailable("ISBNdb non configuré")` sans appel HTTP.
- [ ] Annulation du `CancellationToken` pendant l'appel → `OperationCanceledException`
  propagée, sans nouvelle tentative (`RG-107`).
- [ ] Débit : un `TokenBucketRateLimiter` singleton par processus (`RequestsPerSecond`
  jetons par seconde, file `QueueLimit` 100) est acquis avec le jeton d'annulation de
  l'appelant. Tester que 3 appels successifs avec `RequestsPerSecond = 1` attendent au
  moins 2 s (`TimeProvider` factice).
- [ ] Usage : `quotaGuard.RecordUsageAsync(usage, credits)` compte 1 pour un `Found`
  unitaire, 1 pour une recherche, et `Found.Count` pour un lot. Rien pour un 404.

**`IsbndbQuotaGuard`** (scoped, s'appuie sur `IProjectDbContext`) :
- l'état d'épuisement est mis en cache mémoire 30 s ;
- les écritures font un upsert de la ligne `Id = 1` et de `IsbndbDailyUsages`. Elles
  sont rejouées une fois sur conflit de concurrence et ne font jamais échouer l'appel
  métier : on journalise un avertissement et on continue.

**Enregistrement DI :**
- `AddHttpClient<IIsbndbClient, IsbndbClient>` avec `BaseAddress` = `BaseUrl` et
  `Timeout` = `TimeoutMilliseconds` ;
- `Configure<IsbndbOptions>("Isbndb")` ;
- limiteur singleton.

Commit : `feat(infrastructure): add rate-limited ISBNdb client with shared quota guard`

---

### Tâche 6 : Application — conversion de la notice et genres (`RG-100` à `RG-103`, `RG-123`, `RG-124`)

**Files :**
- Create : `Application/Isbndb/IsbndbNoticeMapper.cs`, `IsbndbGenreMapper.cs`
- Test : `Application.tests/Isbndb/IsbndbNoticeMapperTests.cs`,
  `IsbndbGenreMapperTests.cs`

`IsbndbNoticeMapper.ToPatch(IsbndbBook) → BookMetadataPatch` et
`ToRecord(IsbndbBook, fetchedAt) → BookIsbndbRecord`. Règles :
- `Title` = `title` (à défaut `title_long`) ; le titre long reste dans le record.
- `Authors` : chaque entrée « Nom, Prénom » devient « Prénom Nom » ; une entrée sans
  virgule est gardée telle quelle ; une entrée contenant `;` est découpée ; jointure
  `", "`. `["Hugo, Victor"]` → `Victor Hugo` ; `["Saint-Exupéry, Antoine de"]` →
  `Antoine de Saint-Exupéry`.
- `PublicationYear` = 4 premiers chiffres de `date_published` (`1999`, `2015-06-09`,
  `2021-12`).
- `PhysicalFormat` (≤ 50) : `Paperback`→`Broché`, `Mass Market Paperback`→`Poche`,
  `Hardcover`→`Relié`, `ePub`/`eBook`/`Kindle Edition`→`Numérique`,
  `Audio CD`→`Livre audio`, `Board book`→`Tout-carton`, sinon la valeur brute.
- `Language` : `fra`→`fr`, `eng`→`en`, `spa`→`es`, `deu`/`ger`→`de`, `ita`→`it`,
  sinon le code brut s'il fait 10 caractères ou moins.
- `Summary` = `synopsis`, à défaut `overview`, nettoyé des balises HTML et des entités,
  puis passé par `BookSummaryText.Clean`.
- `CoverUrl` = `image` uniquement, seulement si l'URL est en HTTPS (`RG-101`).
- `Pages` = `pages` si > 0 ; `Edition` = `edition` (≤ 100).
- `Genres` = `IsbndbGenreMapper.Map(subjects, publisher, authors)`.
- Les champs absents de la notice ne sont pas dans `Fields` : ils n'effacent rien.

`IsbndbGenreMapper.Map` → `(IReadOnlyList<string> Genres, IReadOnlyList<string>
UnmatchedSubjects)`. Les genres viennent uniquement de la liste du catalogue
(`src/Catalog/src/app/core/catalog-genres.ts`, 16 valeurs).
- **Bruit ignoré** : `Self Service`, `Arborist Merchandising Root`, `Subjects`, tout sujet
  égal à un auteur de la notice (forme « Nom, Prénom » ou « Nom »).
- **Table de correspondance** (comparaison sans casse ni accents, sujet entier ou
  segment) :

| Sujet | Genre |
|---|---|
| `Romans`, `Roman`, `Fiction`, `Literary`, `Literature & Fiction`, `Classics`, `Genre Fiction` | Roman |
| `Science Fiction`, `Science-fiction` | Science-fiction |
| `Fantasy`, `Epic` *(si `Fantasy` présent)* | Fantasy |
| `Science Fiction & Fantasy` | Science-fiction **et** Fantasy |
| `Bandes dessinées`, `Comics`, `Graphic Novels`, `Comics & Graphic Novels` | Bande dessinée |
| `Manga`, `Mangas` | Manga |
| `Mystery`, `Policiers`, `Romans policiers`, `Crime`, `Detective` | Policier |
| `Thriller`, `Thrillers`, `Suspense` | Thriller |
| `Romance`, `Romans sentimentaux` | Romance |
| `History`, `Histoire`, `Historical` | Histoire |
| `Biography`, `Biographies`, `Biography & Autobiography`, `Mémoires` | Biographie |
| `Juvenile Fiction`, `Children's Books`, `Jeunesse`, `Albums jeunesse`, `Young Adult` | Jeunesse |
| `Essays`, `Essais`, `Philosophy`, `Philosophie` | Essai |
| `Drama`, `Dramas & Plays`, `Théâtre` | Théâtre |
| `Poetry`, `Poésie` | Poésie |
| `Cooking`, `Cookbooks`, `Cuisine` | Cuisine |
| `Reference`, `Study Aids`, `Education`, `Travel`, `Nature`, `Science` | Documentaire |

- **Indices d'éditeur** : `Dargaud`, `Dupuis`, `Casterman`, `Le Lombard`, `Delcourt`,
  `Glénat` (hors `Glénat Manga`), `Soleil`, `Bamboo` → Bande dessinée ; `Kana`, `Pika`,
  `Kurokawa`, `Ki-oon`, `Kazé`, `Glénat Manga`, `Tonkam` → Manga. Réutiliser les listes
  de `Recommendations/Similarity/SimilarityFeatures.cs` plutôt que les dupliquer.
- **« Roman » générique** n'est ajouté que si aucun genre plus précis n'est trouvé. Au
  plus 3 genres. L'ordre suit la table.
- Les sujets sans correspondance (hors bruit) sont renvoyés pour le rapport E1.

**Tests d'abord :**
- `["Literature & Fiction","Genre Fiction","Science Fiction & Fantasy","Fantasy","Epic"]`
  → `[Science-fiction, Fantasy]` (CA-14).
- `["Arborist Merchandising Root","Self Service"]` → `[]`.
- `["Hugo, Victor"]` avec l'auteur `Hugo, Victor` → `[]`.
- `["Bandes dessinées"]` → `[Bande dessinée]`.
- `["Romans"]` → `[Roman]`.
- `[]` avec l'éditeur `Dargaud` → `[Bande dessinée]`.
- Les cas de conversion de la notice ci-dessus, dont le JSON réel de *Les misérables*
  9782070409228 relevé en spec §10, à mettre en fixture.

Commit : `feat(application): map ISBNdb notices and subjects to catalogue fields and genres`

---

### Tâche 7 : Application — `IsbndbLookupService` (cache par ISBN, `RG-104`)

**Files :** Create `Application/Isbndb/IsbndbLookupService.cs` (+ interface) ; Test
`Application.tests/Isbndb/IsbndbLookupServiceTests.cs`

- `LookupAsync(Isbn13, usage, ct)` et `LookupManyAsync(IReadOnlyList<Isbn13>, usage,
  ct)` s'appuient sur `HybridCache`, clé `isbndb-book:{isbn13}`.
  - Une entrée est mise en cache pour `Found` et `NotFound` (`LookupCacheHours`).
  - `QuotaExhausted` et `Unavailable` ne sont **jamais** mis en cache.
- `LookupManyAsync` lit d'abord le cache ISBN par ISBN. Il n'envoie en requête groupée
  que les absents du cache. Il écrit ensuite **chaque** résultat dans le cache, trouvés
  et manquants.
- Un ISBN « manquant » d'un lot est **confirmé** par `GetBookAsync`, gratuit s'il est
  vraiment absent (`RG-111`), avant d'être mis en cache comme `NotFound`.
- Un ISBN-13 non valide n'est jamais envoyé.
- **Tests :**
  - un `LookupAsync` puis un `LookupManyAsync` sur le même ISBN ne font qu'**un** appel
    client (CA-02) ;
  - un lot de 3 ISBN dont 1 déjà en cache → un `GetBooksAsync` de 2 ;
  - `QuotaExhausted` n'est pas mis en cache ;
  - l'annulation se propage.

Commit : `feat(application): share one ISBNdb lookup per ISBN across scans and batches`

---

### Tâche 8 : remplissage d'une fiche — ISBNdb d'abord, secours ensuite

**Files :**
- Modify :
  - `Application/Books/Commands/Background/EnrichPendingBooksCommand.cs` (+ liste
    `Isbn13s`) et son handler ;
  - `Application/Common/Interfaces/Services/IBookMetadataEnrichmentQueue.cs`
    (`DequeueBatchAsync(int max, CancellationToken)`) ;
  - `Infrastructure/Services/Bibliographic/BookMetadataEnrichmentQueue.cs` et
    `BookMetadataEnrichmentBackgroundService.cs` ;
  - `Application/Books/Common/BookMetadataResult.cs` (+ `IsbndbOutcome`, `Pages`,
    `Edition`, `WorkKey`) ;
  - `Infrastructure/Services/Bibliographic/BibliographicMetadataResolver.cs` et
    `CachedBibliographicMetadataResolver.cs`.
- Test : `Application.tests/Books/Commands/EnrichPendingBooksIsbndbTests.cs`,
  `Infrastructure.tests/Bibliographic/BookMetadataEnrichmentQueueTests.cs` (ajouts),
  `BibliographicMetadataResolverTests.cs` (ajouts)

**Résolveur.**
- `BibliographicMetadataResolver.ResolveAsync` appelle d'abord
  `IsbndbLookupService.LookupAsync(isbn, usage, ct)`. L'usage est passé en paramètre
  (`Scan` par défaut).
- `Found` → `BookMetadataResult` avec `Source = "ISBNdb"`, `CoverSource = "ISBNdb"`,
  `IsbndbOutcome = Found` et le `IsbndbBook` attaché. Aucune source gratuite n'est
  appelée **pour les champs**.
- `NotFound` → chaîne actuelle BnF → Open Library → Google Books, avec
  `IsbndbOutcome = NotFound`.
- `QuotaExhausted` ou `Unavailable` → même chaîne, avec
  `IsbndbOutcome = Deferred`.
- Le cache du résolveur passe à **une seule clé par ISBN**. On supprime la distinction
  `core` / `details`, puisque la notice ISBNdb est complète.
- Identifiant d'œuvre (`RG-115`) : un appel BnF puis Open Library, gratuits, **seulement
  depuis l'enrichissement** (pas sur le chemin d'affichage du scan), et seulement si
  `WorkId` est vide et non corrigé. Ce complément ne modifie aucun autre champ.

**Enrichissement.**
- La file se vide **par lots**. Le service attend le premier ISBN, rassemble pendant au
  plus 500 ms jusqu'à `BulkSize` ISBN, puis envoie un seul
  `EnrichPendingBooksCommand(Isbn13s: lot)`. Le handler appelle
  `LookupManyAsync(lot, Scan)` une fois, puis traite chaque ISBN. L'import CSV admin
  (`ImportAdminBooksCommandHandler`) en bénéficie automatiquement.
- `Found` :
  - `book.ApplyIsbndbNotice(IsbndbNoticeMapper.ToPatch(...))` ;
  - upsert de `BookIsbndbRecord` ;
  - complément `WorkId` ;
  - si un `BookRecommendationJob` existe, `Request(now, force: true)` puis mise en file
    (motif existant, lignes 465–480).
- `NotFound` : secours actuel (inchangé), puis `book.MarkAbsentFromIsbndb(now)`, puis la
  même relance de recommandation.
- `Deferred` : secours actuel, marqueurs inchangés (faux / faux), **pas** de relance de
  recommandation.
- Les modes de rafraîchissement existants (couverture, genres, détails,
  `FullCatalogRefreshStartedAt`) **excluent** les fiches `IsIsbndbSourced`. Une fiche
  ISBNdb n'est jamais redemandée (`RG-104`) : ajouter `&& !book.IsIsbndbSourced` à
  chaque branche de `candidateQuery` qui vise des fiches `Resolved` ou `Manual`.

**Tests d'abord :**
- fiche `Pending` + ISBNdb `Found` → `IsIsbndbSourced`, champs écrasés, record créé,
  BnF non appelée pour les champs ;
- `NotFound` → secours appliqué, `IsAbsentFromIsbndb` ;
- `QuotaExhausted` → secours appliqué, deux marqueurs à faux, job non relancé (CA-03) ;
- un lot de 3 ISBN de la file → un seul `LookupManyAsync` ;
- fiche ISBNdb `Resolved` sans couverture → pas candidate au rafraîchissement de
  couverture.

Commit : `feat(enrichment): resolve books from ISBNdb first with temporary fallback`

---

### Tâche 9 : lecture de fiche au scan et delta de la scanette

**Files :**
- Modify :
  - `Application/Books/Queries/GetBookMetadata/GetBookMetadataQueryHandler.cs` (+
    `WorkKey`, `Pages`, `Edition` ; source `ISBNdb`) ;
  - `Application/Books/Common/ScanCatalogBookResult.cs` (+ `WorkKey`, `Publisher`,
    `PublicationYear`, `OtherIsbns` : liste des ISBN-13 du record ISBNdb, vide sinon) ;
  - `GetCatalogDeltaQueryHandler.cs` ;
  - les contrats `ScanCatalogBookResponse` et `BookMetadataResponse` ;
  - `BookController.cs`.
- Test : `Application.tests/Books/Queries/GetBookMetadataQueryHandlerTests.cs`,
  `GetCatalogDeltaQueryHandlerTests.cs` (ajouts)

- Fiche `Resolved` ou `Manual` → réponse depuis la base, sans aucun appel (inchangé).
  Fiche inconnue ou `Pending` → résolveur avec l'usage `Scan`.
- Le delta expose `workKey`, `publisher`, `publicationYear` et `otherIsbns`. Le
  chargement de `OtherIsbns` se fait par une seule requête jointe sur
  `BookIsbndbRecords`, sans N+1.
- `BookMetadataResult` porte `WorkKey`, calculé par `WorkKey.Compute` quand la fiche
  n'est pas encore en base.

Commit : `feat(scan-api): expose work keys and edition details for same-work detection`

---

### Tâche 10 : recommandations sur fiche définitive (`RG-112` à `RG-114`)

**Files :**
- Modify :
  - `Application/Recommendations/Commands/ProcessBookRecommendations/ProcessBookRecommendationsCommandHandler.cs` ;
  - `RefreshBookSimilarityProfiles/RefreshBookSimilarityProfilesCommandHandler.cs` ;
  - `Infrastructure/Services/Recommendations/BibliographicNoticeReader.cs` (aucun
    changement de comportement attendu ; à relire seulement).
- Test : `Application.tests/Recommendations/ProcessBookRecommendationsIsbndbTests.cs`,
  `RefreshBookSimilarityProfilesIsbndbTests.cs`

- [ ] `ProcessBookRecommendations` : un livre non définitif
  (`!IsIsbndbSourced && !IsAbsentFromIsbndb`) est traité comme `MetadataStatus ==
  Pending`. Il est compté `waitingForMetadata`, le job reste `Pending` sans démarrer, et
  il est relancé par les transitions des tâches 8, 11 et 12. `CloseScanSessionCommandHandler`
  ne change pas : il crée et demande les jobs comme aujourd'hui.
- [ ] `RefreshBookSimilarityProfiles` construit la `SimilarityEdition` d'une fiche ISBNdb
  à partir de `BookIsbndbRecord`, **sans appel ISBNdb**.
  - Champs ISBNdb : `Title` = titre de la fiche, `Subtitle` tiré de `TitleLong` s'il est
    plus long, `Authors` = auteurs de la fiche, `AuthorSurnames` = noms extraits comme
    dans la tâche 2.
  - `Subjects` = sujets du record hors bruit, `Forms` = genres de la fiche,
    `Dewey` = premier `dewey_decimal`, `EditionSummary` = `Synopsis`.
  - Champs complétés par `noticeReader.ReadAsync` si la BnF ou Open Library répondent
    (gratuit) : `Audience333`, `CnljReviewed`, `Collection`, `SeriesTitle`,
    `SeriesNumber`, `PartNumber`, `PartTitle`, `BnfWorkId`, `OpenLibraryWorkKey`.
  - Si le lecteur ne répond pas, le profil est quand même `NoticeFound = true`. Une fiche
    ISBNdb a toujours une notice.
- [ ] Le complément de résumé BnF ou Open Library (bloc `ApplyAutomaticMetadata(... Summary ...)`)
  est **ignoré** pour une fiche `IsIsbndbSourced` : il ne doit pas réécrire
  `MetadataSource`.
- [ ] Une fiche hors ISBNdb garde le chemin actuel (notice BnF ou Open Library).
- **Tests :**
  - job d'une fiche de secours → reste `Pending`, `waitingForMetadata = 1` (CA-05, première
    moitié) ;
  - fiche ISBNdb sans notice BnF → profil trouvé, et le texte d'embedding contient le
    synopsis et les sujets.

Commit : `feat(recommendations): embed only definitive notices, built from stored ISBNdb data`

---

### Tâche 11 : rattrapage nocturne (`RG-108` à `RG-111`, `RG-129`)

**Files :**
- Create : `Application/Isbndb/Commands/CatchUpIsbndbBooks/CatchUpIsbndbBooksCommand.cs`,
  `…Handler.cs`, `Worker/IsbndbCatchUpFunction.cs`
- Test : `Application.tests/Isbndb/CatchUpIsbndbBooksCommandHandlerTests.cs`,
  `Worker.tests/IsbndbCatchUpFunctionTests.cs`

**Étapes du handler, dans l'ordre :**
0. **Clés d'œuvre.** Calculer `WorkKey` pour toutes les fiches où elle est `null` alors
   que le titre et les auteurs sont présents. Lots de 500, sans appel externe.
1. **Créer un `IsbndbCatchUpRun`.**
2. **Sélectionner les candidats** : `!IsIsbndbSourced && !IsAbsentFromIsbndb &&
   MetadataStatus != Discarded && RedirectedToIsbn13 == null`, dans l'ordre suivant
   (`RG-108`) :
   - en stock (`QuantityAvailable > 0` ou une annonce non libérée) ;
   - suivie (watchlist d'édition ou d'œuvre active, ou demande manuelle ouverte) ;
   - `FirstSeenAt` décroissant, puis ISBN.
   La lecture se fait par pages de `BulkSize`.
3. **Traiter chaque page.**
   - Un appel `LookupManyAsync(page, CatchUp)`.
   - Trouvés → même application que la tâche 8 (`ApplyIsbndbNotice`, record, `WorkId`,
     relance de recommandation si un job existe).
   - Confirmés absents → `MarkAbsentFromIsbndb` + relance de recommandation.
   - `SaveChanges` à chaque page : c'est le point de reprise.
4. **Arrêts.**
   - `QuotaExhausted` → arrêt immédiat, `StopReason = QuotaExhausted`, aucun nouvel
     appel (`RG-109`, CA-06).
   - `Unavailable` → arrêt, `StopReason = Unavailable`.
   - Annulation → arrêt, `StopReason = Cancelled`, compte rendu enregistré avec
     `CancellationToken.None`.
5. **Revérification mensuelle** (`RG-111`), seulement si le quota n'est pas épuisé.
   - Cibles : fiches `IsAbsentFromIsbndb && IsbndbCompletedAt == null &&
     IsbndbCheckedAt < now - AbsentRecheckDays`, même ordre de priorité.
   - Appels unitaires uniquement : gratuits si le livre est toujours absent.
   - Trouvée → `ApplyIsbndbNotice` ; sinon `RecordIsbndbCheck`.
6. **Clôturer le run.** `Remaining` = nombre de candidats restants.

**Worker.**
- Fonction `IsbndbCatchUp`, `[TimerTrigger("%Isbndb:CatchUpSchedule%")]`, sur le modèle
  de `SocialImportFunction`.
- Fonction `IsbndbCatchUpManual`, `HttpTrigger` POST avec une clé de fonction, sur le
  modèle de `BookEnrichmentFunction.Enrich`, pour lancer le rattrapage à la main.
- Ajouter `Isbndb__CatchUpSchedule` aux réglages du Worker (tâche 18) et dans
  `local.settings` d'exemple s'il existe.

**Tests d'abord :**
- l'ordre de priorité est respecté ;
- un quota épuisé à la page 2 laisse la page 1 enregistrée, aucun appel ensuite, et
  `StopReason = QuotaExhausted` ;
- un champ manuel n'est pas écrasé (CA-07) ;
- un absent confirmé déclenche la relance de recommandation (CA-05, seconde moitié) ;
- une fiche complétée n'est pas revérifiée ;
- toutes les fiches d'avant la migration sont candidates (CA-15).

Commit : `feat(worker): nightly ISBNdb catch-up replacing fallback notices`

---

### Tâche 12 : administration — API (E1, E2, E3)

**Files :**
- Create :
  - `Application/Isbndb/Queries/GetIsbndbStatus/*` ;
  - `GetIsbndbAbsentBooks/*` ;
  - `Commands/SetIsbndbCompletion/*` ;
  - `Commands/RecheckIsbndbBook/*` ;
  - `Contracts/Isbndb/*.cs` ;
  - `Api/Controllers/IsbndbAdministrationController.cs` (policy `Administration`, sur le
    modèle de `ManualRequestAdministrationController`).
- Modify :
  - `Api/Program.cs` (`Use…Controller`) ;
  - `Application/Books/Common/AdministrationResults.cs` : `AdminBookResult` +
    `IsIsbndbSourced`, `IsAbsentFromIsbndb`, `IsbndbCheckedAt`, `IsbndbCompletedAt`,
    `Pages`, `Edition`, `Summary`, `Genres` (liste), `Subjects` (sujets du record ou de
    secours, en lecture seule), `FallbackSource` (source de secours affichée) ;
  - `GetAdminBookQueryHandler.cs` ;
  - `UpdateBookMetadataCommand`, son validateur et son handler (`Pages`, `Edition`,
    `Genres` en liste choisie dans les 16 genres du catalogue) ;
  - `BookAdministrationController.cs` (`PUT …/metadata`) ;
  - `GetCatalogAdminOverviewQueryHandler.cs` (compteur « hors ISBNdb à compléter » pour
    le badge).
- Test : un fichier de tests par handler dans `Application.tests/Isbndb/`, plus
  `Api.tests` pour l'autorisation (401/403 sans rôle `Administration`).

**Endpoints :**

| Méthode | Route | Rôle |
|---|---|---|
| GET | `/books/admin/isbndb/status` | E1 (`RG-125`) : `dailyLimit`, `dailyRemaining`, `observedAt`, `resetsAt`, `status`, `exhaustedUntil`, `usageToday[{usage,credits}]`, `pendingCatchUp{total,inStock,beforeMigration}`, `absentToComplete`, `lastRun{startedAt,endedAt,filled,absent,remaining,stopReason}`, `unmatchedSubjects[{subject,count}]` (10 premiers, calculés sur les records ISBNdb). **Aucun appel ISBNdb.** |
| GET | `/books/admin/books/isbndb-absent?status=to-complete\|completed&search=&page=&pageSize=` | E2 (`RG-126`). Tri : en stock d'abord, puis `FirstSeenAt` décroissant. Chaque ligne : ISBN, titre, auteurs, couverture, source de secours, quantité, `missingFields` (`Summary`, `CoverUrl`, `Genre`, `Publisher`, `PublicationYear`, `Pages`, `Title`, `Authors`). |
| POST | `/books/admin/books/{isbn13}/isbndb-completion` `{ "completed": true\|false }` | E3 (`RG-127`). `true` exige titre + auteur (sinon 400, `Book.IsbndbCompletionIncomplete`). Relance la recommandation (`force`). |
| POST | `/books/admin/books/{isbn13}/isbndb-recheck` | E3 (`RG-128`). Usage `Administration`. Trouvée → `ApplyIsbndbNotice`. Absente → `RecordIsbndbCheck`. Quota épuisé → 409 `Isbndb.QuotaExhausted` avec `resetsAt`. |

- [ ] Modifier le résumé, le titre, les auteurs ou les genres d'une fiche **complétée**
  relance la recommandation (`RG-113`). Ajouter ce cas au handler
  `UpdateBookMetadata`, qui relance déjà la recommandation quand le résumé change
  (ligne ~102).

Commit : `feat(admin-api): ISBNdb status, absent books, completion and recheck endpoints`

---

### Tâche 13 : recherche publique ISBNdb (`RG-116` à `RG-118`)

**Files :**
- Create : `Application/Isbndb/Queries/SearchIsbndbEditions/*`
- Modify : `Api/Controllers/BibliographicReferenceController.cs`,
  `Api/Common/RateLimiting/RateLimiting.cs`, `RateLimitingPolicies.cs` (`IsbndbSearch` :
  fenêtre glissante d'une heure, `PublicSearchPerVisitorPerHour` permis, partition
  `ClientPartitionKey`)
- Test : `Application.tests/Isbndb/SearchIsbndbEditionsQueryHandlerTests.cs`,
  `Api.tests` (429 au-delà du plafond)

**Endpoint** `GET /catalog/reference/isbndb?q=`, `AllowAnonymous`, policy
`IsbndbSearch`, `CancellationToken` = `RequestAborted`. Le handler :
1. **Valide** la requête : 3 à 200 caractères après trim, sinon 400.
2. **Normalise** la clé : minuscules, sans accents, espaces compactés.
3. **Lit le cache** HybridCache `isbndb-search:{clé}`, `SearchCacheMinutes`. La fabrique
   n'est appelée que si l'entrée est absente. HybridCache n'annule la fabrique que si
   **tous** les appelants annulent (`RG-107`) : le documenter dans le code et le tester.
4. **Applique le plafond journalier**, dans la fabrique et avant l'appel : si
   `IsbndbDailyUsages[aujourd'hui, PublicSearch] >= PublicSearchDailyCredits`, la
   réponse est `items = []`, `unavailable = "limit"`, et elle n'est pas mise en cache.
5. **Appelle ISBNdb** : `SearchAsync(q, PublicSearch)`, 100 résultats, `language=fr`.
   Une réponse `QuotaExhausted` ou `Unavailable` donne `items = []`,
   `unavailable = "quota"` ou `"unavailable"`, et n'est pas mise en cache.
6. **Filtre** les résultats :
   - retire les ISBN-13 déjà présents dans `Books` et visibles au catalogue public
     (`PublicCatalogBookVisibility`) ;
   - garde uniquement les ISBN-13 valides ;
   - garde uniquement la langue `fra`.
7. **Construit la réponse** : `items[{isbn13, title, authors (« Prénom Nom »),
   publisher, publicationYear, coverUrl (image), binding (traduit)}]`, `total`,
   `truncated` (vrai si `total > 100`, pour l'invitation à préciser la recherche de
   `Q-07`). La réponse est toujours un **200** (`RG-118`), sauf un 429 du limiteur, que
   le front traite comme `limit`.

La déduplication avec les références BnF et Open Library affichées au-dessus se fait
côté front (tâche 14), qui les a déjà en mémoire.

**Tests d'abord :**
- même requête 2 fois → un seul appel client ;
- plafond atteint → `unavailable = "limit"` sans appel ;
- un ISBN du catalogue est filtré ;
- `"  Les MISÉRABLES "` et `"les miserables"` partagent la même clé.

Commit : `feat(catalog-api): on-demand cached ISBNdb edition search`

---

### Tâche 14 : Catalog public — « Voir d'autres éditions » (E4)

**Écran de référence :** `features/search/catalog-search-page.component.html`, section
`external-block` (« Pas encore dans la bourse aux livres »). Reprendre ses classes
`reference-card`, `reference-list`, `external-empty`, `external-loader` et
`reference-follow`.

**Files :**
- Modify : `features/search/catalog-search-page.component.{ts,html,scss}`,
  `core/catalog.models.ts`, `core/catalog-api.service.ts`
- Test : `catalog-search-page.component.spec.ts` (ajouts)

- [ ] L'encart ne s'affiche **que** sur la dernière page des références externes.
  - Condition : `externalResponse` est chargé, et soit
    `externalResponse.items.length < externalResponse.pageSize`, soit la liste est vide
    (« Aucun autre titre… »).
  - Libellés : `Vous n'avez pas trouvé l'édition que vous cherchez ?`, puis
    `D'autres éditions en français peuvent exister ailleurs.` et le bouton
    `Voir d'autres éditions`.
- [ ] Au clic, les éditions sont chargées et affichées dans un bloc sous l'encart.
  - Titre `Autres éditions proposées`, compteur `{n} éditions`.
  - Les ISBN déjà listés dans `externalResponse.items` sont retirés.
  - Chaque carte réutilise `reference-card` avec le seul bouton `Suivre cette édition`
    (`followReference(item, 'Edition')`). Pas de suivi d'œuvre pour ces résultats
    (`RG-117`).
- [ ] **États** (`RG-118`) :
  - chargement : `vpd-loader`, libellé `Recherche d'autres éditions…` ;
  - vide : `Aucune autre édition trouvée pour cette recherche.` ;
  - `truncated` : `Seules les 100 premières éditions sont affichées. Précisez votre recherche pour affiner.` ;
  - `limit`, `quota`, `unavailable` ou HTTP 429 :
    `Les propositions supplémentaires sont momentanément indisponibles. Réessayez plus tard.`,
    sans style d'erreur rouge.
- [ ] **Annulation** (`RG-107`, CA-10) : l'abonnement HTTP est annulé
  (`takeUntilDestroyed` ou `switchMap`) quand la recherche change, quand on change de
  page ou quand le composant est détruit. Le bloc se réinitialise à chaque nouvelle
  recherche. Pas de relance automatique : un nouveau clic est nécessaire.
- **Tests :**
  - bouton absent sur une page intermédiaire, présent sur la dernière et sur une liste
    vide (CA-09) ;
  - dédoublonnage ;
  - désabonnement à la destruction ;
  - message neutre sur un 429.
- Vérification visuelle à 390 et 1440 px contre la section existante : pas de
  défilement horizontal.

Commit : `feat(catalog): offer ISBNdb editions under the last external results page`

---

### Tâche 15 : Catalog admin — carte ISBNdb, « Livres hors ISBNdb », fiche (E1, E2, E3)

**Écrans de référence :**
- E1 : tuiles du tableau de bord (`overview`) ;
- E2 : section `isbn-queue` (« ISBN en cours d'analyse ») et `manual-requests-view` pour
  la structure en composant séparé ;
- E3 : bloc `metadata` et cartes latérales `side-card` de la fiche du catalogue.

**Files :**
- Create : `features/administration/isbndb/isbndb-status-card.component.*`,
  `isbndb-absent-view.component.*`
- Modify :
  - `core/catalog-admin-api.service.ts` (4 appels de la tâche 12) ;
  - `core/catalog.models.ts` ;
  - `core/catalog-administration-route.ts` (section `isbndb-absent`) ;
  - `features/administration/catalog-administration-page.component.{ts,html,scss}` :
    navigation « Le fonds de livres » → `Livres hors ISBNdb`, après
    `ISBN en cours d’analyse`, badge = `absentToComplete`, masqué à 0, préchargé comme
    les autres badges (PR #327). S'y ajoutent la carte E1 dans `overview`, puis la fiche
    E3.
- Test : specs des deux nouveaux composants, ajouts à
  `catalog-administration-page.component.spec.ts`

- [ ] **E1 — carte `ISBNdb`.**
  - Kicker `Source des fiches`. Pastille d'état `Opérationnel`,
    `Quota épuisé jusqu'à {HH:mm}` ou `Service indisponible`.
  - `Crédits restants aujourd'hui` : `{restant} / {limite}`, avec une jauge.
  - `Renouvellement cette nuit à {HH:mm} (heure de Paris)`, calculé depuis `resetsAt`
    avec `Europe/Paris`. Mention `Lu sur la réponse ISBNdb de {HH:mm}`.
  - `Consommés aujourd'hui · {n}` : Rattrapage nocturne, Scans, Recherche publique,
    Administration.
  - `En attente du rattrapage` : `{n} fiches`, puis `dont {x} en stock · {y} d'avant la migration`.
  - Lien `{n} livres hors ISBNdb à compléter →`.
  - Bandeau `Dernier rattrapage · {date} {début} → {fin}` : remplies, absentes,
    restantes, motif d'arrêt (`Quota épuisé — reprise la nuit prochaine`,
    `Service indisponible`, `Terminé`).
  - Ligne `Sujets sans genre correspondant` (10 premiers, avec leur compte). Masquée si
    vide.
- [ ] **E2 — `Livres hors ISBNdb`.**
  - Titre `Livres hors ISBNdb.` Texte :
    `ISBNdb ne connaît pas ces livres : leur fiche vient de la BnF, d'Open Library ou de Google Books et il manque souvent des informations. Complétez-les, puis marquez-les complétées pour relancer les recommandations.`
  - Filtres `À compléter · {n}` (par défaut) et `Complétées · {n}`. Recherche
    `ISBN, titre ou auteur…`.
  - Colonnes : couverture, livre, `Fiche de secours`, `Champs manquants` (pastilles :
    Résumé, Couverture, Genre, Éditeur, Année, Pages, Titre, Auteur), `En stock`, action
    `Compléter →`, qui ouvre la fiche du catalogue.
  - Pagination de 5 fiches, comme le catalogue admin.
  - Vide : `Aucun livre à compléter` / `Les livres qu'ISBNdb ne connaît pas apparaîtront ici.`
  - Mobile : cartes empilées.
- [ ] **E3 — fiche.**
  - Pastille de provenance sous la couverture : `ISBNdb`, `Secours · en attente du rattrapage`,
    `Hors ISBNdb · à compléter` ou `Hors ISBNdb · complétée`.
  - Formulaire existant étendu avec `Nombre de pages`, `Édition`, `Format` (liste :
    Broché, Relié, Poche, Numérique, Livre audio, Tout-carton), `Langue`, `Résumé`
    (zone de texte) et `Genres`. Les genres sont des pastilles à bascule parmi les 16
    genres du catalogue (`catalog-genres.ts`) et remplacent le champ texte `Genre`.
    Pour une fiche hors ISBNdb, un champ vide porte la mention `· manquant`.
  - Mention :
    `Un champ enregistré ici devient une correction manuelle et n'est plus écrasé.`
  - `Sujets reçus` en lecture seule.
  - Nouvelle carte latérale `Provenance de la fiche` : ISBNdb (`Trouvé` /
    `Livre inconnu` / `Pas encore interrogé`), `Source de secours`,
    `Dernière vérification`.
  - Bouton `Marquer comme complétée`, désactivé sans titre ni auteur, avec l'aide
    `Retire la fiche de « À compléter » et recalcule ses recommandations. Titre et auteur obligatoires.`
    Il devient `Rouvrir` une fois la fiche complétée.
  - Bouton `Revérifier dans ISBNdb`, avec l'aide
    `Gratuit si le livre reste inconnu · 1 crédit s'il est trouvé.` Il est désactivé
    avec `Quota épuisé jusqu'à {HH:mm}` pendant un épuisement.
  - Les prix ne sont affichés nulle part.
- Vérification visuelle à 390 et 1440 px contre les écrans de référence.

Commit : `feat(catalog-admin): ISBNdb quota card, absent books list and completion`

---

### Tâche 16 : Scanette — « Autre édition déjà à la bourse » (E5, `RG-119` à `RG-122`, `Q-01`)

**Écran de référence :** `scanner/scanner.component.html`, `tri-result` : `verdict-card`,
`book-card`, `book-facts`. Couleurs : variables `--scan-*` de `src/styles.scss`.

**Files :**
- Modify :
  - `offline/scan-offline.model.ts` (`ScanCatalogBook` + `workKey`, `publisher`,
    `publicationYear`, `otherIsbns`, tous optionnels) ;
  - `scanner/book-metadata.model.ts` (`workKey`) ;
  - `offline/scan-sync.service.ts` et `scan-local-store.service.ts` (stockage des
    nouveaux champs) ;
  - `scan-local-store-migration.ts` (une resynchronisation complète forcée une fois) ;
  - `offline/scan-verdict.service.ts` ;
  - `scanner/scanner.component.{ts,html}`, `src/styles.scss`.
- Test : `scan-verdict.service.spec.ts`, `scanner.component.spec.ts`,
  `scan-local-store-migration.spec.ts` (ajouts)

- [ ] `ScanVerdictService.siblings(book | metadata, catalog)` renvoie les autres
  éditions, avec leur force de lien.
  - **Lien fort** : même `workId` non nul, ou l'ISBN de l'une figure dans les
    `otherIsbns` de l'autre.
  - **Lien faible** : même `workKey` non nul sans lien fort.
- [ ] `calculate` :
  - `totalKnownQuantity` de `TooMany` = ISBN + éditions **fortement** liées (`Q-01`,
    `RG-121`) ;
  - les liens faibles n'influencent jamais le verdict ;
  - priorité `RG-15` inchangée ;
  - `LocalVerdict` gagne `otherEditions: {isbn13, publisher, publicationYear, quantity,
    strong}[]`, limité aux éditions ayant `qtyAvailable + qtyAnnounced > 0`.
- [ ] **Encart sous `book-card`**, affiché si `otherEditions` n'est pas vide :
  - titre `Autre édition déjà à la bourse` ;
  - une ligne par édition : `{éditeur || 'Édition inconnue'} · {année}` et
    `{n} exemplaire(s)` ;
  - pour un lien faible seul : `Même titre et même auteur — à vérifier`.
  - Le résumé du verdict `TooMany` dû à l'œuvre devient
    `Déjà {n} exemplaires de cette œuvre · toutes éditions`.
- [ ] **Hors ligne :**
  - calcul sur la copie locale (`RG-122`) ;
  - ISBN inconnu localement et métadonnées non obtenues → pas d'encart ;
  - métadonnées obtenues plus tard → l'encart apparaît sans changer de verdict déjà
    affiché si le bénévole a déjà décidé.
- **Tests :**
  - deux éditions de *Les Misérables* avec le même `workId` → encart et total d'œuvre
    (CA-13) ;
  - même `workKey` seulement → encart « à vérifier » et verdict par ISBN ;
  - ancien cache sans `workKey` → aucune erreur.
- Vérification visuelle à 390 px.

Commit : `feat(scan): flag other editions of the same work already in stock`

---

### Tâche 17 : documentation de la spec dans le module

**Files :** Modify `docs/bourse-aux-livres/06-regles-metier.md`, `05-administration.md`
(§4, file de travail « Livres hors ISBNdb »), `03-parcours-benevole-scan.md` (§3.3,
encart). Ne pas modifier `docs/features/isbndb-migration/*`.

- [ ] Dans `06-regles-metier.md`, ajouter une section « Source ISBNdb » qui renvoie à
  `RG-100` à `RG-129` (même forme que la section « Demandes manuelles », ligne ~567).
  Ajouter une note sous `RG-10` : seuil compté sur l'œuvre si le lien est fiable (`RG-121`).

Commit : `docs(isbndb-migration): reference ISBNdb rules from the book fair module`

---

### Tâche 18 : infra — secret et réglages (sans déploiement)

**Files :** Modify `infra/main.bicep`, `infra/modules/KeyVault/appSecrets.module.bicep`,
`infra/parameters/main.dev.bicepparam`, `.github/workflows/infra-deploy.yml`

Reprendre **exactement** le chemin de `googleBooksApiKey` :
- `@secure() param isbndbApiKey string = ''` ;
- secret Key Vault `isbndb-api-key` dans `appSecrets` ;
- `secretUris` ;
- secret de Container App sur l'API **et** le Worker (blocs ~1279 et ~1635) ;
- variable d'environnement `Isbndb__ApiKey` (`secretRef`), conditionnelle si vide ;
- `readEnvironmentVariable('ISBNDB_API_KEY', '')` ;
- `ISBNDB_API_KEY: ${{ secrets.ISBNDB_API_KEY }}` aux deux endroits du workflow.

Ajouter aussi `Isbndb__RequestsPerSecond` (`'1'`) et `Isbndb__CatchUpSchedule`
(`'0 30 2 * * *'`) au Worker et à l'API.

- [ ] `az bicep build --file infra/main.bicep` passe, sans aucun déploiement.

Commit : `feat(infra): wire ISBNdb API key and settings to API and worker`

---

### Tâche 19 : validation complète et PR

- [ ] Suite complète (skill §5) : build, tests backend, `npm test` et `npm run build`
  dans Catalog et Scan, Bicep, `graphify update .`.
- [ ] Contrôle manuel local, avec la clé dans les secrets utilisateur ou une variable
  d'environnement, **jamais** dans un fichier suivi :
  - scanner un ISBN connu (9782070409228) donne une fiche ISBNdb ;
  - un ISBN inconnu donne une fiche hors ISBNdb ;
  - la carte E1 affiche le quota lu dans les en-têtes.
  Pas plus de 20 crédits.
- [ ] Titre de la PR : `feat(books): make ISBNdb the primary book notice source`.
- [ ] Section « Risks and follow-up » :
  - créer le secret GitHub `ISBNDB_API_KEY`, puis lancer `Infra - deploy` avant
    d'activer ;
  - au premier rattrapage, toutes les fiches existantes sont candidates : prévoir
    plusieurs nuits, un crédit par livre trouvé ;
  - le timer est en UTC (3 h 30 ou 4 h 30 à Paris) ;
  - l'état du quota peut retarder d'une requête ;
  - la table des genres est à enrichir avec le rapport E1 ;
  - sans maquettes, les écrans sont alignés sur les écrans de référence cités.

## Hors plan

- La fiche publique (`Q-06`) ne change pas.
- Aucune édition admin de la table des genres (`Q-05`).
- Pas de page suivante pour la recherche ISBNdb publique (`Q-07`).
