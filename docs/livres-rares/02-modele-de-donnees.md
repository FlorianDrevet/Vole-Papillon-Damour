# 02 — Modèle de données

## 1. Nouvel agrégat `RareBook`

Emplacement : `src/Backend/Vole_Papillon_Damour.Domain/RareBookAggregate/`.

Il suit les conventions de `BookAggregate` : agrégat scellé, constructeur privé,
fabrique statique `Create`, constructeur public sans paramètre pour EF, mutations par
méthodes qui retournent `bool changed`, et `DomainTime.RequireUtc` sur chaque instant.

```
RareBookAggregate/
├── RareBook.cs
├── Entities/
│   └── RareBookPhoto.cs
└── ValueObjects/
    ├── RareBookId.cs
    ├── RareBookPhotoId.cs
    ├── RareBookCondition.cs
    ├── RareBookShelf.cs
    ├── RareBookStatus.cs
    └── RareBookSlug.cs
```

### `RareBook : AggregateRoot<RareBookId>`

| Propriété | Type | Notes |
|---|---|---|
| `Id` / `RareBookId` | `RareBookId` | `Guid` encapsulé, `CreateUnique()` comme `ProductId` |
| `Slug` | `RareBookSlug` | Dérivé du titre + auteur + année, unique. Voir §3 |
| `Isbn13` | `Isbn13?` | **Facultatif.** `Isbn13` est un `readonly record struct` : utiliser `Isbn13?` |
| `Title` | `string` | Obligatoire, ≤ 300 |
| `AuthorMention` | `string?` | « Auteur ou mention » dans la maquette : accepte « Illustrées par Gustave Doré », ≤ 300 |
| `Publisher` | `string?` | ≤ 200 |
| `PublicationYear` | `int?` | 1 à 9999, comme `Book` |
| `Shelf` | `RareBookShelf` | « Rayon d'affichage ». Voir `Q4` |
| `Price` | `decimal` | Prix ferme, en euros. `decimal(10,2)`. **Pas `double`** — le `Product.Price` du dépôt est un `double`, ne pas répliquer cette erreur sur de la monnaie |
| `Condition` | `RareBookCondition` | 4 valeurs, voir §2 |
| `PublicDescription` | `string?` | ≤ 1200 (compteur visible dans la maquette : « 412 / 1200 ») |
| `Binding` | `string?` | « Reliure », ex. « Demi-cuir à coins », ≤ 120 |
| `Dimensions` | `string?` | « Format », texte libre, ex. « 32 × 44 cm », ≤ 60 |
| `PageCount` | `int?` | |
| `ShelfLocation` | `string?` | « Emplacement », ex. « Table rares · caisse 2 », ≤ 120 |
| `Status` | `RareBookStatus` | `Draft` \| `Published` |
| `IsSold` | `bool` | « Encore disponible » inversé |
| `SoldAt` | `DateTime?` | UTC |
| `SoldAtFairId` | `AssoEventsId?` | La bourse qui a encaissé, pour `D5` |
| `PriceSetBy` | `string?` | « Prix fixé par », ex. « Conseil du 5 mars », ≤ 120 |
| `CreatedAt` / `CreatedBy` | `DateTime` / `UserId` | Traçabilité affichée dans la maquette |
| `UpdatedAt` / `UpdatedBy` | `DateTime` / `UserId` | |
| `RowVersion` | `byte[]` | Concurrence optimiste, comme `Book` |
| `Photos` | `IReadOnlyList<RareBookPhoto>` | Collection privée exposée en lecture seule |

**Invariants à couvrir par des tests de domaine :**

- Un `RareBook` publié a un titre non vide et un prix strictement positif.
- Un `RareBook` publié a au moins une photo. *(À confirmer — la maquette admin filtre
  « sans photo » comme une alerte, ce qui suggère que c'est possible mais indésirable.
  Retenu : non bloquant à la publication, remonté comme avertissement.)*
- Le prix ne peut pas être négatif ; `0` est refusé sur une fiche publiée.
- `MarkSold` est idempotent et exige une fiche publiée.
- `Publish` sur une fiche déjà publiée ne change rien et retourne `false`.
- Réordonner les photos exige une permutation exacte des identifiants existants — ni
  ajout, ni oubli.

