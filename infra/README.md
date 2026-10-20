# Infrastructure Azure - Vole-Papillon-Damour

## Migration France Central

`rg-vpd-dev` garde sa localisation de groupe de ressources en West Europe. Le
Bicep le reference comme groupe existant et cree en parallele les ressources
regionales avec le suffixe `-dev-fc` en France Central. Le serveur `vpd-sql-dev`
et la base `vole-papillon-damour-db` restent en France Central et ne sont pas
recrees ni deplaces.

| Ressource cible | Nom France Central |
| --- | --- |
| Container Apps Environment | `vpd-cae-dev-fc` |
| API / Website / BackOffice | `vpd-api-ca-dev-fc` / `vpd-web-ca-dev-fc` / `vpd-bo-ca-dev-fc` |
| Scan / Catalog / Worker | `vpd-scan-ca-dev-fc` / `vpd-catalog-ca-dev-fc` / `vpd-worker-ca-dev-fc` |
| Storage Account | `vpdstdevfc` |
| Key Vault | `vpd-kv-dev-fc` |
| Log Analytics | `vpd-law-dev-fc` |
| Application Insights | `vpd-<api,web,bo,scan,catalog,worker>-appi-dev-fc` |
| Managed identities | `vpd-<api,web,bo,scan,catalog,worker>-id-dev-fc` |

La migration a été basculée le 14 octobre 2026 après confirmation explicite du
propriétaire. Les anciennes ressources sont retirées; Azure finalise encore
l'environnement ACA `vpd-cae-dev`, marqué `ScheduledForDelete`. OVH pointe l'apex
et les quatre CNAME (`www`, `backoffice`, `livres`, `scan`) directement vers les
ACA France Central. L'apex et `www` redirigent en HTTPS vers `/accueil`; les trois
autres domaines répondent `200` avec des certificats valides. Aucun service CDN
OVH actif n'a été trouvé. Le worker France Central est le seul worker conservé;
le webhook ACS vise l'API France Central. Le retour vers la pile West Europe
n'est plus disponible.

Les blobs ont été synchronisés de `vpdstdev` vers `vpdstdevfc` et les 346 URL en
base ont été réécrites avant la suppression de l'ancien compte. Le workflow de
migration ponctuelle a été retiré. La base SQL n'a été ni déplacée ni
reconfigurée.

L'ACR partage `vpdacrshared` est dans `rg-core-florian`, en France Central, avec
le SKU Basic, l'admin desactive et l'acces public active comme l'ancien ACR.
`main.bicep` ne le cree pas: il recoit `containerRegistryName`,
`containerRegistryResourceGroupName` et `containerRegistrySubscriptionId`, puis
attribue les roles de lecture aux identites des ACA et les droits complets a
l'identite CI fournie. Les variables GitHub de depot sont `ACR_NAME`,
`ACR_RESOURCE_GROUP`, `ACR_SUBSCRIPTION_ID` et `ACR_CI_PRINCIPAL_ID`.

Déploiement de l'environnement `development` : un Container App Environment
hébergeant les six applications, chacune avec son Application Insights.

## Inventaire historique des ressources West Europe

Le tableau conserve les noms de l'ancienne pile pour référence. Les apps,
certificats et ressources régionales en double ont été supprimés le 14 octobre
2026; l'environnement ACA est en cours de suppression. SQL et ACS, déjà
partagés ou conservés en place, n'ont pas été supprimés.

