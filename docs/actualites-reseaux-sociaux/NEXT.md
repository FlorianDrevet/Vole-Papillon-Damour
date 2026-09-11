# NEXT — où en est l'import des actualités depuis les réseaux sociaux

> **À lire en premier en arrivant sur ce sujet. À mettre à jour en dernier avant de le
> quitter**, même en pleine étape.
>
> Ce fichier porte **ce que git ne sait pas** : l'état du compte Instagram, de
> l'application Meta, de la revue d'application, du jeton, de la ressource Foundry, et
> les tests manuels passés. Les étapes, elles, sont dans les paliers de
> [`01-vision-et-perimetre.md`](01-vision-et-perimetre.md), section 6.
>
> Le [`NEXT.md`](../../NEXT.md) de la racine ne couvre **que** la bourse aux livres. Les
> deux sujets avancent séparément.

---

## En un coup d'œil

| | |
|---|---|
| **Palier en cours** | Mise en service externe de la v1 après implémentation des paliers `L1` à `L4` |
| **Prochaine action** | Répondre à `Q-ACT-01` (compte Instagram professionnel) et `Q-ACT-04` (propriétaire de l'application Meta), puis fournir l'identifiant du compte, le jeton et la date plancher au déploiement ; le déploiement Azure reste à confirmer |
| **Ce qui est prêt dans le code** | Brouillons, lectures publiques filtrées, relecture/publication BackOffice, import Instagram minuté, copie des médias, idempotence, titres Foundry avec repli, alertes et garde-fou de volume |
| **Dernière machine** | Windows — `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-facebook-news-pr122` |
| **Dernière mise à jour** | 2026-09-10 — implémentation `L1` à `L4`, CI et `what-if` Azure validés ; aucun déploiement Azure appliqué |
| **Branche** | `delivery/pr-122` — worktree dédié, livraison vers la PR `#122` (`docs/actualites-import-reseaux-sociaux`) |

---

## Ressources externes encore à créer

Le dépôt contient désormais le code et les modules de déploiement, mais aucune ressource
Meta, Key Vault, Foundry ou Azure Monitor n'a été créée ou modifiée par cette livraison.
Voici la liste à cocher avant l'activation réelle de l'import.

| Ressource | Où | État | Bloque |
|---|---|---|---|
| Compte Instagram **professionnel** | Application Instagram, réglages du compte | **Non vérifié** | Tout (`Q-ACT-01`) |
| Business Manager au nom de l'association | business.facebook.com | **Non créé** | `Q-ACT-04` |
| Application Meta | developers.facebook.com | **Non créée** | `L2` |
| Revue d'application, accès avancé à `instagram_business_basic` | developers.facebook.com | **Non demandée** | `L2` |
| Jeton longue durée, posé dans Key Vault sous `instagram-access-token` | Azure Key Vault du projet | **Non créé** | `L2` |
| Identifiant du compte Instagram (`SocialImport__UserId`) | Graph API, après authentification | **Inconnu** | `L2` |
| Version de la Graph API à figer | Configuration du Worker | **Fixée dans le code à `v22.0` ; à confirmer au moment de l'activation** | `L2` |
| Ressource Azure AI Foundry + déploiement de modèle | Abonnement Azure du projet | **Module Bicep prêt, ressource non créée** | `L3` |
| Rôle `Cognitive Services OpenAI User` pour l'identité managée du Worker | Azure | **Attribution Bicep prête, rôle non attribué** | `L3` |
| Règles d'alerte (jeton, authentification, échecs répétés) | Azure Monitor, groupe d'action existant | **Modules Bicep prêts, règles non créées** | `L4` |
| Page Facebook de l'association | facebook.com | **Non créée**, et pas décidée (`Q-ACT-03`) | `L5` |

---

## Les décisions prises

Ce que la spécification a déjà tranché, et qui n'attend plus personne.

| Sujet | Décision |
|---|---|
| Source lue en v1 | **Instagram seul.** Le profil Facebook personnel n'est pas lisible par API pour cet usage (`02`, section 2) |
| Déclencheur | **Minuteur, toutes les 30 minutes.** Aucun webhook Instagram ne couvre les publications du compte lui-même (`DT-ACT-01`) |
| Chemin d'authentification | **Instagram Login**, qui n'exige pas de page Facebook et n'accroche pas le projet au compte d'une personne |
| Où vit la fonction | Dans **`Vole_Papillon_Damour.Worker`** existant, à côté de `Enrich` et `Sweep` (`DT-ACT-02`) |
| Publication | **Brouillon obligatoire.** Rien n'est visible sur le site sans validation humaine (`RG-ACT-04`) |
| Images | **Recopiées** dans `actuality-images`. Aucune URL de CDN Meta en base : elles expirent (`RG-ACT-10`) |
| Titre | **Modèle classe nano** via Azure AI Foundry, identité managée, repli par date si échec (`05`) |
| Scraping | **Jamais**, quelle que soit la difficulté rencontrée (`ENF-ACT-15`) |
| Trace d'import | **Table à part**, pour survivre à la suppression du brouillon (`DT-ACT-08`) |

---

## Ce qui attend encore une réponse

| Question | Attendue de | Bloquant ? |
|---|---|---|
| `Q-ACT-01` — le compte Instagram est-il professionnel ? | L'association, en trois écrans | **Oui**, activation impossible sans réponse |
| `Q-ACT-02` — le compte est-il relié à une page Facebook ? | L'association | Non si l'on prend Instagram Login |
| `Q-ACT-04` — qui détient l'application Meta ? | Le bureau | **Oui**, avant de créer l'application |
| `Q-ACT-03` — crée-t-on une page Facebook ? | Le bureau | Non pour la v1 ; conditionne le palier `L5` |
| `Q-ACT-08` — le jeton se renouvelle-t-il tout seul ? | Arbitrage technique, à prendre au palier `L4` | Non |
| `Q-ACT-05`, `Q-ACT-06`, `Q-ACT-07` | Se répondent **après** avoir vu de vrais imports | Non |

---

## Les mesures à faire, qui ne se décident pas

| À mesurer | Quand | Pourquoi |
|---|---|---|
| Part de **reels et vidéos** dans les publications des six derniers mois | Avant `L2` | Si elle est majoritaire, la v1 perd beaucoup de son intérêt (`Q-ACT-07`, `RG-ACT-11`) |
| Part de publications **sans image** | Avant `L2` | Elles ne produiront aucune actualité (`RG-ACT-12`) |
| **Taux de titres conservés tels quels** sur les dix premières actualités importées | Après `L3` | Décide si la génération vaut son coût, ou si le repli par date suffit (`05`, section 8) |
| Fréquence réelle de publication | Après `L2` | Ajuste la cadence du minuteur (`ENF-ACT-02`) |

---

## Les tests manuels à passer, et par qui

Les tests automatisés locaux sont passés ; les essais avec une vraie publication et les
vérifications visuelles restent à faire après configuration. Trois échappent au
développeur seul.

| Test | Qui | Quand |
|---|---|---|
| Une actualité existante reste visible après la migration | Développeur | **Tests de modèle et défaut `Published` passés ; vérification DEV à faire** |
| Un brouillon n'apparaît nulle part sur le site public, y compris par son URL directe | Développeur | **Handlers filtrés et testés ; vérification HTTP DEV à faire** |
| Une légende à plusieurs paragraphes s'affiche correctement sur mobile et desktop | Développeur | **Code `pre-line` livré ; test navigateur à faire** |
| Le parcours complet : publier sur Facebook → voir le brouillon → publier → voir le site | **Avec la présidente**, sur une vraie publication | `L2` |
| Le brouillon rend le contrôle du droit à l'image praticable : les images sont assez grandes pour décider | **Un administrateur non développeur** | `L2` |
| Les titres proposés sont acceptables sur de vrais contenus | **Un administrateur non développeur** | `L3` |
| L'alerte d'expiration du jeton arrive bien, et à quelqu'un qui sait quoi en faire | Développeur, puis destinataire réel | `L4` |

---

## Journal

### 2026-09-10 — implémentation des paliers L1 à L4

La branche de la PR `#122` contient maintenant l'implémentation dans l'ordre du plan :

- `L1` : `ActualityStatus`, brouillons importés, migration `AddSocialActualityImport`,
  trace `SocialPostImport`, filtrage des lectures publiques, endpoints `drafts` et
  `publish`, relecture BackOffice et rendu `pre-line` ;
- `L2` : `InstagramFeedClient` officiel, fonction `ImportSocialActualities` minutée,
  nettoyage de légende, téléchargement préalable de tous les médias, copie Blob,
  plafond configurable, idempotence et secret Key Vault conditionnel ;
- `L3` : générateur `Microsoft.Extensions.AI` branché sur Azure OpenAI/Foundry par
  identité managée, validation mécanique du titre et repli par date ;
- `L4` : alerte de jeton proche de l'expiration, alerte d'authentification, détection de
  trois échecs consécutifs, garde-fou de cinq posts et signalement des brouillons de
  plus de trente jours.

La validation locale couvre le build de la solution .NET, `90` tests Domain, `191`
tests Application, `94` tests Infrastructure, `15` tests API, le build Worker, les
deux compilations Bicep, le contrat de bootstrap BackOffice et les builds Angular ; le
Website pré-rend ses 25 routes. Les avertissements de budget Angular et CommonJS du
dépôt restent présents. Aucune migration, ressource Azure, configuration Meta,
déploiement ou test avec un vrai compte n'a été effectué.

La source v1 reste **Instagram professionnel uniquement**. Une publication Facebook
recopiée vers Instagram est donc récupérable via le compte Instagram ; un profil
Facebook personnel ne l'est pas. La Page Facebook, le webhook `feed` et la vérification
`X-Hub-Signature-256` restent le palier `L5`, conditionné à `Q-ACT-03`.

### 2026-09-10 — validation de livraison et prévisualisation Azure

Le câblage des paramètres sociaux et Foundry a été ajouté aux deux étapes du workflow
`Infra - deploy`, puis poussé sur la PR dans `a8df4dd`. Les deux exécutions CI de la
tête de branche sont vertes ; la PR `#122` est sans conflit et prête à être fusionnée,
mais reste ouverte.

Le workflow `Infra - deploy` a été lancé depuis GitHub Actions en mode `what-if` sur
`development` (`run 34528967017`). La prévisualisation est passée sans erreur et n'a
appliqué aucune modification. Les secrets `INSTAGRAM_*` et `TITLE_GENERATION_*` de
l'environnement GitHub restent vides : seuls les changements conditionnels liés à la
configuration sociale apparaissent donc dans la prévisualisation. Aucun compte Meta,
jeton, ressource Foundry, règle sociale Azure Monitor ou migration de base n'est encore
activé.

### 2026-09-10 — spécification écrite

Dossier [`docs/actualites-reseaux-sociaux/`](README.md) créé dans le worktree
`feat-facebook-news-sync` : note à la présidente, vision et paliers, contraintes des
plateformes, parcours, 23 règles métier, titre généré, 27 exigences non fonctionnelles,
questions ouvertes, architecture technique.

Deux constats ont retourné la demande initiale, tous deux vérifiés dans la documentation
Meta et sourcés dans [`02`](02-sources-et-contraintes-plateformes.md) :

- le **profil Facebook personnel** n'est pas lisible par API pour cet usage — les usages
  autorisés de `user_posts` se limitent aux livres-souvenirs, au repartage de ses propres
  souvenirs et au contrôle parental ;
- **aucun webhook Instagram** ne couvre les publications du compte lui-même ; le champ
  `feed`, qui ferait exactement ce qui était demandé, n'existe que sur les pages
  Facebook. Le déclencheur est donc un minuteur, et l'événementiel reste conditionné à
  la création d'une page (`Q-ACT-03`).

À cette date, aucune ressource externe n'était créée et aucun compte Meta ou Azure
n'avait été touché. La spécification est ensuite devenue l'implémentation décrite
ci-dessus, sans modifier ces ressources externes.
