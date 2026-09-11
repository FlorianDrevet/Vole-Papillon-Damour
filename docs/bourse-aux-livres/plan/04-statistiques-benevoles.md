# Statistiques bénévoles — plan et tranche livrée

## Intention

La maquette décrit une même lecture personnelle dans deux contextes : une vue compacte
dans l'application de scan et une vue plus posée dans le Catalog. Elle est visible après
connexion par un bénévole ayant le rôle `Tri` ou `Caisse`, sans créer un espace public ni
une vue d'administration.

Le principe retenu est une seule source de vérité côté API : les statistiques sont
reconstruites depuis les sessions, le ledger `BookMovements`, les fiches de livres, les
bourses, le profil local et les alertes effectivement envoyées. Le client ne transmet
jamais un identifiant de bénévole à calculer.

## Contrat et autorisation

| Élément | Décision |
|---|---|
| Endpoint | `GET /scan/me/statistics` |
| Autorisation | policy `ScanVolunteer`, donc rôle Entra `Tri` ou `Caisse` |
| Identité | claim `oid` / object identifier, lu côté API |
| Réponse | `VolunteerStatisticsResponse`, avec blocs `scan` et `cash` |
| Persistance | aucune table ni migration supplémentaire |
| Cache mobile | IndexedDB, store `session`, clé `volunteer-statistics`, protégé par `accountId` |

Le bloc `scan` fournit les volumes, durées, taux de gardés, douze mois, moments de la
semaine, impact, genres et six dernières sessions. Le bloc `cash` fournit ventes brutes
et nettes, annulations/corrections, ventilation par bourse, pic horaire, titres et genres
les plus vendus, rapprochement tri-caisse et part de recette quand la recette de la bourse
est renseignée.

## Données estimées

Les horodatages de ventes ne donnent pas une durée de présence exacte : la durée de caisse
est reconstruite entre première et dernière vente par bourse et journée, avec un plancher
de quinze minutes. La cadence, le rapprochement entre tri et caisse, la part de recette et
le lien entre un tri et un lecteur sont donc marqués comme estimés dans le contrat et dans
l'interface. La recette ne constitue pas un prix par livre et aucun classement entre
bénévoles n'est affiché.

## Étapes

1. **Contrat backend** — records Application/Contracts, query CQRS, agrégations isolées et
   tests de projection/isolation/annulation.
2. **Accès API** — route `GET /scan/me/statistics`, mapping typé et régression de policy.
3. **Client Scan** — appel API, fallback IndexedDB par compte, état hors ligne, route
   `/statistiques` et lien depuis l'accueil ; Tri et Caisse partagent l'écran et peuvent
   basculer de rôle si le jeton porte les deux rôles.
4. **Client Catalog** — détection des rôles, troisième onglet `Ma contribution` dans le
   compte connecté, rendu desktop/mobile de la vue détaillée.
5. **Validation manuelle après livraison** — avec un compte `Tri`, un compte `Caisse` et
   un compte portant les deux rôles : vérifier l'isolation, le renouvellement de session,
   la présence du dernier snapshot hors ligne, les valeurs `estimé`, les douze mois et le
   repli sans ventes/tri. Vérifier aussi les viewports mobiles de l'application et du
   Catalog après déploiement.

## État de cette tranche

Les étapes 1 à 4 sont implémentées dans la branche dédiée. Les tests backend et
ChromeHeadless couvrent les nouveaux contrats, services, route, cache et écrans. Aucune
migration, ressource Azure, configuration Entra ou donnée de bourse n'a été modifiée ; le
smoke connecté réel reste une vérification post-merge.
