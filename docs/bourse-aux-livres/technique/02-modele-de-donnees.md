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
| `WatchlistAggregate` | `Watchlist` | La liste de recherche d'une personne, et l'état de ses alertes |

`OutboxMessage` n'est pas un agrégat métier mais une table d'infrastructure — voir
[`06-traitements-differes.md`](06-traitements-differes.md).

**Il n'y a pas d'agrégat `Member`.** `DT-14` pose qu'il n'existe **qu'une seule table de
personnes**, la table `Users` existante. Un membre du site est une personne sans rôle
(`DT-10`) qui possède une `Watchlist` ; un bénévole est une personne avec un rôle. La même
peut être les deux, et n'a qu'une ligne.

### Pourquoi le mouvement est un agrégat distinct

Il serait tentant d'en faire une collection dans `Book`. À proscrire : charger une
fiche chargerait alors des milliers de lignes, et les mouvements sont consultés par
bourse et par session bien plus souvent que par livre. Ils référencent la fiche par
identifiant, ils n'y sont pas contenus.

## 2. Tables

### `Books`

```
Isbn13                char(13)      PK
RedirectedToIsbn13    char(13)      NULL   -- DT-19, fiche absorbée
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

`RedirectedToIsbn13` est nul pour une fiche canonique. Pour une fiche absorbée, il
pointe directement vers la fiche canonique ; la résolution est obligatoire avant toute
nouvelle écriture, et l'historique de la fiche absorbée reste consultable. La contrainte
interdit l'auto-référence, les cycles et les chaînes de redirection.

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

Après une fusion, les annonces actives sont résolues vers l'ISBN canonique dans la
même transaction. Les annonces historiques conservent leur traçabilité ; les lectures
du catalogue ne publient qu'une fiche canonique.

### `BookMovements`

```
Id             uniqueidentifier PK
Isbn13         char(13)         FK Books
Type           tinyint          NOT NULL
               -- EntreeAnnonce|EntreeDirecte|Bascule|Vente|Refus|Correction|Retrait
