# Reprise du front catalogue — P2/P3

> Document de passage pour la session qui refondra `src/Catalog`. Cette branche ne
> modifie volontairement aucun fichier de `src/Catalog` : le contrat backend et le
> parcours d'administration sont livrés séparément afin de ne pas entrer en conflit
> avec la refonte en cours.

## État livré

- P2 : lecture publique du catalogue, recherche, fiche édition, œuvre, prochaine
  bourse, sitemap et recherche bibliographique externe.
- P3 : identité Entra, watchlist édition/œuvre, demande de suppression de compte,
  suspension volontaire des alertes et administration complète du stock, des
  sessions, des alertes, des membres et des paramètres.
- `src/BackOffice` expose maintenant `/administration` pour les actions
  d'administration. Ce n'est pas une route publique du catalogue.
- Les instants JSON sont en UTC (`DateTimeOffset`). Les jours de ventes sont des
  `DateOnly` au format ISO `YYYY-MM-DD`.
- Le backend ne connaît ni prix par livre ni emplacement dans le local. Le front ne
  doit donc afficher ni prix unitaire, ni rayon, ni promesse de réservation.

## Authentification et règles de séparation

Le front public appelle les routes anonymes sans jeton. Les routes `/catalog/me/*`
nécessitent un bearer Entra acquis avec la portée API configurée dans l'environnement
Catalog. Le jeton doit contenir `oid` et l'API synchronise cet identifiant avec le
`User` local ; ne jamais envoyer un `userId` choisi dans le navigateur.

Les routes `/books/admin/*` demandent le rôle applicatif `Administration` ou `Admin`.
Elles sont consommées par le BackOffice, pas par le front public. Une réponse `401`
doit ramener vers la connexion ; une réponse `403` doit afficher « droits
administrateur requis » sans boucler vers la connexion.

Les données membres et d'administration ne doivent jamais être rendues en SSR :
attendre le navigateur et le jeton, puis charger les données. Les pages `/compte` et
les éventuelles pages privées restent `noindex, nofollow`.

## Routes publiques à raccorder

| Méthode et route | Paramètres | Utilisation |
|---|---|---|
| `GET /catalog/search` | `q`, `genre`, `availability=all\|available\|next-fair`, `rare`, `sort=relevance\|recent`, `page`, `pageSize` | Résultats publics, sans fiches masquées ou redirigées |
| `GET /catalog/books/{isbn13}` | ISBN-10 ou ISBN-13 accepté | Fiche édition publique |
| `GET /catalog/works/{workId}` | identifiant d'œuvre | Regroupement des éditions |
| `GET /catalog/fairs/next` | aucun | Prochaine bourse de livres |
| `GET /catalog/sitemap.xml` | aucun | Sitemap dynamique |
| `GET /catalog/reference/search` | `q` (2–200 caractères), `page`, `pageSize` (20 par défaut, maximum 50) | Recherche externe Open Library, séparée du catalogue local |
| `GET /books/{isbn13}/metadata` | ISBN-10 ou ISBN-13 | Notice BnF/Open Library pour le flux de référence/tri |

`q` doit être encodé par le client. Pour la recherche externe, présenter clairement
« Référentiel externe » : ses résultats ne sont pas des livres disponibles et ne
doivent pas être mélangés à la recherche locale. `BookReferenceSearchResponse` renvoie
`generatedAt`, `query`, `items`, `page`, `pageSize`; chaque item contient `isbn13`
éventuellement nul, `workId`, `title`, `authors`, `publisher`, `publicationYear`,
`coverUrl` et `source`.

La fiche publique (`PublicCatalogBookResponse`) contient notamment `isbn13`,
`title`, `authors`, `publisher`, `publicationYear`, `physicalFormat`, `language`,
`genre`, `workId`, `coverUrl`, `quantityAvailable`, `quantityAnnounced`,
`nextFairAt`, `lastAvailableAt`, `firstSeenAt`, `updatedAt` et `isRare`.
Afficher les états suivants sans inventer de valeur :

- `quantityAvailable > 0` : disponible dès maintenant ;
- `quantityAvailable === 0` et `quantityAnnounced > 0` : annoncé pour une bourse ;
- les deux à zéro : épuisé, mais la fiche reste visible pour permettre une alerte ;
- `isRare` : signal éditorial, jamais un prix.

## Routes membre `/catalog/me`

Toutes les routes ci-dessous utilisent le compte Entra courant.

