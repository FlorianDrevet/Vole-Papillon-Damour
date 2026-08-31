# 08 — Questions ouvertes et risques assumés

Ce document recense ce qui n'est pas tranché et ce qui est tranché mais risqué.

**À lire en premier avant tout travail d'architecture technique.**

| # | Sujet | Statut | Qui décide |
|---|---|---|---|
| `Q-01` | Passage du tri au rayon | 🔴 **Bloquant** | Association + technique |
| `Q-02` | Source de la valeur marchande | 🟠 À instruire | Étude technique |
| `Q-03` | Proportion de livres sans ISBN | 🟠 À mesurer | Palier 0 |
| `Q-04` | Dérive du stock | 🟡 Risque assumé | — |
| `Q-05` | Prix de vente et section « rares » | 🟡 À préciser | Association |
| `Q-06` | Deux applications d'administration | 🟡 Risque assumé | — |
| `Q-07` | Genres et classement | 🟡 À préciser | Association |
| `Q-08` | Choix du matériel de scan | 🟢 Après palier 0 | Association |

---

## `Q-01` — Comment les livres passent-ils du tri au rayon ?

> 🔴 **Cette question doit être tranchée avant de commencer l'architecture technique.**
> Elle détermine s'il existe une entité « lot », si elle porte un code-barres, si elle
> a un cycle de vie propre, et comment le travail de plusieurs bénévoles en parallèle
> se concilie. La reprendre après coup coûterait une refonte du modèle de données et
> des écrans de scan.

Le besoin est constant, quelle que soit l'option : **rien ne doit être publié ni
notifié tant que les livres ne sont pas physiquement disponibles** (`RG-20`). Plusieurs
jours peuvent séparer le tri de la mise en rayon.

### Option A — Le carton étiqueté *(recommandée)*

Le bénévole ouvre un carton dans l'application, y scanne des livres, puis le ferme :
l'application produit une **étiquette code-barres à imprimer et à coller sur le carton
physique**. À la bourse, un autre bénévole scanne l'étiquette et valide : tout le
contenu passe en rayon d'un coup.

| Pour | Contre |
|---|---|
| Le lien entre le carton physique et son contenu numérique est matérialisé : on ne se demande jamais « quel lot est-ce ? » | Nécessite une imprimante et des étiquettes dans le local de tri |
| Aucun livre n'est scanné deux fois | Un carton dont l'étiquette est perdue ou déchirée doit pouvoir être rattrapé par une recherche manuelle |
| Plusieurs bénévoles peuvent trier en parallèle sans se gêner | Impose de définir ce qui se passe si un carton est ouvert puis vidé ailleurs |
| Traçabilité complète : qui a trié, quand, quel contenu | |

### Option B — Le lot nommé avec bouton de validation

Des lots nommés (« Tri du 12 mars ») restent ouverts, plusieurs en parallèle. Un bouton
bascule un lot entier en rayon.

| Pour | Contre |
|---|---|
| Aucun matériel supplémentaire | **Rien ne relie un lot au carton physique.** Il faut se souvenir que le carton rouge correspond au lot du 12 mars |
| Le plus simple à concevoir | Devient ingérable dès que plusieurs cartons attendent en même temps |
| | Risque de valider le mauvais lot, erreur silencieuse et difficile à rattraper |

### Option C — Le double scan

Un mode « tri » qui n'enregistre rien, et un mode « rangement » qui publie. Les livres
sont scannés une fois au tri pour la décision, une seconde fois au rangement pour la
publication.

| Pour | Contre |
|---|---|
| Le plus simple techniquement : aucune notion de lot | **Double le travail de scan.** Sur mille livres par session, c'est une heure de bénévolat perdue à chaque fois |
| Le stock ne reflète que ce qui est réellement rangé | Le scan de rangement est un travail sans contrepartie visible pour celui qui le fait : c'est celui qu'on cessera de faire en premier |
| | Aucune trace des livres triés mais jamais rangés |

