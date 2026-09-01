# 02 — Modèle de données

Traduction du modèle conceptuel de `../02-glossaire-et-cycle-de-vie.md` §4 vers les
conventions du dépôt : agrégats `Domain`, configurations EF Core, migrations.

## 1. Agrégats

Quatre nouveaux agrégats dans `Vole_Papillon_Damour.Domain`, suivant la convention
existante — `AggregateRoot<TId>`, identifiants fortement typés, dossier `XxxAggregate/`
avec `Entities/` et `ValueObjects/`.

| Agrégat | Racine | Rôle |
|---|---|---|
| `BookAggregate` | `Book` | La fiche livre, clé métier = ISBN-13 |
| `ScanSessionAggregate` | `ScanSession` | La session de tri, son mode, son cycle de vie |
| `BookMovementAggregate` | `BookMovement` | Le mouvement, source de vérité des quantités |
| `WatchlistAggregate` | `Member` | Le membre du site et sa liste de recherche |

`OutboxMessage` n'est pas un agrégat métier mais une table d'infrastructure — voir
[`06-traitements-differes.md`](06-traitements-differes.md).

### Pourquoi le mouvement est un agrégat distinct

Il serait tentant d'en faire une collection dans `Book`. À proscrire : charger une
fiche chargerait alors des milliers de lignes, et les mouvements sont consultés par
bourse et par session bien plus souvent que par livre. Ils référencent la fiche par
identifiant, ils n'y sont pas contenus.

## 2. Tables

### `Books`

```
Isbn13                char(13)      PK
WorkId                nvarchar(64)  NULL   -- RG-46, peut rester vide
Title                 nvarchar(500) NULL
Authors               nvarchar(500) NULL
Publisher             nvarchar(200) NULL
PublicationYear       int           NULL
PhysicalFormat        nvarchar(50)  NULL
Language              nvarchar(10)  NULL
Genre                 nvarchar(100) NULL

QuantityAvailable     int           NOT NULL DEFAULT 0
SalesCount            int           NOT NULL DEFAULT 0
RejectionCount        int           NOT NULL DEFAULT 0

IsRare                bit           NOT NULL DEFAULT 0   -- marquage manuel, RG-50
IsHiddenFromCatalog   bit           NOT NULL DEFAULT 0
CoverBlobRef          nvarchar(200) NULL

MetadataStatus        tinyint       NOT NULL  -- Pending|Resolved|NotFound|Manual
MetadataSource        tinyint       NULL      -- Bnf|OpenLibrary|Manual
MetadataFetchedAt     datetime2     NULL      -- exigé par la Licence Ouverte
ResolveAttempts       int           NOT NULL DEFAULT 0
LastAttemptAt         datetime2     NULL
RawPayload            nvarchar(max) NULL      -- notice source telle quelle, DT-02
ManuallyEditedFields  nvarchar(max) NULL      -- JSON : champs figés, RG-05

FirstSeenAt           datetime2     NOT NULL
LastAvailableAt       datetime2     NULL
UpdatedAt             datetime2     NOT NULL  -- filigrane de synchronisation
RowVersion            rowversion              -- concurrence optimiste
```

**Aucune colonne de prix** : `RG-50`, les prix sont décidés au comptoir.

**`QuantityAnnounced` n'est pas ici** : la quantité annoncée est portée par bourse,
dans la table suivante. `RG-10` somme disponible + annoncé, toutes bourses confondues.

Index : `WorkId` (rapprochement `RG-46`), `MetadataStatus` + `LastAttemptAt` (file de
rattrapage), `UpdatedAt` (synchronisation delta), index plein texte sur
`Title` + `Authors` (`DT-07`).

### `BookAnnouncements`

```
Id             uniqueidentifier PK
Isbn13         char(13)         FK Books
AssoEventsId   uniqueidentifier NULL   -- NULL = annonce sans date, RG-24
Quantity       int              NOT NULL
Status         tinyint          NOT NULL -- Announced|Released|Cancelled
CreatedAt      datetime2        NOT NULL
ReleasedAt     datetime2        NULL
ScanSessionId  uniqueidentifier FK
```

