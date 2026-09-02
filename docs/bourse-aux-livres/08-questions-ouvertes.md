# 08 — Questions ouvertes et risques assumés

Ce document recense ce qui n'est pas tranché et ce qui est tranché mais risqué.

**À lire en premier avant tout travail d'architecture technique.**

| # | Sujet | Statut | Qui décide |
|---|---|---|---|
| `Q-01` | Passage du tri au rayon | ✅ **Tranchée** | — |
| `Q-02` | Source de la valeur marchande | 🟢 **Reportée hors v1** | — |
| `Q-03` | Proportion de livres sans ISBN | 🟠 À mesurer | Palier 0 |
| `Q-04` | Dérive du stock | 🟡 Risque assumé | — |
| `Q-05` | Prix de vente et section « rares » | ✅ **Tranchée** | — |
| `Q-06` | Deux applications d'administration | 🟡 Risque assumé | — |
| `Q-07` | Genres et classement | 🟡 À préciser | Association |
| `Q-08` | Choix du matériel de scan | 🟢 Après palier 0 | Association |
| `Q-09` | Bourse annoncée puis déplacée | 🟡 Risque assumé | — |
| `Q-10` | Choix du référentiel bibliographique | 🟠 À instruire | Étude technique |
| `Q-11` | Amorçage du catalogue | ✅ Tranchée | — |

---

## `Q-01` — Comment les livres passent-ils du tri au rayon ?

> ✅ **Tranchée.** Aucune des trois options initialement proposées n'a été retenue.
> La solution adoptée supprime l'étape.

### La solution retenue : deux modes choisis avant de scanner

Avant de commencer une session de tri, le bénévole choisit entre deux modes, qui
s'appliquent à tous les scans de la session (`RG-20`) :

| Mode | Effet |
|---|---|
| `DISPONIBLE MAINTENANT` | Les livres sont vendables immédiatement et apparaissent comme disponibles sur le site (`RG-21`) |
| `PROCHAINE BOURSE` | Les livres sont **annoncés en ligne avec la date de la prochaine bourse**, sans être vendables. À cette date, ils deviennent disponibles **automatiquement** (`RG-22`, `RG-23`) |

### Pourquoi cette solution est meilleure que les trois options écartées

Le besoin d'origine était : *ne pas annoncer comme disponible un livre encore dans un
carton*. Les trois options y répondaient toutes par un **geste humain de publication**,
et c'est précisément ce geste qui posait problème.

| Option écartée | Ce qui la disqualifie |
|---|---|
| **A — Carton étiqueté** | Imprimante et étiquettes dans le local ; un carton dont l'étiquette est perdue devient un problème ; le geste de scan de l'étiquette peut être oublié |
| **B — Lot nommé** | Rien ne relie un lot au carton physique ; ingérable dès que plusieurs cartons attendent ; risque de valider le mauvais lot |
| **C — Double scan** | Double le travail de scan : sur mille livres par session, une heure de bénévolat perdue à chaque fois |

La solution retenue **supprime le geste au lieu de l'optimiser**. Elle apporte :

- **Aucune tâche supplémentaire dans le local.** Le rangement physique reste manuel,
  mais ne s'accompagne d'aucune saisie.
- **Rien qui puisse être oublié.** La bascule est pilotée par une date, pas par une
  personne. Il n'existe plus de carton qu'on aurait négligé de déclarer.
- **Aucun matériel supplémentaire.** Ni imprimante, ni étiquettes.
- **Aucun double scan.** Chaque livre est scanné une fois.
- **Une promesse publique datée**, donc tenable : le site n'affiche jamais « disponible »
  pour un livre qui ne l'est pas, il affiche « disponible à partir du 14 mars ».

### Ce que cette solution coûte en retour

Elle n'est pas gratuite, et les documents en tirent les conséquences :

| Contrepartie | Traitement |
|---|---|
| **Le risque se déplace sur une erreur de mode.** Une session de deux cents livres scannée dans le mauvais mode publie tout immédiatement, sans que rien ne le signale | Mode affiché en permanence à l'écran (`ENF-19`), rappelé à la clôture de session (`03` §3.6), et **rattrapable en bloc** par un administrateur (`RG-25`) |
| **L'agenda des bourses devient critique.** Ce n'était qu'un affichage ; il pilote désormais la disponibilité réelle du catalogue | `RG-36` en fait la source unique, et `RG-38` traite les bourses déplacées, annulées ou passées |
| **Il faut gérer l'absence de bourse programmée** | `RG-24` : annonce sans date, rattachement automatique à la création de la bourse, alertes différées, et une file de travail en administration (`05` §4) |
| **Les alertes partent plus tôt qu'avant**, dès le tri | Assumé : elles portent une date, donc n'invitent jamais à un déplacement prématuré (`RG-28`) |

