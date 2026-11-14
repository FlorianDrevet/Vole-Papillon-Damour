# Handoff Codex — Gestion des coups de cœur

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Sur « Trouver mon prochain livre », un visiteur ou un membre peut retirer un coup de cœur
après confirmation, et le retrait reste acquis même après un rechargement immédiat. Il peut
aussi chercher et parcourir sa liste par pages, sans limite de nombre. Pendant le calcul des
suggestions, il voit l'avancement en direct, et les livres apparaissent sans recharger la page.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-favorites-management`
- Branche : `feat/favorites-management`, qui contient déjà la conception (spec, maquettes,
  plan). La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/favorites-management/01-fonctionnel.md` : **la spec, elle fait foi**
   (`RG-160` à `RG-170`).
2. `docs/features/favorites-management/02-plan.md` : **le plan à exécuter** (tâche 0,
   tâches 1 à 11, tâche finale, couverture des zones).
3. `docs/features/favorites-management/maquettes/README.md`, puis
   `maquettes/preview/index.html`. Les écrans E4, E5 et E6 n'ont pas de maquette propre :
   leurs maquettes sont dans `docs/features/book-favorites/maquettes/`, avec les changements
   listés dans `maquettes/README.md`.
4. Skills du repo : `cqrs-feature` et `dotnet-patterns` (API), `angular-patterns` et
   `ui-ux-front-saas` (Catalog), `tdd-workflow` et `xunit-unit-testing` (tests).
5. Contexte fonctionnel précédent : `docs/features/book-favorites/01-fonctionnel.md`
   (`RG-130` à `RG-159`, toujours valables sauf `RG-131` et le délai de `RG-134`) et
   `.github/memory/21-book-favorites.md`.
6. Référence SSE : `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauStream.cs`
   et `src/Backend/Vole_Papillon_Damour.Api/Controllers/AssoEventsController/EventsController.cs`
   (ligne 306). Le pattern `X-Accel-Buffering: no` y est déjà appliqué.

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 6 — retrait confirmé | `E2-Etats.html` | `E11-erreur-retrait` |
| 7 — fenêtre E12 | `E12-Desktop.html`, `E12-Mobile.html` | `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer`, `E12-fermer` |
| 8 — sans limite | `E2-Desktop.html` | `E2-compteur` |
| 9 — recherche et pagination | `E2-Desktop.html`, `E2-Mobile.html`, `E2-Etats.html` | `E2-recherche`, `E2-pagination`, `E11-recherche-vide` |
| 10 — avancement en direct | `E2-Desktop.html`, `E2-Etats.html` | `E2-avancement-calcul`, `E2-calcul-en-cours`, `E11-calcul-en-cours`, `E11-calcul-indisponible`, `E11-calcul-indisponible-texte`, `E11-calcul-long` |

Largeurs de vérification : **390 px et 1440 px**. À 390 px, la recherche, la pagination et
E12 (en feuille basse) tiennent à l'écran sans défilement horizontal de la page.

## Spécifique à cette feature

- **Constantes non négociables** : 10 coups de cœur par page ; `pageSize` entre 1 et 50 ;
  recherche limitée à 100 caractères ; sondage serveur toutes les 2 secondes ; durée maximale
  d'un flux 10 minutes (`Favorites:ProgressMaxDuration`) ; heartbeat à 15 secondes ;
  500 ISBN au plus par requête du flux visiteur ; délai de saisie de 250 ms avant la recherche ;
  reconnexion du flux avec un délai entre 500 ms et 5 s.
- **Textes** : reprendre les libellés des maquettes **mot pour mot**, en particulier
  « Calcul des suggestions : N sur M coups de cœur prêts », « Suggestions indisponibles pour
  ce livre », « Le retrait n'a pas pu être enregistré. Réessayez. ».
- **Interdits** :
  - aucune migration de base (les trois tables restent inchangées) ;
  - ne pas modifier la logique de calcul du Worker (`ProcessBookRecommendations`) ; le flux ne
    lance aucun calcul ;
  - aucun appel ISBNdb ajouté (ni dans la recherche, ni dans la pagination, ni dans le flux) ;
  - pas d'hexadécimal en dur dans les composants Catalog : utiliser les variables du catalogue ;
  - pas de `EventSource` pour le flux membre, qui ne peut pas envoyer `Authorization` (utiliser
    le lecteur `fetch` de la tâche 10).
- **Flags / configuration** : aucun nouveau flag. `RecommendationSettings.Enabled` reste le
  commutateur existant ; les nouvelles options de flux sont dans `FavoritesOptions`, avec les
  valeurs ci-dessus par défaut.
- **Azure / infra** : aucun déploiement, aucune commande `az` en écriture, aucune nouvelle
  ressource. Le déploiement sera fait par l'utilisateur, dans cet ordre : API (Books runtime),
  puis Catalog.

## PR

- Titre : `feat(favorites): confirmed removal, search, pagination and live computation progress`
- « Risks and follow-up » :
  - Les échecs déjà présents relevés à la tâche 0 sont à lister tels quels, sans les corriger.
  - `E2-Mobile.html` ne reprend pas la préférence e-mail du membre (zone inchangée, hors
    périmètre de cette feature).
  - Déploiement API avant Catalog ; sinon l'avancement en direct ne fonctionne pas.
  - Le flux visiteur est découpé à 500 ISBN : vérifier le comportement au-delà sur une liste
    réelle.
  - Les valeurs de sondage et de durée sont des valeurs de départ, à ajuster après mesure.