Cette table porte `RG-22`, `RG-23`, `RG-24` et `RG-38`. La bascule est un changement de
statut plus un mouvement, sur les lignes dont la bourse a commencé.

`AssoEventsId` nullable est ce qui rend `RG-24` possible : une annonce sans date se
rattache plus tard.

Index : `AssoEventsId` + `Status` (balayage de bascule) ; index filtré sur
`AssoEventsId IS NULL` (file « annonces sans date » de `05` §4).

### `BookMovements`

```
Id             uniqueidentifier PK
Isbn13         char(13)         FK Books
Type           tinyint          NOT NULL
               -- EntreeAnnonce|EntreeDirecte|Bascule|Vente|Refus|Correction|Retrait
Quantity       int              NOT NULL     -- signé
OccurredAt     datetime2        NOT NULL
ScanSessionId  uniqueidentifier FK NULL      -- NULL pour caisse et corrections
VolunteerId    uniqueidentifier FK NULL      -- RG-41
AssoEventsId   uniqueidentifier FK NULL      -- RG-33
Note           nvarchar(500)    NULL         -- motif d'une correction
```

**Table en ajout seul.** On n'y met jamais à jour, on n'y supprime jamais : une
annulation (`RG-17`, `RG-49`) produit un mouvement inverse. C'est l'historique
comptable exigé par `ENF-22`, et c'est ce qui rend `ENF-06` trivial — deux appareils
hors ligne produisent deux lignes, jamais un conflit.

Index : `Isbn13` + `OccurredAt` ; `AssoEventsId` + `Type` (statistiques par bourse) ;
`ScanSessionId` (reprise en bloc `RG-25`).

### `ScanSessions`

```
Id                  uniqueidentifier PK
VolunteerId         uniqueidentifier FK
Mode                tinyint          NOT NULL -- DisponibleMaintenant|ProchaineBourse
TargetAssoEventsId  uniqueidentifier NULL     -- bourse visée, NULL si RG-24
StartedAt           datetime2        NOT NULL
LastScanAt          datetime2        NOT NULL -- pilote la clôture par inactivité
EndedAt             datetime2        NULL
CloseReason         tinyint          NULL     -- Manuelle|Inactivite|Deconnexion|JetonExpire
Status              tinyint          NOT NULL -- EnCours|Terminee|Reprise
ScannedCount        int              NOT NULL DEFAULT 0
KeptCount           int              NOT NULL DEFAULT 0
RejectedCount       int              NOT NULL DEFAULT 0
```

`LastScanAt` porte `RG-43` : le balayage d'inactivité cherche les sessions `EnCours`
dont `LastScanAt` dépasse le seuil.

Contrainte : **une seule session ouverte par bénévole** — index unique filtré sur
`Status = EnCours`.

### `Members` et `MemberWatchlistItems`

Identité déléguée à Entra External ID (`ENF-16`) : **aucun mot de passe stocké**.

```
Members
  Id                uniqueidentifier PK
  ExternalSubjectId nvarchar(200)    NOT NULL UNIQUE  -- claim sub
  Email             nvarchar(320)    NOT NULL
  Status            tinyint          NOT NULL -- Actif|Bloque|AlertesSuspendues
  BounceCount       int              NOT NULL DEFAULT 0   -- RG-31
  CreatedAt, LastSeenAt   datetime2

MemberWatchlistItems
  Id        uniqueidentifier PK
  MemberId  uniqueidentifier FK Members
  Scope     tinyint          NOT NULL -- Oeuvre|Edition, RG-46
  WorkId    nvarchar(64)     NULL     -- si Scope = Oeuvre
  Isbn13    char(13)         NULL     -- si Scope = Edition
  AddedAt   datetime2        NOT NULL
```

Contrainte : exactement l'un des deux champs cibles renseigné, selon `Scope`.

**Aucune clé étrangère vers `Books`** : `RG-47` permet de suivre un livre que
l'association n'a jamais reçu, donc sans fiche. C'est délibéré, et c'est le point que
l'implémentation ratera si on ne le lit pas.

Index : `WorkId` et `Isbn13` — ce sont eux qui rendent `RG-13` instantané au scan.

