# 06 — Règles métier

Chaque règle est numérotée, formulée pour être vérifiable, et référencée depuis les
autres documents. Les valeurs de seuil citées sont des **valeurs de départ à ajuster** :
elles sont paramétrables (`05` §9) et seront toutes fausses au premier essai.

> Les numéros sont des **identifiants stables**, pas un ordre de lecture. Une règle
> ajoutée après coup prend un numéro libre plutôt que de décaler les autres : les
> sections sont thématiques, la numérotation ne l'est pas.

---

## Identification du livre

### `RG-01` — Validité de l'ISBN
Un code n'est accepté que s'il est un ISBN-10 ou ISBN-13 syntaxiquement valide, clé de
contrôle comprise. Tout ISBN-10 est converti en ISBN-13 avant enregistrement.
**Une fiche est identifiée par son ISBN-13 et par lui seul.**

### `RG-02` — Code non-ISBN
Un code-barres valide mais qui n'est pas un ISBN (article alimentaire, code interne) est
refusé avec un message explicite. Aucune fiche n'est créée, aucun mouvement enregistré.

### `RG-03` — ISBN valide sans métadonnées
Si aucune source ne connaît l'ISBN, la fiche est **quand même créée**, avec le seul
ISBN, et rejoint la file « fiches sans métadonnées » (`05` §4). Le bénévole peut la
garder : l'absence de titre ne doit jamais bloquer le tri.

### `RG-04` — Scan répété rapproché
Le même ISBN scanné deux fois en moins de 5 secondes dans la même session affiche
« déjà scanné à l'instant », **sans bloquer**. Deux exemplaires identiques dans un don
sont fréquents ; le bénévole tranche.

### `RG-05` — Priorité de la correction manuelle
Une métadonnée corrigée par un administrateur ne peut jamais être écrasée par une
actualisation automatique ultérieure. Le champ est marqué comme corrigé manuellement.

### `RG-06` — Suppression d'une fiche
La suppression est refusée dès qu'un mouvement de vente est rattaché à la fiche. Dans ce
cas, seul le masquage du catalogue public est possible : l'historique de vente est une
donnée comptable de l'association.

### `RG-07` — Fusion de fiches
Deux fiches désignant la même édition peuvent être fusionnées. Les mouvements et les
quantités sont additionnés, les listes de recherche reportées, l'ISBN conservé est
l'ISBN-13.

---

## Aide à la décision au tri

### `RG-10` — Verdict « inutile d'en garder »
Déclenché lorsque la quantité disponible **plus** la quantité annoncée, toutes bourses
confondues, atteint le seuil de doublon *(valeur de départ : 5)*.

Le comptage inclut les exemplaires annoncés : sans cela, deux bénévoles triant en
parallèle garderaient chacun cinq exemplaires du même titre le même jour.

### `RG-11` — Verdict « premier exemplaire »
Aucun exemplaire disponible, aucun annoncé, aucune vente passée : la fiche est nouvelle
pour l'association.

### `RG-12` — Signal « ce titre se vend »
Affiché dès que les ventes cumulées atteignent le seuil de demande
*(valeur de départ : 1)*.

Le nombre de ventes est affiché en clair plutôt qu'interprété : « vendu 7 fois » est
plus utile à un bénévole qu'un jugement de l'application.

### `RG-13` — Signal « recherché »
Affiché si au moins un membre actif a cet ISBN dans sa liste de recherche. Le nombre de
demandeurs est affiché, **jamais leur identité**.

### `RG-14` — Signal « livre de valeur » *(hors v1 — voir `Q-02`)*

**Cette règle n'est pas implémentée en v1.** L'estimation de valeur marchande est
reportée : elle n'est pas prioritaire, et aucune source fiable n'est identifiée.

Quand elle le sera, elle fonctionnera **en asynchrone et jamais pendant le scan** :
interroger plusieurs sources et calculer une moyenne est trop lent pour tenir le délai
d'affichage du verdict (`ENF-01`). L'estimation est calculée en arrière-plan et ses
résultats apparaissent :