Quantity       int              NOT NULL     -- signé
OccurredAt     datetime2        NOT NULL     -- heure client, ordre réel des gestes
ReceivedAt     datetime2        NOT NULL     -- heure serveur, audit
ClockSuspect   bit              NOT NULL DEFAULT 0
ScanSessionId  uniqueidentifier FK NULL      -- NULL pour caisse et corrections
VolunteerId    uniqueidentifier FK NULL      -- RG-41
AssoEventsId   uniqueidentifier FK NULL      -- RG-33
Note           nvarchar(500)    NULL         -- motif d'une correction
ClientGestureId uniqueidentifier NULL        -- clé d'idempotence, voir ci-dessous
```

**`ClientGestureId` est ce qui rend l'endpoint de lot idempotent** (`03` §4, `04` §4).
Sans lui, la première retransmission après une coupure double les mouvements — c'est-à-dire
fausse les quantités, en silence, un jour de bourse.

Il est produit par l'appareil, porté par le geste dans la file de sortie, et **recopié sur
la ligne d'annonce** que le geste engendre le cas échéant : un scan gardé en mode
`PROCHAINE BOURSE` produit un mouvement **et** une annonce, donc l'identifiant ne peut pas
être la clé primaire de l'un des deux.

Il vaut `NULL` pour tout mouvement d'origine serveur — bascule (`RG-23`), correction
administrative (`RG-35`) —, d'où un **index unique filtré sur
`ClientGestureId IS NOT NULL`**.

La déduplication d'un lot se fait en une lecture : le handler relève les identifiants déjà
connus parmi ceux du lot, et ne traite que les autres. Une annulation (`RG-17`) est
elle-même un geste, avec son propre identifiant — elle passe par le même chemin.

**Deux horodatages, pas un.** Un geste produit hors ligne est daté par le client — le
serveur ne le voit parfois que des heures plus tard. Mais l'horloge d'un appareil n'est
pas fiable, et une horloge fausse polluerait les statistiques par bourse. On conserve
donc `OccurredAt` (client, pour l'ordre réel) **et** `ReceivedAt` (serveur, pour
l'audit). Si l'heure client est absurde — dans le futur, ou antérieure au début de la
session — on retient l'heure serveur et on lève `ClockSuspect`.

**Table en ajout seul.** On n'y met jamais à jour, on n'y supprime jamais : une
annulation (`RG-17`, `RG-49`) produit un mouvement inverse. C'est l'historique
comptable exigé par `ENF-22`, et c'est ce qui rend `ENF-06` trivial — deux appareils
hors ligne produisent deux lignes, jamais un conflit.

Une annulation locale d'un geste qui n'a jamais quitté l'appareil ne produit aucune
ligne serveur. Une annulation après transmission est, elle, un nouveau mouvement
inverse avec un nouvel identifiant client ; cette distinction est celle de `DT-18`.

Toutes les valeurs temporelles de ces tables sont des instants UTC, y compris les
colonnes `datetime2`. `OccurredAt` représente l'instant client normalisé, `ReceivedAt`
la réception serveur ; `ClockSuspect` conserve le signal d'une horloge cliente
incohérente.

Index : `Isbn13` + `OccurredAt` ; `AssoEventsId` + `Type` (statistiques par bourse) ;
`ScanSessionId` (reprise en bloc `RG-25`) ; **unique filtré sur `ClientGestureId`**.

### `ScanSessions`

```
Id                  uniqueidentifier PK
VolunteerId         uniqueidentifier FK
Mode                tinyint          NOT NULL -- DisponibleMaintenant|ProchaineBourse
TargetAssoEventsId  uniqueidentifier NULL     -- bourse visée, NULL si RG-24
StartedAt           datetime2        NOT NULL
LastScanAt          datetime2        NOT NULL -- heure client du dernier scan
LastSyncAt          datetime2        NOT NULL -- dernier contact de l'appareil
LateArrivals        bit              NOT NULL DEFAULT 0 -- gestes reçus après clôture
EndedAt             datetime2        NULL
CloseReason         tinyint          NULL     -- Manuelle|Inactivite|Deconnexion|JetonExpire
Status              tinyint          NOT NULL -- EnCours|Terminee|Reprise
ScannedCount        int              NOT NULL DEFAULT 0
KeptCount           int              NOT NULL DEFAULT 0
RejectedCount       int              NOT NULL DEFAULT 0
```

`LastScanAt` porte `RG-43` : le balayage d'inactivité cherche les sessions `EnCours`
dont `LastScanAt` dépasse le seuil.

**`LastSyncAt` est distinct et indispensable.** Un bénévole qui trie hors ligne pendant
trois heures paraît inactif au serveur, qui ne juge que sur ce qu'il a reçu : sa session
serait close et ses alertes envoyées alors qu'il scanne encore. Le balayage exige donc
que **les deux** horodatages soient périmés. `LateArrivals` marque les sessions ayant
reçu des gestes après leur clôture, pour que l'administration les repère
([`04-app-scan.md`](04-app-scan.md) §4).

Contrainte : **une seule session ouverte par bénévole** — index unique filtré sur
`Status = EnCours`.

### `Users` — la table de personnes, modifiée

`DT-14` : **une seule table de personnes**, celle qui existe déjà. Identité déléguée à
Entra External ID (`ENF-26`), donc **aucun mot de passe stocké**.

```
Users                                     -- existante, modifiée
  Id            uniqueidentifier PK
  ExternalId    nvarchar(64)     NULL     -- oid Entra ; NULL après anonymisation
  Email         nvarchar(320)    NULL     -- copie d'affichage ; NULL après anonymisation
  Name          (value object)   NULL
  CreatedAt     datetime2        NOT NULL
  LastSeenAt    datetime2        NOT NULL
  AnonymizedAt  datetime2        NULL     -- ENF-12, voir plus bas

  -- supprimées par DT-10 : Password, Salt, Role
```

**La clé de rapprochement est `oid`, jamais `sub`.** Dans un locataire externe, `sub` est
appairé par application : le même compte présente un `sub` différent au catalogue et à
l'application de scan. C'est le défaut que `DT-14` corrige, et il est invisible tant qu'on
ne teste qu'avec une seule application.

Index : **unique filtré** sur `ExternalId WHERE ExternalId IS NOT NULL` — l'anonymisation
libère la valeur, et deux anonymisations entreraient sinon en collision.

Le rapprochement se fait **à la première connexion** (`10` §5) : au premier appel
authentifié, si aucune ligne ne porte cet `oid`, l'API en crée une. Pas de tâche de fond,
pas de synchronisation, pas de dérive.

### `Watchlists` et `WatchlistItems`

La facette « membre » d'une personne : sa liste de recherche, et l'état de ses alertes.

```
Watchlists
  UserId        uniqueidentifier PK, FK Users
  AlertStatus   tinyint          NOT NULL  -- Actif|Suspendu|Bloque
  BounceCount   int              NOT NULL DEFAULT 0   -- RG-31
  CreatedAt     datetime2        NOT NULL

