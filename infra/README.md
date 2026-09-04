# Infrastructure Azure - Vole-Papillon-Damour

Déploiement de l'environnement `development` : un Container App Environment
hébergeant les cinq applications, chacune avec son Application Insights.

## Ressources déployées

Tout est créé dans le groupe de ressources `rg-vpd-dev` (région `westeurope`).

| Ressource | Nom | Rôle |
| --- | --- | --- |
| Container App Environment | `vpd-cae-dev` | Héberge les cinq Container Apps |
| Container App | `vpd-api-ca-dev` | API .NET 10, port 8080 |
| Container App | `vpd-web-ca-dev` | Website Angular SSR, port 8080 |
| Container App | `vpd-bo-ca-dev` | BackOffice Angular servi par nginx, port 8080 |
| Container App | `vpd-scan-ca-dev` | App de scan Angular publique, port 8080, HTTPS |
| Container App | `vpd-worker-ca-dev` | Worker Azure Functions .NET isolated, `kind=functionapp` |
| Application Insights | `vpd-api-appi-dev` / `vpd-web-appi-dev` / `vpd-bo-appi-dev` / `vpd-scan-appi-dev` / `vpd-worker-appi-dev` | Un par application |
| Log Analytics | `vpd-law-dev` | Workspace commun aux cinq Application Insights |
| ACS Email | `vpd-acs-email-dev` / `mail.volepapillondamour.fr` | Service d'envoi, donnees en France |
| Container Registry | `vpdacrdev` | Images poussées par les pipelines applicatives |
| Azure SQL | `vpd-sql-dev` / base `vole-papillon-damour-db` | `S1` Standard, 20 DTU, 250 Go, sans pause automatique (France Central) |
| Storage Account | `vpdstdev` | Conteneurs blob `loto-images`, `actuality-images`, `event-images`, `product-images`, `book-covers` |
| Key Vault | `vpd-kv-dev` | Connection strings SQL et Storage, clé de signature JWT (à supprimer avec l'authentification maison, voir `infra/entra/`) |
| Managed Identity | `vpd-api-id-dev` / `vpd-web-id-dev` / `vpd-bo-id-dev` / `vpd-scan-id-dev` / `vpd-worker-id-dev` | Une par application |

Chaque Container App tourne sous sa propre identité managée. Les cinq ont
`AcrPull` sur le registry ; l'API et le worker ont en plus `Key Vault Secrets User`.
L'API et le worker ont chacun `Monitoring Metrics Publisher` sur leur Application
Insights. Le worker est une Azure Function native (`kind=functionapp`). La configuration
de mesure `P1-1` vise `minReplicas: 0` et `maxReplicas: 1` pour vérifier que le timer se
réveille sans hôte chaud ; il faut revenir à une réplique minimum si l'observation de deux
heures échoue.

Les cinq composants Application Insights ont un plafond de 1 Go/jour via leur
ressource `pricingPlans`. Les alertes worker (heartbeat absent, annonces dues en
retard, file d'e-mails en retard) sont envoyées au groupe Azure Monitor
`vpd-alerts-dev`, vers l'adresse de contact du projet. Le premier déploiement
nécessite la confirmation du destinataire envoyée par Azure.

Les conteneurs blob sont en accès `Blob` (lecture anonyme) : `BlobService`
renvoie l'URL brute du blob au client, les images doivent donc être lisibles
sans SAS.

Le scaling est à `minReplicas: 1` pour l'API, le Website, le BackOffice et le Scan ; le
worker reste à `minReplicas: 0`, `maxReplicas: 1` pendant la mesure `P1-1`. Les quatre
applications HTTP évitent ainsi un démarrage à froid, tandis que le worker est observé
comme hôte planifié sans réplique chaude. Le réglage est dans
`parameters/main.dev.bicepparam`.

## Configuration Azure à faire une seule fois

### 1. Enregistrer les resource providers

```bash
for provider in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights \
  Microsoft.ContainerRegistry Microsoft.Sql Microsoft.Storage \
  Microsoft.KeyVault Microsoft.ManagedIdentity Microsoft.Communication; do
  az provider register --namespace "$provider"
done
```

### 2. Créer l'identité utilisée par GitHub (OIDC, sans secret)

Le tenant Entra ID interdit `az ad app create` aux utilisateurs standard
(*Insufficient privileges*). On passe donc par une identité managée
user-assigned, qui accepte les federated credentials sans aucun droit annuaire
et que `azure/login` traite exactement comme une app registration.

```bash
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

az group create -n rg-vpd-identity-dev -l westeurope
az identity create -g rg-vpd-identity-dev -n vpd-github-deploy-id -l westeurope   --query "{clientId:clientId, principalId:principalId}" -o json
```

Deux rôles au niveau **subscription** sont nécessaires. `Role Based Access
Control Administrator` est indispensable : `main.bicep` attribue lui-même
`AcrPull` et `Key Vault Secrets User` aux identités des Container Apps.

```bash
PRINCIPAL_ID=<principalId retourné ci-dessus>

az role assignment create --assignee-object-id "$PRINCIPAL_ID"   --assignee-principal-type ServicePrincipal   --role "Contributor" --scope "/subscriptions/$SUBSCRIPTION_ID"

az role assignment create --assignee-object-id "$PRINCIPAL_ID"   --assignee-principal-type ServicePrincipal   --role "Role Based Access Control Administrator" --scope "/subscriptions/$SUBSCRIPTION_ID"
```

### 3. Déclarer la federated credential

Les workflows applicatifs et d'infrastructure tournent dans l'environnement GitHub `development`, donc
le `subject` doit viser l'environnement, pas une branche :

```bash
az identity federated-credential create   --name github-development   --identity-name vpd-github-deploy-id   --resource-group rg-vpd-identity-dev   --issuer https://token.actions.githubusercontent.com   --subject "repo:<OWNER>/<REPO>:environment:development"   --audiences api://AzureADTokenExchange
```

### 4. Créer l'environnement et les secrets GitHub

Dans *Settings → Environments*, créer `development`, puis y ajouter :

| Secret | Valeur |
| --- | --- |
| `AZURE_CLIENT_ID` | le `clientId` de l'identité managée, étape 2 |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
| `SQL_ADMIN_LOGIN` | login administrateur SQL, par exemple `vpdadmin` |
| `SQL_ADMIN_PASSWORD` | mot de passe SQL (≥ 12 caractères, 3 des 4 classes majuscule/minuscule/chiffre/spécial) |
| `JWT_SECRET` | clé de signature des tokens de l'API, ≥ 32 caractères aléatoires. **Voué à disparaître** : l'authentification passe à Entra External ID (voir `entra/README.md`) |

C'est aussi l'endroit où activer une *required reviewer* si un déploiement doit
être approuvé avant de partir.

### 5. Vérifier les noms globalement uniques

`vpdacrdev`, `vpd-kv-dev`, `vpd-sql-dev` et `vpdstdev` doivent être libres à
l'échelle d'Azure. En cas de collision, changer le préfixe `vpd` dans
`main.bicep` et répercuter les valeurs `REGISTRY_NAME` / `SQL_SERVER` des
workflows.

### 6. Vérifier la région autorisée pour Azure SQL

La subscription n'a pas le droit de provisionner Azure SQL partout, et la
restriction n'apparaît qu'à la création (`ProvisioningDisabled`) : `az provider
show` liste des régions qui sont en fait refusées. `sqlLocation` vaut
`francecentral`, validé sur cette subscription ; le reste de l'environnement
est en `westeurope`. Pour tester une autre région :

```bash
az sql server create -n vpd-sql-probe-$RANDOM -g rg-vpd-identity-dev   -l <region> -u probeadmin -p "<mot-de-passe-sans-caractère-!>"
```

## Pipelines

Les pipelines sont en `workflow_dispatch` uniquement : rien ne part sur
Azure sans un lancement manuel.

| Workflow | Ce qu'il fait |
| --- | --- |
| `Infra - deploy` | `what-if` (défaut) ou `deploy` de `main.bicep` sur la subscription |
| `API - deploy` | build + push de l'image API, bascule de `vpd-api-ca-dev`, migrations EF optionnelles |
| `Website - deploy` | build + push de l'image Website, bascule de `vpd-web-ca-dev` |
| `BackOffice - deploy` | build + push de l'image BackOffice, bascule de `vpd-bo-ca-dev` |
| `Scan - deploy` | build + push de l'image Scan avec l'URL API, bascule de `vpd-scan-ca-dev` et publication HTTPS |
| `Worker - deploy` | build + push de l'image Functions, bascule de `vpd-worker-ca-dev` et contrôle du host |
| `Books runtime - deploy` | build + push coordonné API + Worker, migration EF optionnelle avant rollout, puis bascule des deux Container Apps |

### Ordre du premier déploiement

1. `Infra - deploy` en mode `what-if`, pour relire ce qui va être créé.
2. `Infra - deploy` en mode `deploy`. Les cinq Container Apps démarrent sur
   l'image placeholder `containerapps-helloworld` : c'est normal, elles n'ont
   pas encore d'image applicative.
3. `Books runtime - deploy` avec `run_migrations` coché : les migrations sont
   appliquées avant le rollout de l'API et du Worker.
4. `Scan - deploy`, puis `Worker - deploy`.
5. `Website - deploy`, puis `BackOffice - deploy` si leurs images doivent aussi être
   reconstruites sur cette branche.

Les fronts doivent être déployés après l'API : le bundle Angular embarque
l'`api_url` en dur, donc la pipeline lit le FQDN de `vpd-api-ca-dev` et le
passe en `--build-arg` au moment du build de l'image.

### Google Analytics 4 du Website

La pipeline `Website - deploy` lit la variable d'environnement GitHub
`GOOGLE_ANALYTICS_MEASUREMENT_ID` et la passe au build Docker du Website. Cette
variable doit contenir l'identifiant GA4 au format `G-XXXXXXXXXX` dans
l'environnement GitHub `development`. Si elle est absente, le tag reste
désactivé. Le chargement côté navigateur reste conditionné au consentement
« mesure d'audience » de la bannière de cookies.

### Redéployer l'infra sans écraser les applications

`Infra - deploy` lit l'image qui tourne actuellement sur chaque Container App et
la réinjecte dans le déploiement. Relancer l'infra ne fait donc jamais revenir
une application au placeholder.

### Migrations EF Core

`run_migrations` ouvre une règle de firewall SQL sur l'IP du runner, applique
`dotnet ef database update`, puis referme la règle même en cas d'échec. Le
reste du temps, la base n'accepte que le trafic Azure - c'est par cette règle
que les Container Apps la joignent, leurs IP de sortie n'étant pas fixes.
En production, l'API ne lance pas les migrations au démarrage ; seul l'environnement
`Development` conserve cette commodité locale. Le workflow runtime applique donc le
schéma avant de créer la nouvelle révision.

## Points à traiter côté application

- Une connection string Application Insights réelle a été commitée dans
  `src/Backend/Vole_Papillon_Damour.Api/appsettings.json`. Elle en a été
  retirée, mais **elle reste dans l'historique git** : la clé correspondante
  est à révoquer côté Azure.
- L'API lit `Cors:AllowedOrigins` et utilise les FQDN publics dev déclarés dans
  `main.dev.bicepparam`. Une liste vide conserve `AllowAnyOrigin` uniquement pour le
  développement local ; ne pas déployer cette valeur en production.
- Le Website et le BackOffice envoient leur télémétrie navigateur via
  `@microsoft/applicationinsights-web`, initialisé dans `main.ts`. La
  connection string est figée dans le bundle au build de l'image, comme
  l'`api_url` : une image construite hors pipeline n'envoie rien, plutôt que
  d'échouer.

## Travailler en local

```bash
# Compiler le template
az bicep build --file infra/main.bicep --outfile out/main.json

# Compiler les paramètres (les secrets viennent des variables d'environnement)
az bicep build-params --file infra/parameters/main.dev.bicepparam --outfile out/params.json
```

## Scan public et worker Functions

Le Scan est servi par nginx sur le port `8080` avec ingress externe et TLS géré par
Azure Container Apps. Son FQDN HTTPS peut être ouvert directement dans Safari sur un
iPhone ; aucune redirection DNS ni tunnel depuis le poste de développement n'est
nécessaire. La caméra est activée seulement après autorisation du site par Safari.

Le worker est une Azure Function native sur Container Apps (`kind=functionapp`). Il
utilise l'outbox SQL, le stockage Azure pour `AzureWebJobsStorage`, les secrets Key Vault
et l'identité managée dédiée. Pendant `P1-1`, sa cible est `minReplicas: 0`,
`maxReplicas: 1` pendant la mesure du timer sur deux heures.