---

## `Q-02` — Où trouver la valeur marchande d'un livre ?

> 🟢 **Reportée hors v1.** Non prioritaire, et faisabilité non acquise.

**Décision.** L'estimation automatique de la valeur marchande ne fait pas partie de la
v1. Elle reste souhaitable mais ne conditionne rien d'autre : `RG-14` est spécifiée et
inactive, et son absence n'a d'effet sur aucune autre règle.

**Deux raisons.**

*Ce n'est pas prioritaire.* Le cœur du besoin est l'aide au tri sur le doublon et la
demande. Les livres rares sont un cas minoritaire, aujourd'hui traité à l'œil par les
bénévoles expérimentés — imparfaitement, mais traité.

*La faisabilité n'est pas acquise.* Il n'existe pas de source gratuite, fiable et
légalement exploitable donnant le prix d'occasion d'un livre en France à partir de son
ISBN. Les places de marché n'offrent pas d'accès public à cette donnée et leurs
conditions d'utilisation interdisent généralement l'extraction automatisée.

### La contrainte à retenir pour plus tard : l'asynchrone

Même avec une source disponible, **l'estimation ne peut pas se faire pendant le scan**.
Interroger plusieurs sources, agréger les résultats et calculer une moyenne prend
plusieurs secondes, là où le verdict doit s'afficher en moins d'une (`ENF-01`). Un scan
qui attend une réponse réseau externe casse la cadence du tri, et c'est la cadence qui
fait l'adoption de l'outil.

La conception cible est donc :

| Étape | Où |
|---|---|
| Le livre est scanné | Verdict immédiat sur les seules données internes |
| L'estimation est calculée en arrière-plan | Sans bloquer quoi que ce soit |
| Les résultats apparaissent | Au **récapitulatif de fin de session** (`03` §3.6) pour les livres qui viennent d'être triés, et dans une **file de travail en administration** (`05` §4) |

**Conséquence à assumer** : le livre a déjà été trié, voire rangé, quand le signalement
arrive. Il faudra aller le rechercher physiquement en rayon. C'est faisable — le titre
et l'ISBN sont connus — mais c'est un travail manuel, et c'est le prix de l'asynchrone.

### Si le sujet est repris

1. Existe-t-il une source, gratuite ou payante, dont les conditions d'utilisation
   autorisent cet usage ?
2. À quel coût pour le volume envisagé ? Le cadre est fixé par `ENF-23`.
3. Peut-on réduire le volume en n'interrogeant que les livres présentant des indices
   internes de valeur — année d'édition ancienne, éditeur ou collection listés par
   l'association, titre déjà vendu au-dessus du tarif ordinaire ?

La troisième piste est la plus prometteuse : elle transforme un problème de volume en
un problème de filtre, et le filtre peut être construit sans aucune source externe.

### En attendant

Le marquage manuel « rare » par un administrateur existe dès la v1 (`05` §4). Il
alimente la section « livres rares » et le signalement en caisse (`03` §5). C'est ce
qui fait vivre la section aujourd'hui, et cela continuera de fonctionner tel quel.

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

> ✅ **Tranchée.** Le prix est décidé au comptoir par le bénévole. Le système n'en
> connaît aucun.

**La décision.** Aucun prix n'est stocké, affiché, calculé ni totalisé par l'application
(`RG-50`). La caisse enregistre *quels* livres sortent, jamais *combien* ils rapportent.
L'encaissement reste entièrement manuel, comme aujourd'hui.

### Ce que cela change

| | |
|---|---|
| **Écran de caisse** | Réécrit (`03` §5) : ni colonne prix, ni total. Le bouton devient `VALIDER` et non `ENCAISSER` — il enregistre une sortie de stock, il n'encaisse rien |
| **Livres rares** | Le signalement en caisse devient **critique** et passe d'une pastille discrète à un encadré en pleine largeur. C'est la seule protection contre un livre expertisé à 35 € vendu 2 € par quelqu'un qui l'ignore. Le montant est porté **physiquement sur le livre** |
| **Statistiques** | Le nombre de livres vendus reste connu ; la recette, non |
| **Fiche livre** | Aucun champ de prix, nulle part |