### Ce qu'il faut pour décider

- Combien de cartons attendent en moyenne entre le tri et la mise en rayon ?
- Le tri et la mise en rayon se font-ils dans le même local ?
- Une imprimante est-elle envisageable dans le local de tri ?
- Le tri se fait-il par plusieurs personnes en même temps ?

---

## `Q-02` — Où trouver la valeur marchande d'un livre ?

> 🟠 Fonctionnalité retenue (`RG-14`), faisabilité non garantie.

La détection automatique des livres de valeur a été retenue plutôt qu'un simple drapeau
manuel. C'est **le seul point de la spécification dont la faisabilité n'est pas
acquise**.

**Le problème.** Il n'existe pas de source gratuite, fiable et légalement exploitable
donnant le prix d'occasion d'un livre en France à partir de son ISBN. Les places de
marché n'offrent pas d'accès public à cette donnée, et leurs conditions d'utilisation
interdisent généralement l'extraction automatisée.

**À instruire en phase technique :**

1. Existe-t-il une source, gratuite ou payante, exploitable et dont les conditions
   d'utilisation autorisent cet usage ?
2. À quel coût, pour le volume envisagé (environ 1 000 interrogations par session) ?
   Le cadre est fixé par `ENF-23`.
3. Une consultation à la demande, uniquement sur les livres présentant des indices de
   valeur, suffirait-elle à réduire ce volume ?

**Repli si aucune source acceptable n'est trouvée.** `RG-14` s'appuie alors sur des
indices internes, sans source externe :

- année d'édition antérieure à un seuil,
- éditeur ou collection figurant sur une liste tenue par l'association,
- ISBN inscrit sur une liste blanche alimentée par les bénévoles expérimentés,
- livre déjà vendu par le passé au-dessus du tarif ordinaire.

Le verdict devient « à faire expertiser » au lieu d'afficher un montant. **Le reste de
la spécification est inchangé** : c'est une bascule interne à une seule règle, prévue
pour ne rien coûter d'autre.

---

## `Q-03` — Quelle proportion de dons n'a pas d'ISBN exploitable ?

> 🟠 À mesurer pendant le palier 0.

Les livres sans ISBN sont hors périmètre (décision arrêtée). Reste à savoir ce que cela
représente : un angle mort de 3 % est négligeable, un angle mort de 30 % remet en cause
l'intérêt du système.

Trois cas à distinguer pendant le test :

| Cas | Conséquence |
|---|---|
| Pas d'ISBN du tout (édition ancienne) | Définitivement hors périmètre |
| ISBN imprimé mais code-barres illisible ou absent | Récupérable par saisie manuelle |
| ISBN présent et lisible, mais aucune métadonnée disponible | Récupérable, `RG-03` s'applique |

Si le premier cas dépasse une part significative du fonds, la décision « hors
périmètre » devra être rouverte.

---

## `Q-04` — Dérive du stock

> 🟡 Risque assumé, conséquence d'une décision arrêtée.

Le suivi par ISBN sans exemplaire individuel signifie qu'un livre vendu sans être
scanné reste indéfiniment « disponible ». L'erreur ne se corrige jamais d'elle-même :
elle s'accumule.

Ce risque est traité par `RG-31` (remise à plat périodique) et par le suivi de l'écart
d'inventaire au tableau de bord (`05` §1 et §6). Il ne peut pas être éliminé.

**Ce qui doit être surveillé au palier 1** : si l'écart après une bourse dépasse une
proportion que l'association juge inacceptable, c'est que la discipline de scan en
caisse n'est pas tenable dans les conditions réelles. Il faudra alors revoir
l'organisation de la caisse, et non ajouter des fonctionnalités.

---

## `Q-05` — Prix de vente et section « livres rares »

> 🟡 À préciser avec l'association.

Les livres ordinaires sont vendus 1 à 2 €. Questions restées sans réponse :