| Ressource | Nom | Rôle |
| --- | --- | --- |
| Container App Environment | `vpd-cae-dev` | Héberge les six Container Apps |
| Container App | `vpd-api-ca-dev` | API .NET 10, port 8080 |
| Container App | `vpd-web-ca-dev` | Website Angular SSR, port 8080 |
| Container App | `vpd-bo-ca-dev` | BackOffice Angular servi par nginx, port 8080 |
| Container App | `vpd-scan-ca-dev` | App de scan Angular publique, port 8080, HTTPS |
| Container App | `vpd-catalog-ca-dev` | Catalogue public Angular SSR, port 8080, HTTPS |
| Container App | `vpd-worker-ca-dev` | Worker Azure Functions .NET isolated, `kind=functionapp` |
| Application Insights | `vpd-api-appi-dev` / `vpd-web-appi-dev` / `vpd-bo-appi-dev` / `vpd-scan-appi-dev` / `vpd-catalog-appi-dev` / `vpd-worker-appi-dev` | Un par application |
| Log Analytics | `vpd-law-dev` | Workspace commun aux six Application Insights |
| ACS Email | `vpd-acs-email-dev` / `mail.volepapillondamour.fr` | Service d'envoi, donnees en France |
| ACS Communication Service | `vpd-acs-comm-dev` | Endpoint d'envoi du Worker, domaine Email lié |
| Event Grid ACS | `vpd-acs-email-delivery-reports-dev` | Rapports de livraison vers le webhook API |
| Container Registry | `vpdacrdev` | Images poussées par les pipelines applicatives |
| Azure SQL | `vpd-sql-dev` / base `vole-papillon-damour-db` | `S0` Standard, 10 DTU, 250 Go, sans pause automatique (France Central) |
| Storage Account | `vpdstdev` | Conteneurs blob `loto-images`, `actuality-images`, `event-images`, `product-images` |
| Key Vault | `vpd-kv-dev` | Connection strings SQL et Storage, clé de signature JWT (à supprimer avec l'authentification maison, voir `infra/entra/`) |
| Managed Identity | `vpd-api-id-dev` / `vpd-web-id-dev` / `vpd-bo-id-dev` / `vpd-scan-id-dev` / `vpd-catalog-id-dev` / `vpd-worker-id-dev` | Une par application |

Chaque Container App France Central utilise sa propre identité managée `-dev-fc`.
Les six identités ont `AcrPull` sur l'ACR partagé ; l'API et le worker ont aussi
`Key Vault Secrets User`. Le worker reçoit `Communication and Email Service Owner`
sur le service ACS global. Le secret `email-bounce-webhook-secret` est écrit dans le
nouveau Key Vault par Bicep et n'est jamais exposé dans les outputs.
`communicationServiceRoleAssignmentName` reste vide afin que Bicep crée une
affectation déterministe pour l'identité Worker France Central.
L'API et le worker ont chacun `Monitoring Metrics Publisher` sur leur Application
Insights. Le worker est une Azure Function native (`kind=functionapp`). La configuration
de mesure `P1-1` vise `minReplicas: 0` et `maxReplicas: 1` pour vérifier que le timer se
réveille sans hôte chaud ; il faut revenir à une réplique minimum si l'observation de deux
heures échoue.

Les six composants Application Insights ont un plafond de 1 Go/jour via leur
ressource `pricingPlans`. Toutes les alertes Azure Monitor passent par le groupe
`vpd-alerts-dev-fc`, qui écrit **uniquement à `afdrevet@outlook.com`**
(`monitoringAlertEmail`) ; aucune notification d'infrastructure ne part vers
`volepapillondamour@sfr.fr`. Les règles couvrent le worker (heartbeat, annonces et
file d'e-mails en retard, e-mails en échec), l'import social, les performances de
l'API (5xx, P95 par opération, métadonnées lentes), le SQL lent, les exceptions
serveur répétées, les *Failure Anomalies* de l'API, du worker et du catalogue, et
des tests de disponibilité multi-régions (API `/health`, site, catalogue, scan ;
`availabilityTestsEnabled`, ~17 €/mois). En DEV, ces tests restent déclarés mais
sont désactivés et les alertes planifiées sont évaluées quatre fois par jour (`PT6H`).
Les *Failure Anomalies* utilisent leur fréquence dédiée de `PT1M`, comme dans
l'[exemple Bicep Microsoft pour `FailureAnomaliesDetector`](https://learn.microsoft.com/en-us/azure/templates/microsoft.alertsmanagement/2019-03-01/smartdetectoralertrules).
Le déploiement réel `35858128356` a confirmé que le détecteur refuse `PT5M` dans cet
environnement ; sa fréquence ne doit donc pas reprendre celle des alertes planifiées.
Les composants Website, BackOffice et Catalog utilisent aussi 25 % de sampling
d'ingestion en DEV ; l'API, le worker et la Scanette restent à 100 % pour conserver
les signaux opérationnels. Les valeurs par défaut de production restent à 100 % et
les tests de disponibilité actifs. Le classeur partagé
« VPD - Performance et santé » regroupe latences, dépendances, erreurs, worker et
front-ends. Ses onglets **Embeddings et coûts** et **Infrastructure** ajoutent les
tokens d'entrée et la durée de chaque lot Azure OpenAI, un coût estimatif, les pics de
CPU/mémoire, les réplicas/redémarrages des six Container Apps et l'utilisation DTU de
la base SQL. Le tarif estimatif par million de tokens est le paramètre Bicep
`embeddingCostUsdPerMillionTokens` (0,02 USD par défaut) ; il doit être ajusté au tarif
du modèle, de la région et du contrat Azure. Ce n'est pas une donnée de facturation.
Les spans d'embeddings n'incluent ni texte, ni ISBN, ni identifiant de livre.
La liste détaillée est dans
`docs/bourse-aux-livres/technique/11-observabilite.md` §8. Un changement de
destinataire déclenche un e-mail de confirmation d'Azure à accepter.

L'API exporte aussi les spans métier `books.metadata.resolve`,
`books.metadata.provider` et `books.scan.persist`, ainsi que leurs histogrammes de
durée. `OTEL_SERVICE_NAME=vpd-api` permet de les distinguer dans le workspace partagé.
La procédure et les requêtes Kusto de diagnostic sont dans
[`docs/bourse-aux-livres/technique/11-observabilite.md`](../docs/bourse-aux-livres/technique/11-observabilite.md).

Le module ACS envoie également `EmailSendMailOperational` et
`EmailStatusUpdateOperational` vers `vpd-law-<env>`. Ces journaux séparent l'acceptation
par ACS de la livraison au serveur destinataire et exposent, pour un identifiant de
message, le statut de remise et les codes SMTP. Une fois ce changement d'infrastructure
déployé, utiliser les requêtes du §7 de la documentation d'observabilité pour vérifier un
destinataire iCloud. Il n'y a pas de liste blanche iCloud à configurer dans ACS ; côté
iCloud, vérifier les indésirables, les règles et les expéditeurs bloqués.

Les conteneurs blob servis par URL brute sont en accès `Blob` (lecture anonyme), donc les
images doivent être lisibles sans SAS. Les couvertures issues de BnF, Open Library ou
Google Books continuent d'utiliser directement leurs URLs HTTPS vérifiées. Les images
choisies dans le catalogue administratif sont stockées dans le conteneur public
`book-covers`, déclaré par le template Bicep.

Le scaling DEV est à `minReplicas: 1` pour l'API, le Website et le Catalog. Le BackOffice
et le Scan sont à `minReplicas: 0`, `maxReplicas: 2`, avec un `cooldownPeriod` de
`10 800` secondes (trois heures) avant le retour à zéro après la dernière requête HTTP.
Les six Container Apps utilisent la [combinaison minimale documentée pour Consumption](https://learn.microsoft.com/en-us/azure/container-apps/containers)
par réplique : `0.25` vCPU et `0.5Gi` de mémoire.
Le module utilise `Microsoft.App/containerApps@2025-07-01`, qui déclare cette propriété
dans le contrat ARM.
Le premier appel HTTP réveille une réplique ; les usages rapprochés réinitialisent le délai
et évitent des démarrages à froid répétés pendant une session. Le worker reste à
`minReplicas: 0`, `maxReplicas: 1` pendant la mesure `P1-1`. Le réglage est dans
`parameters/main.dev.bicepparam`.

## Configuration Azure à faire une seule fois

### 1. Enregistrer les resource providers

```bash
for provider in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights \
  Microsoft.ContainerRegistry Microsoft.Sql Microsoft.Storage \
  Microsoft.KeyVault Microsoft.ManagedIdentity Microsoft.Communication \
  Microsoft.EventGrid Microsoft.ContainerInstance; do
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
az identity create -g rg-vpd-identity-dev -n vpd-github-deploy-id -l francecentral --query "{clientId:clientId, principalId:principalId}" -o json
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
| `ACS_EMAIL_WEBHOOK_SECRET` | secret partagé entre Event Grid et le webhook API, ≥ 1 caractère aléatoire |

C'est aussi l'endroit où activer une *required reviewer* si un déploiement doit
être approuvé avant de partir.

### 5. Vérifier les noms globalement uniques

`vpdacrshared`, `vpd-kv-dev-fc`, `vpd-sql-dev` et `vpdstdevfc` sont les noms
utilisés par la migration. L'ACR est déjà créé dans `rg-core-florian`; le serveur
SQL existant reste `vpd-sql-dev`. Le stockage et Key Vault ont des noms globaux
et doivent être disponibles avant le déploiement.

### 6. Vérifier l'ACR partagé

Le registre `vpdacrshared` existe dans `rg-core-florian` en France Central. Les
variables de dépôt GitHub `ACR_NAME`, `ACR_RESOURCE_GROUP`,
`ACR_SUBSCRIPTION_ID` et `ACR_CI_PRINCIPAL_ID` pilotent Bicep et les workflows.
Le principal CI reçoit le rôle `Owner` sur ce registre; les six identités ACA
reçoivent `AcrPull`. Les tags de `vpdacrdev` ont été importés et vérifiés dans
`vpdacrshared` avant suppression de l'ancien registre. Le workflow ponctuel de
migration ACR a été retiré.

## Pipelines

Les pipelines sont en `workflow_dispatch` uniquement : rien ne part sur
Azure sans un lancement manuel.

| Workflow | Ce qu'il fait |
| --- | --- |
| `Infra - deploy` | `what-if` (défaut) ou `deploy` de `main.bicep` sur la subscription; le seul paramètre manuel est le mode |
| `API - deploy` | build + push de l'image API dans l'ACR partagé, bascule de `vpd-api-ca-dev-fc`, migrations EF optionnelles |
| `Website - deploy` | build + push de l'image Website dans l'ACR partagé, bascule de `vpd-web-ca-dev-fc` |
| `BackOffice - deploy` | build + push de l'image BackOffice dans l'ACR partagé, bascule de `vpd-bo-ca-dev-fc` |
| `Scan - deploy` | build + push de l'image Scan avec l'URL API et l'origine `https://scan.volepapillondamour.fr`, bascule de `vpd-scan-ca-dev-fc` |
| `Catalog - deploy` | build + push de l'image Catalogue avec les URLs API/domaine et les identifiants publics GA4/Clarity, bascule de `vpd-catalog-ca-dev-fc` |
| `Worker - deploy` | build + push de l'image Functions, bascule de `vpd-worker-ca-dev-fc` et contrôle du host |
| `Books runtime - deploy` | build + push coordonné API + Worker, migration EF optionnelle avant rollout, puis bascule des deux Container Apps |
| `ACS Email - configure` | vérification DNS/amorçage ACS et réparation manuelle de compatibilité ; le câblage normal est dans Bicep |

### Étapes réalisées (référence historique)

La pile France Central a été déployée et validée avant le basculement OVH. Les
images ont été copiées dans l'ACR partagé, les blobs synchronisés et les 346 URL
SQL réécrites, puis les apps, la supervision et les workflows ont été basculés.
L'API et les cinq domaines ont été contrôlés avant suppression des doublons.
Les workflows ponctuels ACR et Storage ont été retirés après leur usage.

Les fronts doivent être déployés après l'API : le bundle Angular embarque
l'`api_url` en dur, donc la pipeline lit le FQDN de `vpd-api-ca-dev-fc` et le
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

Le câblage ACS runtime (secret Key Vault, identité Worker, paramètres
`BookAlerts__Email__*`, Event Grid et attente du handshake webhook) est désormais
dans `infra/main.bicep`. Le workflow `ACS Email - configure` reste un outil de
vérification/amorçage DNS ; il ne doit pas être utilisé comme source de vérité
pour les paramètres des Container Apps.

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
Azure Container Apps. Son domaine canonique est
`https://scan.volepapillondamour.fr` ; le FQDN ACA reste disponible comme adresse
technique de secours. La caméra est activée seulement après autorisation du site par
Safari.

Le worker est une Azure Function native sur Container Apps (`kind=functionapp`). Il
utilise l'outbox SQL, le stockage Azure pour `AzureWebJobsStorage`, les secrets Key Vault
et l'identité managée dédiée. Pendant `P1-1`, sa cible est `minReplicas: 0`,
`maxReplicas: 1` pendant la mesure du timer sur deux heures.
