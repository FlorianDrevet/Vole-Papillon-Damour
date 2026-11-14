# Coups de cœur et suggestions personnalisées

## Parcours Catalog

- La page `/trouver-mon-prochain-livre` est une route client rendue par `CatalogFavoritesPageComponent`; l’accueil expose une entrée et les suggestions activées par `Recommendations:Enabled`.
- `CatalogFavoritesService` orchestre les déclarations anonymes, la reprise de liste lors de la création de compte, les préférences membre, le calcul de suggestions et les états d’indisponibilité.
- `LocalFavoritesStore` conserve les entrées anonymes dans le navigateur, sans identifiant de visiteur transmis avec la liste. Les plafonds sont 50 entrées locales et 200 entrées membre.
- Les composants de feature vivent dans `src/Catalog/src/app/features/favorites/`; le panneau d’ajout recherche d’abord le catalogue. ISBNdb n’est utilisé qu’au flux d’ajout/éditions supplémentaires, jamais pendant la recherche E3.
- La recherche des livres à ajouter ne garde que les résultats dont la langue est explicitement française. Le catalogue expose `language`; BnF lit le champ UNIMARC `101$a`; Open Library retourne la langue des éditions; la recherche ISBNdb publique demande `language=fr`. Un résultat sans langue française vérifiée est écarté. Le filtre est propre aux coups de cœur et ne change pas la recherche bibliographique partagée par Catalog et l’administration.
- Les préférences membre comprennent l’exigence minimale de cœurs, l’activation des suggestions et le consentement séparé à l’e-mail de bourse.

## Domaine, API et persistance

- `Domain/FavoriteBookAggregate/MemberFavoriteBook` représente les éditions choisies par un membre; `AnonymousFavoriteCount` conserve un agrégat par ISBN plutôt qu’une identité anonyme.
- `Book.IsFavoriteReference` distingue une fiche de référence créée pour un coup de cœur. L’arrivée d’un exemplaire la transforme en fiche ordinaire. Les références ne doivent pas apparaître dans la recherche publique, les voisins ni le sitemap.
- Les handlers CQRS sont sous `Application/Favorites/` et `Application/Recommendations/`. Les déclarations anonymes utilisent `/catalog/favorites/declarations` et `/catalog/favorites/suggestions`; les opérations membre sont sous `/catalog/me/favorites` et `/catalog/me/recommendations`.
- La migration additive `20261114004906_AddBookFavorites` ajoute les tables et colonnes nécessaires. Elle doit être appliquée par `Books runtime - deploy` avec `run_migrations: true`; ne pas l’appliquer à une base distante depuis un poste de développement.

## E-mail avant la bourse

- `FairDigestCampaign` sélectionne nouveautés et retours, conserve les consentements et déduplique l’envoi par bourse et membre. Le Worker utilise `FairDigestFunction`; le contenu et le rendu vivent dans `Application/FairDigest/` et `Infrastructure/Services/FairDigest/`.
- L’administration Catalog appelle les routes `/books/admin/fair-digest`, `/preview`, `/test`, `/suspend` et `/resume`. L’aperçu est isolé dans un iframe `sandbox`.
- `FairDigest:Enabled` reste `false` par défaut. Ne pas appeler l’envoi de test pendant une validation; examiner l’aperçu avec une vraie bourse avant activation publique.

## Suivi de mise en production — 2026-11-14

- La PR #392 est fusionnée. `Books runtime - deploy` #43618534632 et `Catalog - deploy` #43618559675 ont réussi; la migration `AddBookFavorites` a été appliquée par le runtime. L’utilisateur a ensuite lancé `Infra - deploy` #43621670567, également réussi.
- Après ce déploiement, `vpd-api-ca-dev-fc--0000032` (`vpd-api:aa39dce`) est `Unhealthy` / `ActivationFailed`, avec 100 % du trafic et un conteneur en `CrashLoopBackOff`; la dernière révision saine est `0000030` (`vpd-api:b852687`). Les journaux donnent `OptionsValidationException` sur `Recommendations:HeartThresholds`, et `GET /catalog/me/favorites` répond toujours 404.
- Cause dans le binding .NET : l’array prérempli dans `RecommendationOptions` est concaténé aux cinq valeurs de `appsettings.json`, puis échoue à la validation des cinq seuils distincts. La correction initialise l’array vide avant le binding et applique les seuils par défaut après le binding si aucune valeur n’existe; elle ne demande pas une nouvelle migration ni un changement Bicep.
- La [PR #394](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/394) sur `fix/book-favorites-production` corrige aussi les textes/états vides Catalog, place le consentement e-mail uniquement dans les préférences du profil et limite les recherches de coups de cœur aux livres français. Après fusion, déployer la nouvelle image API avec `Books runtime - deploy` (sans migration puisque la migration est déjà appliquée), déployer Catalog, puis vérifier l’état sain de la révision et la route favorites authentifiée.
- Validation locale de cette branche : builds production Catalog, API et Worker réussis; le build Worker signale 55 avertissements dans des fichiers non modifiés et 0 erreur; Graphify mis à jour; capture headless du Catalog à 390 et 1 440 px sans débordement horizontal. Les tests automatisés n’ont pas été exécutés. Aucun correctif Azure ou nouveau déploiement n’a été fait par Codex.

## Scan, validation et livraison

- `ScanCatalogBook.favoriteCount` est optionnel pour accepter le cache hors ligne existant. `ScanVerdictService` somme le livre et les éditions liées de façon forte; les liens d’œuvre faibles ne contribuent pas et le verdict garde la même priorité.
- Le bandeau E10 est affiché en Tri et Consultation, jamais en Caisse. Les bundles déjà en cache affichent 0 jusqu’à leur synchronisation.
- Vérifier avec `dotnet build/test src/Backend/Vole_Papillon_Damour.slnx`, `npm test` et `npm run build` depuis `src/Catalog` et `src/Scan`, plus `az bicep build --file infra/main.bicep`. Docker indisponible signifie que les parcours Aspire/Keycloak restent à refaire localement avant validation runtime.
- Les seuils de cœurs et `SimilarMinScore` demandent une revue d’environ 50 suggestions sur le vrai catalogue. Aucun déploiement ni migration distante n’a été effectué lors de l’implémentation initiale.