### Ce que l'on perd, et comment on le récupère

La promesse initiale de `05` §2 — recette par bourse — n'est plus tenable à partir des
scans. Elle est remplacée par une **saisie manuelle d'un seul montant à la clôture de
chaque bourse** (`RG-51`) : celui du comptage de caisse que l'association fait de toute
façon.

Rapproché du nombre de livres vendus, ce montant donne le panier moyen et permet de
comparer les bourses entre elles. Une bourse à 800 livres pour 1 100 € et une autre à
800 livres pour 700 € ne racontent pas la même histoire — et l'on obtient cette
comparaison avec un champ, sans jamais tarifer un seul livre.

### Ce que cela ne remet pas en cause

Le scan de sortie sert d'abord à décrémenter le stock, et cela fonctionne sans connaître
le moindre prix. Le catalogue public, la fiabilité des quantités, les alertes et l'aide
au tri sont **totalement indépendants** de cette question.

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

## `Q-09` — Une bourse annoncée puis déplacée

> 🟡 Risque assumé, conséquence directe de la solution retenue en `Q-01`.

Un membre reçoit « ce livre sera disponible à la bourse du 14 mars ». La bourse est
ensuite reportée au 21.

En v1, **aucun e-mail de correction n'est envoyé** (`RG-38`). Le site affiche bien la
nouvelle date, la bascule automatique suit, mais l'e-mail déjà parti reste faux. Un
membre peut donc se déplacer le 14 pour rien.

C'est le seul cas où le système peut faire déplacer quelqu'un inutilement — exactement
le risque que l'on cherchait à éviter au départ, réintroduit par une autre porte.

**Ce qui limite la portée du problème** : les dates de bourse sont fixées longtemps à
l'avance et bougent rarement, et le report d'une bourse est de toute façon communiqué
par les canaux habituels de l'association.

**À trancher si le cas se produit** : envoyer un e-mail de correction aux membres
alertés sur une bourse dont la date a changé. Techniquement simple, puisque
l'historique des alertes est conservé (`02` §4) — mais à ne construire que si le besoin
est réel.

---

## `Q-10` — Quel référentiel bibliographique ?

> 🟠 À instruire. La fonctionnalité est retenue ; le choix de la source ne l'est pas.

Le référentiel sert à trois choses, d'importance très inégale :

| Usage | Criticité |
|---|---|
| Métadonnées d'un ISBN scanné : titre, auteur, éditeur, couverture | **Indispensable.** Sans lui, l'écran de tri n'affiche qu'un numéro |
| Recherche par titre ou auteur d'un livre jamais reçu (`RG-47`) | **Indispensable** au cas d'usage central des alertes |
| Regroupement des éditions en œuvres (`RG-46`) | **Souhaitable.** Son absence dégrade la fonctionnalité sans l'empêcher |

**Ce qu'il faut vérifier :**

1. **La couverture du fonds français**, y compris l'édition ancienne et le livre
   jeunesse. C'est la mesure la plus utile du palier 0 : un référentiel qui ignore un
   tiers des dons rend l'écran de tri inutilisable.
2. **La présence d'un regroupement en œuvres.** Certaines sources modélisent
   explicitement l'œuvre et ses éditions ; d'autres ne renvoient que des notices d'ISBN
   isolées. Sans regroupement, `RG-46` doit se rabattre sur un rapprochement
   titre + auteur normalisés — qui produit des faux positifs sur les séries, les
   homonymes et les adaptations.
3. **Les conditions d'utilisation et les limites de débit**, pour environ un millier
   d'interrogations par session de tri (`ENF-23`).
4. **La possibilité de conserver localement les notices déjà vues.** Un livre scanné une
   fois ne devrait plus jamais nécessiter d'appel externe : c'est ce qui permet de tenir
   `ENF-01` et de fonctionner hors ligne (`ENF-05`).

**Repli sur le regroupement en œuvres.** Si aucune source fiable n'est trouvée, on
conserve la portée `ÉDITION` seule et la portée `ŒUVRE` s'appuie sur un rapprochement
titre + auteur, en assumant les faux positifs — un membre prévenu à tort vaut mieux
qu'un membre jamais prévenu, dans un contexte sans réservation ni engagement.

---

## `Q-11` — Amorçage du catalogue

> ✅ **Tranchée.** Remplissage progressif, pas de reprise préalable de l'existant.

