# 03 — Couche application et API

## 1. Organisation

Suivre `.github/skills/cqrs-feature/SKILL.md` et le découpage de
`Application/Books/` :

```
Application/RareBooks/
├── Commands/
│   ├── CreateRareBook/
│   ├── UpdateRareBook/
│   ├── PublishRareBook/
│   ├── UnpublishRareBook/
│   ├── DeleteRareBook/
│   ├── MarkRareBookSold/
│   ├── AddRareBookPhoto/
│   ├── ReorderRareBookPhotos/
│   ├── UpdateRareBookPhotoCaption/
│   └── DeleteRareBookPhoto/
├── Queries/
│   ├── GetPublicRareBooks/
│   ├── GetPublicRareBookBySlug/
│   ├── GetAdminRareBooks/
│   ├── GetAdminRareBook/
│   └── GetRareBookMigrationQueue/
└── Common/
    ├── RareBookResults.cs
    └── RareBookProjector.cs
```

Contrats dans `Contracts/RareBooks/{Requests,Responses}`.

## 2. Commandes

| Commande | Entrées notables | Règles |
|---|---|---|
| `CreateRareBookCommand` | Tous les champs d'identité + `Isbn13?`, `UserId` | Crée en `Draft`. Si `Isbn13` fourni : refuser si une fiche rare le porte déjà. Calcule le slug |
| `UpdateRareBookCommand` | Champs modifiables, `RowVersion` | Concurrence optimiste. Le slug **ne bouge pas** |
| `PublishRareBookCommand` | `RareBookId` | Valide les invariants de publication (`02 §1`). Retourne les avertissements (ex. aucune photo) sans bloquer |
| `UnpublishRareBookCommand` | `RareBookId` | Retire de la vitrine et du signalement caisse |
| `DeleteRareBookCommand` | `RareBookId` | **Supprime d'abord les blobs**, puis la ligne. La fiche `Book` liée et son historique restent intacts |
| `MarkRareBookSoldCommand` | `RareBookId`, `AssoEventsId?`, `occurredAt`, `UserId` | Idempotent. Déclenche les alertes (`D6`). Appelée par la caisse **et** manuellement depuis l'admin |
| `AddRareBookPhotoCommand` | `RareBookId`, flux, nom, type MIME, `UserId` | Valide type et taille (≤ 8 Mo, JPEG/WebP/PNG). Téléverse puis ajoute en dernière position |
| `ReorderRareBookPhotosCommand` | `RareBookId`, liste ordonnée d'ids | Exige une permutation exacte |
| `DeleteRareBookPhotoCommand` | `RareBookPhotoId` | Supprime le blob puis la ligne, puis **recompacte les positions** |

Toutes les commandes enregistrent `UpdatedBy` / `UpdatedAt` depuis l'utilisateur appelant
et `IDateTimeProvider`, comme le reste du dépôt.

## 3. Requêtes

| Requête | Usage | Notes |
|---|---|---|
| `GetPublicRareBooksQuery` | `/livres-rares` | Filtre `Status = Published`. Paramètres : `shelf`, `includeSold`, `sort=price-desc\|price-asc\|recent`, pagination. Les vendus sont **conservés et affichés barrés** (maquette `LivresRares`), donc `includeSold` par défaut à `true` |
| `GetPublicRareBookBySlugQuery` | `/livres-rares/:slug` | 404 si `Draft`. Renvoie les photos ordonnées et les 3 autres fiches de la bourse |
| `GetAdminRareBooksQuery` | Liste admin | Filtres : recherche texte, statut, disponibilité, avec/sans ISBN, fourchette de prix, **sans photo** |
| `GetAdminRareBookQuery` | Fiche admin | Inclut la traçabilité |
| `GetRareBookMigrationQueueQuery` | File de migration | Fiches en `Draft` issues de la reprise (`02 §6`) |

## 4. Endpoints

Nouveau fichier
`src/Backend/Vole_Papillon_Damour.Api/Controllers/RareBookController.cs`, méthode
d'extension `UseRareBookController`, enregistrée dans `Program.cs` à côté des autres.
Style *minimal API*, comme `BookAdministrationController.cs`.

### Public — anonyme

| Méthode | Route | Notes |
|---|---|---|
| `GET` | `/catalog/rare-books` | Liste publique |
| `GET` | `/catalog/rare-books/{slug}` | Fiche publique |

À soumettre au même *rate limiting* que le reste du catalogue public
(`Api/Common/RateLimiting/RateLimitingPolicies.cs`).

### Gestion — politique `RareBooks`

| Méthode | Route |
|---|---|
| `GET` | `/rare-books/admin` |
| `GET` | `/rare-books/admin/{id}` |
| `GET` | `/rare-books/admin/migration-queue` |
| `POST` | `/rare-books/admin` |
| `PUT` | `/rare-books/admin/{id}` |
| `POST` | `/rare-books/admin/{id}/publish` |
| `POST` | `/rare-books/admin/{id}/unpublish` |
| `POST` | `/rare-books/admin/{id}/sold` |
| `DELETE` | `/rare-books/admin/{id}` |
| `POST` | `/rare-books/admin/{id}/photos` |
| `PUT` | `/rare-books/admin/{id}/photos/order` |
| `PATCH` | `/rare-books/admin/photos/{photoId}` |
| `DELETE` | `/rare-books/admin/photos/{photoId}` |

