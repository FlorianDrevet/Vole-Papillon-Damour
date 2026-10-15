# Demandes de livres saisies par l'administration — spécification fonctionnelle

> Statut : proposition, 2026-10-12. Étend le module
> [`docs/bourse-aux-livres/`](../../bourse-aux-livres/README.md) ; les règles nouvelles
> prennent la suite de la numérotation existante (`RG-77` à `RG-89`).

## 1. Contexte

La liste de recherche et ses alertes (`RG-46`, `RG-47`, `RG-27` à `RG-32`, `04` §7) supposent un compte
membre et une adresse e-mail. Or une partie du public de la bourse — en particulier des
personnes âgées — n'a pas internet. Aujourd'hui, la présidente tient pour elles **une
liste papier** des livres qu'elles cherchent et doit s'en souvenir au tri.

Deux problèmes en découlent :

- au scan, rien ne signale aux bénévoles qu'un livre est attendu par l'une de ces
  personnes ; il devient disponible et peut être vendu à quelqu'un d'autre ;
- un membre inscrit qui suit le même livre est alerté, et la personne sans internet,
  souvent inscrite depuis plus longtemps sur la liste papier, passe après lui.

## 2. Objectifs

| # | Objectif |
|---|---|
| O1 | Un administrateur saisit, pour une personne sans compte, ses coordonnées (nom, prénom, téléphone) et les livres qu'elle cherche. |
| O2 | Au tri, un livre attendu par une telle personne est repéré comme les autres livres recherchés, avec une mention « demande manuelle » et les coordonnées de la personne, pour être **mis de côté**. |
| O3 | L'administration dispose d'une liste des livres trouvés pour ces personnes et marque chacun **donné** ou **refusé**. |
| O4 | Un exemplaire mis de côté pour une demande manuelle **n'alerte pas** les membres qui suivent le même livre ; s'il est refusé, il rejoint le stock et les membres sont alertés. |
| O5 | Les coordonnées de ces personnes sont recueillies avec leur accord, vues par le moins de monde possible et effacées quand elles ne servent plus. |

## 3. Hors périmètre

- Les **livres rares** : une demande manuelle ne peut viser qu'une œuvre ou une édition,
  jamais une fiche rare.
- La **caisse** : une demande manuelle n'a aucun effet sur l'écran de caisse.
- Tout **contact automatique** de la personne (SMS, appel, courrier) : c'est la
  présidente qui appelle.
- Toute visibilité sur le **site public** : la personne n'a pas de compte, ses demandes
  n'apparaissent ni au catalogue ni dans les compteurs publics.
- La **conversion** d'une demande manuelle en compte membre.
- Une **priorité choisie à la main** entre plusieurs personnes : l'ancienneté décide
  (`RG-79`).

## 4. Vocabulaire et acteurs

| Terme | Définition |
|---|---|
| **Demandeur manuel** | Personne sans compte, enregistrée par un administrateur : nom, prénom, téléphone, accord de conservation. Un demandeur porte une ou plusieurs demandes. |
| **Demande manuelle** | Un livre cherché par un demandeur manuel, avec une portée `ŒUVRE` (défaut) ou `ÉDITION`, comme une entrée de liste de recherche (`RG-46`). |
| **Exemplaire mis de côté** | Exemplaire scanné au tri ou prélevé du stock pour une demande manuelle. Il n'est ni disponible ni annoncé ; le bénévole le met de côté, sans lieu de rangement géré par le système. |

| Acteur | Ce qu'il fait |
|---|---|
| **Administrateur** (rôle `Administration`, la présidente en pratique) | Crée et modifie les demandeurs et leurs demandes, prélève un exemplaire en stock, marque donné / refusé, annule, supprime. |
| **Bénévole de tri** (rôle `Tri`) | Voit le verdict « À mettre de côté » au scan et, en ligne, les coordonnées du demandeur pour ce livre ; met l'exemplaire de côté. |
| **Membre** | N'a aucune action : il est simplement alerté plus tard, ou pas, selon `RG-81` et `RG-84`. |
| **Demandeur manuel** | N'a aucun accès au système. Il est appelé par la présidente. |

## 5. Cycle de vie d'une demande manuelle

