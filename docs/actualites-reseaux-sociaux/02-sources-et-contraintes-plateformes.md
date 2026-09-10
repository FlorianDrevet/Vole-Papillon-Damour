# 02 — Sources et contraintes des plateformes

> Ce document conditionne tous les autres. La demande initiale était : « récupérer
> automatiquement la publication Facebook, si possible par un déclencheur événementiel ».
> La réponse est **non pour Facebook, non pour l'événementiel**, et il y a une bonne
> solution derrière. Voici pourquoi.

## 1. L'état des lieux

| Élément | Valeur observée |
|---|---|
| Facebook affiché sur le site | `https://www.facebook.com/melvin.drevet.1` — **profil personnel**, pas une page |
| Instagram affiché sur le site | `https://www.instagram.com/vole_papillon_damour` |
| Geste de publication | Publication sur Facebook, diffusion croisée cochée vers Instagram |
| Où c'est câblé dans le code | `footer.component.html`, `navigation-mobile.component.html`, `contact.component.html` |

Le format d'URL `facebook.com/prenom.nom.1` est celui d'un **profil personnel**. Aucune
page Facebook au nom de l'association n'existe dans le dépôt.

## 2. Pourquoi Facebook est écarté comme source

Lire les publications d'un **profil personnel** exige la permission `user_posts` de la
Graph API. Cette permission existe toujours, mais Meta en encadre strictement les usages
autorisés, et la revue d'application (*App Review*) n'accepte que trois familles de cas :
fabriquer des livres ou albums physiques ou numériques à partir du fil d'une personne,
aider une personne à repartager ses propres souvenirs, et le contrôle parental analysant
les publications de mineurs.

**Republier le fil d'une personne sur le site d'une association ne rentre dans aucun de
ces trois cas.** Une demande de revue serait refusée, et construire la fonctionnalité en
espérant le contraire reviendrait à parier tout le projet sur une décision de Meta.

Les **pages**, elles, sont prévues pour ça : `pages_read_engagement` donne accès au
contenu publié par la page. Mais l'association n'a pas de page — voir `Q-ACT-03`.

**Conclusion : tant qu'il n'y a pas de page Facebook, Facebook n'est pas une source
lisible.** Ce n'est pas une limite de notre implémentation, c'est la règle de la
plateforme.

## 3. Pourquoi il n'y a pas de déclencheur événementiel

Meta propose bien des webhooks. Ce qu'ils couvrent, précisément :

| Plateforme | Champs de webhook disponibles | Prévient-il d'une **nouvelle publication du compte lui-même** ? |
|---|---|---|
| **Page Facebook** | `feed`, et une trentaine d'autres | **Oui.** Le champ `feed` notifie « presque tous les changements du fil d'une page, dont les publications, partages, mentions J'aime ». Abonnement conditionné à `pages_manage_metadata` |
| **Instagram** | `comments`, `live_comments`, `mentions`, `messages`, `message_edit`, `message_reactions`, `messaging_handover`, `messaging_postbacks`, `messaging_referral`, `messaging_seen`, `standby`, `story_insights` | **Non.** Aucun champ ne concerne la publication d'un média par le compte lui-même. Tous portent sur les interactions reçues |
| **Profil personnel** | — | Sans objet : le contenu n'est pas lisible (section 2) |

Donc :

- **Sur Instagram, il faut interroger périodiquement.** C'est la seule voie. Ce n'est
  pas un choix de facilité.
- **Sur une page Facebook, l'événementiel serait possible** — et c'est l'argument
  technique en faveur de la création d'une page (`Q-ACT-03`, palier `L5`).

> `DT-ACT-01` — **Déclencheur : minuteur (`TimerTrigger`), pas événement.** Motif
> ci-dessus. La cadence est fixée par `ENF-ACT-01`.

## 4. La source retenue pour la v1 : Instagram

Le contenu qui nous intéresse **est déjà sur Instagram**, puisque la présidente coche la
diffusion croisée. On lit donc là où c'est autorisé.

### Ce qu'il faut réunir

