# Technique — architecture de l'import d'actualités

> Comment construire ce que décrivent les documents fonctionnels du dossier parent.
> Les décisions déjà tranchées portent le préfixe `DT-ACT`.

## 1. Les décisions

| # | Décision | Motif |
|---|---|---|
| `DT-ACT-01` | **Déclencheur minuteur**, pas événementiel | Aucun webhook Instagram ne couvre les publications du compte lui-même ; le webhook `feed` est réservé aux pages Facebook ([`../02`](../02-sources-et-contraintes-plateformes.md), section 3) |
| `DT-ACT-02` | La fonction vit dans **`Vole_Papillon_Damour.Worker`**, pas dans une nouvelle Function App | Le projet existe déjà, avec `Enrich` et `Sweep`, son image, son Container App, son identité managée et ses secrets. Une seconde application ne serait qu'un doublon d'infrastructure |
| `DT-ACT-03` | Le travail passe par **une commande MediatR** dans `Application`, la fonction ne contient que le réveil et la journalisation | C'est la forme de `BookEnrichmentFunction` et `BookSweepFunction`, et c'est ce qui rend le traitement testable sans hôte Functions |
| `DT-ACT-04` | `Actuality` gagne un **état** et une **trace d'origine** | Sans état, pas de brouillon (`RG-ACT-04`) ; sans trace, pas d'idempotence (`RG-ACT-02`) |
| `DT-ACT-05` | Le modèle est appelé via **`Microsoft.Extensions.AI`**, derrière une abstraction du projet | Changer de modèle ou de fournisseur ne doit toucher qu'`Infrastructure` |
| `DT-ACT-06` | **Identité managée** partout : Foundry et Key Vault. Seul le jeton Meta est un secret | Un secret de moins est un renouvellement de moins à oublier |
| `DT-ACT-07` | Le site rend l'article avec **`white-space: pre-line`** | Correctif obligatoire, indépendant de l'import : aujourd'hui `<p>{{ article.article }}</p>` avale tous les retours à la ligne |
| `DT-ACT-08` | La trace d'import est une **table à part**, pas une colonne de `Actuality` | `RG-ACT-02` exige que la trace survive à la suppression du brouillon. Une colonne disparaîtrait avec la ligne |

## 2. Domaine

### `Actuality` — ce qui change

```
Actuality (existant)
  Id, Title, Date, Article, UrlPrincipalImage,
  FacebookLink?, InstagramLink?, Images[]
+ Status              : ActualityStatus  (Draft | Published)
+ TitleNeedsReview    : bool
+ ImportedAt          : DateTimeOffset?  (null si saisie à la main)
```

Deux méthodes de comportement, pas des propriétés publiques en écriture :

- `Publish()` — passe en `Published`, refuse si l'article ou le titre est vide.
- `MarkTitleReviewed()` — retire `TitleNeedsReview`, appelé à l'enregistrement.

Les fabriques existantes `Create` et `Update` gardent leur signature et produisent
`Published` (`RG-ACT-06`) ; une fabrique `CreateImported` produit `Draft`. Le
constructeur EF public reste tel quel.

### `SocialPostImport` — nouvel agrégat

```
SocialPostImport
  Id, Source (Instagram | FacebookPage), ExternalId,
  Permalink, PublishedAt, ImportedAt,
  ActualityId?          (null après suppression du brouillon)
```

Index unique sur `(Source, ExternalId)`. C'est **la** garantie d'idempotence : elle est
en base, pas dans le code, donc elle tient même si deux passages se chevauchaient.

### Migration EF

Une seule migration, `AddActualityImportTracking` :

- `Actualities.Status` (`int`, non nul, **défaut `Published`**) — c'est ce défaut qui
  garantit qu'aucune actualité existante ne disparaît du site au déploiement ;
- `Actualities.TitleNeedsReview` (`bit`, non nul, défaut `0`) ;
- `Actualities.ImportedAt` (`datetimeoffset`, nul) ;
- table `SocialPostImports` avec son index unique ;
- index sur `Actualities (Status, Date DESC)` — toutes les lectures publiques passent
  par là.

## 3. Application

```
Application/Actuality/
  Commands/
    Background/ImportSocialActualitiesCommand.cs        (+ Handler, + Result)
    PublishActuality/PublishActualityCommand.cs         (+ Handler)
  Queries/
    GetDraftActualities/GetDraftActualitiesQuery.cs     (+ Handler)
  Common/
    CaptionCleaner.cs                                   (RG-ACT-07, pur, testable seul)
    FallbackTitle.cs                                    (RG-ACT-20)
Application/Common/Interfaces/Services/
  ISocialFeedClient.cs
  IActualityTitleGenerator.cs
```

### `ISocialFeedClient`

