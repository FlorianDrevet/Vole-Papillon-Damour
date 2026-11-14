# Plan d'implémentation — Gestion des coups de cœur

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

Spec : [`01-fonctionnel.md`](01-fonctionnel.md). Maquettes : [`maquettes/README.md`](maquettes/README.md).
Règles : `RG-160` à `RG-170`. Remplacent `RG-131` (plafonds) et le délai de `RG-134`.

## 1. Vue d'ensemble

Le travail se fait en deux parties dans la même branche, puis une PR :

- **API** (tâches 1 à 5) : plafonds levés, recherche et pagination côté serveur, statut
  « indisponible », deux flux SSE d'avancement (membre et visiteur).
- **Catalog** (tâches 6 à 11) : retrait confirmé et immédiat, fenêtre E12, recherche et
  pagination, avancement en direct, message de reprise.

Ordre : 1 → 2 → 3 → 4 → 5, puis 6 → 7 → 8 → 9 → 10 → 11. La tâche 6 peut démarrer en
parallèle de l'API, car elle ne dépend d'aucun changement serveur. La tâche 9 (recherche)
consomme le contrat de la tâche 2. La tâche 10 consomme les flux des tâches 4 et 5.

**Aucune migration de base.** Les tables `MemberFavoriteBooks`, `AnonymousFavoriteCounts` et
`BookRecommendationJobs` restent inchangées. Vérifier avant la PR qu'aucun fichier
`Migrations/` n'est ajouté.

**Ordre de déploiement :** API (Books runtime) avant Catalog, car le Catalog attend les
nouveaux contrats. L'inverse casse le flux d'avancement.

## 2. Décisions techniques

- **Retrait : commit immédiat.** Le délai de 6 secondes de `catalog-favorites.service.ts`
  est supprimé. La confirmation de E12 remplace le filet « Annuler » (`Q-01`). C'est la
  correction du bug observé : un rechargement dans les 6 secondes faisait réapparaître le livre.
- **Pagination et recherche côté serveur pour le membre** (`Q-05`). Les œuvres sont
  regroupées avant la pagination, donc la page compte des cartes (`RG-130`). Pour le visiteur,
  la liste reste dans le navigateur et la pagination est locale.
- **Avancement en direct : SSE, pas de push depuis le Worker.** L'API lit les statuts de
  `BookRecommendationJobs` toutes les 2 secondes pendant qu'un client est connecté, et n'émet
  un événement que si la valeur change. Le calcul reste dans le Worker (`RG-139`).
  Pattern de référence : `Api/LiveTableau/LiveTableauStream.cs` et
  `Controllers/AssoEventsController/EventsController.cs` (ligne 306). Ce pattern pose déjà
  `X-Accel-Buffering: no`, à reprendre.
- **Le flux ne transporte pas la liste.** Il envoie seulement l'avancement. À `complete`, le
  Catalog relance les lectures existantes (suggestions et favoris). Cela évite de dupliquer le
  contrat des suggestions dans le flux.
- **Authentification du flux membre : `fetch` en streaming, pas `EventSource`.**
  `EventSource` ne peut pas envoyer d'en-tête `Authorization`. Le client écrit un petit lecteur
  SSE sur `fetch` (`core/favorites/favorite-progress-stream.ts`). Pour le visiteur, la requête
  est un POST (le contrat de suggestions anonymes est déjà un POST, `CatalogApiService.getFavoriteSuggestions`).
- **Statut `Unavailable`.** Un job en `Failed`, ou sans job, est exposé comme `Unavailable`,
  et non comme `Computing` (`RG-167`). Aujourd'hui, `GetMyFavoritesQueryHandler` ramène tout
  statut autre que `Ready`/`NoNotice` à `Computing`, ce qui laisse une carte « en cours » sans fin.
- **Valeurs de départ :** sondage serveur 2 s ; durée maximale d'un flux 10 min (`RG-168`) ;
  heartbeat `ping` à 15 s. Elles vont dans `FavoritesOptions`, réglables sans republier.

## 3. Points à arbitrer avant exécution