- dans le récapitulatif de fin de session (`03` §3.6), pour les livres qui viennent
  d'être triés,
- et dans une file de travail en administration (`05` §4), pour traitement différé.

**Conséquence à assumer d'ici là** : un livre de valeur passe en rayon au tarif
ordinaire et devra être retiré physiquement une fois signalé. Le repérage à l'œil par
les bénévoles expérimentés reste donc le seul filet en v1. L'objectif `O4` n'est pas
couvert par la v1.

Le marquage manuel « rare » par un administrateur, lui, existe dès la v1 (`05` §4) :
c'est ce qui alimente la section « livres rares » et le signalement en caisse.

### `RG-15` — Priorité des verdicts
Un livre peut relever de plusieurs règles à la fois. Un seul verdict principal est
affiché, dans cet ordre :

1. Recherché par un membre (`RG-13`)
2. Ce titre se vend (`RG-12`)
3. Inutile d'en garder (`RG-10`)
4. Premier exemplaire (`RG-11`)

Un livre déjà marqué « rare » à la main affiche ce signalement en complément du verdict,
sans le remplacer. Quand `RG-14` sera implémentée, elle n'entrera pas non plus dans
cette liste : son résultat arrive après le scan, pas pendant.

**« Recherché » et « ça se vend » l'emportent délibérément sur « trop d'exemplaires ».**
Un sixième exemplaire d'un titre que quelqu'un attend doit être gardé. Les signaux non
retenus comme verdict principal restent visibles dans le détail sous la fiche.

### `RG-16` — Aucune décision automatique
L'application n'écarte jamais un livre d'elle-même. Elle informe ; le bénévole décide.

### `RG-17` — Annulation du dernier scan
Le dernier geste enregistré peut être annulé depuis l'écran de scan, sans passer par
un menu. L'annulation supprime le mouvement correspondant, et l'alerte qui en découlait
si elle n'est pas encore partie.

### `RG-18` — Fenêtre d'annulation
L'annulation reste possible sur les gestes de la session en cours, et non sur le seul
dernier scan, tant que la session n'est pas clôturée.

### `RG-19` — Scan suivant valant validation
En mode tri, scanner un nouveau livre vaut « garder » pour le précédent. Ce
comportement doit être vérifié au palier 0 : s'il produit trop de faux « gardés », il
est remplacé par une validation explicite.

---

## Sessions de scan

La session est l'unité de travail du tri. Elle porte le mode, regroupe les mouvements,
déclenche les alertes à sa clôture et constitue l'unité de correction.

### `RG-43` — Ouverture et clôture d'une session
Une session **s'ouvre** au moment où le bénévole choisit un mode de mise à disposition
(`RG-20`). Il n'y a pas d'autre manière d'en ouvrir une, et un bénévole ne peut avoir
qu'une seule session ouverte à la fois.

Elle **se clôture** dans quatre cas :

| Cause | Détail |
|---|---|
| Clôture explicite | Le bénévole appuie sur `TERMINER` |
| Inactivité | Aucun scan pendant 2 heures *(valeur paramétrable)* |
| Déconnexion | Le bénévole se déconnecte de l'application |
| Expiration du jeton d'authentification | La session de travail suit la session d'authentification |

Une session close ne peut plus recevoir de scan. Reprendre le tri ouvre une nouvelle
session, avec un nouveau choix de mode.

La clôture par inactivité a une conséquence directe sur les alertes (`RG-44`) : un
bénévole qui range son appareil sans appuyer sur `TERMINER` retarde les e-mails de
deux heures au maximum. C'est acceptable, et c'est la raison pour laquelle le délai
est court plutôt qu'illimité.

### `RG-44` — Les alertes sont mises en attente à la clôture, puis envoyées après un délai
Les alertes constituées pendant une session (`RG-28`) sont **mises en file d'attente au
moment où la session se clôture**, quelle qu'en soit la cause, et **envoyées 2 heures
plus tard** *(délai paramétrable)*.

Aucun e-mail ne part pendant que le bénévole scanne, ni au moment même où il termine.

