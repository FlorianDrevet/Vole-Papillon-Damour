# Handoff Codex — Coups de cœur et suggestions personnalisées

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Tout visiteur, avec ou sans compte, peut déclarer ses **coups de cœur**, y compris des
livres jamais passés à la bourse. Il reçoit des suggestions de livres disponibles qui leur
ressemblent, notées de 1 à 5 cœurs, sur une nouvelle page « Trouver mon prochain livre » et
en tête de l'accueil. Les membres qui l'acceptent reçoivent un **e-mail trois jours avant
chaque bourse**, piloté depuis l'administration. Au tri, la **scanette** signale qu'un
livre a déjà plu à des lecteurs de la bourse.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-book-favorites`
- Branche : `feat/book-favorites`. Elle contient déjà la conception : spec, maquettes et
  plan. La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/book-favorites/01-fonctionnel.md` : **la spec, elle fait foi**
   (`RG-130` à `RG-159`, décisions §10).
2. `docs/features/book-favorites/02-plan.md` : **le plan à exécuter** (tâches 0 à 25).
3. `docs/features/book-favorites/maquettes/README.md`, puis
   `maquettes/preview/index.html` (22 écrans, 94 zones).
4. Skills du repo : `cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `tdd-workflow`, `angular-patterns`, `ui-ux-front-saas`, `local-auth-testing`.
5. `docs/recommandations-livres/07-specification.md` : le moteur de recommandations
   existant, que la feature réutilise.
6. `docs/features/isbndb-migration/01-fonctionnel.md` : les règles ISBNdb (`RG-100` à
   `RG-129`) qui s'appliquent à la création des fiches de référence.
7. `docs/features/book-alert-email-template/01-fonctionnel.md` : le gabarit d'e-mail
   (`RG-90` à `RG-99`) que réutilise l'e-mail avant la bourse.

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 12 | `E8-Email600.html`, `E8-Email390.html`, `E11-Etats.html` | `E8-*`, `E11-email-sans-nouveautes` |
| 16 | `E3-Desktop.html`, `E3-Mobile.html`, `E11-Etats.html` | `E3-*`, `E11-plafond-liste`, `E11-livre-introuvable` |
| 17 | `Main.html`, `E2-Mobile.html`, `E11-Etats.html` | `E2-*`, `E11-e2-vide`, `E11-calcul-en-cours`, `E11-aucune-suggestion`, `E11-suggestions-desactivees`, `E11-stockage-indisponible`, `E11-annuler-retrait` |
| 18 | `E1-Desktop.html`, `E1-Mobile.html`, `E11-Etats.html` | `E1-*`, `E11-encart-entree`, `E11-ne-plus-afficher` |
| 19 | `E5-Desktop.html`, `E5-Mobile.html`, `E6-Desktop.html`, `E6-Mobile.html` | `E5-*`, `E6-*` |
| 20 | `E4-Desktop.html`, `E4-Mobile.html` | `E4-*` |
| 21 | `E7-Desktop.html`, `E7-Mobile.html` | `E7-*` |
| 22 | `E9-Desktop.html`, `E9-Mobile.html` | `E9-*` |
| 23 | `E10-Garder.html`, `E10-Trop.html`, `E10-HorsLigne.html` | `E10-*` |

La liste exacte des zones de chaque tâche est dans sa ligne « Maquette » du plan.

Largeurs de vérification : 390 et 1440 px pour le Catalog ; 390 px pour la scanette ; 600 et
390 px pour l'e-mail (rendu via l'aperçu d'administration).

## Spécifique à cette feature

- **Constantes non négociables.**
  - Plafonds : 50 coups de cœur dans un navigateur, 200 dans un compte ; 20 créations de
    fiche par visiteur et par jour ; 300 crédits ISBNdb « Coups de cœur » par jour ; au plus
    50 ISBN par requête de suggestions anonyme.
  - Exigence : 1 à 4 cœurs minimum, défaut 3. Cœurs : paliers
    `0.50,0.58,0.66,0.74,0.82`, avec 1 palier de plus si au moins 2 coups de cœur
    contribuent, plafond 5.
  - E-mail : J-3 à 10 h heure de Paris ; au plus 8 nouveautés et 6 retours ; envoi si au
    moins 1 nouveauté ou au moins 3 retours ; un seul envoi par bourse et par membre.
  - Consentement à l'e-mail jamais pré-coché.
  - Couleurs : cœur `#c8364e` ; bandeau de la scanette `#d6304f`, distinct du verdict
    `#b02a33`.
- **Interdits.**
  - Appeler ISBNdb pendant la recherche de E3 (seulement à l'ajout ou sur « Voir d'autres
    éditions »).
  - Modifier le verdict de tri ou sa priorité.
  - Montrer une fiche de référence dans le catalogue public (recherche, œuvre, fiche,
    plan du site, voisins).
  - Proposer une fiche de référence comme voisin d'un autre livre.
  - Stocker un identifiant de visiteur anonyme avec ses ISBN.
  - Envoyer un vrai e-mail pendant la validation.
  - Modifier les colonnes existantes de `Books` autrement que par ajout.
- **Flags / configuration.** Section `Recommendations` étendue (`HeartThresholds`,
  `DefaultMinimumHearts`), sections `Favorites` et `FairDigest` du plan.
  `FairDigest:Enabled = false` par défaut. Toute la feature reste derrière
  `Recommendations:Enabled`.
- **Azure / infra.**
  - Ajouter les paramètres `FairDigest:*` dans `infra/` (valeurs par défaut).
  - `az bicep build` seulement.
  - Aucun déploiement, aucune commande `az` en écriture, aucune migration appliquée à une
    base distante.

## PR

- Titre : `feat(catalog): add favorite books, heart-rated suggestions and pre-fair email`
- « Risks and follow-up » :
  - les paliers de cœurs et `SimilarMinScore` sont à calibrer sur le vrai catalogue
    (relecture d'une cinquantaine de suggestions) ;
  - activer `FairDigest:Enabled` seulement après une revue de l'aperçu sur une vraie
    bourse et la mise à jour de la page RGPD ;
  - la migration `AddBookFavorites` est additive ; à appliquer avec `Books runtime -
    deploy` ;
  - les données de la scanette déjà en cache n'ont pas `favoriteCount` (0) jusqu'à la
    prochaine synchronisation complète ;
  - les écarts au plan et les échecs de la tâche 0.