| Prérequis | État | Référence |
|---|---|---|
| Le compte Instagram est un compte **professionnel** (entreprise ou créateur) | **À vérifier — bloquant** | `Q-ACT-01` |
| Le compte est relié à une **page Facebook** (détermine le chemin d'authentification) | **À vérifier** | `Q-ACT-02` |
| Une **application Meta** existe, détenue par l'association | **À créer** | `Q-ACT-04` |
| L'application a l'**accès avancé** aux permissions de lecture | À demander en revue | ci-dessous |

### Les deux chemins d'authentification

Meta propose deux configurations, qui mènent au même contenu :

| | **Instagram Login** | **Facebook Login for Business** |
|---|---|---|
| Point d'entrée | `graph.instagram.com` | `graph.facebook.com` |
| Identifiants utilisés | Ceux d'Instagram | Ceux de Facebook |
| Permission de lecture | `instagram_business_basic` | `instagram_basic` |
| Prérequis | Compte professionnel | Compte professionnel **relié à une page** |

**Recommandation : Instagram Login.** Il ne suppose pas de page Facebook, donc il ne
dépend pas de la réponse à `Q-ACT-02`, et il n'accroche pas le projet au profil personnel
de la présidente. Si une page est créée plus tard (`L5`), le second chemin redevient
intéressant parce qu'il ouvre aussi le webhook `feed`.

### Le jeton, et le piège des 60 jours

Le cycle est en trois temps : un code d'autorisation valable **une heure**, échangé
contre un jeton court valable **une heure**, échangé contre un **jeton long valable 60
jours**, renouvelable avant son expiration.

C'est **la principale fragilité d'exploitation** de la fonctionnalité : un jeton qui
expire arrête l'import en silence. Traité par `ENF-ACT-05` et `Q-ACT-08`.

### Ce qu'on lit

Un appel à la liste des médias du compte, avec les champs
`id`, `caption`, `media_type`, `media_url`, `permalink`, `thumbnail_url`, `timestamp`,
et pour les carrousels l'arête `children` (`id`, `media_type`, `media_url`,
`thumbnail_url`).

Deux points à connaître :

- `media_type` vaut `IMAGE`, `VIDEO` ou `CAROUSEL_ALBUM`. Le traitement diffère
  (`RG-ACT-09`, `RG-ACT-11`).
- **`media_url` est une URL de CDN signée et temporaire.** La stocker en base
  produirait des images mortes quelques jours plus tard. D'où `RG-ACT-10` : on
  télécharge et on recopie immédiatement dans le stockage de l'association.

La version de la Graph API (`v2x.0`) est à figer au moment de l'implémentation, puis à
inscrire dans la configuration — Meta déprécie chaque version au bout d'environ deux ans.

## 5. Ce qu'on ne fera pas

**Aucun scraping.** Ni du HTML d'Instagram, ni d'une « API non officielle », ni d'un
service tiers qui fait l'un ou l'autre à notre place. C'est contraire aux conditions
d'utilisation de Meta, cassé au premier changement de page, et indéfendable pour une
association. Si l'API officielle ne permet pas quelque chose, la réponse est que le
site ne le fera pas.

**Aucun flux RSS de contournement.** Les passerelles RSS pour Instagram reposent toutes
sur du scraping ou sur un compte tiers, avec les mêmes problèmes plus une dépendance
supplémentaire.

## 6. Ce qui se passerait avec une page Facebook

Pour mémoire, parce que c'est le seul chemin qui rende la demande initiale — un
déclencheur événementiel sur Facebook — réalisable :

1. L'association crée une page à son nom, la présidente en est administratrice.
2. Elle publie sur la page au lieu de son profil ; la diffusion croisée vers Instagram
   fonctionne de la même façon.
3. L'application s'abonne au champ `feed` de la page (`pages_manage_metadata`) et lit
   le contenu (`pages_read_engagement`).
4. Une fonction déclenchée par HTTP reçoit la notification **au moment** de la
   publication, vérifie la signature `X-Hub-Signature-256`, et enfile le même traitement
   d'import que la voie Instagram.

Bénéfices annexes, indépendants de ce projet : la présence Facebook appartient à
l'association et non à une personne, et les liens `facebook.com/melvin.drevet.1`
présents dans trois composants du site pointeraient enfin vers une identité
associative.

Coût : un changement d'habitude pour la présidente, et la reprise des liens dans le
site. Décision : `Q-ACT-03`.

## Sources

- [Instagram Platform — Overview (Meta for Developers)](https://developers.facebook.com/docs/instagram-platform/overview/) — configurations Instagram Login / Facebook Login, permissions, cycle et durées de vie des jetons
- [Graph API Webhooks — Instagram reference](https://developers.facebook.com/docs/graph-api/webhooks/reference/instagram/) — liste exhaustive des champs de webhook Instagram
- [Graph API Webhooks — Page reference](https://developers.facebook.com/docs/graph-api/webhooks/reference/page/) — champ `feed` et permission `pages_manage_metadata`
- [Meta — Permissions Reference](https://developers.facebook.com/docs/permissions/) — usages autorisés de `user_posts`, `pages_read_engagement`, `pages_manage_metadata`