| Méthode et route | Corps | Réponse / comportement |
|---|---|---|
| `GET /catalog/me/watchlist` | — | `WatchlistResponse`: `generatedAt`, `alertStatus`, `bounceCount`, `items` |
| `POST /catalog/me/watchlist` | `{ scope: "Work"\|"Edition", workId?: string, isbn13?: string }` | `AddedWatchlistItemResponse` |
| `DELETE /catalog/me/watchlist/{itemId}` | — | `204 No Content` |
| `PATCH /catalog/me/alerts` | `{ enabled: boolean }` | `{ alertStatus, bounceCount, changed }` |
| `DELETE /catalog/me` | — | demande de suppression ; prévoir un état « en cours » si l'API renvoie une suppression asynchrone |

### Parcours watchlist

1. Depuis une fiche ou un résultat, proposer d'abord « Suivre cette œuvre » quand
   `workId` existe ; l'édition est une option secondaire explicite.
2. Si l'utilisateur choisit une édition, envoyer uniquement `scope=Edition` et
   `isbn13`; si l'utilisateur choisit une œuvre, envoyer uniquement `scope=Work` et
   `workId`.
3. Recharger la watchlist après chaque ajout/suppression. Gérer `409` pour doublon ou
   limite atteinte avec un message compréhensible.
4. Pour une œuvre encore absente du catalogue, conserver la cible externe et afficher
   « Pas encore reçu ». Ne pas créer une fiche locale depuis le navigateur.
5. Dans la liste, garder la portée visible (« œuvre » ou « édition ») et afficher
   `lastAlertAt` lorsqu'il existe.

### Parcours alertes et désabonnement

`alertStatus` peut valoir `Active`, `Suspended`, `Blocked` ou `None` (watchlist non
créée). `Suspended` peut venir d'un choix de l'utilisateur ou de rebonds ; `Blocked`
est un blocage de l'association et ne peut pas être réactivé par le membre.

Le front doit afficher un bouton ou une case « Recevoir les alertes » qui appelle
`PATCH /catalog/me/alerts`. Pour un lien de désinscription e-mail, créer une route
publique de confirmation dans le catalogue, demander une connexion si nécessaire,
puis appeler `{ enabled: false }`; ne jamais mettre un bearer dans l'URL. Le builder
e-mail backend ajoute déjà le lien configuré par `BookAlerts:Email:UnsubscribeUrl`.

Le statut ne garantit pas qu'un e-mail soit parti : les alertes sont regroupées par
membre et par session, puis retardées selon `AlertDelayMinutes`. Le front doit
présenter ce délai comme une règle de disponibilité, pas comme une réservation.

## Contrat d'administration consommé par le BackOffice

Le BackOffice livré utilise `/administration`. La refonte du catalogue n'a pas à
réimplémenter cette surface, mais ces routes servent de référence si une partie est
un jour déplacée dans un portail privé.

### Vue, fiches et stock

| Méthode et route | Corps / paramètres |
|---|---|
| `GET /books/admin/overview` | `from`, `to` optionnels |
| `GET /books/admin/books` | `search`, `metadataStatus`, `rare`, `hidden`, `undated`, `page`, `pageSize` |
| `GET /books/admin/books/{isbn13}` | détail, annonces et ledger des mouvements |
| `POST /books/admin/books` | `AddAdminBookRequest` : ISBN, quantité, note et métadonnées facultatives |
| `PATCH /books/admin/books/{isbn13}/metadata` | `UpdateAdminBookMetadataRequest` |
| `PATCH /books/admin/books/{isbn13}/quantity` | `{ quantityAvailable, note }` |
| `POST /books/admin/books/{isbn13}/withdrawals` | `{ quantity, note }` |
| `PATCH /books/admin/announcements/{announcementId}/quantity` | `{ quantity, note }` |
| `POST /books/admin/books/{isbn13}/rare?isRare=true\|false` | aucun corps |
| `POST /books/admin/books/{isbn13}/visibility?hidden=true\|false` | aucun corps |
| `POST /books/admin/books/{sourceIsbn13}/merge` | `{ targetIsbn13, note }` |
| `DELETE /books/admin/books/{isbn13}` | suppression uniquement d'une fiche sans historique interdit |
| `GET /books/admin/dead-stock` | `minAgeMonths`, `minQuantity` |

Les corrections ne suppriment jamais une ligne du ledger. Les réponses d'opération
renvoient `changed` et, quand applicable, `movementId`. Une fusion redirige la fiche
source vers l'ISBN cible sans réécrire l'historique ; l'UI doit afficher la cible
canonique et désactiver les actions sur la fiche redirigée.

### Bourses et statistiques

