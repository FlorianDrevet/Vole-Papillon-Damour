# Bourse aux livres — plan d'exécution

Ce dossier dit **dans quel ordre construire**, et comment reprendre le travail sur une
autre machine sans rien perdre. Il ne redéfinit ni le fonctionnel ni l'architecture : il
s'y réfère par leurs identifiants (`RG-nn`, `ENF-nn`, `DT-nn`, `QT-nn`, `R-nn`).

> **Une convention de renvoi, parce que les deux dossiers ont les mêmes numéros.**
> `04-site-public.md` existe côté fonctionnel et `04-app-scan.md` côté technique ; écrire
> « `04` §2 » ne désigne donc rien. Dans les lots, un renvoi à un chapitre s'écrit
> **`F-nn`** pour le fonctionnel ([`../`](../README.md)) et **`T-nn`** pour la technique
> ([`../technique/`](../technique/README.md)). `F-05` §5 est l'écran de désengorgement,
> `T-05` §4 est l'ajout à la liste de recherche : sans le préfixe, les deux s'écrivaient
> pareil.

| Vous cherchez… | Lisez |
|---|---|
| **Où j'en suis** | [`../../../NEXT.md`](../../../NEXT.md) — à la racine du dépôt |
| Quoi faire, dans quel ordre | Les fichiers de lots ci-dessous |
| Pourquoi c'est fait comme ça | [`../technique/`](../technique/README.md) |
| Ce que le système doit faire | [`../README.md`](../README.md) |

## Les lots

| Lot | Contenu | Étapes | Détail |
|---|---|---|---|
| [`00`](00-socle-et-prealable.md) | **Socle technique**, puis **préalable d'identité et délais externes** | `L0-1` à `L0-13` | Fin |
| [`01`](01-palier-0-sonde.md) | **Palier 0** — sonde de faisabilité, et les mesures qui décident de la suite | `S0-1` à `S0-5` | Fin |
| [`02`](02-palier-1-socle-interne.md) | **Palier 1** — le socle interne : tri, quantités, bascule, caisse | `P1-1` à `P1-11` | Moyen |
| [`03`](03-paliers-2-et-3.md) | **Paliers 2 et 3** — vitrine publique, puis alertes | — | **Grossier, volontairement** |

**Pourquoi le dernier est grossier.** Le principe directeur n°4 de l'architecture — « ce
qui n'est pas mesuré n'est pas décidé » — s'applique au plan lui-même. Détailler le palier
3 avant que le palier 0 ait mesuré `QT-01` et `QT-03`, c'est écrire des étapes qu'on
réécrira. Les paliers 2 et 3 se détaillent quand le palier 1 tient, pas avant.

## Comment lire un lot

Chaque lot est une suite d'**étapes numérotées** portant un identifiant stable — `L0-3`,
`P1-7` — pour que `NEXT.md` puisse les désigner sans ambiguïté. Chaque étape porte au
moins :

| Marque | Sens |
|---|---|
| 🔧 | Ce qu'il y a à faire |
| ✅ | Comment la machine vérifie que c'est fait — compilation, tests, requête |
| 🧪 | **Le test manuel** : ce que vous devez faire de vos mains, et ce que vous devez voir |
| 🚀 | Le déploiement, quand il y en a un |
| 📌 | **L'état hors dépôt** produit par l'étape, à consigner dans `NEXT.md` |

🧪 et 📌 sont les deux qui comptent pour le travail sur plusieurs machines. Le reste, git
le porte.

## Le système de reprise

### Le problème réel

Travailler sur plusieurs machines successivement, ce n'est pas un problème de code : git
le règle. Ce qui se perd, c'est **tout ce qui n'est pas dans le dépôt** :

- ce qui a été cliqué dans le portail Azure ou dans le locataire Entra ;
- quel enregistrement DNS est posé, et depuis quand il propage ;
- quel test manuel a été passé, quand, et avec quel résultat ;
- quelle mesure est **en cours** — `QT-08` demande d'attendre quarante-huit heures ;
- ce qui est réellement déployé, par opposition à ce qui est commité ;
- où l'on s'était arrêté au milieu d'une étape.

`NEXT.md` existe pour ça, et **pour ça seulement**. Ce n'est pas une liste de tâches en
double des lots : c'est l'état que git ne sait pas porter.

### Le rituel, dans les deux sens

**En arrivant sur une machine** — dans cet ordre, sans exception :

```bash
git pull
cat NEXT.md          # avant toute autre chose
```

Puis vérifier les prérequis que `NEXT.md` signale — connexion Azure, SDK, outils.

**En quittant une machine** — même en pleine étape, surtout en pleine étape :