`ENF-13` (suppression après trois ans d'inactivité) s'appuie sur `LastSeenAt`.
`ENF-12` impose que la suppression efface aussi la liste et l'historique d'alertes :
cascade explicite.

### `AssoEventsRevenue`

```
AssoEventsId  uniqueidentifier PK, FK
AmountEuros   decimal(10,2)    NOT NULL
EnteredBy     uniqueidentifier FK
EnteredAt     datetime2        NOT NULL
```

Un seul montant par bourse, saisi à la main (`RG-51`). Table séparée plutôt qu'une
colonne ajoutée à `AssoEvents` : le domaine `Books` ne modifie jamais l'agrégat
événement — frontière posée en [`00-vue-densemble.md`](00-vue-densemble.md) §6.

## 3. Ce qui n'est pas stocké

| Donnée | Pourquoi |
|---|---|
| Prix, totaux, encaissements | `RG-50` — décidés au comptoir |
| Exemplaires individuels | Décision fonctionnelle : quantité par ISBN |
| Valeur marchande estimée | `RG-14` hors v1. Colonnes à ajouter le jour venu, pas avant |
| Motif d'un refus au tri | Décision fonctionnelle : compteur seul |
| Poids d'un livre | Absent du catalogage bibliothécaire, et sans usage identifié |

## 4. Configuration EF Core

Convention du dépôt : une classe par agrégat dans
`Infrastructure/Persistence/Configurations/`, ramassée par
`ApplyConfigurationsFromAssembly`. Ajouter les `DbSet` à `ProjectDbContext` **et** à
`IProjectDbContext`.

Points d'attention :

- **Identifiants fortement typés** : reprendre les conversions déjà en place pour
  `ProductId`, `OrderId` et consorts.
- **`RawPayload` et `ManuallyEditedFields`** en `nvarchar(max)`. Le JSON natif de SQL
  Server suffit pour les interroger ponctuellement ; ils ne sont sur aucun chemin
  critique.
- **`RowVersion` sur `Books`** : deux scanettes peuvent incrémenter la même fiche.
- **Collation insensible aux accents** sur `Title` et `Authors`, sans quoi la recherche
  de `ENF-08` est jugée inutilisable dès le premier essai.
- **Index plein texte** sur `Title` + `Authors` (`DT-07`).

## 5. Transactions

Voir `DT-06` : le `BaseRepository` existant appelle `SaveChangesAsync()` **à chaque
opération**, ce qui interdit l'atomicité entre agrégats.

Trois traitements l'exigent :

| Traitement | Ce qui doit être atomique |
|---|---|
| Clôture de session (`RG-44`) | Statut de la session **et** insertion des lignes d'outbox |
| Scan gardé | Mouvement, compteurs de la fiche, compteurs de la session, éventuelle annonce |
| Reprise en bloc (`RG-25`) | Mouvements inverses, mouvements rejoués, quantités, annulation des alertes en attente |

Ces handlers écrivent via le `DbContext` avec un `SaveChanges` unique, ou une
transaction explicite. **Ne pas passer par le `BaseRepository` pour ces cas.**

## 6. Migrations

Le dossier `Infrastructure/Migrations/` existe déjà. Découpage suggéré — une migration
par palier fonctionnel plutôt qu'une seule massive :

1. `Books`, `BookMovements`, `ScanSessions`, `BookAnnouncements` — palier 1
2. Index plein texte et collation — palier 2
3. `Members`, `MemberWatchlistItems`, `OutboxMessage` — palier 3

**Aucune reprise de données initiale.** Le catalogue démarre vide, conformément à
`Q-11` et `RG-48` : il se remplit au fil des tris.

## 7. Volumétrie attendue

| Table | Après 5 ans | Remarque |
|---|---|---|
| `Books` | ~20 000 lignes, 5 à 25 Mo | Selon conservation des notices brutes |
| `BookMovements` | ~150 000 lignes, ~20 Mo | Croissance linéaire, aucune purge prévue |
| `BookAnnouncements` | quelques dizaines de milliers | |
| `ScanSessions` | quelques milliers | |
| **Total hors images** | **< 100 Mo** | Justifie `DT-02` |

Les couvertures, en blob, sont le seul poste volumineux — quelques gigaoctets — et ne
touchent pas la base.