### `RareBookPhoto : Entity<RareBookPhotoId>`

| Propriété | Type | Notes |
|---|---|---|
| `Id` | `RareBookPhotoId` | |
| `RareBookId` | `RareBookId` | |
| `BlobUri` | `Uri` | URL absolue HTTPS retournée par `IBlobService` |
| `BlobName` | `string` | Nom du blob, **nécessaire pour la suppression** — voir `01 §6.1` |
| `Caption` | `string?` | « Dos », « Page de titre », « Gravure », « Défaut ». ≤ 80 |
| `Position` | `int` | 0-indexé, contigu. `Position == 0` ⇒ vignette |
| `ContentType` | `string` | `image/jpeg` ou `image/webp` |
| `SizeBytes` | `long` | La maquette affiche « 5 photos · 14,2 Mo » |
| `UploadedAt` / `UploadedBy` | `DateTime` / `UserId` | |

Pas de propriété `IsCover` : la vignette est `Position == 0`. Un seul concept d'ordre,
pas deux sources de vérité.

## 2. Value objects

### `RareBookCondition`

Quatre valeurs, **reprises littéralement des maquettes** — ce n'est pas l'échelle eBay à
six niveaux évoquée au départ :

| Valeur | Libellé affiché |
|---|---|
| `AsNew` | Comme neuf |
| `GoodWithFlaws` | Bon, défauts signalés |
| `Worn` | Usé |
| `Damaged` | Abîmé |

Suivre le patron `EnumValueObject` (`Domain/Common/Models/EnumValueObject.cs`) déjà
utilisé par `ProductSection` et `EventsType`.

### `RareBookShelf`

Quatre valeurs par défaut issues des maquettes : `Éditions anciennes`, `Illustrés`,
`Beaux-arts`, `Régionalisme`. Voir `Q4` : si la liste doit être éditable, la porter dans
`AssociationSettings` et garder ici une simple `string` validée.

### `RareBookStatus`

`Draft` | `Published`. Voir `D8`.

## 3. Slug

`RareBookSlug` : minuscules, sans accents, mots séparés par `-`, dérivé de
`{titre}-{auteur}-{année}` puis tronqué à 120 caractères. En cas de collision, suffixer
`-2`, `-3`… Le slug est **calculé à la création et figé** : le changer casserait les
liens partagés et les e-mails d'alerte déjà envoyés.

Réutiliser la logique de slug du catalogue si elle existe — vérifier
`src/Catalog/src/app/core/` et le projecteur public
(`Application/Books/Common/PublicCatalogProjector.cs`) avant d'en écrire une nouvelle.

## 4. Persistance

- `IProjectDbContext` (`Application/Common/Interfaces/Persistence/IProjectDbContext.cs`)
  gagne `DbSet<RareBook> RareBooks` et `DbSet<RareBookPhoto> RareBookPhotos`.
- Deux configurations dans
  `Infrastructure/Persistence/Configurations/` : `RareBookConfiguration.cs` et
  `RareBookPhotoConfiguration.cs`, sur le modèle de `BookConfiguration.cs` et
  `BookAnnouncementConfiguration.cs`.
- Conversions de value objects : suivre `BookPersistenceConversions.cs`.
- Index requis :
  - unique sur `Slug` ;
  - unique **filtré** sur `Isbn13` quand il est non nul — une fiche catalogue ne peut
    pas porter deux fiches rares ;
  - composite `(Status, IsSold, Price)` pour la liste publique triée par prix ;
  - `(RareBookId, Position)` unique sur les photos.
- Suppression d'une fiche : `Cascade` sur les photos. **La suppression en base ne
  supprime pas les blobs** : le handler doit les supprimer explicitement avant.

### Migration

Une seule migration, nommée `AddRareBooks`, dans
`Infrastructure/Migrations/`. Elle crée les deux tables et **supprime la colonne
`Books.IsRare`** (voir §5). Vérifier ensuite que le modèle EF n'a plus de changement en
attente, comme le fait le reste du dépôt.

