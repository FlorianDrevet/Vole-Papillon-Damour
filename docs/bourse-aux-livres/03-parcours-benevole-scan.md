# 03 — L'application de scan (parcours bénévole)

## 1. Contexte d'usage

Ce n'est pas une application de bureau. Elle est utilisée debout, à une main, dans le
bruit, souvent par des personnes qui ne se connecteront pas deux fois de suite au même
appareil. Le geste se répète des centaines de fois par session.

| Contrainte | Conséquence de conception |
|---|---|
| Une main tient le livre | Tout doit être atteignable au pouce, dans la moitié basse de l'écran |
| Des centaines de répétitions | Le cas nominal se fait **sans aucun appui sur l'écran** : on scanne, on lit, on scanne le suivant |
| Terminal de scan avec gâchette physique | La gâchette doit suffire. L'application n'impose jamais de valider un scan par un appui à l'écran |
| Téléphone sans gâchette | Un bouton de déclenchement large, permanent, en bas de l'écran |
| Bénévoles peu à l'aise avec le numérique | Aucun menu imbriqué. Un mode actif à la fois, affiché en permanence |
| Local possiblement mal couvert en réseau | Fonctionnement dégradé hors-ligne obligatoire — voir `ENF-05` |

**Règle d'ergonomie générale.** Le nombre d'appuis à l'écran par livre trié doit être
de **zéro dans le cas nominal** et de **un au maximum** dans les autres cas.

## 2. Connexion et choix du mode

Chaque bénévole a un compte individuel (décision arrêtée, `01` §6). La session reste
ouverte durablement sur l'appareil : on ne se reconnecte pas à chaque session de tri.

Au lancement, un seul choix, en gros boutons :

```
   ┌─────────────────────────────────────┐
   │   Bonjour Michel                    │
   │                                     │
   │   ┌───────────────────────────────┐ │
   │   │   📖   TRIER DES LIVRES       │ │
   │   └───────────────────────────────┘ │
   │   ┌───────────────────────────────┐ │
   │   │   💶   CAISSE                 │ │
   │   └───────────────────────────────┘ │
   │   ┌───────────────────────────────┐ │
   │   │   🔍   CONSULTER              │ │
   │   │        n'enregistre rien      │ │
   │   └───────────────────────────────┘ │
   │                                     │
   │   Changer d'utilisateur             │
   └─────────────────────────────────────┘
```

Seuls les modes autorisés par les droits du bénévole sont affichés (`RG-40`).

Seul `TRIER` ouvre une session (`RG-43`). La caisse enregistre des mouvements isolés
rattachés à la bourse en cours, et la consultation n'enregistre rien du tout.

**Le mode actif est visible en permanence** — bandeau de couleur distincte en haut de
l'écran, libellé en toutes lettres. Un scan de vente déclenché depuis le mode tri
serait une erreur coûteuse et silencieuse : la couleur du bandeau est la garantie
principale contre cette confusion.

Il n'existe pas de mode de mise en rayon : la publication ne repose sur aucun geste de
validation. Elle découle du mode de mise à disposition choisi à l'ouverture d'une
session de tri (§3.1) et, le cas échéant, d'une bascule automatique à la date de la
bourse (`RG-23`).

## 3. Mode TRI

### 3.1 Choisir le mode de mise à disposition

**C'est l'écran le plus important du dispositif, et le plus dangereux.** Il n'apparaît
qu'une fois, à l'ouverture d'une session de tri, et engage tous les scans qui suivent
(`RG-20`).

```
┌───────────────────────────────────────┐
│   Nouvelle session de tri             │
│   Ces livres seront…                  │
│                                       │
│   ┌───────────────────────────────┐   │
│   │  📗  DISPONIBLES MAINTENANT   │   │
│   │      mis en vente tout de     │   │
│   │      suite                    │   │
│   └───────────────────────────────┘   │
│                                       │
│   ┌───────────────────────────────┐   │
│   │  📅  À LA PROCHAINE BOURSE    │   │
│   │      annoncés en ligne pour   │   │
│   │      le 14 mars               │   │
│   └───────────────────────────────┘   │
│                                       │
└───────────────────────────────────────┘
```

- La date réelle de la prochaine bourse est affichée sur le bouton, jamais un libellé
  générique. Le bénévole doit voir ce qu'il promet au public.
