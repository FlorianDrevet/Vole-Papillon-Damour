# Infrastructure Azure — Vole-Papillon-Damour

Déploiement de l'environnement `development` : un Container App Environment
hébergeant les trois applications, chacune avec son Application Insights.

## Ressources déployées

Tout est créé dans le groupe de ressources `rg-vpd-dev` (région `westeurope`).

| Ressource | Nom | Rôle |
| --- | --- | --- |
| Container App Environment | `vpd-cae-dev` | Héberge les trois Container Apps |
| Container App | `vpd-api-ca-dev` | API .NET 10, port 8080 |
| Container App | `vpd-web-ca-dev` | Website Angular SSR, port 8080 |
| Container App | `vpd-bo-ca-dev` | BackOffice Angular servi par nginx, port 8080 |
| Application Insights | `vpd-api-appi-dev` / `vpd-web-appi-dev` / `vpd-bo-appi-dev` | Un par application |
| Log Analytics | `vpd-law-dev` | Workspace commun aux trois Application Insights |
| Container Registry | `vpdacrdev` | Images poussées par les pipelines applicatives |
| Azure SQL | `vpd-sql-dev` / base `vole-papillon-damour-db` | Serverless `GP_S_Gen5_1`, pause auto après 60 min |
| Storage Account | `vpdstdev` | Conteneurs blob `loto-images`, `actuality-images`, `event-images`, `product-images` |
| Key Vault | `vpd-kv-dev` | Connection strings SQL et Storage, clé de signature JWT |
| Managed Identity | `vpd-api-id-dev` / `vpd-web-id-dev` / `vpd-bo-id-dev` | Une par application |

Chaque Container App tourne sous sa propre identité managée. Les trois ont
`AcrPull` sur le registry ; seule celle de l'API a `Key Vault Secrets User` et
`Monitoring Metrics Publisher`, puisque les fronts ne lisent aucun secret.

Les conteneurs blob sont en accès `Blob` (lecture anonyme) : `BlobService`
renvoie l'URL brute du blob au client, les images doivent donc être lisibles
sans SAS.

Le scaling est à `minReplicas: 0` : la première requête après une période
d'inactivité paie un démarrage à froid. Passer à `1` dans
`parameters/main.dev.bicepparam` si ce n'est pas acceptable.

## Configuration Azure à faire une seule fois

### 1. Enregistrer les resource providers

```bash
for provider in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights \
  Microsoft.ContainerRegistry Microsoft.Sql Microsoft.Storage \
  Microsoft.KeyVault Microsoft.ManagedIdentity; do
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

Les quatre workflows tournent dans l'environnement GitHub `development`, donc
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
| `JWT_SECRET` | clé de signature des tokens de l'API, ≥ 32 caractères aléatoires |

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

Les quatre pipelines sont en `workflow_dispatch` uniquement : rien ne part sur
Azure sans un lancement manuel.

| Workflow | Ce qu'il fait |
| --- | --- |
| `Infra - deploy` | `what-if` (défaut) ou `deploy` de `main.bicep` sur la subscription |
| `API - deploy` | build + push de l'image API, bascule de `vpd-api-ca-dev`, migrations EF optionnelles |
| `Website - deploy` | build + push de l'image Website, bascule de `vpd-web-ca-dev` |
| `BackOffice - deploy` | build + push de l'image BackOffice, bascule de `vpd-bo-ca-dev` |

### Ordre du premier déploiement

1. `Infra - deploy` en mode `what-if`, pour relire ce qui va être créé.
2. `Infra - deploy` en mode `deploy`. Les trois Container Apps démarrent sur
   l'image placeholder `containerapps-helloworld` : c'est normal, elles n'ont
   pas encore d'image applicative.
3. `API - deploy` avec `run_migrations` coché — le schéma de la base est vide
   au premier passage.
4. `Website - deploy`, puis `BackOffice - deploy`.

Les fronts doivent être déployés après l'API : le bundle Angular embarque
l'`api_url` en dur, donc la pipeline lit le FQDN de `vpd-api-ca-dev` et le
passe en `--build-arg` au moment du build de l'image.

### Redéployer l'infra sans écraser les applications

`Infra - deploy` lit l'image qui tourne actuellement sur chaque Container App et
la réinjecte dans le déploiement. Relancer l'infra ne fait donc jamais revenir
une application au placeholder.

### Migrations EF Core

`run_migrations` ouvre une règle de firewall SQL sur l'IP du runner, applique
`dotnet ef database update`, puis referme la règle même en cas d'échec. Le
reste du temps, la base n'accepte que le trafic Azure — c'est par cette règle
que les Container Apps la joignent, leurs IP de sortie n'étant pas fixes.

## Points à traiter côté application

- Une connection string Application Insights réelle a été commitée dans
  `src/Backend/Vole_Papillon_Damour.Api/appsettings.json`. Elle en a été
  retirée, mais **elle reste dans l'historique git** : la clé correspondante
  est à révoquer côté Azure.
- L'API autorise toutes les origines (`AllowAnyOrigin`). Aucune configuration
  CORS n'est donc nécessaire pour que les fronts l'appellent, mais c'est à
  resserrer avant une mise en production.
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
