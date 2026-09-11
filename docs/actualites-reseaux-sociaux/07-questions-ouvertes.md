# 07 — Questions ouvertes et risques assumés

## Les trois questions bloquantes

Elles ne se décident pas en réunion : elles se **constatent**, dans l'application
Instagram et dans le Business Manager de Meta. Tant qu'elles n'ont pas de réponse, le
activation de la v1 reste bloquée. Le palier `L1` — le socle éditorial — peut être
construit sans elles.

### `Q-ACT-01` — Le compte Instagram est-il **professionnel** ?

**Pourquoi c'est bloquant.** Les API Meta ne lisent que les comptes professionnels
(entreprise ou créateur). Un compte personnel est hors de portée, quelle que soit
l'implémentation.

**Comment répondre.** Réglages Instagram → Type de compte. Trois écrans.

**Si la réponse est non.** La bascule est gratuite, réversible, et invisible pour les
abonnés. Elle donne accès aux statistiques du compte, ce qui intéressera probablement
l'association par ailleurs. **Sans elle, ce projet n'existe pas** et la seule option
reste la saisie manuelle.

### `Q-ACT-02` — Le compte est-il relié à une **page Facebook** ?

**Pourquoi ça compte.** C'est ce qui départage les deux chemins d'authentification
(`02`, section 4). Instagram Login ne l'exige pas ; Facebook Login for Business, si.

**Recommandation.** Prendre **Instagram Login** de toute façon : il rend la réponse
inutile pour la v1 et n'accroche pas le projet au compte Facebook d'une personne. La
question ne redevient structurante que si `Q-ACT-03` est tranchée positivement.

### `Q-ACT-04` — Qui **détient** l'application Meta ?

**Le risque.** Une application créée depuis le compte Facebook personnel de la
présidente disparaît avec ce compte. C'est exactement le problème que pose déjà le lien
`facebook.com/melvin.drevet.1` affiché comme identité de l'association.

**Recommandation.** Créer un **Business Manager au nom de l'association**, y rattacher
l'application, et donner à au moins deux personnes du bureau le rôle d'administrateur.
Cinq minutes maintenant, une reconstruction complète plus tard.

## Les décisions d'organisation

### `Q-ACT-03` — L'association crée-t-elle une **page Facebook** ?

**Ce que ça débloque.**

| | Sans page (v1) | Avec page (palier `L5`) |
|---|---|---|
| Source lisible | Instagram seul | Instagram **et** Facebook |
| Déclenchement | Interrogation toutes les 30 min | **Événementiel**, au moment de la publication |
| Propriété de la présence Facebook | Le profil d'une personne | L'association |
| Liens du site | Pointent vers un profil personnel | Pointent vers l'association |

**Ce que ça coûte.** Un changement d'habitude pour la présidente — publier depuis la
page — et la reprise des liens dans trois composants du site.

**Qui décide.** Le bureau, pas la technique. C'est la seule question de ce dossier qui
soit d'abord une question d'association.

### `Q-ACT-05` — La validation humaine reste-t-elle **obligatoire pour toujours** ?

La v1 impose le brouillon (`RG-ACT-04`). La question se repose après quelques mois : si
les dix ou vingt premières actualités importées ont toutes été publiées sans la moindre
correction, la relecture devient une formalité qu'on finira par expédier — et une
formalité expédiée ne protège plus rien.

**Ne pas trancher maintenant.** Répondre sur des faits, en regardant le taux de
corrections réelles. Si la publication automatique est un jour retenue, elle devra
rester **conditionnelle** : jamais pour un post contenant des visages, jamais pour un
titre de repli.

### `Q-ACT-06` — Génère-t-on le **texte alternatif** des images ?

Aujourd'hui la galerie d'une actualité utilise `alt=""` et l'image principale reprend le
titre. Faire décrire les photos par un modèle améliorerait l'accessibilité, mais sur des
photos de personnes — dont des enfants — une description automatique peut qualifier des
gens de travers, et ce serait publié.

**Position en v1 : non** (`ENF-ACT-25`). À reprendre séparément, avec l'accessibilité du
site dans son ensemble, pas en marge d'un projet d'import.

### `Q-ACT-07` — Que fait-on des **reels** et des vidéos ?

La v1 garde la vignette (`RG-ACT-11`), parce que le modèle de données n'a pas de champ
vidéo. Si la présidente publie majoritairement des reels, la fonctionnalité perdra
beaucoup de son intérêt et il faudra ouvrir le sujet « actualité vidéo » — qui est un
projet à part entière, pas une option.

**À mesurer** sur les publications réelles des six derniers mois, avant de construire.

### `Q-ACT-08` — Le jeton se renouvelle-t-il **tout seul** ?

Deux options :

| | Renouvellement automatique | Renouvellement manuel |
|---|---|---|
| Principe | La fonction rafraîchit le jeton au-delà de 30 jours et réécrit le secret dans Key Vault | Une alerte 15 jours avant expiration, un humain refait le geste |
| Coût | L'identité managée du Worker doit pouvoir **écrire** dans Key Vault | Nul |
| Risque | Un droit d'écriture de plus sur le coffre | Un oubli arrête l'import ; deux oublis et personne ne sait plus comment faire |

**Recommandation : l'automatique, avec l'alerte comme filet** — l'alerte seule finit
toujours par être ignorée. Mais c'est une extension de droits sur le coffre de secrets,
et cela se décide explicitement.

## Risques assumés

| Risque | Pourquoi on l'accepte |
|---|---|
| **Meta change ses règles ou ses permissions** | Hors de tout contrôle. Atténué par le fait que la saisie manuelle n'est jamais retirée et que le site ne dépend pas de l'import (`ENF-ACT-18`) |
| **Le titre généré est parfois plat** | Il est relu à chaque fois. Un titre plat coûte trente secondes ; l'absence de titre coûte la fonctionnalité |
| **Le lien Facebook des actualités importées reste vide** (`RG-ACT-14`) | Rien dans les données Instagram ne permet de retrouver le post Facebook correspondant. Le lien Instagram suffit à renvoyer vers la conversation |
| **Une publication très ancienne ne sera jamais reprise** | La date plancher (`RG-ACT-03`) est un choix délibéré. Une reprise d'historique sera un geste manuel, si elle est un jour souhaitée |
| **La qualité des images dépend de ce que sert Meta** | Pas de retraitement en v1 (`ENF-ACT-27`). À rouvrir si les temps de chargement se dégradent |

## Ce qui n'attend plus de réponse

| Sujet | Tranché |
|---|---|
| Facebook comme source directe | **Non**, tant qu'il n'y a pas de page (`02`, section 2) |
| Déclencheur événementiel sur Instagram | **Impossible**, aucun webhook ne couvre ses propres publications (`02`, section 3) |
| Scraping en repli | **Jamais** (`ENF-ACT-15`) |
| Publication automatique en v1 | **Non**, brouillon obligatoire (`RG-ACT-04`) |
| Où vit la fonction | Dans le Worker existant, pas dans une nouvelle Function App (`DT-ACT-02`) |