- **A. Retrait depuis E3 (panneau d'ajout).** Le bouton de retrait du panneau d'ajout
  (`add-favorite-panel.component.ts:219`) n'est couvert ni par `RG-134` ni par E12. Choix
  recommandé : le faire passer par E12, comme E2, E5 et E6. Il reste immédiat dans les deux cas.
- **B. Taille d'une requête du flux visiteur.** Le visiteur n'a plus de plafond, donc sa
  liste ISBN peut être longue. Proposition : limiter la requête à 500 ISBN, côté API, comme
  limite technique et non comme plafond produit. Au-delà, le client envoie la liste en plusieurs
  flux. À confirmer.

## 4. Tâche 0 — État initial

Avant toute modification, dans le worktree :

1. `dotnet build src/Backend/Vole_Papillon_Damour.slnx -c Release`
2. `dotnet test src/Backend/Vole_Papillon_Damour.slnx`
3. Dans `src/Catalog` : `npm ci`, puis `npm test -- --watch=false` et `npm run build`.

Noter les échecs déjà présents dans le message de la PR, sans les corriger. Ne pas commit.

## 5. Partie API

### Tâche 1 — Lever les plafonds (`RG-163`)

**Fichiers**
- `src/Backend/Vole_Papillon_Damour.Application/Favorites/FavoritesOptions.cs` : retirer `AccountMax` et `LocalMax`.
- `.../Favorites/Commands/AddMyFavorite/AddMyFavoriteCommandHandler.cs` : supprimer les deux contrôles `itemCount >= options.Value.AccountMax` (lignes 49-51 et 72-74).
- `.../Favorites/Commands/MergeMyFavorites/MergeMyFavoritesCommandHandler.cs` : ligne 58, tous les coups de cœur locaux sont repris ; `Skipped` reste à 0.
- `.../Favorites/Common/MyFavoritesResult.cs` : retirer le champ `Maximum`.
- `.../Favorites/Queries/GetMyFavorites/GetMyFavoritesQueryHandler.cs` : lignes 51 et 181, ne plus passer `Maximum`.
- `src/Backend/Vole_Papillon_Damour.Infrastructure/DependencyInjection.cs` : lignes 124-125, retirer la validation `AccountMax`.
- Retirer `Errors.Favorites.LimitReached` s'il n'a plus d'appelant.

**Test d'abord** (`src/Backend/Vole_Papillon_Damour.Application.tests/Favorites/MemberFavoriteCommandHandlerTests.cs`) :
- l'ajout du 201e coup de cœur réussit ;
- la fusion reprend tous les coups de cœur, sans plafond.

**Commit :** `feat(favorites): lever la limite de coups de cœur`

### Tâche 2 — Pagination et recherche côté serveur (`RG-164`, `RG-165`)

**Contrat** — `GET /catalog/me/favorites` (`MemberAccountController.cs`, vers la ligne 219) :
- `page` (défaut 1), `pageSize` (défaut 10, de 1 à 50), `q` (100 caractères au plus, espaces rognés).
- Réponse : `workTotal` (œuvres, sans filtre), `matchingWorkTotal`, `page`, `pageSize`, `pageCount`, `works` (la page demandée).
- `total` (nombre de lignes) est remplacé par `workTotal`.

**Recherche** : titre, auteurs et ISBN de chaque édition, sans tenir compte de la casse ni des
accents (décomposition Unicode, suppression des marques diacritiques). Le filtre s'applique aux
œuvres après regroupement.

**Fichiers**
- `.../Favorites/Queries/GetMyFavorites/GetMyFavoritesQuery.cs` : ajouter `Page`, `PageSize`, `Query`.
- `.../Favorites/Queries/GetMyFavorites/GetMyFavoritesQueryValidator.cs` : bornes ci-dessus.
- `.../Favorites/Queries/GetMyFavorites/GetMyFavoritesQueryHandler.cs` : filtre et pagination après le regroupement.
- `.../Favorites/Common/MyFavoritesResult.cs` : nouveaux champs.
- `MemberAccountController.cs` (`ToResponse`, vers la ligne 758) : nouveaux champs de réponse.

**Tests d'abord**
- Application : `src/Backend/Vole_Papillon_Damour.Application.tests/Favorites/Queries/GetMyFavoritesQueryHandlerTests.cs` (nouveau). Vingt-cinq œuvres donnent 10 œuvres en page 1 et `pageCount` 3 ; `q=hugo` trouve Victor Hugo ; l'ISBN d'une édition secondaire trouve son œuvre.
- API : `src/Backend/Vole_Papillon_Damour.Api.tests/Favorites/FavoritesEndpointsTests.cs`, paramètres invalides refusés.

**Commit :** `feat(favorites): recherche et pagination des coups de cœur côté serveur`

### Tâche 3 — Statut « indisponible » (`RG-167`)

**Fichiers**
- `.../Favorites/Queries/GetMyFavorites/GetMyFavoritesQueryHandler.cs`, méthode `ProjectEdition` (vers la ligne 190) : `Ready`/`NoNotice` → nom du statut ; `Failed` ou absence de job → `Unavailable` ; `Pending`/`InProgress` → `Computing`.
- `.../Recommendations/Queries/GetSuggestionsForFavorites/GetSuggestionsForFavoritesQueryHandler.cs` : même mapping pour les `seeds` du visiteur.
- Vérifier que l'ajout d'un coup de cœur crée toujours un job (`FavoriteReferenceService.cs`, ligne 88). Sans job, le cas « absence de job » resterait en calcul.

**Test d'abord** : `Application.tests/Favorites/Queries/GetMyFavoritesQueryHandlerTests.cs` : job `Failed` → `Unavailable` ; pas de job → `Unavailable` ; `Pending` → `Computing`.

**Commit :** `fix(favorites): afficher les calculs en échec comme indisponibles`

### Tâche 4 — Flux SSE d'avancement, membre (`RG-166`, `RG-168`, `RG-169`, `RG-170`)

**Contrat** — `GET /catalog/me/favorites/progress/stream`, authentifié, `text/event-stream`, `Cache-Control: no-store`, `X-Accel-Buffering: no`.

Événements :
- `progress` avec `{"total": N, "ready": M}`. `total` = nombre d'œuvres, `ready` = œuvres dont aucun job n'est `Pending` ou `InProgress`. Émis au démarrage, puis à chaque changement.
- `ping` à chaque heartbeat.
- `complete` quand `ready == total`, puis fermeture.
- `timeout` après `Favorites:ProgressMaxDuration` (10 min), puis fermeture.
- Si les suggestions sont désactivées (`RG-169`) : `complete` immédiat, sans calcul. Si `RecommendationSettings.Enabled` est faux : 404, comme les autres routes.

**Fichiers**
- Extraire le regroupement (union-find et `Find`/`Union`, dans `GetMyFavoritesQueryHandler`) vers `.../Favorites/Common/FavoriteWorkGrouper.cs`, partagé par la liste et le flux. Ne pas dupliquer la logique.
- `.../Favorites/Queries/GetMyFavoriteProgress/GetMyFavoriteProgressQuery.cs` et `...Handler.cs` : lecture légère (ISBN et jobs, sans les éditions complètes).
- `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/`, sur le modèle de `LiveTableauStream.cs` : nouveau `Api/Favorites/FavoriteProgressStream.cs`.
- `MemberAccountController.cs` : nouvelle route à côté de `GetMyBookFavorites`.
- `.../Favorites/FavoritesOptions.cs` : `ProgressPollInterval` (2 s), `ProgressMaxDuration` (10 min), `ProgressHeartbeat` (15 s), liés depuis la configuration (`Favorites:`) comme les autres options.

**Tests d'abord** : `src/Backend/Vole_Papillon_Damour.Api.tests/Favorites/FavoriteProgressStreamTests.cs` (nouveau). Émission au changement seulement, `complete` à zéro, `timeout` à la durée maximale, rien à émettre si les suggestions sont désactivées.

**Commit :** `feat(favorites): flux SSE d'avancement du calcul pour le membre`

### Tâche 5 — Flux SSE d'avancement, visiteur (`Q-07`)

**Contrat** — `POST /catalog/favorites/progress/stream`, anonyme, politique de limitation `RateLimitingPolicies.PublicCatalog` comme `/catalog/favorites/suggestions` (`BookController.cs`, vers la ligne 117). Corps : `{isbn13s, minimumHearts}`, validé comme `FavoriteSuggestionsRequest`. Mêmes événements que la tâche 4.

Limite technique de la requête, selon l'arbitrage B. Aucun identifiant n'est conservé côté serveur (`RG-156`, `RG-139`).

**Fichiers** : `BookController.cs`, `Api/Favorites/FavoriteProgressStream.cs` (réutilisé), la lecture anonyme dans `Application/Favorites/Queries/...`.

**Tests d'abord** : `FavoriteProgressStreamTests.cs`, cas visiteur.

**Commit :** `feat(favorites): flux SSE d'avancement pour le visiteur`

## 6. Partie Catalog

### Tâche 6 — Retrait confirmé et immédiat (`RG-160`, `RG-161`)

**Fichiers**
- `src/Catalog/src/app/core/favorites/catalog-favorites.service.ts` : supprimer `PendingRemoval`, `UNDO_DURATION_MS`, `pendingRemovals`, `undoRemoval`, `finalizeRemoval`, `removePending`, `localPendingRemovalCount`. `remove(isbn13)` retire le livre de la liste localement, envoie aussitôt `api.withdrawAnonymousFavorite` ou `memberApi.removeFavorite`, puis relance `refresh()`. En cas d'échec, il remet le livre (`local.add` pour le visiteur, relecture pour le membre) et renseigne `currentError`.
- `catalog-favorites-page.component.ts` : supprimer `removedTitle`, `undoRemoval` et le timer de 6 s.
- `catalog-favorites-page.component.html` : supprimer le bloc `E11-annuler-retrait` (lignes 46-51) ; afficher `E11-erreur-retrait` (`role="alert"`, bouton « Fermer le message ») quand `favorites.error()` est renseigné.

**Tests d'abord** (`catalog-favorites.service.spec.ts`) : le retrait est envoyé sans attendre ; après confirmation, une relecture ne renvoie plus le livre ; si l'envoi échoue, le livre revient et l'erreur est exposée.

**Maquette :** `preview/E2-Etats.html` — zones `E11-erreur-retrait`

**Commit :** `fix(favorites): enregistrer le retrait à la confirmation`

### Tâche 7 — Fenêtre de confirmation E12 (`RG-160`)

**Fichiers**
- Nouveau composant `src/Catalog/src/app/features/favorites/favorite-removal-dialog/` (`.ts`, `.html`, `.scss`, `.spec.ts`), sur le modèle de `features/favorites/account-step-dialog/`. Entrées : `open`, `entry`. Sorties : `closed`, `confirmed`.
- Comportement : « Annuler » reçoit le focus à l'ouverture ; Échap, la croix et « Annuler » ferment sans rien changer ; le focus reste à l'intérieur et revient au bouton d'origine ; `role="dialog"`, `aria-modal="true"`, titre relié par `aria-labelledby`.
- Responsive : sous 640 px, feuille basse (maquette mobile).
- Branchements :
  - E2 : `catalog-favorites-page.component.html`, `(removeRequested)` ouvre la fenêtre au lieu d'appeler `remove`.
  - E5 : `features/book-detail/catalog-book-detail-page.component.ts`, ligne 139.
  - E6 : `features/account/purchases/account-purchases.component.ts`, ligne 122.
  - E3 : selon l'arbitrage A.

**Tests d'abord** : `favorite-removal-dialog.component.spec.ts` (focus initial, Échap, confirmation). Mettre à jour les specs des pages E2, E5 et E6.

**Maquette :** `preview/E12-Desktop.html` — zones `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer`, `E12-fermer` ; `preview/E12-Mobile.html` — zones `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer`

**Commit :** `feat(favorites): confirmer le retrait d'un coup de cœur`

### Tâche 8 — Retirer la limite côté Catalog (`RG-163`)

**Fichiers**
- `core/favorites/local-favorites.store.ts` : supprimer `MAX_FAVORITES` (ligne 16) et le refus de `add` (lignes 56-68).
- `core/favorites/catalog-favorites.service.ts` : supprimer `MAX_MEMBER_FAVORITES` (ligne 28) et les contrôles de `add` (lignes 156-158 et 180-182).
- `features/favorites/favorite-list.component.ts` et `.html` : retirer l'entrée `maximum` ; le compteur affiche le total (« 137 coups de cœur »).
- `catalog-favorites-page.component.html` : retirer `[maximum]` (ligne 41).
- `core/catalog.models.ts` : retirer `maximum` de `CatalogMemberFavoritesResponse`.

**Tests d'abord** : `local-favorites.store.spec.ts` (le 51e livre s'ajoute).