```text
                 scan au tri (RG-79)
                 ou prélèvement (RG-82)
EN RECHERCHE ─────────────────────────────► TROUVÉE ──── donné (RG-83) ────► DONNÉE  (close)
     ▲                                         │
     │   refusé + « continuer à chercher »     │
     └─────────────────────────────────────────┤
                                               └── refusé + « clore » (RG-84) ► REFUSÉE (close)

EN RECHERCHE ou TROUVÉE ── annuler (RG-86) ──► ANNULÉE (close)

TROUVÉE ── scan annulé ou livre retiré de sa session (RG-85) ──► EN RECHERCHE
```

Une demande **trouvée** est toujours liée à **un** exemplaire mis de côté, et un
exemplaire mis de côté sert **une** demande.

## 6. Parcours

### Parcours A — Saisie depuis la liste papier

1. La présidente ouvre *Demandes manuelles* (E1, E2), puis l'onglet « Personnes ».
2. Elle recherche d'abord le nom ou le téléphone. Si la personne existe, elle ouvre sa
   fiche ; sinon, elle crée une nouvelle fiche et saisit les coordonnées avec l'accord
   de la personne (E3).
3. Elle ajoute un livre : recherche par titre, auteur ou ISBN dans le référentiel
   bibliographique, choix de la portée (E4). La demande est créée « en recherche ».
4. Elle ajoute les autres livres de cette personne de la même façon.

### Parcours B — Le livre est déjà en stock

1. À l'ajout du livre (E4), l'encart indique « Déjà 2 exemplaires disponibles ».
2. La présidente choisit « Prélever un exemplaire » : le stock disponible passe à 1, la
   demande passe directement à « trouvée » (`RG-82`).
3. Elle prend le livre dans le stock, puis le traite comme au parcours D.

### Parcours C — Le livre arrive au tri

1. Un bénévole de tri scanne *Le Petit Prince* ; une demande manuelle de portée `ŒUVRE`
   est en recherche pour ce titre.
2. L'écran affiche le verdict « À METTRE DE CÔTÉ — demande manuelle », et en ligne
   « Mme Jeanne Martin · 06 12 34 56 78 » (E6).
3. Le bénévole met le livre de côté. La demande passe à
   « trouvée » ; aucun membre n'est alerté pour cet exemplaire (`RG-81`).
4. Un second exemplaire du même titre est scanné plus tard : s'il ne reste aucune
   demande manuelle en recherche pour ce livre, il suit le circuit normal (verdict
   habituel, stock, alertes).

### Parcours D — Remise ou refus

1. Le tableau de bord (E7) et la barre latérale (E1) indiquent « 1 livre à remettre ».
2. La présidente ouvre l'onglet *Trouvés à remettre* (E2), appelle Mme Martin.
3. a. Mme Martin vient le chercher : « Donné » (E5). La demande est close, l'exemplaire
      sort définitivement (`RG-83`).
   b. Mme Martin ne veut plus du livre : « Refusé » puis « Clore la demande » (E5).
   c. Mme Martin veut une autre édition, plus lisible : « Refusé » puis « Continuer à
      chercher ».
   Dans les cas b et c, l'exemplaire entre en stock et les membres qui suivent le livre
   sont alertés immédiatement (`RG-84`).

### Parcours E — La personne ne cherche plus rien

1. La présidente annule une demande (`RG-86`), ou supprime le demandeur à sa demande
   (`RG-88`).
2. Sans action, les coordonnées sont effacées trois mois après la dernière demande close
   (`RG-89`).

## 7. Écrans

| # | Écran | Statut | Écran d'origine |
|---|---|---|---|
| E1 | Barre latérale de l'administration | *modifié* | barre latérale (F-11, E4 de `11-signalement-livre-introuvable.md`) |
| E2 | Liste « Demandes manuelles » | *nouveau* | grammaire de « Livres introuvables » (F-11, E5) |
| E3 | Fiche demandeur (création et modification) | *nouveau* | — |
| E4 | Ajout d'un livre à un demandeur | *nouveau* | ajout de fiche de l'espace Inventaire (`05` §4) |
| E5 | Traitement d'un livre trouvé (donné / refusé) | *nouveau* | dialogue de vérification (F-11, E6) |
| E6 | Scanette — écran de résultat, verdict « À mettre de côté » | *modifié* | verdict « À garder » (`03` §3.3, maquette `VerdictGarder`) |
| E7 | Tableau de bord de l'administration | *modifié* | tableau de bord (F-11, E7) |