```
   scan …  scan …  scan …     clôture              envoi
   ─────────────────────────────●───────────────────●──────►
                                │◄─── délai 2 h ───►│
                                │                   │
              correction sans   │   correction      │  plus
              conséquence       │   possible :      │  rattrapable
                                │   les e-mails     │
                                │   sont annulés    │
```

Cela produit quatre effets voulus :

- **Un seul e-mail par membre et par session** (`RG-29`), au lieu d'un par livre.
- **Une fenêtre de correction qui survit à la clôture.** Une erreur constatée après
  coup — mauvais mode, session close par mégarde, livres scannés par erreur — se
  corrige encore sans qu'aucun e-mail ne soit parti (`RG-45`).
- **Un rattrapage possible sur les sessions closes automatiquement**, par inactivité ou
  par expiration du jeton, dont personne n'a vu le récapitulatif.
- **Un fait générateur unique et observable**, plutôt qu'un envoi diffus dans le temps.

**Ce que cela coûte** : le délai s'ajoute à celui de la clôture automatique. Entre le
dernier scan d'un bénévole qui range son appareil sans terminer et l'envoi effectif, il
peut s'écouler jusqu'à 4 heures. C'est sans conséquence : les livres annoncés ne sont
de toute façon disponibles qu'à la date de la bourse, et les livres rendus disponibles
immédiatement ne le sont qu'à la prochaine ouverture du local.

Les alertes en attente sont visibles et actionnables en administration (`05` §4 bis) :
on peut les annuler, ou forcer leur envoi sans attendre le délai.

### `RG-45` — Correction d'une session
Un administrateur peut, depuis l'écran des sessions (`05` §4 bis) :

| Correction | Effet |
|---|---|
| Changer le mode de la session | Rebascule tous ses livres (`RG-25`) |
| Changer la bourse de rattachement | Pour une session annoncée sur la mauvaise date |
| Retirer un livre de la session | Annule les mouvements de ce livre et corrige les quantités |
| Annuler la session entière | Annule tous ses mouvements |

Toute correction produit des mouvements tracés et attribués (`RG-35`) ; rien n'est
effacé.

Effet sur les alertes selon le moment (`RG-44`) : tant que le délai de 2 h suivant la
clôture n'est pas écoulé, les alertes en attente sont annulées ou recalculées avec la
correction. Passé ce délai, les e-mails sont partis et l'administrateur en est informé.

L'écran des sessions permet également d'agir directement sur la file d'attente :
annuler les alertes d'une session, ou forcer leur envoi immédiat sans attendre la fin
du délai.

---

## Modes de mise à disposition et publication

Ce bloc remplace la mécanique de lot et de carton, écartée en `Q-01`.

### `RG-20` — Le mode est choisi avant de scanner et vaut pour toute la session
Avant son premier scan, le bénévole choisit entre `DISPONIBLE MAINTENANT` et
`PROCHAINE BOURSE`. **Ce choix détermine l'effet public de tous les scans de la
session** et ne peut pas être modifié en cours de session : le changer reviendrait à
produire une session au contenu hétérogène, impossible à reprendre en bloc (`RG-25`).

Pour changer de mode, on termine la session en cours et on en ouvre une nouvelle.

Le mode actif est affiché en permanence, en toutes lettres et sur fond de couleur
distincte (`ENF-19`). C'est la seule protection contre l'erreur décrite en `RG-25`.

### `RG-21` — Effet du mode `DISPONIBLE MAINTENANT`
Chaque livre gardé incrémente immédiatement la quantité disponible. Le livre apparaît
au catalogue public comme disponible, et les alertes correspondantes partent
(`RG-28`).

### `RG-22` — Effet du mode `PROCHAINE BOURSE`
Chaque livre gardé incrémente la quantité annoncée, rattachée à la prochaine session de
bourse à venir. Le livre apparaît au catalogue public comme **annoncé pour cette date**,
et non comme disponible. Il ne peut pas être vendu en caisse tant que la bascule n'a pas
eu lieu.

Le rattachement se fait à la bourse à venir la plus proche à la date du scan, telle que
définie dans l'agenda (`RG-36`).

