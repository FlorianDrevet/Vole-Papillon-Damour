# Coups de cœur et suggestions personnalisées

## Parcours Catalog

- La page `/trouver-mon-prochain-livre` est une route client rendue par `CatalogFavoritesPageComponent`; l’accueil expose une entrée et les suggestions activées par `Recommendations:Enabled`.
- `CatalogFavoritesService` orchestre les déclarations anonymes, la reprise de liste lors de la création de compte, les préférences membre, le calcul de suggestions et les états d’indisponibilité.
- `LocalFavoritesStore` conserve les entrées anonymes dans le navigateur, sans identifiant de visiteur transmis avec la liste. Les plafonds sont 50 entrées locales et 200 entrées membre.
- Les composants de feature vivent dans `src/Catalog/src/app/features/favorites/`; le panneau d’ajout recherche d’abord le catalogue. ISBNdb n’est utilisé qu’au flux d’ajout/éditions supplémentaires, jamais pendant la recherche E3.
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

## Scan, validation et livraison

- `ScanCatalogBook.favoriteCount` est optionnel pour accepter le cache hors ligne existant. `ScanVerdictService` somme le livre et les éditions liées de façon forte; les liens d’œuvre faibles ne contribuent pas et le verdict garde la même priorité.
- Le bandeau E10 est affiché en Tri et Consultation, jamais en Caisse. Les bundles déjà en cache affichent 0 jusqu’à leur synchronisation.
- Vérifier avec `dotnet build/test src/Backend/Vole_Papillon_Damour.slnx`, `npm test` et `npm run build` depuis `src/Catalog` et `src/Scan`, plus `az bicep build --file infra/main.bicep`. Docker indisponible signifie que les parcours Aspire/Keycloak restent à refaire localement avant validation runtime.
- Les seuils de cœurs et `SimilarMinScore` demandent une revue d’environ 50 suggestions sur le vrai catalogue. Aucun déploiement ni migration distante n’a été effectué lors de l’implémentation initiale.