**Le problème.** Le local contient déjà plusieurs milliers de livres. Le catalogue
démarre vide, et le système ne connaît que ce qu'il a scanné.

**La décision.** On ne reprend pas l'existant avant de démarrer. Le catalogue se
remplit au fil des tris. Une ou plusieurs sessions de scan des rayons déjà remplis
peuvent accélérer les choses, en mode `DISPONIBLE MAINTENANT`, mais ce n'est **pas un
prérequis** et cela ne conditionne aucun palier.

**Ce que cela coûte**, détaillé en `RG-48` : pendant plusieurs mois, `RG-10` (« inutile
d'en garder ») ne se déclenche presque jamais, `RG-11` (« premier exemplaire ») se
déclenche à tort, `RG-12` (« ce titre se vend ») reste muet faute d'historique, et le
catalogue public n'expose qu'une fraction du fonds réel.

**La conséquence à ne pas négliger est humaine.** Un bénévole à qui l'outil annonce
« premier exemplaire » sur un livre qu'il sait présent en cinq exemplaires conclura que
l'outil ne marche pas. Il faut le dire avant, pas après. C'est aussi un argument pour
prioriser le scan des rayons les plus denses : c'est là que le doublon coûte le plus
cher, et là que l'outil devient crédible le plus vite.

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
| 2026-08-31 | Passage du tri au rayon | **Deux modes choisis avant de scanner**, plus bascule automatique à la date de la bourse. Aucune des trois options lot/carton n'a été retenue — `Q-01` |
| 2026-08-31 | Moment d'envoi de l'alerte | Au scan, avec la date annoncée — pas à la bascule |
| 2026-08-31 | Mode « prochaine bourse » sans bourse programmée | Accepté ; rattachement à la prochaine bourse créée, alertes différées |
| 2026-08-31 | Correction d'une erreur de mode | Reprise en bloc de la session entière |
| 2026-08-31 | Estimation de la valeur marchande | **Reportée hors v1**, et asynchrone quand elle existera — jamais pendant le scan |
| 2026-08-31 | Cycle de vie d'une session | Ouverte au choix du mode ; close sur demande, après 2 h d'inactivité, à la déconnexion ou à l'expiration du jeton |
| 2026-08-31 | Moment d'envoi des alertes | Mises en file **à la clôture de la session**, envoyées **2 h après** — jamais au scan. Le délai est la fenêtre de rattrapage d'une erreur constatée après coup |
| 2026-09-01 | Suivre un livre jamais reçu | La recherche s'élargit à un référentiel bibliographique externe ; on peut suivre n'importe quel livre publié — `RG-47` |
| 2026-09-01 | Granularité d'une demande | **Œuvre par défaut** (toutes éditions), édition précise en option — `RG-46` |
| 2026-09-01 | Amorçage du catalogue | Progressif, sans reprise préalable de l'existant — `Q-11`, `RG-48` |
| 2026-09-01 | Périmètre de la session | Le tri seul. La caisse et la consultation n'ouvrent pas de session — `RG-43` |
| 2026-09-01 | Prix de vente | **Décidés au comptoir. Le système n'en connaît aucun** — `Q-05`, `RG-50` |
| 2026-09-01 | Recette d'une bourse | Saisie manuelle d'un montant unique à la clôture, facultative — `RG-51` |
| 2026-09-02 | Écran de remise à plat de l'inventaire | **Reporté.** Non construit avec le reste de l'administration ; la correction fiche par fiche (`05` §4) tient lieu d'interim — `05` §6 |
| 2026-09-02 | Fournisseur d'identité | **Entra External ID pour tous les publics** — membres, bénévoles, administrateurs. L'authentification maison du backend est supprimée, pas conservée en parallèle — `DT-10` |
| 2026-09-02 | Gestion des droits | **Rôles applicatifs** `Tri`, `Caisse`, `Administration`, portés par l'enregistrement de l'API et attribués aux comptes. Ni groupes de sécurité, ni rôle en base. Un membre du public n'a aucun rôle — `DT-10`, `10` §4 |
| 2026-09-02 | Configuration du locataire | **Par script PowerShell** (`infra/entra/`), jamais à la main dans le portail. Deux exceptions : la création du locataire et le flux d'inscription — `ENF-27` |
| 2026-09-02 | Ordre de livraison | Le **socle d'identité passe en tout premier**, avant la sonde de faisabilité : tout en dépend, et une migration d'authentification faite après coup se ferait sur un système en service — `01` §7 |