### `RG-23` — Bascule automatique
À la date d'ouverture de la bourse de rattachement, tous les exemplaires annoncés pour
cette bourse deviennent disponibles. La quantité annoncée passe en quantité disponible.

**Cette bascule est automatique. Aucun geste humain ne la déclenche et aucun oubli ne
peut la retarder.** C'est ce qui remplace le geste de mise en rayon.

Aucune alerte n'est envoyée à la bascule : elle l'a déjà été à l'annonce (`RG-28`).

### `RG-24` — Annonce sans bourse datée
Si aucune bourse à venir n'est inscrite à l'agenda au moment du scan, le mode
`PROCHAINE BOURSE` reste utilisable. Les exemplaires sont alors annoncés **sans date**
et se rattachent automatiquement à la prochaine bourse dès sa création.

Conséquences :

- Le catalogue public affiche « prochainement disponible, date à préciser ».
- **Les alertes correspondantes ne partent pas immédiatement** : elles sont mises en
  attente et envoyées quand la date est connue. Une alerte sans date n'aiderait
  personne à se déplacer.
- Un écran d'administration liste les annonces en attente de date (`05` §4). Une
  quantité importante d'annonces sans date est le signe d'un agenda non tenu.

### `RG-25` — Reprise en bloc d'une session
Un administrateur peut rebasculer **une session de scan entière** d'un mode à l'autre,
en une seule action. La reprise :

- inverse les mouvements produits et les rejoue dans le mode correct,
- corrige les quantités disponibles et annoncées en conséquence,
- marque la session comme `REPRISE`, sans effacer son historique.

Cette règle traite **l'erreur la plus probable et la plus silencieuse du système** :
une session de deux cents livres scannée dans le mauvais mode. Sans elle, la seule
issue serait une correction fiche par fiche.

**Le moment de la correction est déterminant** (`RG-44`) :

| Quand | Effet sur les alertes |
|---|---|
| Session encore ouverte | Aucune alerte n'est en file. Correction intégrale. |
| Session close, délai de 2 h non écoulé | Les alertes sont en attente et sont **annulées** avec la correction. Correction intégrale, invisible du public. |
| Délai écoulé | Les e-mails sont partis. Les quantités sont rétablies, mais l'administrateur est informé de ce qui n'est plus rattrapable. |

En pratique, une erreur repérée dans les deux heures suivant la fin d'un tri se corrige
donc entièrement. Au-delà, seul l'état du catalogue est rétabli.

### `RG-26` — Fiche à quantité nulle
Une fiche dont les quantités disponible et annoncée tombent à zéro **reste dans le
catalogue public**, marquée « épuisé », et reste ajoutable à une liste de recherche.
C'est le cas d'usage central des alertes.

---

## Listes de recherche et alertes

### `RG-27` — Taille d'une liste de recherche
Limitée à un nombre raisonnable d'entrées par membre *(valeur de départ : 100)*.

### `RG-28` — Constitution d'une alerte
Une alerte est **constituée** lorsqu'un ISBN de la liste d'un membre est rendu
disponible ou annoncé pour une bourse datée, si son compte est actif et ses alertes non
suspendues.

Elle est constituée **au scan**, mise en file **à la clôture de la session**, et
**envoyée 2 heures plus tard** (`RG-44`). Elle n'est jamais constituée à la bascule
automatique : le membre a déjà été prévenu, avec la date.

Le message diffère selon le mode :

| Mode de la session | Message |
|---|---|
| `DISPONIBLE MAINTENANT` | « disponible dès à présent », avec les dates de la prochaine ouverture |
| `PROCHAINE BOURSE`, date connue | « sera disponible à la bourse du 14 mars » |
| `PROCHAINE BOURSE`, date inconnue | alerte différée jusqu'à ce que la date existe (`RG-24`) |

Annoncer tôt avec une date est délibéré : le membre a le temps de s'organiser, et la
promesse reste tenable puisqu'elle est datée. C'est ce qui remplace la garantie
apportée auparavant par le geste de mise en rayon.

### `RG-29` — Regroupement
Plusieurs livres d'un même membre rendus disponibles ou annoncés dans une même session
donnent lieu à **un seul e-mail** listant tous les titres concernés.