WatchlistItems
  Id        uniqueidentifier PK
  UserId    uniqueidentifier FK Watchlists
  Scope     tinyint          NOT NULL -- Oeuvre|Edition, RG-46
  WorkId    nvarchar(64)     NULL     -- si Scope = Oeuvre
  Isbn13    char(13)         NULL     -- si Scope = Edition
  AddedAt   datetime2        NOT NULL
```

**Pourquoi le statut d'alerte n'est pas sur `Users`.** Il ne décrit pas une personne, il
décrit l'usage qu'elle fait des alertes. Le loger sur l'identité obligerait le domaine
`Books` à écrire dans l'agrégat `User` à chaque rebond, et donnerait des colonnes vides à
toute personne qui ne s'est jamais inscrite à quoi que ce soit — c'est-à-dire à tous les
bénévoles. Ici, **la ligne n'existe que pour qui se sert de la fonction**.

Conséquence utile : « membre inscrit » reste ce que `DT-10` en fait — un compte valide
sans aucun rôle. Rien n'est à écrire nulle part au moment de l'inscription.

Contrainte : exactement l'un des deux champs cibles renseigné, selon `Scope`.

**Aucune clé étrangère vers `Books`** : `RG-47` permet de suivre un livre que
l'association n'a jamais reçu, donc sans fiche. C'est délibéré, et c'est le point que
l'implémentation ratera si on ne le lit pas.

Index : `WorkId` et `Isbn13` — ce sont eux qui rendent `RG-13` instantané au scan.

### Suppression et anonymisation

`ENF-13` (suppression après trois ans d'inactivité) s'appuie sur `Users.LastSeenAt`.

`ENF-12` impose que la suppression efface la liste **et** l'historique d'alertes : cascade
explicite depuis `Watchlists`. Mais la ligne de personne elle-même n'est pas toujours
supprimable — `RG-41` exige que tout mouvement porte l'identité du bénévole qui l'a
produit, et `ENF-12` conserve explicitement les mouvements de vente. Deux cas :

| Cas | Traitement |
|---|---|
| Aucun mouvement ne pointe vers la personne — un membre du public | **Suppression de la ligne `Users`**, cascade sur `Watchlists`, `WatchlistItems` et `UserAlertHistory` |
| Des mouvements y pointent — une bénévole | **Anonymisation** : `Email`, `Name` et `ExternalId` effacés, `AnonymizedAt` horodaté, cascade identique. Les mouvements pointent vers une ligne qui n'identifie plus personne |

Dans les deux cas, **le compte doit aussi disparaître du locataire** : effacer nos données
en laissant l'identité vivante n'est pas une suppression. Ce volet-là n'est pas conçu —
c'est le constat `R-06` de [`revue.md`](revue.md), et il reste ouvert.

### `UserAlertHistory`

```
Id               uniqueidentifier PK
UserId           uniqueidentifier FK Users
Isbn13           char(13)         NOT NULL   -- pas de clé étrangère, voir ci-dessous
SentAt           datetime2        NOT NULL
OutboxMessageId  uniqueidentifier NULL FK    -- traçabilité, l'envoi qui l'a produite
```

**Cette table porte `RG-30` à elle seule.** L'anti-répétition — un même couple
membre/ISBN pas plus d'une fois sur trente jours glissants — exige d'interroger
`(membre, ISBN, date d'envoi)`. La table `OutboxMessage` ne le permet pas : elle est par
membre **et par session**, son contenu est un `PayloadJson` opaque, et une alerte groupée
(`RG-29`) y couvre plusieurs livres en une ligne. Un message envoyé n'est pas un
historique requêtable.

**Aucune clé étrangère vers `Books`**, pour la même raison que
`WatchlistItems` : `RG-47` permet de suivre un livre que l'association n'a jamais
reçu.

**Elle s'écrit à l'envoi, pas à la mise en file.** C'est le point à ne pas inverser.
`RG-30` se vérifie **deux fois** :

| Moment | Rôle de la vérification |
|---|---|
| À la clôture de session (`CloseScanSession`) | Détermine ce qui sera annoncé au bénévole dans son récapitulatif. **Indicative** |
| Au moment de l'envoi, dans `sweep` | **Fait foi.** C'est le « relire l'état en base avant d'envoyer » de [`06`](06-traitements-differes.md) §4 |

Sans la seconde, deux sessions closes à quelques minutes d'écart passeraient toutes deux
le contrôle et enverraient toutes deux — l'anti-répétition serait contournée par un simple
chevauchement.

Index : `UserId` + `Isbn13` + `SentAt` décroissant.

`ENF-12` impose la cascade : supprimer un membre efface sa liste **et** son historique
d'alertes. C'est nommément cité par l'exigence.

### `AssociationSettings`

Les huit valeurs de `05` §9, que `ENF-25` exige modifiables **sans redéploiement**.

```
Id                        tinyint          PK, CHECK (Id = 1)   -- ligne unique
DuplicateThreshold        int              NOT NULL DEFAULT 5    -- RG-10
DemandSalesThreshold      int              NOT NULL DEFAULT 1    -- RG-12
DeadStockMinAgeDays       int              NOT NULL              -- 05 §5
DeadStockMinQuantity      int              NOT NULL              -- 05 §5
WatchlistMaxItems         int              NOT NULL DEFAULT 100  -- RG-27
AlertCooldownDays         int              NOT NULL DEFAULT 30   -- RG-30
SessionIdleTimeoutMinutes int              NOT NULL DEFAULT 120  -- RG-43
AlertDelayMinutes         int              NOT NULL DEFAULT 120  -- RG-44
UpdatedAt                 datetime2        NOT NULL
UpdatedBy                 uniqueidentifier FK
```

**Une ligne unique à colonnes typées, pas une table clé/valeur.** L'ensemble est connu et
fixe ; des colonnes nommées se sérialisent directement vers l'appareil, se valident par le
type, et se lisent sans convention. Le prix est qu'ajouter un paramètre demande une
migration — pour une personne seule qui en fait déjà, c'est le moindre des deux maux
(`ENF-24`).

**Pas de seuil de valeur pour `RG-14`** : la règle est hors v1, et `§3` ci-dessous pose
qu'on n'ajoute pas ses colonnes avant le jour venu.

**Ces valeurs doivent atteindre l'appareil**, sans quoi le verdict calculé hors ligne
(`04` §5) n'applique pas les seuils réels. La réponse de
`GET /scan/catalog/delta` porte donc un bloc `settings` accompagné de `UpdatedAt` — neuf
entiers, le coût est nul et cela évite un second appel qui pourrait échouer seul.

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
| Envoi d'une alerte (`RG-30`) | Passage de la ligne d'outbox à `Sent` **et** écriture des lignes de `UserAlertHistory`. Envoyer sans historiser rouvre la fenêtre d'anti-répétition |

Ces handlers écrivent via le `DbContext` avec un `SaveChanges` unique, ou une
transaction explicite. **Ne pas passer par le `BaseRepository` pour ces cas.**

## 6. Migrations

Le dossier `Infrastructure/Migrations/` existe déjà. Découpage suggéré — une migration
par palier fonctionnel plutôt qu'une seule massive :

0. **Suppression de `User.Password`, `User.Salt`, `User.Role` et ajout de
   `User.ExternalId`** — au **préalable d'identité** (`10` §6). Elle précède les trois
   suivantes et conditionne tout ce qui s'authentifie
1. `Books`, `BookMovements`, `ScanSessions`, `BookAnnouncements`, `AssociationSettings` —
   palier 1. Les paramètres viennent dès la première : le verdict de `RG-10` et `RG-12`
   les lit. **La collation insensible aux accents de §4 en fait partie** : elle porte sur
   les colonnes créées ici, et la changer plus tard sur des colonnes remplies et indexées
   est un tout autre chantier
2. Index plein texte — palier 2. L'index peut attendre le catalogue public, la collation
   non
3. `Watchlists`, `WatchlistItems`, `UserAlertHistory`, `OutboxMessage` — palier 3

**Aucune reprise de données initiale.** Le catalogue démarre vide, conformément à
`Q-11` et `RG-48` : il se remplit au fil des tris.

## 7. Volumétrie attendue

| Table | Après 5 ans | Remarque |
|---|---|---|
| `Books` | ~20 000 lignes, 5 à 25 Mo | Selon conservation des notices brutes |
| `BookMovements` | ~150 000 lignes, ~20 Mo | Croissance linéaire, aucune purge prévue |
| `BookAnnouncements` | quelques dizaines de milliers | |
| `ScanSessions` | quelques milliers | |
| `UserAlertHistory` | quelques dizaines de milliers | Une ligne par membre et par livre annoncé |
| `AssociationSettings` | **1** | Une ligne, par construction |
| **Total hors images** | **< 100 Mo** | Justifie `DT-02` |

Les couvertures, en blob, sont le seul poste volumineux — quelques gigaoctets — et ne
touchent pas la base.
