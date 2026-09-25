# 06 — Tutoriel : déployer `text-embedding-3-small` dans Foundry pour le benchmark

> **24 septembre 2026.** Objectif : rendre le modèle d'embedding appelable depuis ce
> poste, pour lancer `python -m bench.run --provider azure`. Durée : 15 minutes environ.
> Coût du benchmark : environ **0,002 $**.

> **État au 24 septembre 2026 : fait.** Sur le compte `vpd-actuality-title-dev` (groupe
> `rg-vpd-dev`), le déploiement `text-embedding-3-small` a été créé (GlobalStandard,
> capacité 50). Le rôle *Cognitive Services OpenAI User* a été attribué au compte de
> Florian, sur ce compte Foundry seulement. Ces deux changements **ne sont pas dans le
> Bicep** (voir §10).

## 0. Ce qui existe déjà

D'après `docs/actualites-reseaux-sociaux/NEXT.md` et `infra/modules/AiFoundry/aiFoundry.module.bicep` :

- un compte **Azure OpenAI / Foundry** existe dans l'abonnement de l'association, région
  **`francecentral`**, avec le déploiement `actuality-title` (`gpt-4.1-nano`, SKU
  `GlobalStandard`) ;
- son nom est dans la variable GitHub `TITLE_GENERATION_ACCOUNT_NAME` ;
- **l'authentification par clé est désactivée** (`disableLocalAuth: true`) : l'accès se
  fait uniquement par **Microsoft Entra ID**, c'est-à-dire avec un rôle Azure.

On **ajoute un déploiement** à ce compte : pas de nouvelle ressource, pas de nouveau
coût fixe. Les embeddings se paient au token.

## 1. Prérequis

- Azure CLI installé (`az version`).
- Un compte de l'annuaire de l'association avec, sur le compte Foundry ou son groupe de
  ressources : **Contributeur** (pour créer le déploiement) et **Propriétaire** ou
  **Administrateur de l'accès utilisateur** (pour vous attribuer un rôle).

## 2. Ouvrir une session Azure séparée de celle du travail

Votre Azure CLI est connecté à l'abonnement de votre entreprise. Pour ne pas mélanger,
utilisez un **profil CLI dédié**. La variable `AZURE_CONFIG_DIR` le rend indépendant, sans
toucher à votre session habituelle.

```powershell
$env:AZURE_CONFIG_DIR = "$HOME\.azure-vpd"
az login --tenant <id-ou-domaine-du-locataire-de-l-association>
az account set --subscription "<nom-ou-id-de-l-abonnement-de-l-association>"
az account show --query "{abonnement:name, utilisateur:user.name}" -o table
```

Cette variable ne vaut que pour la fenêtre PowerShell en cours. **Gardez cette fenêtre
ouverte** jusqu'à la fin du tutoriel, ou redéfinissez la variable dans chaque nouvelle
fenêtre.

## 3. Retrouver le compte Foundry

```powershell
az cognitiveservices account list `
  --query "[?kind=='OpenAI'].{nom:name, groupe:resourceGroup, region:location, endpoint:properties.endpoint}" `
  -o table
```

Notez **`nom`**, **`groupe`** et **`endpoint`** (de la forme
`https://<nom>.openai.azure.com/`). Dans la suite, remplacez `<compte>` et `<groupe>`.

## 4. Déployer le modèle

```powershell
az cognitiveservices account deployment create `
  --name <compte> `
  --resource-group <groupe> `
  --deployment-name text-embedding-3-small `
  --model-name text-embedding-3-small `
  --model-version 1 `
  --model-format OpenAI `
  --sku-name GlobalStandard `
  --sku-capacity 50
```

- **`--sku-capacity 50`** = 50 000 tokens par minute. C'est une limite de débit, **pas un
  coût** : on ne paie que les tokens consommés. Le benchmark en consomme environ 80 000,
  donc une à deux minutes.
- **`GlobalStandard`** est le type déjà utilisé par `actuality-title`. Le traitement peut
  avoir lieu hors de France. Seules des **métadonnées de livres** sont envoyées, aucune
  donnée personnelle. Pour rester en Europe, remplacez par `--sku-name DataZoneStandard`.