- Si aucune bourse n'est programmée, le second bouton indique « date à préciser » et
  reste utilisable : les livres seront rattachés à la prochaine bourse créée (`RG-24`).
- Le mode ne peut pas être changé en cours de session. Pour en changer, on termine la
  session et on en ouvre une autre (`RG-20`).

### 3.2 Écran d'attente

L'écran de scan porte toute la valeur du produit. Il doit être lisible en une
demi-seconde, à bout de bras.

```
┌───────────────────────────────────────┐
│ 📅 PROCHAINE BOURSE (14 mars)         │  ← bandeau de mode, permanent
│ Michel   ·   127 triés                │
├───────────────────────────────────────┤
│                                       │
│              [ viseur ]               │
│                                       │
│      Scannez le code-barres           │
│                                       │
├───────────────────────────────────────┤
│         ⌨  Saisir un code             │
│  ┌─────────────────────────────────┐  │
│  │          S C A N N E R          │  │  ← téléphone uniquement
│  └─────────────────────────────────┘  │
└───────────────────────────────────────┘
```

Le compteur de la session (`127 triés`) est la seule statistique affichée : il entretient
le rythme sans distraire. Le bandeau de mode, lui, ne disparaît jamais.

### 3.3 Écran de résultat

Le verdict occupe le haut de l'écran, en couleur, lisible sans lire le détail.

**Cas « on en a déjà trop »**

```
┌───────────────────────────────────────┐
│ 📅 PROCHAINE BOURSE (14 mars)         │
│ Michel   ·   128 triés                │
├───────────────────────────────────────┤
│ ╔═══════════════════════════════════╗ │
│ ║  🔴   INUTILE D'EN GARDER         ║ │
│ ║       déjà 6 (4 dispo + 2 annonc.)║ │
│ ╚═══════════════════════════════════╝ │
│  ┌────┐                               │
│  │couv│  Le Petit Prince              │
│  │    │  Antoine de Saint-Exupéry     │
│  └────┘  Gallimard · 1999             │
│                                       │
│  Disponibles    4                     │
│  Annoncés       2                     │
│  Déjà vendus    2                     │
│  Recherché par  —                     │
├───────────────────────────────────────┤
│   ┌──────────┐      ┌──────────────┐  │
│   │  ÉCARTER │      │    GARDER    │  │
│   └──────────┘      └──────────────┘  │
└───────────────────────────────────────┘
```

**Cas « à garder absolument »**

```
│ ╔═══════════════════════════════════╗ │
│ ║  🟢   À GARDER                    ║ │
│ ║       2 personnes le recherchent  ║ │
│ ╚═══════════════════════════════════╝ │
```

**Cas « livre déjà marqué rare »** — marquage manuel par un administrateur (`05` §4).
L'estimation automatique de valeur, elle, n'existe pas en v1 et n'apparaîtra jamais sur
cet écran : son calcul est trop lent pour tenir le délai de scan (`RG-14`, `ENF-01`).

```
│ ╔═══════════════════════════════════╗ │
│ ║  🟣   BAC « LIVRES RARES »        ║ │
│ ╚═══════════════════════════════════╝ │
```

**Cas « livre inconnu du système »**

```
│ ╔═══════════════════════════════════╗ │
│ ║  ⚪   PREMIER EXEMPLAIRE          ║ │
│ ╚═══════════════════════════════════╝ │
```

Le verdict affiché est déterminé par les règles `RG-10` à `RG-16`, qui définissent
aussi leur ordre de priorité lorsque plusieurs s'appliquent.

### 3.4 Enchaîner sans toucher l'écran

**Scanner le livre suivant vaut « garder » pour le précédent.** C'est le comportement
par défaut, et c'est ce qui rend la cadence tenable : le bénévole regarde le verdict,
pose le livre dans le bon bac, et scanne le suivant. Zéro appui.

Le bouton `ÉCARTER` n'est utilisé que lorsque le bénévole écarte effectivement. Le
bouton `GARDER` est redondant avec le scan suivant ; il existe pour le dernier livre
d'une série et pour ceux qui préfèrent confirmer.

Ce choix est délibéré et mérite d'être testé au palier 0 : il rend le geste fluide mais
signifie qu'un scan par erreur compte comme un livre gardé. D'où `RG-17` (annulation du
dernier scan) et `RG-18` (le geste d'annulation reste accessible pendant toute la
session).

### 3.5 Saisie manuelle du code