```
Task<IReadOnlyList<SocialPost>> GetRecentPostsAsync(CancellationToken ct);

SocialPost : ExternalId, Caption, Permalink, PublishedAt,
             Medias[] { Kind (Image|Video), ContentUrl, ThumbnailUrl }
```

L'abstraction ne parle **ni d'Instagram ni de Meta** : le jour où la source devient une
page Facebook (`L5`), seule l'implémentation change.

### `IActualityTitleGenerator`

```
Task<string?> GenerateAsync(string article, CancellationToken ct);
```

Retourne `null` en cas d'échec, de dépassement de délai, ou de titre invalide. **Ne lève
jamais.** Le repli est la responsabilité de l'appelant (`RG-ACT-20`), et c'est ce qui
rend le chemin dégradé facile à tester.

### Le handler d'import — l'ordre compte

1. `GetRecentPostsAsync` ;
2. filtre par date plancher (`RG-ACT-03`), puis par `SocialPostImports` déjà connus
   (`RG-ACT-02`), puis tri chronologique, puis plafond (`RG-ACT-17`) ;
3. pour chaque post retenu, dans une portée isolée :
   - nettoyage de la légende ;
   - **téléchargement de toutes les images d'abord**, puis téléversement — un échec ici
     abandonne le post entier avant toute écriture en base (`RG-ACT-19`) ;
   - appel au générateur de titre, repli si `null` ;
   - `CreateImported` + insertion de `SocialPostImport`, **dans la même transaction**.
4. agrégation du compte-rendu, renvoyé à la fonction pour journalisation.

Un échec sur un post n'interrompt pas la boucle : il est compté et journalisé.

## 4. Infrastructure

```
Infrastructure/Services/Social/
  InstagramFeedClient.cs        ISocialFeedClient, HttpClient typé
  InstagramOptions.cs           GraphApiVersion, UserId, AccessToken,
                                ImportFloorDate, MaxPostsPerRun, TimeoutMilliseconds
  MediaDownloader.cs            télécharge une URL de CDN vers un Stream
Infrastructure/Services/Ai/
  FoundryActualityTitleGenerator.cs   IActualityTitleGenerator
  TitleGenerationOptions.cs           Endpoint, DeploymentName, Temperature,
                                      MaxOutputTokens, TimeoutMilliseconds
  GeneratedTitleValidator.cs          contraintes de forme (05, section 3)
```

Enregistrement dans `DependencyInjection.AddInfrastructure`, sur le modèle exact des
clients bibliographiques déjà présents (`AddHttpClient<I…, …>` avec délai et
`User-Agent` issus des options).

**Le téléversement des images réutilise `IBlobService.UploadActualityImagesAsync`.**
Rien à ajouter : le conteneur `actuality-images` existe et est déjà monté dans le
Worker (`BlobSettings__ContainerActualityImagesName`).

## 5. API

Trois handlers de requête à modifier — c'est le **seul** endroit du projet où l'import
touche du code servant déjà le public :

| Endpoint | Changement |
|---|---|
| `GET /actuality/all` | Filtre `Status == Published` |
| `GET /actuality/latest` | `ActualityRepository.GetLatestActualityAsync` filtre `Status == Published` avant `Take(3)` |
| `GET /actuality/{id}` | Renvoie « non trouvée » pour un brouillon (`RG-ACT-05`) |

Trois endpoints à ajouter, tous en `RequireAuthorization("IsAdmin")` :

| Endpoint | Objet |
|---|---|
| `GET /actuality/drafts` | Liste des brouillons pour le bandeau du BackOffice |
| `POST /actuality/{id}/publish` | Passage en `Published` |
| `GET /actuality/all?includeDrafts=true` | Liste complète pour l'écran d'administration |

⚠️ `EndpointAuthorizationTests` couvre déjà l'autorisation des endpoints existants : les
trois nouveaux doivent y être ajoutés dans la même livraison.

## 6. Le Worker

```
Vole_Papillon_Damour.Worker/SocialImportFunction.cs
```

Même forme que `BookEnrichmentFunction` : `IServiceScopeFactory`, une portée par
passage, `ISender.Send`, un `LogInformation` structuré en sortie.

```
[Function("ImportSocialActualities")]
public async Task Run([TimerTrigger("%SocialImport:Schedule%")] TimerInfo timer, …)
```

Le `%…%` renvoie la cadence dans la configuration (`ENF-ACT-02`), au lieu de la figer
dans l'attribut.

`Program.cs` ne change pas : `AddApplication()` et `AddInfrastructure()` couvrent déjà
tout l'enregistrement.

## 7. Infrastructure Azure

### Key Vault