### E1 — Barre latérale (modifié)

Nouvelle entrée dans le groupe *Pendant la bourse*, après *Livres introuvables* :

```text
PENDANT LA BOURSE
  ▦ Tableau de bord
  ⌗ Sessions de scan        3
  ⤓ Désengorgement
  ⚑ Livres introuvables     7
  ☎ Demandes manuelles      2    ← nouveau, compteur = demandes trouvées à remettre
```

Le compteur n'apparaît que s'il est supérieur à 0. L'entrée n'est visible que du rôle
`Administration` (`RG-77`).

### E2 — Liste « Demandes manuelles » (nouveau)

```text
Travail
Demandes manuelles.
Livres recherchés par des personnes sans internet. Quand un livre est trouvé,
appelez la personne puis indiquez s'il a été donné ou refusé.

[ Gérer les personnes ]

[ Trouvés à remettre 2 ] [ En recherche 14 ] [ Closes 31 ] [ Personnes 42 ]
Rechercher : [ nom, téléphone ou titre… ]

┌──────────────────────────────────────────────────────────────────────────────┐
│ Le Petit Prince · A. de Saint-Exupéry          Trouvé le 12 oct. au tri      │
│ Gallimard · 1999 (édition mise de côté)        Session de Michel             │
│ Jeanne MARTIN · 06 12 34 56 78                 Demandé le 3 sept.            │
│                                          [ Donné ]  [ Refusé… ]             │
└──────────────────────────────────────────────────────────────────────────────┘
```

- **Trouvés à remettre** (onglet par défaut) : demandes « trouvées », de la plus
  ancienne trouvaille à la plus récente. Chaque ligne indique l'édition mise de côté,
  l'origine (scan au tri avec le nom du bénévole, ou prélèvement en stock) et la date.
- **En recherche** : demandes en attente, avec le titre, la portée, le demandeur, la date
  de la demande, et l'action « Annuler ». Une mention « déjà en stock » apparaît si un
  exemplaire est disponible ou annoncé, avec l'action « Prélever » (`RG-82`).
- **Closes** : demandes données, refusées ou annulées, avec la date et l'auteur du
  geste. Les demandes dont les coordonnées ont été effacées affichent « Demandeur
  effacé » (`RG-89`).
- La recherche porte sur le nom, le prénom, le téléphone et le titre.
- Un clic sur le nom du demandeur ouvre sa fiche (E3).
- L'onglet **Personnes** liste les fiches dont les coordonnées sont conservées et permet
  de les rechercher par nom, prénom ou téléphone. L'ouverture d'une fiche permet
  d'ajouter d'autres livres sans recréer la personne. La création d'une nouvelle fiche
  n'est activée qu'après une recherche ; les correspondances restent visibles à côté de
  l'action de création.
- **État vide** de *Trouvés à remettre* : « Aucun livre à remettre pour le moment. »

### E3 — Fiche demandeur (nouveau)

```text
← Demandes manuelles

Demandeur
Jeanne MARTIN

Nom *        [ Martin                ]
Prénom *     [ Jeanne                ]
Téléphone *  [ 06 12 34 56 78        ]
[x] La personne a accepté que l'association conserve ses coordonnées pour la
    rappeler au sujet de ces livres. *

                                                      [ Enregistrer ]

Livres recherchés                                     [ + Ajouter un livre ]
  Le Petit Prince        Œuvre     Trouvé — à remettre     [ Donné ] [ Refusé… ]
  Les Misérables (Folio) Édition   En recherche            [ Annuler ]
  Le Grand Meaulnes      Œuvre     Donné le 2 oct.

Supprimer ce demandeur et ses demandes…
```

- Nom, prénom, téléphone et accord sont obligatoires ; un téléphone français ou
  international est accepté, affiché par groupes de deux chiffres.