## 5. Retrait de `Book.IsRare` — les neuf points d'appel

C'est la partie la plus risquée du module. Chacun de ces points lit `IsRare` aujourd'hui
et doit être rebasculé sur « existe-t-il une fiche `RareBook` publiée liée à cet
ISBN ? ».

| # | Fichier | Rôle | Remplacement |
|---|---|---|---|
| 1 | `Application/Books/Queries/SearchCatalog/SearchCatalogQueryHandler.cs:230` | Filtre `rare=true` de la recherche publique | Jointure sur `RareBooks` publiées non vendues |
| 2 | `Application/Books/Common/PublicCatalogProjector.cs:61` | Projection publique | Idem |
| 3 | `Application/Books/Queries/GetCatalogDelta/GetCatalogDeltaQueryHandler.cs` (2 points + le record ligne 401) | **Delta synchronisé par la scanette hors ligne** | Voir [`07`](07-scanette.md) : le delta doit désormais embarquer les fiches rares elles-mêmes, prix et vignette compris |
| 4 | `Application/Books/Queries/Admin/AdminQueryProjection.cs:61` | Projection admin | Jointure |
| 5 | `Application/Books/Queries/Admin/GetAdminBooksQueryHandler.cs:36` | Filtre admin `rare` | Jointure |
| 6 | `Application/Books/Queries/Admin/GetCatalogAdminOverviewQueryHandler.cs:67` | Compteur du tableau de bord | Compte de fiches rares publiées |
| 7 | `Application/Books/Queries/GetVolunteerStatistics/GetVolunteerStatisticsQueryHandler.cs:192` | « Livres rares repérés » par bénévole | Compte de fiches **créées par** ce bénévole |
| 8 | `Application/Books/Commands/ScanBook/ScanBookCommandHandler.cs:340` | Verdict « Rare » au tri | Présence d'une fiche liée |
| 9 | `Application/Books/Commands/RegisterSale/RegisterSaleCommandHandler.cs:145,174` | Signalement en caisse | Voir [`07`](07-scanette.md) : porte désormais le prix |

À supprimer entièrement :

- `Application/Books/Commands/BookFlags/MarkBookRareCommandHandler.cs` et la partie
  `IsRare` de `BookFlagCommands.cs` / `BookFlagResult.cs` ;
- l'endpoint `POST /books/admin/books/{isbn13}/rare`
  (`Api/Controllers/BookAdministrationController.cs:268-296`) ;
- l'interrupteur « Livre rare » de la fiche catalogue admin
  (`catalog-administration-page.component.html:410`), remplacé par une action
  « Créer la fiche livre rare → ».

`HideBookCommandHandler` et `IsHiddenFromCatalog` **ne sont pas concernés** : ils restent
tels quels.

## 6. Reprise des données existantes

La migration supprime `Books.IsRare`. Les livres qui le portaient à `true` doivent être
retrouvables, sinon le travail de marquage déjà fait par les bénévoles est perdu.

Procédure, dans la migration `AddRareBooks` :

1. Créer les tables.
2. Pour chaque `Book` où `IsRare = 1`, insérer un `RareBook` en statut **`Draft`**,
   reprenant `Isbn13`, `Title`, `Authors` → `AuthorMention`, `Publisher`,
   `PublicationYear`. `Price = 0`, `Condition = GoodWithFlaws`, aucune photo,
   `CreatedBy` = un identifiant système documenté.
3. Supprimer la colonne `IsRare`.

Ces fiches alimentent la **file de migration** du portail admin décrite en
[`06`](06-portail-administration.md) : « *N* livres étaient marqués rares sous l'ancien
système et n'ont pas encore de fiche complète. Tant qu'ils n'en ont pas, ils
n'apparaissent plus dans la section publique. »

C'est volontaire et il faut le dire à l'association avant le déploiement : **la section
publique « Livres rares » sera vide au lendemain de la migration**, jusqu'à ce que les
fiches soient complétées et publiées.

Prévoir un `Down` qui restaure la colonne et repositionne `IsRare = 1` pour les ISBN
ayant une fiche.
