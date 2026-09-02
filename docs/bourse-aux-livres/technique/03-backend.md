# 03 — Backend

## 1. Conventions à respecter

Relevées dans le dépôt. Le module livres ne les redéfinit pas.

| Élément | Convention en place |
|---|---|
| Découpage `Application` | `Fonctionnalite/{Commands,Queries,Common}` |
| Dispatch | MediatR, handlers proches de leur fonctionnalité |
| Validation | FluentValidation + `ValidationBehavior` dans `Common/Behaviors` |
| Contrats de transport | Dans `Contracts`, jamais dans Angular ou MAUI |
| Règles métier | Dans les agrégats du `Domain`, pas dans les contrôleurs |
| Endpoints | Contrôleurs par fonctionnalité dans `Api/Controllers` |
| Persistance | `Configurations/`, `Repositories/`, `ProjectDbContext` |
| Autorisation | Politique `IsAdmin` existante, limiteurs de débit nommés |

## 2. Tranches à créer

```
Application/Books/
  Commands/    ScanBook, RejectBook, UndoLastScan,
               OpenScanSession, CloseScanSession,
               RegisterSale, VoidSale,
               ReassignSessionMode, CancelSession, RemoveBookFromSession,
               AdjustQuantity, MergeBooks, MarkAsRare, HideBook,
               EnterFairRevenue
  Queries/     GetBookByIsbn, SearchCatalog, SearchBibliographicReference,
               GetCatalogDelta, GetSessions, GetSessionDetail,
               GetDashboard, GetFairStatistics, GetDeadStock,
               GetPendingAlerts, GetMetadataWorkQueue
  Common/      Interfaces, projections partagées

Application/Watchlist/
  Commands/    AddWatchlistItem, RemoveWatchlistItem, DeleteMyAccount,
               RecordEmailBounce
  Queries/     GetMyWatchlist

Contracts/Books/        DTO de transport
Domain/BookAggregate/   + ScanSessionAggregate, BookMovementAggregate, WatchlistAggregate
Infrastructure/
  Persistence/Configurations/  + Repositories/
  Services/Bibliographic/      BnfSruClient, OpenLibraryClient, MetadataResolver
  Services/Email/
```

## 3. Les handlers qui méritent de l'attention

La plupart sont triviaux. Trois concentrent la difficulté.

### `ScanBook`

Le chemin le plus chaud du système (`ENF-01`, `ENF-03`).

1. Normaliser l'ISBN en 13 chiffres, contrôler la clé (`RG-01`, `RG-02`).
2. Charger ou créer la fiche — création en statut `Pending` si inconnue (`RG-03`).
3. Calculer le verdict à partir **des seules données internes** : quantités, ventes,
   demandes (`RG-10` à `RG-15`).
4. Écrire mouvement, compteurs et éventuelle annonce **en une transaction** (`DT-06`).
5. Répondre.
6. **Hors transaction et hors réponse** : déclencher la résolution des métadonnées si
   la fiche est `Pending`.

Le point 6 est ce qui fait tenir `ENF-02`. Un appel externe ne doit jamais retarder la
réponse ni faire échouer l'écriture.

### `CloseScanSession`

Porte `RG-43`, `RG-44` et `RG-29`. Dans **une seule transaction** :

1. Passer la session en `Terminee` avec sa cause de clôture.
2. Déterminer les livres de la session correspondant à des listes de recherche — par
   ISBN **ou par œuvre** (`RG-46`) — en respectant `RG-30` (anti-répétition) et le
   statut du membre.
3. Regrouper **par membre** (`RG-29`) et insérer une ligne d'outbox par membre, avec
   `DueAt = maintenant + 2 h`.
4. Cas `RG-24` : si la session visait une bourse sans date, **aucune ligne d'outbox
   n'est créée** — l'alerte est différée jusqu'au rattachement.

Appelé depuis quatre origines — bouton, inactivité, déconnexion, jeton expiré — donc
**le handler doit être idempotent** : une session déjà `Terminee` ne produit rien.

### `ReassignSessionMode` (`RG-25`, `RG-45`)

Le rattrapage le plus important de l'administration. Dans une transaction :

1. Générer les mouvements inverses de la session.
2. Rejouer les mouvements dans le mode cible, annonces comprises.
3. Recalculer les quantités des fiches touchées.
4. **Selon l'échéance** : si les lignes d'outbox ne sont pas encore dues, les annuler ou
   les recalculer ; sinon, retourner la liste des alertes déjà parties afin que
   l'administrateur en soit informé.
5. Marquer la session `Reprise`.

## 4. Endpoints