- « Supprimer ce demandeur » demande une confirmation et applique `RG-88`.
- La fiche n'est créée qu'après la recherche dans l'onglet *Personnes* ; une personne
  existante est réutilisée en ouvrant sa fiche.
- À la création, la fiche s'ouvre directement sur l'ajout d'un premier livre (E4).

### E4 — Ajout d'un livre (nouveau)

```text
Ajouter un livre pour Jeanne MARTIN

Rechercher  [ titre, auteur ou ISBN            ] [ Chercher ]
Portée
  (●) Toutes les éditions d'une œuvre (par défaut)
  ( ) Uniquement une édition

Éditions trouvées (défilement dans cette zone)
  ○ Le Petit Prince — Antoine de Saint-Exupéry — Gallimard, 1999
  ● Le Petit Prince — Antoine de Saint-Exupéry — Folio, 2007
  (cliquer une édition sélectionne automatiquement « Uniquement une édition »)

[ Annuler ]                                        [ Ajouter la demande ]

┌ Déjà à la bourse ───────────────────────────────────────────┐
│ 2 exemplaires disponibles · 1 annoncé pour le 14 mars       │
│ [ Prélever un exemplaire ]                                  │
└─────────────────────────────────────────────────────────────┘

                                  [ Annuler ]  [ Ajouter la demande ]
```

- La recherche réutilise le référentiel bibliographique des membres (`RG-47`) : un livre
  jamais reçu peut être demandé.
- La portée `ŒUVRE` est proposée par défaut (`RG-46`). Si la notice choisie n'est
  rattachée à aucune œuvre, seule la portée `ÉDITION` est proposée, avec la mention
  « Ce livre ne peut être suivi que dans cette édition ».
- L'encart « Déjà à la bourse » n'apparaît que si des exemplaires correspondants sont
  disponibles ou annoncés. « Prélever un exemplaire » crée la demande directement à
  l'état « trouvée » (`RG-82`) ; « Ajouter la demande » la crée « en recherche ».
- Une demande identique (même demandeur, même cible) encore ouverte est refusée :
  « Jeanne Martin cherche déjà ce livre. »
- **Cas d'erreur** : référentiel indisponible → « La recherche de livres est
  momentanément indisponible. Réessayez dans quelques minutes. »

### E5 — Traitement d'un livre trouvé (nouveau)

Ouvert depuis « Donné » ou « Refusé… » en E2 ou E3.

```text
Donné                                   Refusé
─────────────────────────────           ─────────────────────────────────────────
Le Petit Prince a été remis à           Jeanne Martin ne prend pas ce livre.
Jeanne Martin ?                         Le livre rejoindra le stock et les
                                        membres qui le suivent seront prévenus
L'exemplaire sort définitivement        par e-mail dès maintenant.
du stock de la bourse.
                                        Et ensuite ?
     [ Annuler ] [ Confirmer ]          (●) Clore la demande
                                        ( ) Continuer à chercher ce livre
                                            pour elle

                                        Mise à disposition : disponible maintenant
                                        (ou : annoncé pour la bourse du 14 mars)

                                             [ Annuler ] [ Confirmer le refus ]
```

- Le mode de mise à disposition affiché est calculé selon `RG-84` ; il n'est pas
  modifiable.
- Après confirmation, la ligne quitte l'onglet *Trouvés à remettre* et un message
  confirme le geste (« Livre marqué comme donné », « Refus enregistré — 3 membres
  prévenus »).

### E6 — Scanette, verdict « À mettre de côté » (modifié)

Reprend l'écran de résultat existant à l'identique ; seuls le bandeau de verdict et un
encart changent.