Un secret de plus, `instagram-access-token`, déclaré dans `appSecrets.module.bicep`
comme `googleBooksApiKey` (paramètre `@secure()`, valeur injectée par le pipeline via
`readEnvironmentVariable`), et référencé dans le bloc `keyVaultSecrets` du Container App
worker de `main.bicep`.

### Variables d'environnement du Worker

```
SocialImport__Schedule            0 */30 * * * *
SocialImport__GraphApiVersion     v2x.0
SocialImport__UserId              <id du compte professionnel>
SocialImport__ImportFloorDate     <date de mise en service>
SocialImport__MaxPostsPerRun      5
SocialImport__AccessToken         → secretRef: instagram-access-token
TitleGeneration__Endpoint         https://<foundry>.openai.azure.com/
TitleGeneration__DeploymentName   <nom du déploiement>
```

### Azure AI Foundry

Nouveau module `infra/modules/AiFoundry/`, sur le patron des modules existants : la
ressource, son déploiement de modèle, et l'attribution du rôle
`Cognitive Services OpenAI User` à l'identité managée du Worker (le dépôt a déjà des
modules `*.roleassignments.module.bicep` à copier).

### Alertes

Deux règles `scheduledQueryRule`, sur le groupe d'action existant :

- échecs consécutifs de la fonction (`ENF-ACT-06`) ;
- expiration proche du jeton (`ENF-ACT-05`) — la fonction émet une trace dédiée quand
  l'âge du jeton dépasse 45 jours, et la règle la surveille.

## 8. Front

### Website — une ligne

`actuality-detail.component.html` : `white-space: pre-line` sur le `<p>` de l'article
(`DT-ACT-07`). Rien d'autre. Le site ne sait pas qu'un import existe.

### BackOffice — `feature/actualities`

- bandeau « *n* brouillon(s) à relire », filtrant la liste ;
- pastille d'état sur chaque carte, et lien vers la publication d'origine pour un
  brouillon importé ;
- marqueur « titre à revoir » ;
- bouton *Publier* à côté de *Enregistrer*.

Le formulaire d'édition existant n'est pas refait.

## 9. Tests

Le dépôt impose le TDD (`.github/skills/tdd-workflow`). Les tests qui portent le risque :

| Sujet | Où | Ce qu'il faut prouver |
|---|---|---|
| Nettoyage de légende | `Application.tests` | Bloc de hashtags terminal retiré, paragraphes conservés, emoji conservés, légende vide gérée (`RG-ACT-07`, `RG-ACT-08`) |
| Idempotence | `Application.tests` | Deux passages sur le même post → une actualité. Post déjà importé puis supprimé → pas de réimport (`RG-ACT-02`) |
| Repli du titre | `Application.tests` | Générateur qui renvoie `null`, qui dépasse le délai, qui renvoie un titre invalide → actualité créée, titre de repli, marqueur posé (`RG-ACT-20`) |
| Validation du titre | `Infrastructure.tests` | Chaque contrainte de `05` section 3, une par test |
| Échec d'image | `Application.tests` | Une image en échec → **aucune** écriture en base (`RG-ACT-19`, `ENF-ACT-21`) |
| Visibilité | `Api.tests` | `all`, `latest` et `{id}` ne renvoient pas de brouillon (`RG-ACT-05`) |
| Autorisation | `Api.tests` | Les trois nouveaux endpoints exigent `IsAdmin` |
| Migration | `Infrastructure.tests` | Les actualités existantes sont `Published` après migration |
| Carrousel et vidéo | `Application.tests` | Ordre des images, vignette pour une vidéo, plafond de 10 (`RG-ACT-09`, `RG-ACT-11`, `RG-ACT-18`) |

Le client Instagram n'est **pas** testé contre l'API réelle : `HttpMessageHandler`
simulé, réponses figées en fixtures, dont les cas d'erreur (jeton expiré, quota
dépassé).

## 10. Ordre de construction

Reprend les paliers de [`../01`](../01-vision-et-perimetre.md), section 6.

| Palier | Livrable technique | Dépendances externes |
|---|---|---|
| `L1` | Domaine, migration, filtrage des lectures publiques, endpoints admin, BackOffice, `pre-line` | **Aucune** |
| `L2` | `ISocialFeedClient` + implémentation, commande d'import, fonction minuteur, secret Key Vault | `Q-ACT-01`, `Q-ACT-04`, revue d'application Meta |
| `L3` | Générateur de titre, validation, module Foundry, attribution de rôle | Ressource Foundry |
| `L4` | Alertes, brouillons dormants, garde-fous | — |
| `L5` | Client page Facebook, fonction HTTP, vérification de signature `X-Hub-Signature-256` | `Q-ACT-03` |

`L1` est livrable seul, et le sera probablement avant que Meta ait répondu. C'est
délibéré : il porte tout le risque de régression du projet, et aucun de ses risques
externes.