Trois familles, **un seul mode d'authentification** depuis `DT-10` : jeton Entra
External ID, validé côté API. Ce qui les distingue n'est plus le mécanisme, c'est le
rôle applicatif exigé.

### Bénévoles — `/books/scan/*`

| Verbe | Route | Règles |
|---|---|---|
| `POST` | `/scan/sessions` | Ouvre une session, retourne le mode et la bourse visée (`RG-20`, `RG-24`) |
| `POST` | `/scan/sessions/{id}/close` | Clôture explicite (`RG-43`) |
| `POST` | `/scan/sessions/{id}/scans` | Un scan gardé ou écarté ; accepte un lot |
| `DELETE` | `/scan/sessions/{id}/scans/last` | `RG-17` |
| `GET` | `/scan/catalog/delta?since=` | Synchronisation de la copie embarquée |
| `POST` | `/scan/sales` | Vente d'un lot de livres (`RG-33`, `RG-37`) |
| `DELETE` | `/scan/sales/{id}` | `RG-49` |
| `GET` | `/scan/books/{isbn}` | Mode consultation, ne produit aucun mouvement |

**`POST /scan/sessions/{id}/scans` doit accepter un lot et être idempotent** : c'est
par lui que remonte la file de sortie d'un appareil resté hors ligne (`ENF-05`). Chaque
geste porte un identifiant produit par le client, de sorte qu'un rejeu ne duplique rien.

### Public — `/catalog/*`

Lecture seule sans authentification, sauf la liste de recherche.

| Verbe | Route | Règles |
|---|---|---|
| `GET` | `/catalog/search` | Deux périmètres distincts, jamais mélangés (`RG-47`) |
| `GET` | `/catalog/books/{isbn}` | Fiche publique |
| `GET` | `/catalog/fairs/next` | Dates depuis `AssoEvents` (`RG-36`) |
| `GET`/`POST`/`DELETE` | `/catalog/me/watchlist` | Authentifié Entra (`RG-46`, `RG-27`) |
| `DELETE` | `/catalog/me` | `ENF-12` |

### Administration — `/books/admin/*`

Rôle explicite (`ENF-18`) : tableau de bord, statistiques par bourse, gestion du
catalogue, sessions et reprises, alertes en attente, membres, bénévoles, paramètres
(`ENF-25`), saisie de recette (`RG-51`).

## 5. Sécurité

| Sujet | Traitement |
|---|---|
| Fournisseur d'identité | **Entra External ID pour tous** (`DT-10`). Aucun mot de passe en base, aucune clé de signature à garder |
| Bénévoles | Rôles `Tri` et `Caisse` dans la revendication `roles` (`RG-40`). Session longue (`ENF-17`) — voir la réserve de `10` §9 |
| Membres | Jeton valide **sans aucun rôle** : c'est ce qui définit le membre (`ENF-16`). Aucune table distincte — une seule table de personnes (`DT-14`) |
| Administrateurs | Rôle `Administration` (`ENF-18`), même annuaire que les membres |
| Autorisation | `RequireRole` sur la revendication `roles`. Aucune lecture en base, aucun appel à l'annuaire |
| Confidentialité des demandeurs | `RG-42` : l'API ne renvoie **jamais** l'identité, seulement un décompte. À couvrir par un test — c'est une fuite facile à introduire |
| Limitation de débit | Réutiliser le mécanisme en place ; un limiteur propre à la recherche publique, qui atteint une source externe |
| Rappel de rebond e-mail | Endpoint dédié, authentifié par secret partagé (`RG-31`) |

## 6. Tests

Le dépôt a `Domain.tests`, `Application.tests`, `Infrastructure.tests` en xUnit et une
convention TDD. Priorités, par ordre de valeur :

1. **`RG-15`** — la table de priorité des verdicts. Peu de code, beaucoup de cas, et une
   erreur y est invisible en production.
2. **`RG-10`** — le comptage doit inclure les annonces, sans quoi deux bénévoles en
   parallèle gardent chacun cinq exemplaires du même titre.
3. **`RG-01`** — conversion ISBN-10 vers 13 et clé de contrôle.
4. **`CloseScanSession`** — idempotence sur les quatre causes, regroupement par membre,
   cas `RG-24` sans date.
5. **`ReassignSessionMode`** — inversion et rejeu, avec et sans alertes déjà parties.
6. **`RG-23`** — bascule à la date, y compris rattrapage d'un retard (`RG-38`).

Les tests de résolution bibliographique s'appuient sur des **notices enregistrées**,
jamais sur un appel réel à la BnF : sinon la suite devient lente et dépendante du réseau.
