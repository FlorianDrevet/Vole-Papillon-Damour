# Bourse aux livres — plan d'exécution

Ce dossier dit **dans quel ordre construire**, et comment reprendre le travail sur une
autre machine sans rien perdre. Il ne redéfinit ni le fonctionnel ni l'architecture : il
s'y réfère par leurs identifiants (`RG-nn`, `ENF-nn`, `DT-nn`, `QT-nn`, `R-nn`).

| Vous cherchez… | Lisez |
|---|---|
| **Où j'en suis** | [`../../../NEXT.md`](../../../NEXT.md) — à la racine du dépôt |
| Quoi faire, dans quel ordre | Les fichiers de lots ci-dessous |
| Pourquoi c'est fait comme ça | [`../technique/`](../technique/README.md) |
| Ce que le système doit faire | [`../README.md`](../README.md) |

## Les lots

| Lot | Contenu | Détail |
|---|---|---|
| [`00`](00-socle-et-prealable.md) | **Socle technique**, puis **préalable d'identité et délais externes** | Fin |
| [`01`](01-palier-0-sonde.md) | **Palier 0** — sonde de faisabilité, et les mesures qui décident de la suite | Fin |
| [`02`](02-palier-1-socle-interne.md) | **Palier 1** — le socle interne : tri, quantités, bascule, caisse | Moyen |
| [`03`](03-paliers-2-et-3.md) | **Paliers 2 et 3** — vitrine publique, puis alertes | **Grossier, volontairement** |

**Pourquoi le dernier est grossier.** Le principe directeur n°4 de l'architecture — « ce
qui n'est pas mesuré n'est pas décidé » — s'applique au plan lui-même. Détailler le palier
3 avant que le palier 0 ait mesuré `QT-01` et `QT-03`, c'est écrire des étapes qu'on
réécrira. Les paliers 2 et 3 se détaillent quand le palier 1 est validé, pas avant.

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