- Qu'est-ce qui distingue un livre à 1 € d'un livre à 2 € ? Est-ce une donnée à porter
  dans le système, ou une décision prise au comptoir ?
- Le prix d'un livre rare est-il fixé à l'avance et enregistré, ou décidé à la vente ?
- La section « rares » a-t-elle un tarif propre, ou un prix par livre ?
- L'encaissement affiche-t-il un total, ou la caisse reste-t-elle entièrement manuelle ?

L'écran de caisse décrit en `03` §5 suppose un prix connu par livre. Si le prix est
décidé au comptoir, cet écran doit être revu.

---

## `Q-06` — Deux applications d'administration

> 🟡 Risque assumé, conséquence d'une décision arrêtée.

L'administration du catalogue est dans le nouveau site public, non dans le `BackOffice`
existant. Conséquences :

- l'authentification et la gestion des rôles administrateur sont à refaire,
- les administrateurs jonglent entre deux interfaces,
- la charte graphique existe en double et divergera avec le temps.

Ce coût est accepté. Il est rappelé ici pour qu'il ne soit pas redécouvert en cours de
développement.

---

## `Q-07` — Genres et classement

> 🟡 À préciser avec l'association.

Le catalogue public propose une navigation par genre (`04` §4). Or :

- Les genres issus des sources de métadonnées sont hétérogènes et souvent absents.
- Le classement physique du local (rayons, bacs) ne correspond probablement pas à ces
  genres.

À trancher : le système reprend-il la nomenclature des sources, ou une liste courte
définie par l'association et calquée sur l'organisation réelle des rayons ?

La seconde option est probablement la bonne — elle permet d'indiquer *où* trouver le
livre dans le local, ce qui est ce que le visiteur veut savoir — mais elle suppose un
travail de correspondance et de correction manuelle sur les fiches.

---

## `Q-08` — Matériel de scan

> 🟢 À décider après le palier 0, pas avant.

Points à vérifier sur le matériel envisagé :

- Une gâchette physique de déclenchement, condition de la cadence visée (`03` §1).
- La capacité à exécuter une application web, ou la nécessité d'une application native.
- L'autonomie sur une session complète (`ENF-04`).
- La lecture fiable de codes-barres abîmés, plastifiés ou froissés.
- Le coût unitaire, pour 2 à 5 appareils.

Aucun achat avant que le palier 0 n'ait démontré que la méthode fonctionne sur un
téléphone ordinaire.

---

## Journal des décisions

| Date | Décision | Retenu |
|---|---|---|
| 2026-08-31 | Granularité de suivi | Quantité par ISBN, sans exemplaire individuel |
| 2026-08-31 | Livres sans ISBN | Hors périmètre v1 |
| 2026-08-31 | Scan de vente en caisse | Systématique |
| 2026-08-31 | Détection des livres de valeur | Estimation automatique, avec repli `Q-02` |
| 2026-08-31 | Site public | Application web distincte |
| 2026-08-31 | Suite à une alerte | Aucune réservation, premier arrivé premier servi |
| 2026-08-31 | Identification des bénévoles | Compte individuel |
| 2026-08-31 | Emplacement de l'administration | Dans le site public |
| 2026-08-31 | Livres écartés au tri | Enregistrés, sans motif |
| 2026-08-31 | Sessions de bourse | `AssoEvents` de type `Books` existants |
| 2026-08-31 | Caisse livres | Dans l'application de scan, pas dans l'application MAUI |
| 2026-08-31 | Canal d'alerte | E-mail en v1, notifications push en v2 |
| 2026-08-31 | Catalogue public | Complet et parcourable |
| 2026-08-31 | Comptes du public | Microsoft Entra External ID |
| 2026-08-31 | Découpage de livraison | Palier 0 (sonde) puis socle interne, vitrine, alertes |
| 2026-08-31 | Passage du tri au rayon | **Non tranché** — voir `Q-01` |