```text
┌───────────────────────────────────────┐
│ ✅ DISPONIBLE MAINTENANT               │
│ Michel   ·   42 triés                 │
├───────────────────────────────────────┤
│ ╔═══════════════════════════════════╗ │
│ ║  🔵   À METTRE DE CÔTÉ            ║ │
│ ║       Demande manuelle            ║ │
│ ╚═══════════════════════════════════╝ │
│ ┌ Pour ──────────────────────────────┐│
│ │ Jeanne MARTIN                      ││
│ │ 06 12 34 56 78                     ││
│ └────────────────────────────────────┘│
│  ┌────┐                               │
│  │couv│  Le Petit Prince              │
│  │    │  Antoine de Saint-Exupéry     │
│  └────┘  Gallimard · 1999             │
│                                       │
│  Disponibles    0                     │
│  Annoncés       0                     │
│  Déjà vendus    2                     │
│  Recherché par  3 membres             │
├───────────────────────────────────────┤
│   ┌──────────┐      ┌──────────────┐  │
│   │  ÉCARTER │      │ METTRE DE CÔTÉ│ │
│   └──────────┘      └──────────────┘  │
└───────────────────────────────────────┘
```

- **Hors ligne** ou coordonnées non chargées : l'encart « Pour » affiche « Coordonnées
  dans l'administration → Demandes manuelles » à la place du nom et du téléphone. Le
  verdict et la mise de côté sont identiques (`RG-80`).
- Le bouton principal s'intitule « Mettre de côté » au lieu de « Garder » ; scanner le
  livre suivant vaut toujours validation (`RG-19`).
- « Écarter » reste possible : l'exemplaire n'est alors pas mis de côté et la demande
  reste en recherche.
- Le signal « livre rare » s'affiche en complément, comme aujourd'hui (`RG-15`).
- Le récapitulatif de fin de session (`03` §3.6) indique « N livres mis de côté pour des
  demandes manuelles ».

### E7 — Tableau de bord (modifié)

Nouvelle tuile à côté de « Livres à vérifier en rayon » :

```text
☎ 2 livres à remettre          → Ouvrir la liste
   dont 1 trouvé il y a plus de 14 jours
```

La tuile n'apparaît que s'il y a au moins une demande trouvée.

## 8. Règles métier

### `RG-77` — Gestion réservée à l'administration
Seul le rôle `Administration` crée, consulte, modifie, annule ou supprime les
demandeurs manuels et leurs demandes, et marque une demande donnée ou refusée.

### `RG-78` — Un demandeur, plusieurs demandes
Un demandeur manuel porte un nom, un prénom, un téléphone (obligatoires) et l'accord de conservation (`RG-87`). Il peut avoir plusieurs demandes ;
chaque demande vise une œuvre ou une édition (`RG-46`), choisie dans le référentiel
(`RG-47`). Un demandeur ne peut pas avoir deux demandes ouvertes sur la même cible. La
limite de taille de `RG-27` s'applique par demandeur.

