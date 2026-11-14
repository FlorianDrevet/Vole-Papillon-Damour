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
- Après le merge de la PR #394, `Books runtime - deploy` #43627546776 et `Catalog - deploy` #43627561852 ont réussi. Le contrôle public répond maintenant `/health` 200 et `/catalog/me/favorites` 401 sans authentification; la route protégée ne répond plus 404.
- Cause dans le binding .NET : un tableau vide sur `RecommendationOptions` supprimait les valeurs par défaut attendues lors d’une construction directe. Le modèle conserve maintenant ses valeurs par défaut; l’enregistrement DI vide le tableau juste avant `Bind` pour éviter la concaténation, puis `PostConfigure` restaure les défauts si la section est absente. Aucun changement de migration ou Bicep n’est requis.
- La [PR #394](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/394) sur `fix/book-favorites-production` corrige les textes/états vides Catalog, place le consentement e-mail uniquement dans les préférences du profil et limite les recherches de coups de cœur aux livres français. Son déploiement API/Catalog est terminé.
- La CI #43627523795 du merge #394 a échoué sur `RecommendationOptionsTests.DefaultsToOneSimilarBookAndAllowsTwentyResults` (seuils par défaut absents) et sur le test Application d’annulation d’un appel isolé pendant une recherche partagée. La branche `fix/ci-book-favorites-after-merge-20261114` restaure les défauts d’options et vide le tableau juste avant le binding; deux tests vérifient les seuils configurés sans duplication et le repli si la section manque. Le test concurrent attend désormais l’annulation du premier appel avant de terminer le travail partagé, sans délais fixes.
- Validation du correctif CI : `RecommendationOptionsTests` 3/3, Infrastructure.tests 255/255, Application.tests 759/759, builds Release API/Worker réussis, Graphify actualisé. Le test d’annulation passe 10/10 exécutions ciblées et la suite Application complète; aucun nouveau déploiement n’a été lancé par Codex.

## Scan, validation et livraison

- `ScanCatalogBook.favoriteCount` est optionnel pour accepter le cache hors ligne existant. `ScanVerdictService` somme le livre et les éditions liées de façon forte; les liens d’œuvre faibles ne contribuent pas et le verdict garde la même priorité.
- Le bandeau E10 est affiché en Tri et Consultation, jamais en Caisse. Les bundles déjà en cache affichent 0 jusqu’à leur synchronisation.
- Vérifier avec `dotnet build/test src/Backend/Vole_Papillon_Damour.slnx`, `npm test` et `npm run build` depuis `src/Catalog` et `src/Scan`, plus `az bicep build --file infra/main.bicep`. Docker indisponible signifie que les parcours Aspire/Keycloak restent à refaire localement avant validation runtime.
- Les seuils de cœurs et `SimilarMinScore` demandent une revue d’environ 50 suggestions sur le vrai catalogue. Aucun déploiement ni migration distante n’a été effectué lors de l’implémentation initiale.