Un code-barres illisible arrive régulièrement sur des livres d'occasion. L'écran de
saisie accepte un ISBN à 10 ou 13 chiffres tapé au clavier numérique, avec contrôle
de la clé de validité (`RG-01`).

C'est le seul cas où l'on tape. Il ne doit jamais être le chemin nominal.

### 3.6 Terminer la session

La session s'est ouverte au choix du mode (§3.1). Elle se clôture de quatre manières
(`RG-43`) :

| Cause | Détail |
|---|---|
| `TERMINER` | Le bénévole clôt lui-même sa session |
| Inactivité | Aucun scan pendant 2 heures |
| Déconnexion | Le bénévole quitte l'application |
| Jeton expiré | La session de travail suit la session d'authentification |

**La clôture met les e-mails d'alerte en file d'attente ; ils partent 2 heures plus
tard** (`RG-44`). Rien ne part pendant que le bénévole scanne, ni au moment où il
termine. Une erreur repérée dans les deux heures qui suivent se corrige donc encore
sans que personne n'ait été prévenu à tort.

L'écran de fin récapitule ce qui vient d'être fait et, surtout, **ce que cela a produit
publiquement** :

```
┌───────────────────────────────────────┐
│   Session terminée                    │
│   2 h 14 de tri                       │
│                                       │
│   230 livres scannés                  │
│   183 gardés                          │
│    47 écartés                         │
│                                       │
│   📅 Annoncés en ligne pour la        │
│      bourse du 14 mars                │
│                                       │
│   ✉ 6 personnes seront prévenues     │
│      dans 2 h qu'un livre qu'elles    │
│      cherchent sera disponible        │
│                                       │
│      En cas d'erreur, prévenez un     │
│      responsable avant l'envoi        │
└───────────────────────────────────────┘
```

Cet écran n'est pas décoratif. Le mode y est répété en clair et l'effet public est
énoncé en langage ordinaire, parce que **c'est le moment où le bénévole peut encore
constater qu'il a scanné dans le mauvais mode** — et où il lui reste deux heures pour
le signaler avant que quoi que ce soit ne parte.

Une session close par inactivité ou par expiration du jeton ne montre cet écran à
personne. Le délai de deux heures joue alors son rôle de filet : l'administration voit
les alertes en attente et peut encore les annuler (`05` §4 bis).

Si l'estimation de valeur marchande est un jour implémentée, c'est aussi sur cet écran
qu'apparaîtront les livres signalés comme potentiellement chers (`RG-14`) : le calcul
étant asynchrone, ses résultats arrivent après le scan, jamais pendant.

## 4. Ce qui remplace la mise en rayon

Il n'y a **aucun geste de mise en rayon**. La question `Q-01` a été tranchée dans un
sens qui supprime l'étape : la publication est décidée en amont, par le mode de la
session, et la disponibilité effective survient toute seule.

| Mode de la session | À la fin du scan | À la date de la bourse |
|---|---|---|
| `DISPONIBLE MAINTENANT` | Le livre est disponible et vendable immédiatement (`RG-21`) | — |
| `PROCHAINE BOURSE` | Le livre est annoncé en ligne avec sa date, non vendable (`RG-22`) | **Bascule automatique** en disponible (`RG-23`) |

Conséquences pour les bénévoles :

- **Personne n'a de geste supplémentaire à faire dans le local.** Le rangement physique
  des livres reste un travail manuel, mais il ne s'accompagne d'aucune saisie.
- **Rien ne peut être oublié.** Il n'existe pas de carton qu'on aurait négligé de
  déclarer : la bascule est pilotée par la date.
- **Le risque s'est déplacé.** Il ne porte plus sur un oubli, mais sur une erreur de
  mode au départ. C'est pourquoi le mode est répété en permanence à l'écran, rappelé à
  la clôture, et rattrapable en bloc par un administrateur (`RG-25`).

L'autre dépendance nouvelle est l'agenda : si la bourse n'est pas saisie ou si sa date
est fausse, la bascule l'est aussi. L'agenda n'est plus un simple affichage, il pilote
la disponibilité (`RG-36`).

## 5. Mode CAISSE

Utilisé pendant une session de bourse. L'application se rattache automatiquement à la
session de bourse ouverte (`RG-33`).

