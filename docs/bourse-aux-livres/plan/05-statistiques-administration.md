# Statistiques d'administration

Cette tranche complète l'espace d'administration de `src/Catalog` avec une seule entrée
latérale **Statistiques** et deux onglets : **Statistiques par bourse** et
**Statistiques des bénévoles**. Le sous-onglet de qualité des données et l'écran 4C de la
maquette sont explicitement hors périmètre.

## Décision d'architecture

La vue bénévole ne peut pas être rendue correctement avec les seuls endpoints privés
existants : `GET /scan/me/statistics` ne connaît qu'un bénévole à la fois et est protégé
par `ScanVolunteer`. Un endpoint d'administration est donc nécessaire :

`GET /books/admin/volunteers/stats?from=&to=&fairId=`

Il est protégé par la policy `Administration` et renvoie une agrégation typée unique :

- synthèse de l'équipe (scans, gardés, vendus, temps et sessions) ;
- contribution de chaque bénévole (rôles déduits de l'activité, temps de tri/caisse,
  volume, écoulement et stock en attente) ;
- activité mensuelle pour la heatmap, renouvellement à 90 jours et genres dominants.

L'agrégation est calculée en une lecture des sessions et mouvements, puis les corrections
de vente sont déduites par leur lien `ReversalOfMovementId`. Les dates sont filtrées en
UTC ; les durées de caisse et le stock « en attente » restent des indicateurs reconstruits,
pas une nouvelle vérité comptable. Aucune migration n'est nécessaire.

## Étapes

1. Ajouter le query CQRS, les résultats Application, les contrats HTTP et la route
   `GET /books/admin/volunteers/stats`.
2. Exposer le contrat dans `CatalogAdminApiService` et ajouter les modèles TypeScript.
3. Regrouper la navigation Catalog sous `Statistiques`, puis construire les deux onglets
   avec les filtres de période et de bourse.
4. Vérifier les corrections de vente, le filtrage période/bourse, les états vides et le
   comportement responsive desktop/mobile.

## Vérification manuelle

Avec un compte portant le rôle `Administration`, ouvrir `/administration`, sélectionner
`Statistiques`, puis vérifier que les deux onglets affichent des données ou un état vide
lisible. Le changement de période et de bourse doit rafraîchir l'endpoint sans exposer de
données à un compte bénévole. La sidebar ne doit afficher qu'une entrée `Statistiques`.