Le regroupement découle directement de `RG-44` : puisque l'envoi a lieu à la clôture,
toutes les alertes d'une session sont connues au même instant. Aucun mécanisme de
temporisation supplémentaire n'est nécessaire.

### `RG-30` — Anti-répétition
Un même couple membre/ISBN ne peut pas donner lieu à plus d'une alerte sur une période
glissante *(valeur de départ : 30 jours)*.

### `RG-31` — Adresse en échec
Après plusieurs échecs de remise consécutifs, les alertes du membre sont suspendues et
l'information lui est présentée à sa prochaine connexion.

### `RG-32` — Absence de réservation
Une alerte n'engage à rien : le livre n'est ni mis de côté ni décompté. La mention
figure dans chaque e-mail.

---

## Ventes et fiabilité du stock

### `RG-33` — Rattachement d'une vente
Toute vente est rattachée à la session de bourse ouverte au moment du scan
(`AssoEvents` de type `Books`). Si aucune session n'est ouverte, la vente est
enregistrée sans rattachement et signalée à l'administration.

### `RG-34` — Remise à plat de l'inventaire
La quantité disponible est un compteur, non un inventaire physique : elle dérive à
chaque vente non scannée.

Une remise à plat est prévue **au minimum après chaque bourse**. Elle permet d'ajuster
les quantités à partir d'un comptage physique, total ou par échantillon. L'écart
constaté est conservé et suivi dans le temps : il constitue l'indicateur de la
discipline de scan en caisse.

Cette règle est la contrepartie directe du choix de suivi par ISBN (`01` §6). Sans
elle, le catalogue public devient faux et le reste.

### `RG-35` — Traçabilité des corrections
Toute correction de quantité produit un mouvement daté et attribué à son auteur. Une
quantité n'est jamais modifiée en silence. Cela vaut aussi pour les mouvements générés
par une reprise de session (`RG-25`) et par la bascule automatique (`RG-23`).

### `RG-36` — Source unique des dates de bourse
Les dates, horaires et adresses affichés sur le site public, ainsi que **la date qui
déclenche la bascule automatique**, proviennent des `AssoEvents` existants. Aucune
ressaisie.

Cette règle a pris une importance nouvelle avec l'abandon du geste de mise en rayon :
l'agenda n'est plus seulement de l'affichage, il pilote la disponibilité réelle du
catalogue.

### `RG-37` — Vente sur quantité nulle
Un livre scanné en caisse alors que sa quantité disponible est déjà à zéro est **vendu
malgré tout** : le client l'a en main. La quantité reste à zéro, la vente est
enregistrée, et l'écart est comptabilisé pour la remise à plat.

Si le livre est annoncé mais pas encore basculé, la caisse le signale au caissier
— l'exemplaire n'était pas censé être en rayon — mais **ne bloque pas la vente**.

### `RG-38` — Bourse déplacée, annulée ou supprimée
Les annonces suivent leur bourse de rattachement :

| Événement | Effet |
|---|---|
| Date avancée ou reculée | La bascule suit la nouvelle date. Aucune alerte de correction n'est envoyée en v1. |
| Bourse annulée ou supprimée | Les exemplaires annoncés reviennent à l'état « annoncé sans date » (`RG-24`) et se rattachent à la bourse suivante. |
| Bourse dont la date est déjà passée | La bascule s'exécute au rattrapage, à la première occasion. Un retard technique ne doit jamais laisser des exemplaires bloqués en annoncé. |

---

## Droits et sécurité

### `RG-40` — Droits par mode
Le tri et la caisse sont soumis à des droits distincts. Un bénévole ne voit que les
modes qui lui sont ouverts.

### `RG-41` — Attribution des gestes
Tout mouvement porte l'identité du bénévole qui l'a produit et la session dont il
relève, afin de permettre la reprise en bloc (`RG-25`).

### `RG-42` — Confidentialité des demandeurs
L'identité des membres qui recherchent un livre n'est jamais exposée dans
l'application de scan, ni sur le site public. Seul un décompte est affiché.
