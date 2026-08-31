# 06 — Règles métier

Chaque règle est numérotée, formulée pour être vérifiable, et référencée depuis les
autres documents. Les valeurs de seuil citées sont des **valeurs de départ à ajuster** :
elles sont paramétrables (`05` §9) et seront toutes fausses au premier essai.

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
Le même ISBN scanné deux fois en moins de 5 secondes dans le même mode affiche
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
Déclenché lorsque la quantité en rayon **plus** la quantité déjà présente dans les lots
non encore mis en rayon atteint le seuil de doublon *(valeur de départ : 5)*.

Le comptage inclut les lots en attente : sans cela, deux bénévoles triant en parallèle
garderaient chacun cinq exemplaires du même titre le même jour.

### `RG-11` — Verdict « premier exemplaire »
Aucun exemplaire en rayon, aucun en lot, aucune vente passée : la fiche est nouvelle
pour l'association.

### `RG-12` — Signal « ce titre se vend »
Affiché dès que les ventes cumulées atteignent le seuil de demande
*(valeur de départ : 1)*.

Le nombre de ventes est affiché en clair plutôt qu'interprété : « vendu 7 fois » est
plus utile à un bénévole qu'un jugement de l'application.

### `RG-13` — Signal « recherché »
Affiché si au moins un membre actif a cet ISBN dans sa liste de recherche. Le nombre de
demandeurs est affiché, **jamais leur identité**.

### `RG-14` — Signal « livre de valeur »
Déclenché si la valeur estimée dépasse le seuil de rareté *(valeur de départ : 15 €)*.
Le livre est orienté vers le bac « livres rares » pour expertise, **jamais tarifé
automatiquement** : le prix reste une décision humaine.

Si aucune source de valeur n'est disponible, cette règle s'appuie sur le repli défini
en `Q-02`.

### `RG-15` — Priorité des verdicts
Un livre peut relever de plusieurs règles à la fois. Un seul verdict principal est
affiché, dans cet ordre :

1. Livre de valeur (`RG-14`)
2. Recherché par un membre (`RG-13`)
3. Ce titre se vend (`RG-12`)
4. Inutile d'en garder (`RG-10`)
5. Premier exemplaire (`RG-11`)

**« Recherché » et « ça se vend » l'emportent délibérément sur « trop d'exemplaires ».**
Un sixième exemplaire d'un titre que quelqu'un attend doit être gardé. Les signaux non
retenus comme verdict principal restent visibles dans le détail sous la fiche.

### `RG-16` — Aucune décision automatique
L'application n'écarte jamais un livre d'elle-même. Elle informe ; le bénévole décide.

---

## Enregistrement des gestes de tri

### `RG-17` — Annulation du dernier scan
Le dernier geste enregistré peut être annulé depuis l'écran de scan, sans passer par
un menu. L'annulation supprime le mouvement correspondant.

### `RG-18` — Fenêtre d'annulation
L'annulation reste possible sur les gestes de la session en cours, et non sur le seul
dernier scan, tant que la session n'est pas clôturée.

### `RG-19` — Scan suivant valant validation
En mode tri, scanner un nouveau livre vaut « garder » pour le précédent. Ce
comportement doit être vérifié au palier 0 : s'il produit trop de faux « gardés », il
est remplacé par une validation explicite.

---

## Mise en rayon et publication

### `RG-20` — Fait générateur unique
**Un livre ne devient visible sur le site public et ne déclenche aucune alerte qu'au
moment de sa mise en rayon effective.** Ni le tri, ni la constitution d'un lot, ni la
fermeture d'un carton ne produisent le moindre effet public.

C'est la règle centrale du système. Toute exception la viderait de son sens.

### `RG-21` — Non-répétition de la mise en rayon
Un lot déjà mis en rayon ne peut pas l'être une seconde fois. Un nouveau scan de son
étiquette affiche l'information de mise en rayon sans rien modifier.

### `RG-22` — Fiche à quantité nulle
Une fiche dont la quantité tombe à zéro **reste dans le catalogue public**, marquée
« épuisé », et reste ajoutable à une liste de recherche. C'est le cas d'usage central
des alertes.

---

## Listes de recherche et alertes

### `RG-23` — Taille d'une liste de recherche
Limitée à un nombre raisonnable d'entrées par membre *(valeur de départ : 100)*.

### `RG-24` — Déclenchement d'une alerte
Une alerte est envoyée à un membre lorsqu'un ISBN de sa liste passe en rayon
(`RG-20`), si son compte est actif et ses alertes non suspendues.

### `RG-25` — Regroupement
Plusieurs livres d'un même membre mis en rayon dans une même opération donnent lieu à
**un seul e-mail** listant tous les titres concernés.

### `RG-26` — Anti-répétition
Un même couple membre/ISBN ne peut pas donner lieu à plus d'une alerte sur une période
glissante *(valeur de départ : 30 jours)*.

### `RG-27` — Adresse en échec
Après plusieurs échecs de remise consécutifs, les alertes du membre sont suspendues et
l'information lui est présentée à sa prochaine connexion.

### `RG-28` — Absence de réservation
Une alerte n'engage à rien : le livre n'est ni mis de côté ni décompté. La mention
figure dans chaque e-mail.

---

## Ventes et fiabilité du stock

### `RG-30` — Rattachement d'une vente
Toute vente est rattachée à la session de bourse ouverte au moment du scan
(`AssoEvents` de type `Books`). Si aucune session n'est ouverte, la vente est
enregistrée sans rattachement et signalée à l'administration.

### `RG-31` — Remise à plat de l'inventaire
La quantité en rayon est un compteur, non un inventaire physique : elle dérive à chaque
vente non scannée.

Une remise à plat est prévue **au minimum après chaque bourse**. Elle permet d'ajuster
les quantités à partir d'un comptage physique, total ou par échantillon. L'écart
constaté est conservé et suivi dans le temps : il constitue l'indicateur de la
discipline de scan en caisse.

Cette règle est la contrepartie directe du choix de suivi par ISBN (`01` §6). Sans
elle, le catalogue public devient faux et le reste.

### `RG-32` — Traçabilité des corrections
Toute correction de quantité produit un mouvement daté et attribué à son auteur. Une
quantité n'est jamais modifiée en silence.

### `RG-33` — Source unique des dates de bourse
Les dates, horaires et adresses affichés sur le site public proviennent des
`AssoEvents` existants. Aucune ressaisie.

### `RG-34` — Vente sur quantité nulle
Un livre scanné en caisse alors que sa quantité en rayon est déjà à zéro est **vendu
malgré tout** : le client l'a en main. La quantité reste à zéro, la vente est
enregistrée, et l'écart est comptabilisé pour la remise à plat.

---

## Droits et sécurité

### `RG-40` — Droits par mode
Chaque mode de l'application de scan (tri, mise en rayon, caisse) est soumis à un droit
distinct. Un bénévole ne voit que les modes qui lui sont ouverts.

### `RG-41` — Attribution des gestes
Tout mouvement porte l'identité du bénévole qui l'a produit, afin de permettre la
correction en bloc d'une série d'erreurs (`05` §8).

### `RG-42` — Confidentialité des demandeurs
L'identité des membres qui recherchent un livre n'est jamais exposée dans
l'application de scan, ni sur le site public. Seul un décompte est affiché.