**Maquette :** `preview/E2-Desktop.html` — zone `E2-compteur`

**Commit :** `feat(favorites): retirer la limite de coups de cœur côté Catalog`

### Tâche 9 — Recherche et pagination E2 (`RG-164`, `RG-165`)

**Fichiers**
- `core/catalog-member-api.service.ts` : `getFavorites(token, {page, pageSize, q})` avec `HttpParams`.
- `core/favorites/catalog-favorites.service.ts` : signaux `query` et `page`. Modifier la recherche ramène à la page 1 ; le composant applique un délai de 250 ms avant d'appeler le service. Pour le visiteur, la même API de pagination est servie par la liste locale.
- `features/favorites/favorite-list.component.ts`, `.html`, `.scss` : champ de recherche (`role="search"`), compteur (« 4 résultats sur 137 » pendant une recherche), liste de 10 éléments, `E2-pagination` (Précédent, « Page N sur M », Suivant), état `E11-recherche-vide` avec « Effacer la recherche ».
- Au changement de page, le focus va en haut de la liste. Retirer le dernier élément d'une page ramène à la page précédente.
- Zones existantes, à conserver telles quelles : `E2-entete`, `E2-lien-menu`, `E2-bandeau-navigateur`, `E2-bouton-creer-compte`, `E2-lien-connexion`, `E2-replier-bandeau`, `E2-coups-de-coeur`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-pastille-disponibilite`, `E2-retirer`.

**Tests d'abord** : `features/favorites/favorite-list.component.spec.ts` (nouveau) : recherche insensible à la casse, pagination, retour à la page précédente.

**Maquette :** `preview/E2-Desktop.html` — zones `E2-recherche`, `E2-pagination`, `E2-coups-de-coeur`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-retirer` ; `preview/E2-Mobile.html` — zones `E2-recherche`, `E2-pagination`, `E2-compteur` ; `preview/E2-Etats.html` — zone `E11-recherche-vide`