```
┌───────────────────────────────────────┐
│ 💶 CAISSE    Bourse du 14 mars        │
├───────────────────────────────────────┤
│                                       │
│  Le Petit Prince            1,00 €    │
│  Astérix chez les Belges    2,00 €    │
│  Atlas Larousse 1932       35,00 €  🟣│
│                                       │
│  ─────────────────────────────────    │
│  3 livres                  38,00 €    │
├───────────────────────────────────────┤
│  ↩ Annuler dernier   │   ENCAISSER    │
└───────────────────────────────────────┘
```

Points de conception :

- Le scan enchaîne sans confirmation : on scanne les livres d'un client à la suite.
- Un livre marqué rare (🟣) est **visuellement signalé** pour éviter qu'il parte au
  tarif ordinaire.
- `Annuler dernier` traite le cas fréquent du double scan, avant encaissement.
- **Une vente déjà encaissée peut être annulée** tant que la bourse est ouverte : le
  client change d'avis, une erreur est constatée après coup. La quantité disponible est
  rétablie et l'annulation est tracée (`RG-49`).
- Plusieurs postes de caisse peuvent fonctionner en parallèle sans coordination : chaque
  vente est un mouvement indépendant rattaché à la bourse (`RG-33`).
- `ENCAISSER` clôt la vente et enregistre les mouvements. Le calcul du rendu de monnaie
  n'est pas dans le périmètre : la caisse reste physique.
- **Un livre scanné alors que la quantité disponible est à zéro est quand même vendu** :
  le client le tient en main, la réalité prime sur le compteur. L'écart est enregistré
  pour la remise à plat (`RG-37`).
- **Un livre encore annoncé et non basculé est signalé au caissier** — il n'était pas
  censé être en rayon — mais la vente n'est pas bloquée pour autant (`RG-37`). C'est le
  signe que des livres ont été rangés en avance sur leur date d'annonce.

## 6. Mode CONSULTATION

On scanne, on voit la fiche, **rien n'est mémorisé** : aucun mouvement, aucune session,
aucune alerte. C'est l'outil de la sonde de faisabilité du palier 0 (`01` §7), et il est
**conservé ensuite** comme mode « je vérifie quelque chose » sans risque de polluer les
données.

Usage courant après le palier 0 : savoir combien d'exemplaires d'un titre sont en rayon
avant de réorganiser une étagère, ou lever un doute sans ouvrir une session de tri.

## 7. Cas d'erreur et de bord

| Situation | Comportement attendu |
|---|---|
| Code-barres illisible après plusieurs tentatives | Proposer la saisie manuelle après 5 secondes sans lecture |
| Code-barres qui n'est pas un ISBN (produit alimentaire, code interne) | Message explicite « ce n'est pas un code de livre », pas d'enregistrement (`RG-02`) |
| ISBN valide mais inconnu des sources de métadonnées | La fiche est créée avec le seul ISBN. Le bénévole peut la garder ; le titre sera complété plus tard par un administrateur (`RG-03`) |
| Même livre scanné deux fois de suite en quelques secondes | Signaler « déjà scanné à l'instant » sans bloquer : deux exemplaires identiques dans un don sont fréquents (`RG-04`) |
| Perte de réseau | L'application continue de scanner et met les gestes en attente. Le verdict affiché s'appuie sur les données locales, avec une mention explicite de leur fraîcheur (`ENF-05`) |
| Application fermée avec une session ouverte | La session est retrouvée à la réouverture, **avec son mode**, et le mode est reconfirmé avant de reprendre le scan |
| Mode `PROCHAINE BOURSE` alors qu'aucune bourse n'est programmée | Le mode reste utilisable ; les livres sont annoncés sans date et se rattacheront à la prochaine bourse créée. Les alertes sont différées (`RG-24`) |
| Session entière scannée dans le mauvais mode | Un administrateur la rebascule en bloc (`RG-25`). C'est l'erreur la plus probable du système ; elle est silencieuse et ne se voit pas à l'écran de scan |
| Deux bénévoles scannent le même ISBN en même temps | Aucun conflit : chaque scan est un mouvement indépendant |
| Batterie de la scanette à plat en pleine session | Les gestes non synchronisés doivent survivre à l'extinction (`ENF-05`) |

## 8. Ce que l'application de scan ne fait pas

- Elle ne modifie pas les métadonnées d'un livre : cela relève de l'administration.
- Elle n'affiche aucune statistique au-delà du compteur de session.
- Elle ne gère ni les comptes du public, ni les alertes.
- Elle n'encaisse pas : elle enregistre des sorties, le paiement reste physique.