La politique `RareBooks` accepte **`LivresRares` ou `Administration`** — voir
[`04`](04-role-livres-rares.md).

Le téléversement de photo est le seul endpoint `multipart/form-data` du module.
Précédent dans le dépôt : `ActualityController.cs` et `ProductController.cs`, qui
passent un `IFormFile` au handler via `IBlobService`.

## 5. Préremplissage bibliographique

**Ne rien écrire de neuf.** L'endpoint existe déjà et convient :

```
GET /catalog/reference/search?q={isbn ou titre}&page=&pageSize=
```

`Api/Controllers/BibliographicReferenceController.cs` → `SearchBibliographicReferencesQuery`
→ `IBibliographicSearchService` (`CachedBibliographicSearchService`, qui interroge BnF
SRU, OpenLibrary et Google Books, avec cache et limitation de débit).

Il retourne `BookReferenceSearchItemResponse` : `Isbn13`, `WorkId`, `Title`, `Authors`,
`Publisher`, `PublicationYear`, `CoverUrl`, `Source`. C'est exactement le jeu de champs
d'identité de `RareBook`.

Le formulaire de création rare consomme cet endpoint **exactement comme le faisait
l'ancien onglet Inventaire** (carte « candidat » à confirmer ou corriger). Le code de
cette carte a été supprimé avec l'onglet par le PR #195 : le récupérer avec
`git show 4190ad5:src/Catalog/src/app/features/administration/catalog-administration-page.component.html`
plutôt que de le réécrire.

⚠️ La couverture renvoyée par ce service est une **couverture d'éditeur**, pas une photo
de l'exemplaire. Elle peut servir d'illustration provisoire d'une fiche en brouillon,
mais une fiche publiée doit montrer les photos du bénévole. Ne pas mélanger les deux
dans `RareBookPhoto`.

## 6. Stockage des photos

`IBlobService`
(`Application/Common/Interfaces/Services/IBlobService.cs`) gagne :

```csharp
Task<Uri> UploadRareBookPhotoAsync(string fileName, Stream stream);
```

et `BlobSettings` un conteneur `BlobContainerRareBookPhotosClient`
(nom : `livres-rares`, conforme à la maquette admin).

**Avant cela, corriger `DeleteFileAsync`** (`01 §6.1`) : la signature actuelle ne prend
qu'un nom de fichier et résout toujours le conteneur des actualités. La rendre
consciente du conteneur, par exemple :

```csharp
Task<string> DeleteFileAsync(BlobContainer container, string blobNameOrUri);
```

C'est un changement de signature qui touche les appelants existants (actualités,
produits, événements, lots). Le faire dans un commit séparé, avec ses tests, avant
d'ajouter le conteneur rare — sinon la suppression d'une photo rare échouera en
silence.

Nommage des blobs : `{rareBookId}/{photoId}.{ext}`. Le préfixe par fiche rend la
suppression en masse triviale.

Déclarer le conteneur dans l'infrastructure : `appsettings.json`,
`appsettings.Development.json`, `AppHost/Program.cs` et le Bicep sous `infra/`.

## 7. Alertes « il est parti »

Réutiliser l'existant, décrit en `D6` :

- `WatchlistAggregate` / `WatchlistItem` : ajouter une cible `RareBookId?` à côté de
  l'`Isbn13` actuel. Vérifier la contrainte d'unicité ajoutée par la migration
  `20260904213549_AddWatchlistItemUniqueness`, qui porte aujourd'hui sur l'ISBN.
- `IBookAlertOutbox` + `IBookAlertEmailSender` : nouveau motif d'alerte
  « livre rare vendu ». Gabarit d'e-mail distinct — le message est l'inverse de
  l'actuel, il annonce une **disparition**.
- `MarkRareBookSoldCommandHandler` alimente l'outbox ; le worker existant
  (`Vole_Papillon_Damour.Worker`) l'expédie.
- Respecter `AlertCooldownDays` et `WatchlistMaxItems` d'`AssociationSettings`.

## 8. Tests attendus

| Couche | Projet | Couverture minimale |
|---|---|---|
| Domaine | `Domain.tests/RareBookAggregateTests/` | Les invariants de `02 §1`, l'ordre des photos, l'idempotence de `MarkSold` |
| Application | `Application.tests/RareBooks/` | Un test par handler, cas nominal + refus. Unicité ISBN, concurrence optimiste, suppression des blobs avant la ligne |
| Application | `Application.tests/Books/` | **Régression** : les neuf points de `02 §5` continuent de produire les mêmes résultats via la nouvelle jointure |
| API | `Api.tests/EndpointAuthorizationTests.cs` | Chaque nouvel endpoint, avec et sans le rôle |
| Infrastructure | `Infrastructure.tests/` | Les configurations EF, les index, la migration de reprise |

Écrire le test avant le code de production, sans exception : c'est la règle du dépôt
(`.github/skills/tdd-workflow/SKILL.md`).