**Commit :** `feat(favorites): recherche et pagination des coups de cœur`

### Tâche 10 — Avancement du calcul en direct (`RG-166` à `RG-170`)

**Fichiers**
- Nouveau `core/favorites/favorite-progress-stream.ts` : lecteur SSE sur `fetch` (GET avec `Authorization` pour le membre, POST pour le visiteur), `AbortController`, analyse des lignes `event:` et `data:`, reconnexion avec délai borné (500 ms à 5 s, comme `src/Website/src/app/shared/clients/sse-client.service.ts`).
- `catalog-favorites.service.ts` : remplacer le sondage (`FAVORITE_REFRESH_INTERVAL_MS`, `MAX_POLL_DURATION_MS`, `updatePolling`, `stopPolling`) par l'ouverture du flux quand un coup de cœur est en calcul. Signaux `progress` (`{total, ready}`) et `progressTimedOut`. `complete` relance `refresh()` puis ferme le flux. Fermeture au `ngOnDestroy` de la page.
- `catalog-favorites-page.component.html` : remplacer le bloc « Suggestions en cours de calcul » par `E2-avancement-calcul` (barre et « Calcul des suggestions : N sur M coups de cœur prêts »). Visible tant que `ready < total` et que les suggestions ne sont pas désactivées (`RG-169`). Après `timeout`, afficher `E11-calcul-long` (`RG-168`).
- Annonce de l'arrivée des suggestions dans une zone `aria-live` distincte, dite une seule fois.
- `favorite-list.component.html` : carte `Unavailable` → `E11-calcul-indisponible-texte` ; carte `Computing` → `E2-calcul-en-cours`.