### `RG-79` — Mise de côté au tri, par ancienneté
Lorsqu'un exemplaire est scanné en mode tri (`DISPONIBLE MAINTENANT` ou `PROCHAINE
BOURSE`) et qu'au moins une demande manuelle **en recherche** le vise (cet ISBN, ou une
édition de cette œuvre), l'exemplaire est **mis de côté** au profit de la demande la
plus ancienne, qui passe à l'état « trouvée ». Un exemplaire sert une seule demande.
L'exemplaire mis de côté n'est ni disponible ni annoncé et n'apparaît pas au catalogue
public. Les exemplaires suivants servent les demandes manuelles suivantes, puis
suivent le circuit normal.

Le verdict « À METTRE DE CÔTÉ — demande manuelle » passe **avant** tous ceux de `RG-15` ;
le signal « livre rare » reste affiché en complément. Le bénévole reste libre d'écarter
le livre (`RG-16`) : la demande reste alors en recherche.

### `RG-80` — Exception à la confidentialité des demandeurs
Par exception à `RG-42`, l'écran de résultat du tri affiche le nom, le prénom et le
téléphone du demandeur manuel servi, **pour ce seul exemplaire**, lorsque l'appareil est
en ligne. Aucune donnée personnelle de demandeur manuel n'est copiée sur les appareils
de scan pour le hors-ligne : sans réseau, seule la mention « demande manuelle » est
affichée, et la mise de côté a lieu à l'identique. Pour les membres, `RG-42` reste inchangée.

### `RG-81` — Pas d'alerte membre pour un exemplaire mis de côté
Un exemplaire mis de côté ne constitue aucune alerte (`RG-28`), même si des membres
suivent ce livre. Le compteur « recherché par N membres » reste affiché à la scanette.

### `RG-82` — Prélèvement dans le stock
À l'ajout d'une demande, si des exemplaires correspondants sont disponibles ou annoncés,
l'administrateur peut **prélever** un exemplaire : la quantité correspondante diminue
d'une unité par un mouvement tracé et attribué (`RG-35`), et la demande est créée
directement à l'état « trouvée ». Le même geste est proposé pour une demande déjà en
recherche lorsque le livre est en stock. Le prélèvement prend un exemplaire disponible
en priorité, sinon un exemplaire annoncé.

### `RG-83` — Donné
Marquer une demande trouvée comme **donnée** la clôt. L'exemplaire mis de côté sort
définitivement ; le geste est tracé et attribué. Il **ne compte pas comme une vente** :
il n'alimente ni le signal « ce titre se vend » (`RG-12`) ni l'historique des ventes.

### `RG-84` — Refusé
Marquer une demande trouvée comme **refusée** fait entrer l'exemplaire en stock :

| Origine de l'exemplaire | Mise à disposition |
|---|---|
| Scanné en `DISPONIBLE MAINTENANT` | Disponible |
| Scanné en `PROCHAINE BOURSE`, bourse encore à venir | Annoncé pour cette bourse |
| Scanné en `PROCHAINE BOURSE`, bourse passée ou annulée | Disponible |
| Prélevé depuis le stock (`RG-82`) | Revient dans la quantité d'où il a été prélevé, ou disponible si la bourse annoncée est passée |

Les alertes des membres sont **constituées et envoyées immédiatement**, sans le délai de
2 heures de `RG-44`, en appliquant `RG-28` (comptes actifs, alertes non suspendues),
`RG-30` (anti-répétition) et `RG-29` (un seul e-mail par membre pour un même geste). Le
message est celui du mode retenu.

L'administrateur choisit ensuite : **clore la demande** (état « refusée ») ou
**continuer à chercher** (retour à « en recherche », avec son ancienneté d'origine).
Le refus est confirmé avant d'être appliqué.

### `RG-85` — Corrections du tri
Si le scan qui a mis un exemplaire de côté est annulé (`RG-17`, `RG-18`), ou si le livre
est retiré de sa session ou la session annulée (`RG-45`), la demande revient à
« en recherche » et l'exemplaire n'est plus mis de côté. Changer le mode d'une session
(`RG-25`) ne touche pas aux exemplaires mis de côté ; il change seulement le mode
appliqué en cas de refus ultérieur. Une demande déjà donnée ou refusée n'est pas
modifiée par une correction de session.

### `RG-86` — Annulation
Une demande en recherche ou trouvée peut être annulée. Si elle était trouvée,
l'exemplaire mis de côté est traité comme un refus (`RG-84`) ; une confirmation le
précise.

### `RG-87` — Accord de conservation
Un demandeur ne peut être enregistré qu'après avoir coché l'accord : « La personne a
accepté que l'association conserve ses coordonnées pour la rappeler au sujet de ces
livres. » La date et l'administrateur qui l'a recueilli sont conservés.

### `RG-88` — Suppression d'un demandeur
Un administrateur peut supprimer un demandeur à tout moment, notamment à sa demande. Ses
demandes ouvertes sont annulées (`RG-86`, donc refus pour les exemplaires trouvés), puis
ses coordonnées sont effacées. Les demandes closes restent, anonymes, pour
l'historique et les statistiques.

### `RG-89` — Effacement automatique
Les coordonnées d'un demandeur sont effacées automatiquement **trois mois**
après la clôture de sa dernière demande, s'il n'en a aucune ouverte. Ses demandes closes
restent, avec la mention « Demandeur effacé ».

## 9. Critères d'acceptation

1. Un administrateur crée un demandeur avec nom, prénom, téléphone et accord ; sans
   l'accord, l'enregistrement est refusé. Un utilisateur sans rôle `Administration`
   n'accède ni à l'écran ni aux données.
2. Il ajoute à ce demandeur un livre jamais reçu par l'association, en portée œuvre ;
   la demande apparaît « en recherche ».
3. Un bénévole scanne au tri une autre édition de cette œuvre : le verdict est « À mettre
   de côté — demande manuelle », avec nom, prénom et téléphone en ligne ; la demande est
   « trouvée » ; la quantité disponible et la quantité annoncée sont inchangées ; aucune
   fiche n'apparaît au catalogue public pour cet exemplaire.
4. Un membre qui suit la même œuvre ne reçoit aucune alerte pour cet exemplaire, ni à la
   clôture de session ni deux heures après.
5. Même scan sans réseau : le verdict et la mise de côté sont identiques, l'encart affiche
   la mention hors ligne, et aucune coordonnée n'est présente sur l'appareil.
6. Deux demandeurs cherchent la même œuvre : le premier exemplaire scanné sert la demande
   la plus ancienne, le second la suivante, le troisième suit le circuit normal et
   alerte les membres.
7. « Donné » clôt la demande ; l'exemplaire n'entre jamais en stock et le compteur de
   ventes du titre n'augmente pas.
8. « Refusé » + « Clore » sur un exemplaire scanné en `DISPONIBLE MAINTENANT` : la
   quantité disponible augmente d'une unité et les membres qui suivent le livre reçoivent
   un e-mail dans les minutes qui suivent, sans attendre deux heures.
9. « Refusé » + « Continuer à chercher » : la demande revient « en recherche » avec sa date
   d'origine, et le prochain exemplaire scanné la sert de nouveau.
10. À l'ajout d'un livre disponible en stock, « Prélever un exemplaire » diminue la
    quantité disponible d'une unité, trace le mouvement et crée la demande « trouvée ».
11. Annuler au tri le scan qui avait mis un exemplaire de côté remet la demande « en
    recherche ».
12. Supprimer un demandeur efface ses coordonnées ; ses demandes closes s'affichent
    « Demandeur effacé ». Trois mois après la clôture de la dernière demande d'un
    demandeur sans demande ouverte, ses coordonnées sont effacées automatiquement.
13. La barre latérale et le tableau de bord affichent le nombre de livres trouvés à
    remettre, et rien quand il est nul.

## 10. Contraintes

- **Hors-ligne de la scanette** (`03` §7, `07`) : l'appareil sait déjà à
  l'avance quels livres sont recherchés. Il doit savoir, de la même façon, qu'un livre
  fait l'objet d'une demande manuelle en recherche, **sans** recevoir de donnée
  personnelle (`RG-80`). Les coordonnées ne sont obtenues qu'en ligne, pour l'exemplaire
  qui vient d'être scanné, et réservées au rôle `Tri`.
- **Conflit d'attribution** : deux postes de tri peuvent scanner simultanément deux
  exemplaires du même titre ; le serveur décide de la demande servie, et un même
  exemplaire ne peut pas servir deux demandes.
- **Scan hors ligne transmis plus tard** : l'attribution de la demande se fait à la
  transmission du geste. Si la demande n'est plus en recherche entre-temps (servie par
  un autre poste, annulée), l'exemplaire suit le circuit normal et le récapitulatif de
  session le signale.
- **Alertes** : la constitution immédiate au refus contourne la file différée de `RG-44`
  mais réutilise le même envoi, le même modèle d'e-mail et le même historique
  anti-répétition.
- **RGPD** : le registre des traitements et la page « Vos données et le RGPD » doivent
  mentionner ce nouveau traitement (finalité, base légale, durée de trois mois, accès
  limité aux administrateurs et, ponctuellement, aux bénévoles de tri). `ENF-10` est
  complété : le téléphone n'est collecté que pour les demandeurs manuels.
- **Traçabilité** (`RG-35`, `RG-41`) : prélèvement, don, refus, annulation et
  suppression sont attribués à l'administrateur qui les fait.

## 11. Questions ouvertes

| # | Question | Proposition par défaut |
|---|---|---|
| QM-2 | Faut-il relancer la présidente pour un livre trouvé depuis longtemps sans décision ? | Simple mention « trouvé il y a plus de 14 jours » sur la tuile du tableau de bord, sans e-mail. |
| QM-3 | La base légale de ce traitement : consentement oral (`RG-87`) ou intérêt légitime de l'association ? | **Tranché (2026-10-12)** : consentement oral recueilli par la présidente, tracé par la case à cocher. |
