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
   │   │   📦   METTRE EN RAYON        │ │
   │   └───────────────────────────────┘ │
   │   ┌───────────────────────────────┐ │
   │   │   💶   CAISSE                 │ │
   │   └───────────────────────────────┘ │
   │                                     │
   │   Changer d'utilisateur             │
   └─────────────────────────────────────┘
```

Seuls les modes autorisés par les droits du bénévole sont affichés (`RG-40`).

**Le mode actif est visible en permanence** — bandeau de couleur distincte en haut de
l'écran, libellé en toutes lettres. Un scan de vente déclenché depuis le mode tri
serait une erreur coûteuse et silencieuse : la couleur du bandeau est la garantie
principale contre cette confusion.

## 3. Mode TRI — l'écran central du projet

C'est l'écran qui porte toute la valeur du produit. Il doit être lisible en une
demi-seconde, à bout de bras.

### Écran d'attente

```
┌───────────────────────────────────────┐
│ 📖 TRI          Michel   ·   127 triés│  ← bandeau permanent
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
le rythme sans distraire.

### Écran de résultat

Le verdict occupe le haut de l'écran, en couleur, lisible sans lire le détail.

**Cas « on en a déjà trop »**

```
┌───────────────────────────────────────┐
│ 📖 TRI          Michel   ·   128 triés│
├───────────────────────────────────────┤
│ ╔═══════════════════════════════════╗ │
│ ║  🔴   INUTILE D'EN GARDER         ║ │
│ ║       déjà 6 en rayon             ║ │
│ ╚═══════════════════════════════════╝ │
│  ┌────┐                               │
│  │couv│  Le Petit Prince              │
│  │    │  Antoine de Saint-Exupéry     │
│  └────┘  Gallimard · 1999             │
│                                       │
│  En rayon      6                      │
│  Déjà vendus   2                      │
│  Recherché par —                      │
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

**Cas « livre de valeur »**

```
│ ╔═══════════════════════════════════╗ │
│ ║  🟣   METTRE DE CÔTÉ              ║ │
│ ║       valeur estimée ~35 €        ║ │
│ ║       → bac « livres rares »      ║ │
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

### Enchaîner sans toucher l'écran

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

### Saisie manuelle du code

Un code-barres illisible arrive régulièrement sur des livres d'occasion. L'écran de
saisie accepte un ISBN à 10 ou 13 chiffres tapé au clavier numérique, avec contrôle
de la clé de validité (`RG-01`).

C'est le seul cas où l'on tape. Il ne doit jamais être le chemin nominal.

## 4. Mode MISE EN RAYON

**Ce mode dépend entièrement de l'arbitrage `Q-01`.** Les trois organisations possibles
et leurs conséquences sont décrites dans `08-questions-ouvertes.md`. Ce qui suit décrit
l'option **carton étiqueté**, recommandée, à titre d'illustration du parcours cible.

### Côté tri : constituer un carton

1. Le bénévole ouvre un carton dans l'application. Il reçoit un numéro.
2. Il trie et scanne normalement ; les livres gardés s'ajoutent au carton en cours.
3. Le carton plein, il le ferme. **L'application produit une étiquette code-barres à
   imprimer et à coller sur le carton physique.**

L'écran affiche en permanence le carton en cours et son nombre de livres.

### Côté local : mettre en rayon

1. Le bénévole passe en mode `METTRE EN RAYON`.
2. Il scanne l'étiquette du carton.
3. L'application affiche : `Carton n° 42 — 87 livres — trié le 3 mars par Michel`.
4. Il valide.

**C'est cette validation, et elle seule, qui rend les 87 livres visibles en ligne et
déclenche les alertes** (`RG-20`).

Aucun livre n'est re-scanné individuellement : c'est tout l'intérêt de l'étiquette.
Un carton ne peut être mis en rayon qu'une fois (`RG-21`).

## 5. Mode CAISSE

Utilisé pendant une session de bourse. L'application se rattache automatiquement à la
session de bourse ouverte (`RG-30`).

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
- `Annuler dernier` traite le cas fréquent du double scan.
- `ENCAISSER` clôt la vente et enregistre les mouvements. Le calcul du rendu de monnaie
  n'est pas dans le périmètre : la caisse reste physique.
- **Un livre scanné alors que la quantité en rayon est à zéro est quand même vendu** :
  le client le tient en main, la réalité prime sur le compteur. L'écart est enregistré
  pour la remise à plat (`RG-34`).

## 6. Mode CONSULTATION (palier 0 uniquement)

Version réduite, sans enregistrement : on scanne, on voit la fiche, rien n'est
mémorisé. C'est l'outil de la sonde de faisabilité décrite en `01` §7.

Elle sert aussi, plus tard, de mode « je vérifie quelque chose » sans risque de
polluer les données. À conserver au-delà du palier 0.

## 7. Cas d'erreur et de bord

| Situation | Comportement attendu |
|---|---|
| Code-barres illisible après plusieurs tentatives | Proposer la saisie manuelle après 5 secondes sans lecture |
| Code-barres qui n'est pas un ISBN (produit alimentaire, code interne) | Message explicite « ce n'est pas un code de livre », pas d'enregistrement (`RG-02`) |
| ISBN valide mais inconnu des sources de métadonnées | La fiche est créée avec le seul ISBN. Le bénévole peut la garder ; le titre sera complété plus tard par un administrateur (`RG-03`) |
| Même livre scanné deux fois de suite en quelques secondes | Signaler « déjà scanné à l'instant » sans bloquer : deux exemplaires identiques dans un don sont fréquents (`RG-04`) |
| Perte de réseau | L'application continue de scanner et met les gestes en attente. Le verdict affiché s'appuie sur les données locales, avec une mention explicite de leur fraîcheur (`ENF-05`) |
| Application fermée avec un carton ouvert | Le carton est retrouvé à la réouverture, quel que soit l'appareil |
| Deux bénévoles scannent le même ISBN en même temps | Aucun conflit : chaque scan est un mouvement indépendant |
| Batterie de la scanette à plat en pleine session | Les gestes non synchronisés doivent survivre à l'extinction (`ENF-05`) |

## 8. Ce que l'application de scan ne fait pas

- Elle ne modifie pas les métadonnées d'un livre : cela relève de l'administration.
- Elle n'affiche aucune statistique au-delà du compteur de session.
- Elle ne gère ni les comptes du public, ni les alertes.
- Elle n'encaisse pas : elle enregistre des sorties, le paiement reste physique.