| Méthode et route | Paramètres / corps |
|---|---|
| `GET /books/admin/fairs` | `includeCancelled`, `page`, `pageSize` |
| `GET /books/admin/fairs/{fairId}/stats` | — |
| `PUT /books/admin/fairs/{fairId}/revenue` | `{ revenue: number | null }` |

`AdminFairStatsResponse` fournit ventes totales, titres distincts, recette facultative,
panier moyen, ventes par genre, top livres, ventes quotidiennes et comparaisons avec
les bourses précédentes. Si `revenue` est nul, afficher « non saisie », pas `0 €`.

### Sessions de tri et rattrapage

| Méthode et route | Paramètres / corps |
|---|---|
| `GET /books/admin/sessions` | `status`, `from`, `to`, `page`, `pageSize` |
| `GET /books/admin/sessions/{scanSessionId}` | mouvements et compteurs d'alertes |
| `POST /books/admin/sessions/{sessionId}/movements/{movementId}/remove` | — |
| `POST /books/admin/sessions/{sessionId}/reassign` | `{ mode: "AvailableNow"\|"NextFair", targetAssoEventsId?: guid }` |
| `POST /books/admin/sessions/{sessionId}/cancel` | — |
| `POST /books/admin/sessions/{sessionId}/alerts/cancel` | — |
| `POST /books/admin/sessions/{sessionId}/alerts/force` | — |

Les opérations `remove`, `reassign` et `cancel` sont destructrices du point de vue
fonctionnel : afficher une confirmation et le nombre de mouvements concernés. Elles
produisent des corrections tracées ; rien ne doit être présenté comme effacé.
Après correction, l'état de session peut devenir `Resumed`. Le délai d'alerte permet
encore une annulation locale avant l'envoi ; une alerte déjà envoyée ne peut pas être
rappelée.

### Alertes, membres et paramètres

| Méthode et route | Paramètres / corps |
|---|---|
| `GET /books/admin/alerts` | `status`, `scanSessionId`, `memberId`, `page`, `pageSize` |
| `POST /books/admin/alerts/{messageId}/cancel` | — |
| `POST /books/admin/alerts/{messageId}/force` | — |
| `GET /books/admin/members` | `search`, `alertStatus`, `page`, `pageSize` |
| `GET /books/admin/members/{memberId}` | détail watchlist et historique d'alertes |
| `POST /books/admin/members/{memberId}/block` | — |
| `POST /books/admin/members/{memberId}/unblock` | — |
| `DELETE /books/admin/members/{memberId}` | suppression/anonymisation selon le statut du fournisseur |
| `GET /books/admin/settings` | — |
| `PUT /books/admin/settings` | `UpdateAdminAssociationSettingsRequest` |

## Checklist d'intégration dans la refonte Catalog

- [ ] Générer les modèles TypeScript à partir des contrats camelCase ou les maintenir
  dans un seul fichier partagé ; ne pas recopier des formes divergentes dans chaque
  page.
- [ ] Centraliser le client API et l'acquisition MSAL ; ne pas mettre de jeton dans
  `localStorage`, le HTML SSR ou une query string.
- [ ] Prévoir les états `loading`, vide, erreur réseau, `401`, `403`, `404`, `409` et
  `503` sur chaque écran.
- [ ] Tester séparément recherche locale et recherche externe, ainsi que les trois
  états de disponibilité et les watchlists œuvre/édition.
- [ ] Tester le retour Entra vers `/compte`, le rafraîchissement de session et la
  suppression de compte sans afficher de données privées en SSR.
- [ ] Ajouter la route de désinscription e-mail et la raccorder à `PATCH
  /catalog/me/alerts` après authentification.
- [ ] Garder les pages publiques indexables avec canoniques, `schema.org/Book`,
  `robots.txt` et sitemap ; garder `/compte` privé avec `noindex, nofollow`.
- [ ] Vérifier mobile (watchlist lisible et actions accessibles au pouce) et desktop
  (résultats, fiche et contrôles sans débordement), clavier, focus visible et labels.
- [ ] Ne pas ajouter de traceur au catalogue public ; toute mesure éventuelle reste
  cantonnée à une zone privée et doit respecter la décision `ENF-14`.
- [ ] Exécuter les tests Catalog ChromeHeadless, le build SSR et un smoke des routes
  publiques et privées après la refonte.

## Hors périmètre volontairement

L'estimation de valeur marchande, la remise à plat d'inventaire en masse, les
notifications push, le support des livres sans ISBN et l'envoi ACS réel ne sont pas
inventés dans cette tranche. Le déploiement de la migration `AddBookFairRevenue`, la
vérification du domaine ACS et le cycle e-mail de bout en bout restent des contrôles
opératoires à faire sur l'environnement cible.
