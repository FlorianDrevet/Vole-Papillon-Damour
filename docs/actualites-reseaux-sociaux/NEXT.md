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
| **Palier en cours** | Vérification opérationnelle de la v1 sur `development` après configuration externe |
| **Prochaine action** | Laisser le Worker exécuter son premier tick de 30 minutes, puis vérifier un import réel en brouillon avec un administrateur ; renouveler le jeton avant son seuil d'alerte |
| **Ce qui est prêt dans le code** | Brouillons, lectures publiques filtrées, relecture/publication BackOffice, import Instagram minuté, copie des médias, idempotence, titres Foundry avec repli, alertes et garde-fou de volume |
| **Dernière machine** | Windows — `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-instagram-activation` |
| **Dernière mise à jour** | 2026-09-11 — configuration Meta/Instagram, secrets `development`, migration EF et déploiements v1 validés ; premier import réel encore à observer |
| **Branche** | `docs/record-instagram-activation-state` — worktree dédié pour documenter l'activation post-PR `#122` |

---

## Ressources externes encore à créer

Le dépôt contient le code et les modules de déploiement. L'activation externe a été
réalisée sur `development` pour le compte Instagram de test ; les valeurs sensibles ne
figurent ni dans git ni dans ce fichier.

| Ressource | Où | État | Bloque |
|---|---|---|---|
| Compte Instagram **professionnel** | Application Instagram, réglages du compte | **Confirmé** | — |
| Business Manager au nom de l'association | business.facebook.com | **Non créé** ; non requis par le chemin Instagram Login v1 | `L5` éventuel |
| Application Meta | developers.facebook.com | **Créée, en mode développement** | Accès public/live |
| Permission `instagram_business_basic` et revue d'application | developers.facebook.com | **Compte test autorisé ; revue/accès avancé non demandés** | Accès de comptes non testeurs |
| Jeton longue durée, posé dans Key Vault sous `instagram-access-token` | GitHub Actions puis Azure Key Vault | **Configuré sur `development` ; renouvellement manuel à prévoir** | `L4` opérationnel |
| Identifiant du compte Instagram (`SocialImport__UserId`) | Configuration du Worker | **Configuré sur `development`** | — |
| Date plancher d'import | Configuration du Worker | **Configurée au `2026-01-01T00:00:00Z`** | À ajuster si le métier souhaite une autre période |
| Version de la Graph API | Configuration du Worker | **Fixée dans le code à `v22.0`** | — |
| Ressource Azure AI Foundry + déploiement de modèle | Abonnement Azure du projet | **Module Bicep prêt, ressource non créée ; titres générés désactivés** | `L3` optionnel |
| Rôle `Cognitive Services OpenAI User` pour l'identité managée du Worker | Azure | **Non attribué ; non nécessaire tant que la génération est désactivée** | `L3` optionnel |
| Règles d'alerte (jeton, authentification, échecs répétés) | Azure Monitor, groupe d'action existant | **Créées par le déploiement `development`** | — |
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
| `Q-ACT-01` — le compte Instagram est-il professionnel ? | L'association, en trois écrans | **Répondu : oui** |
| `Q-ACT-02` — le compte est-il relié à une page Facebook ? | L'association | Non bloquant avec Instagram Login |
| `Q-ACT-04` — qui détient l'application Meta ? | Le bureau | **Application créée et compte test administré par le propriétaire confirmé** |
| `Q-ACT-03` — crée-t-on une page Facebook ? | Le bureau | Non pour la v1 ; conditionne le palier `L5` |
| `Q-ACT-08` — le jeton se renouvelle-t-il tout seul ? | Arbitrage technique, à prendre au palier `L4` | **Non ; renouvellement manuel et alerte avant expiration** |
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
| Une actualité existante reste visible après la migration | Développeur | **Migration appliquée ; API publique `/actuality/latest` et `/actuality/all` répondent `200`** |
| Un brouillon n'apparaît nulle part sur le site public, y compris par son URL directe | Développeur | **Handlers filtrés et testés ; test avec un brouillon réel encore à faire** |
| Une légende à plusieurs paragraphes s'affiche correctement sur mobile et desktop | Développeur | **Code `pre-line` livré ; endpoints Website/BackOffice `200`, contrôle visuel réel encore à faire** |
| Le parcours complet : publier sur Instagram → voir le brouillon → publier → voir le site | **Avec la présidente**, sur une vraie publication | `L2` |
| Le brouillon rend le contrôle du droit à l'image praticable : les images sont assez grandes pour décider | **Un administrateur non développeur** | `L2` |
| Les titres proposés sont acceptables sur de vrais contenus | **Un administrateur non développeur** | `L3` |
| L'alerte d'expiration du jeton arrive bien, et à quelqu'un qui sait quoi en faire | Développeur, puis destinataire réel | `L4` |

---

## Journal

### 2026-09-11 — configuration externe et déploiement de la v1

Le compte Instagram professionnel `vole_papillon_damour` a été autorisé comme testeur
de l'application Meta créée pour le projet. La permission de lecture nécessaire à la v1,
`instagram_business_basic`, est configurée. Un jeton a été généré puis enregistré comme
secret GitHub de l'environnement `development`; le workflow d'infrastructure l'a recopié
dans Azure Key Vault sous `instagram-access-token`. Aucune valeur secrète n'est conservée
dans git ou dans cette mémoire.

Les paramètres `INSTAGRAM_USER_ID`, `INSTAGRAM_IMPORT_FLOOR_DATE` et
`INSTAGRAM_ACCESS_TOKEN_ISSUED_AT` sont également présents dans l'environnement GitHub.
La date plancher choisie pour cette première activation est `2026-01-01T00:00:00Z`.
Les titres Foundry restent désactivés : aucun compte Foundry ni rôle supplémentaire n'a
été provisionné.

La PR [`#122`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/122) a été
fusionnée dans `main` (`a787439`). Le `what-if` Azure
([`34644046943`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34644046943))
et le déploiement d'infrastructure
([`34644271362`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34644271362))
ont réussi. La migration EF a été appliquée pendant le déploiement API
([`34644959944`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34644959944)).
Les déploiements Worker, Website et BackOffice ont également réussi
([`34645223770`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34645223770),
[`34645075744`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34645075744),
[`34645076439`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34645076439)).
La CI du `main` courant est verte
([`34645028343`](https://github.com/FlorianDrevet/Vole-Papillon-Damour/actions/runs/34645028343)).

Le smoke test public après rollout renvoie `200` pour l'API `/health`, les lectures
d'actualités, le Website de développement, le domaine public et le BackOffice public.
Le Worker utilise le minuteur existant toutes les 30 minutes : le premier import réel,
la qualité des brouillons et la publication manuelle restent à vérifier après un vrai
contenu Instagram. L'application Meta est encore en mode développement ; l'accès live
de comptes non testeurs et la revue d'application ne sont pas validés.

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
