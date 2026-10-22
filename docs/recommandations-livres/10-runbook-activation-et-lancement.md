# Activer et lancer les recommandations

Ce runbook couvre l’activation, le premier calcul complet du catalogue et le suivi
quotidien. Les recommandations ne sont plus recalculées par un timer : une clôture de
session de scan met les livres concernés dans une queue Azure durable.

## Activer la fonctionnalité

Après fusion et déploiement de la PR :

1. Dans GitHub Actions, lancer **Infra - deploy** en mode `what-if` avec
   `recommendations_enabled` à `true`.
2. Vérifier que `Recommendations__Enabled` passe à `true` dans l’API et le Worker, que le
   Worker reçoit l’endpoint Azure OpenAI et que son identité reçoit le rôle
   `Cognitive Services OpenAI User`.
3. Vérifier que le plan crée la queue `book-recommendations` dans le compte Storage.
4. Relancer le workflow en mode `deploy` avec `recommendations_enabled` à `true`.

Le workflow exige le secret GitHub `TITLE_GENERATION_ACCOUNT_NAME` pour retrouver le
compte Azure OpenAI partagé. La génération de titres reste indépendante : il n’est pas
nécessaire d’activer `TITLE_GENERATION_ENABLED`. Le déploiement réutilise le déploiement
d’embeddings `text-embedding-3-small` déjà présent ; il ne crée ni le compte Azure OpenAI
ni le modèle.

Pour désactiver ultérieurement les recommandations, relancer le workflow en mode `deploy`
avec `recommendations_enabled` à `false`.

## Calculer le catalogue existant

Dans le Back Office, ouvrir **Catalogue et alertes → Recommandations**, puis sélectionner
**Rattraper le catalogue** et confirmer. L’API répond `202 Accepted` après avoir mis le
travail en file ; aucun paramètre ni clé Azure Functions n’est nécessaire.

Le Worker parcourt les livres visibles par pages de 250 ISBN et met les livres à traiter
dans la queue par groupes d’au plus 50. Il ignore les recommandations déjà terminées. Le
calcul continue en arrière-plan même si le portail est fermé. La liste permet de filtrer
par ISBN, titre, auteur et statut, de voir l’étape et la dernière erreur, et de relancer
un livre en échec. Un traitement affiché « En cours » depuis plus d’une heure peut aussi
être relancé si un message a fini dans la file poison.

Les statuts sont :

- `En attente` : le livre attend la queue ; les métadonnées bibliographiques peuvent
  encore être complétées ;
- `En cours` : lecture de la notice, préparation de l’embedding ou calcul des liens ;
- `Terminé` : embedding et liens de livres similaires enregistrés ;
- `Notice indisponible` : aucune notice exploitable n’a été trouvée ;
- `Échoué` : le portail affiche l’étape et l’erreur, puis permet la relance.

Si Enrich complète ensuite le résumé d’un livre qui attendait ses métadonnées, ce livre
est automatiquement remis en file. Une modification manuelle du titre, des auteurs, du
genre, de l’œuvre ou du résumé fait de même.

## Traitement des nouvelles sessions

À la clôture d’une session, chaque ISBN concerné reçoit un état persistant. Les livres
déjà terminés ne sont pas remis en file. Les autres sont traités par la même queue, sans
timer de recommandations. Si les métadonnées d’un ISBN sont encore en attente, le job
reste consultable dans le portail et repart quand Enrich termine.

Un embedding est réutilisé tant que le texte de profil ou la version du modèle n’ont pas
changé. Le service d’embedding est appelé quand le texte change ou lorsqu’un essai
précédent n’a pas produit de vecteur enregistré. Le calcul des liens peut s’exécuter à
nouveau sans recalculer les vecteurs. Si la version du modèle change sans changer le nom
du déploiement Azure OpenAI, ajuster `recommendationsEmbeddingModelVersion` au déploiement
suivant pour invalider explicitement le cache.

Les suggestions personnalisées restent calculées à partir des achats du membre, comme
auparavant. Elles ne dépendent pas d’un timer de recommandations ni de sa liste « Ma
sélection ».

## Sorties réseau du Worker

L’environnement Container Apps utilise le réseau Azure géré par défaut, sans VNet
personnalisé, avec `internal` à `false`. Ce mode donne au Worker les sorties Internet
standard dont il a besoin pour appeler l’endpoint public Azure OpenAI. Aucun NAT Gateway,
pare-feu ou IP de sortie fixe n’est configuré. Un réseau privé ou une route sortante
personnalisée nécessiterait des ressources Azure supplémentaires.
