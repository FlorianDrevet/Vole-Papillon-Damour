# Activer et lancer les recommandations

Ce runbook concerne l'environnement DEV et la fonction Azure `Recommendations` du
Worker. La fonctionnalité est désactivée par défaut dans Bicep. Le workflow manuel
`Infra - deploy` permet de l'activer ; il ne lance pas le calcul lui-même.

## Activer la fonctionnalité

Après fusion de la PR qui ajoute cette option :

1. Dans GitHub Actions, lancer **Infra - deploy** en mode `what-if` et laisser
   `recommendations_enabled` à `true`.
2. Vérifier le plan : `Recommendations__Enabled` passe à `true` dans l'API et le Worker,
   le Worker reçoit l'endpoint Azure OpenAI, et son identité reçoit le rôle
   `Cognitive Services OpenAI User` sur le compte existant.
3. Relancer le workflow en mode `deploy`, avec `recommendations_enabled` à `true`.

Le workflow exige le secret GitHub `TITLE_GENERATION_ACCOUNT_NAME` pour trouver le compte
Azure OpenAI partagé. La génération de titres reste indépendante : il n'est pas nécessaire
d'activer `TITLE_GENERATION_ENABLED`. Le déploiement d'infrastructure redémarre les apps
concernées avec leurs nouveaux paramètres ; il ne reconstruit pas leurs images. Il réutilise
le déploiement d'embeddings existant `text-embedding-3-small` : il ne crée ni le compte
Azure OpenAI ni ce modèle.

Pour désactiver ultérieurement les recommandations, relancer le même workflow en mode
`deploy` avec `recommendations_enabled` à `false`.

## Sorties réseau du Worker

L'environnement Container Apps utilise le réseau Azure géré par défaut, sans VNet
personnalisé, avec `internal` à `false`. Ce mode donne au Worker les sorties Internet
standard dont il a besoin pour appeler l'endpoint public Azure OpenAI. Aucun NAT Gateway,
pare-feu ou IP de sortie fixe n'est configuré. Un réseau privé ou une route sortante
personnalisée nécessiterait une architecture et des ressources Azure supplémentaires.

## Lancer une passe manuellement

Attendre d'abord que les livres soient importés et que le Worker ait au moins un réplica
prêt. La fonction est un timer Azure Functions nommé `Recommendations`; elle n'a pas de
route HTTP publique. Le lancement manuel passe par son endpoint d'administration local au
Worker.

Dans Azure CLI, lister les clés master du Container App :

```powershell
az containerapp function keys list `
  --name vpd-worker-ca-dev-fc `
  --resource-group rg-vpd-dev `
  --key-type masterKey
```

Copier la valeur `_master` depuis la sortie sans l'enregistrer dans un fichier ni la partager.
Ouvrir ensuite un shell dans le réplica du Worker :

```powershell
az containerapp exec `
  --name vpd-worker-ca-dev-fc `
  --resource-group rg-vpd-dev `
  --command bash
```

Dans ce shell, saisir la clé de façon masquée puis envoyer le POST local. L'image Worker
expose le port `8080`.

```bash
read -r -s -p 'Master key: ' key; echo
curl --fail-with-body -i -X POST \
  'http://127.0.0.1:8080/admin/functions/Recommendations' \
  -H "x-functions-key: $key" \
  -H 'Content-Type: application/json' \
  --data '{}'
unset key
```

La réponse attendue est `202 Accepted`. L'endpoint `/admin/` exige la clé master, qui donne
des droits d'administration sur le runtime Functions ; la garder secrète. La commande de
gestion des clés Container Apps est en préversion et nécessite au moins un réplica actif.
Voir la documentation Microsoft sur [l'exécution manuelle d'une fonction non HTTP](https://learn.microsoft.com/fr-fr/azure/azure-functions/functions-manually-run-non-http),
les [clés des fonctions sur Container Apps](https://learn.microsoft.com/en-us/cli/azure/containerapp/function/keys?view=azure-cli-latest)
et [l'administration des fonctions dans Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/functions-cli).

## Suivre le calcul

Dans un autre terminal, consulter le résumé de l'invocation :

```powershell
az containerapp function invocations summary `
  --name vpd-worker-ca-dev-fc `
  --resource-group rg-vpd-dev `
  --function-name Recommendations `
  --timespan 2h
```

Le journal de fin indique les candidats traités (`Candidates`), les recherches trouvées ou
absentes (`Found`, `NotFound`), puis les livres, embeddings et voisins calculés (`Books`,
`Embedded`, `Neighbors`). Le lancement retourne `202` avant la fin du calcul : attendre la
fin de l'invocation dans les journaux avant d'en démarrer une autre.

Un appel rafraîchit au plus 200 profils (`ProfileBatchSize`) puis recalcule les voisins. Si
7 748 livres n'ont encore aucun profil, il faudra environ 39 invocations séquentielles pour
les couvrir. Une première passe seule ne traite donc qu'une partie du catalogue. L'API
commencera à afficher les recommandations lorsqu'une génération exploitable existe et que
les livres candidats ont des profils suffisants.

Azure OpenAI facture l'utilisation du déploiement d'embeddings. Sur DEV, `NEXT.md` indique
que le reset catalogue est terminé mais que les lots ISBN restent à importer ; lancer la
fonction avant l'import ne produira donc pas de calcul représentatif.