**Tests d'abord** : `favorite-progress-stream.spec.ts` (nouveau) ; mise à jour de `catalog-favorites.service.spec.ts` (plus de sondage, flux ouvert et fermé).

**Maquette :** `preview/E2-Desktop.html` — zones `E2-avancement-calcul`, `E2-calcul-en-cours`, `E2-suggestions`, `E2-nombre-suggestions`, `E2-curseur-exigence`, `E2-carte-suggestion`, `E2-coeurs`, `E2-raison` ; `preview/E2-Etats.html` — zones `E11-calcul-en-cours`, `E11-calcul-indisponible`, `E11-calcul-indisponible-texte`, `E11-calcul-long`

**Commit :** `feat(favorites): avancement du calcul en direct`

### Tâche 11 — Message de reprise E4 (`RG-163`)

**Fichier** : `catalog-favorites-page.component.html`, lignes 10-22 : ne garder que la branche `E4-message-reprise`. Supprimer la branche `E4-message-plafond`. Le champ `skipped` reste dans le modèle, toujours à 0.

**Tests** : `catalog-favorites-page.component.spec.ts` (le message de reprise n'affiche plus de variante plafond).

**Référence :** écran E4 de `book-favorites` (`docs/features/book-favorites/maquettes/`), avec le message modifié décrit dans `maquettes/README.md`.

**Commit :** `feat(favorites): retirer la variante plafond du message de reprise`

## 7. Tâche finale — Validation et PR

1. Relancer la tâche 0 : builds Release et tests API, Application, Infrastructure et Catalog, tous verts sauf les échecs notés.
2. Vérifier à la main le scénario du bug : ajouter un coup de cœur, le retirer, recharger **immédiatement** ; le livre reste retiré.
3. Vérifier à 390 px : recherche, pagination et E12 tiennent à l'écran, sans défilement horizontal (`CA-28`).
4. Exécuter `graphify update .`.
5. Vérifier `python .claude/skills/feature/scripts/check_zones.py docs/features/favorites-management`.
6. Ouvrir la PR vers `main` avec `gh pr create`, en renvoyant vers `docs/features/favorites-management/`.
7. Mettre à jour le registre : `pwsh -NoProfile -File "$env:USERPROFILE\.agents\worktrees\manage-worktrees.ps1" status -Path "<worktree>" -Status "PR ouvert" -PrUrl "<URL>"`.

**Commit :** `chore(favorites-management): validation finale`

## 8. Couverture des zones

Chaque zone des maquettes est citée au moins une fois. Une zone n'est pas reprise ici sauf à
figurer dans la section « Zones non implémentées », avec sa raison.

| Zone | Tâche |
|---|---|
| `E2-entete`, `E2-lien-menu`, `E2-bandeau-navigateur`, `E2-bouton-creer-compte`, `E2-lien-connexion`, `E2-replier-bandeau` | 9 (inchangées) |
| `E2-coups-de-coeur`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-pastille-disponibilite`, `E2-retirer` | 9 (inchangées ou modifiées) |
| `E2-compteur`, `E2-recherche`, `E2-pagination` | 8, 9 |
| `E2-calcul-en-cours`, `E2-avancement-calcul`, `E2-suggestions`, `E2-nombre-suggestions`, `E2-curseur-exigence`, `E2-carte-suggestion`, `E2-coeurs`, `E2-raison` | 10 |
| `E11-erreur-retrait` | 6 |
| `E11-recherche-vide` | 9 |
| `E11-calcul-en-cours`, `E11-calcul-indisponible`, `E11-calcul-indisponible-texte`, `E11-calcul-long` | 10 |
| `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer`, `E12-fermer` | 7 |

## 9. Zones non implémentées

Aucune à ce stade. Si l'arbitrage A retient l'option « immédiat » pour E3, ce bouton ne
prend pas la fenêtre E12, et la raison doit être ajoutée ici.