1. Mettre à jour `NEXT.md` : où j'en suis, ce que j'ai touché hors dépôt, ce qui bloque.
2. Commiter, **y compris du travail incomplet**, sur une branche de travail.
3. Pousser.

**Une branche non poussée est du travail perdu** dès qu'on change de machine. Le coût d'un
commit « wip » est nul ; celui d'une soirée à reconstituer ne l'est pas.

### Pourquoi `NEXT.md` est versionné

Parce qu'un bloc-notes local ne traverse pas les machines, ce qui est précisément le
besoin. La contrepartie est assumée : des cases cochées apparaissent dans l'historique.
C'est le prix, et il est faible.

Deux règles pour qu'il reste supportable :

- **`NEXT.md` reste court.** S'il dépasse un écran ou deux, c'est que du détail y est
  descendu qui appartenait à un lot.
- **Un commit de `NEXT.md` seul se nomme `chore(plan): ...`** et ne mélange pas de code.
  Un conflit sur ce fichier se résout alors en gardant les deux moitiés, sans réfléchir.

### Ce que `NEXT.md` n'est pas

| Pas ça | Où ça va |
|---|---|
| La liste des étapes | Les fichiers de lots |
| Le raisonnement derrière un choix | `technique/01-decisions.md`, en `DT-nn` |
| Ce qu'on a appris du code | `.github/memory/` |
| Les défauts relevés dans la doc | `technique/revue.md`, en `R-nn` |
| Un secret, une clé, une chaîne de connexion | **Nulle part dans le dépôt** |

## Une règle qui traverse tous les lots

`DT-16` : **une tranche livrée sans son instrumentation n'est pas livrée.** Les quatre
questions de [`../technique/11-observabilite.md`](../technique/11-observabilite.md) §9 font
partie du « terminé » de chaque étape qui produit du code, au même titre que les tests.
Elles ne sont pas répétées à chaque étape ; elles s'appliquent à toutes.

## Les quatre arbitrages, et ce qu'ils ont donné

Le plan a buté sur quatre décisions qu'il ne pouvait pas prendre seul. Elles sont tranchées,
et chacune est écrite là où elle s'applique :

| Sujet | Décision | Où elle vit |
|---|---|---|
| Plateformes et distribution de la caisse | **Android seul** — téléphones et tablettes. Les trois autres cibles sont retirées. APK signé, posé à la main sur chaque appareil | `L0-10`, `DT-15` |
| Suppression du compte dans le locataire (`R-06`) | **Au préalable d'identité**, pendant qu'il n'y a personne à supprimer | `L0-11`, étape 8 |
| Genres et classement (`Q-07`) | **Les genres viennent des sources**, et le site n'indique **jamais** où se trouve un livre dans le local | `Q-07`, `F-04` §4 et §9 |
| Repli d'exploitation (`ENF-21`) | **Aucun.** En cas de panne on vend sans enregistrer, rien n'est rattrapé. Le hors-ligne de la caisse devient la seule protection | `ENF-21`, `P1-10` |

## Comment ce plan se lit, vu le mode de travail

**Il n'y a pas de date, et il n'y en aura pas.** Le développement se fait au fil de l'eau,
en testant au fur et à mesure, et rien n'est montré avant que l'ensemble tienne debout.
Trois conséquences sur la lecture des lots :

- **Les paliers sont un ordre de construction, pas un calendrier de livraison.** Rien n'est
  présenté à qui que ce soit entre deux paliers. Ce qui reste vrai, c'est l'ordre : on ne
  publie pas un catalogue sur un stock dont on n'a pas vérifié la fiabilité.
- **Les 🧪 sont des tests que vous faites vous-même**, seul, sans bourse et sans bénévole.
  Ils sont écrits pour ça : chacun dit quoi faire et ce qu'on doit voir. Aucun n'attend une
  validation extérieure.
- **Trois choses ne se testent pas seul**, et il faut le savoir plutôt que faire semblant :
  le ressenti d'un bénévole sur le geste de scan, la discipline de scan en caisse un vrai
  jour de bourse, et la réputation d'un domaine d'envoi. Les deux premières attendent
  l'usage réel ; la troisième est la raison pour laquelle `L0-9` crée la messagerie très en
  avance de son besoin.

**Les critères de passage restent, et ils ne sont pas décoratifs.** Ils ne servent plus à
demander l'autorisation de continuer — ils disent **ce qu'il y aura à écrire ensuite**. Une
lecture caméra insuffisante au palier 0 ne suspend pas le projet : elle fait de l'entrée
clavier le chemin nominal. Peu de `WorkId` ne suspend rien non plus : cela oblige à
concevoir le repli titre + auteur avant les alertes. C'est pour cela que ces mesures se font
tôt, même en travaillant d'une traite.