- Si la commande refuse pour quota, vérifiez-le :
  `az cognitiveservices usage list --location francecentral -o table`.

**Alternative par le portail** : [ai.azure.com](https://ai.azure.com) → sélectionner le
compte → *Modèles + points de terminaison* → *Déployer un modèle* → *Déployer le modèle
de base* → `text-embedding-3-small` → nom du déploiement `text-embedding-3-small`, type
*Global Standard*.

Vérifier :

```powershell
az cognitiveservices account deployment list --name <compte> --resource-group <groupe> `
  --query "[].{deploiement:name, modele:properties.model.name, etat:properties.provisioningState}" -o table
```

## 5. Vous donner le droit d'appeler le modèle

L'authentification par clé est coupée : c'est votre identité qui doit avoir le rôle
**Cognitive Services OpenAI User** sur le compte. Le Worker l'a déjà ; pas vous.

```powershell
$compteId = az cognitiveservices account show --name <compte> --resource-group <groupe> --query id -o tsv
$moi = az ad signed-in-user show --query id -o tsv
az role assignment create --assignee-object-id $moi --assignee-principal-type User `
  --role "Cognitive Services OpenAI User" --scope $compteId
```

Le rôle met **5 à 10 minutes** à s'appliquer. Une erreur `401` ou `403` juste après est
normale.

## 6. Configurer le benchmark

Dans `tools/recommendation-benchmark/`, copiez `.env.example` en `.env.local` (fichier
ignoré par git) et complétez :

```ini
AZURE_OPENAI_ENDPOINT=https://<compte>.openai.azure.com/
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-3-small
AZURE_OPENAI_API_KEY=
```

`AZURE_OPENAI_API_KEY` reste vide : le script obtient un jeton Entra ID par le biais de
votre session `az login`.

## 7. Tester, puis lancer

Dans **la même fenêtre** PowerShell (celle où `AZURE_CONFIG_DIR` est défini) :

```powershell
cd tools/recommendation-benchmark
.\.venv\Scripts\python -m bench.check
```

Attendu : `OK : un vecteur de 1536 dimensions, … tokens`. Puis :

```powershell
.\.venv\Scripts\python -m bench.run --provider azure
```

Le rapport est écrit dans `out/rapport-azure.md`.

## 8. Ce dont j'ai besoin pour lancer le benchmark moi-même

Si vous préférez que je le lance et que je rédige l'analyse, il me faut seulement :

1. les étapes 2 à 6 faites **sur ce poste** ;
2. le chemin du profil CLI (`$HOME\.azure-vpd` si vous gardez celui du tutoriel), pour que
   j'utilise **la même session** que vous. Je n'ai besoin d'aucun secret : le jeton est
   obtenu par Azure CLI.

## 9. Dépannage

| Symptôme | Cause probable | Remède |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT manquant` | `.env.local` absent ou vide | Étape 6 |
| `401 PermissionDenied` / `403` | Rôle pas encore propagé, ou attribué sur un autre compte | Attendre 10 min ; vérifier l'étape 5 |
| `DeploymentNotFound` / `404` | Nom de déploiement différent dans `.env.local` | Comparer avec la liste de l'étape 4 |
| `AzureCliCredential … az login` | Nouvelle fenêtre sans `AZURE_CONFIG_DIR` | Redéfinir la variable (étape 2) |
| `429` | Débit dépassé | Le script réessaie seul ; sinon augmenter `--sku-capacity` |
| `InsufficientQuota` au déploiement | Quota de la région épuisé | Capacité plus faible (10), ou `DataZoneStandard` |

## 10. Et pour la production, plus tard

Ce déploiement manuel sert au benchmark. Si l'approche est retenue, il faudra le décrire
dans le Bicep (`infra/modules/AiFoundry/`, qui ne gère aujourd'hui qu'un seul
déploiement), comme le reste de l'infrastructure. Un déploiement Bicep incrémental ne
supprime pas un déploiement créé à la main : les deux peuvent coexister d'ici là.
