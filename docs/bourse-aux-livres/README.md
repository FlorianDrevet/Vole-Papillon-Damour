# Bourse aux livres — spécifications fonctionnelles

Ce dossier décrit **ce que doit faire** le futur outil de tri, de mise à disposition et de
mise en ligne des livres de la bourse aux livres. Il ne décrit pas *comment* le
construire : l'architecture technique fera l'objet d'un travail séparé, à partir de
ces documents.

## Par où commencer

| Vous êtes… | Lisez |
|---|---|
| Membre du bureau de l'association | [`00-note-presidente.md`](00-note-presidente.md) — se lit seul, sans les autres documents |
| En charge de la conception ou du développement | [`01-vision-et-perimetre.md`](01-vision-et-perimetre.md), puis les suivants dans l'ordre |
| À la recherche d'une règle précise | [`06-regles-metier.md`](06-regles-metier.md) |
| En train de préparer le chiffrage technique | [`08-questions-ouvertes.md`](08-questions-ouvertes.md) **en premier** — certaines décisions sont bloquantes |
| **En train de construire** | [`../../NEXT.md`](../../NEXT.md) pour savoir où l'on en est, puis [`plan/`](plan/README.md) |

## Contenu

| Document | Objet |
|---|---|
| [`00-note-presidente.md`](00-note-presidente.md) | Présentation non technique destinée à la validation par l'association |
| [`01-vision-et-perimetre.md`](01-vision-et-perimetre.md) | Objectifs, acteurs, périmètre, paliers de livraison |
| [`02-glossaire-et-cycle-de-vie.md`](02-glossaire-et-cycle-de-vie.md) | Vocabulaire métier et cycle de vie d'un livre |
| [`03-parcours-benevole-scan.md`](03-parcours-benevole-scan.md) | L'application de scan, écran par écran |
| [`04-site-public.md`](04-site-public.md) | Catalogue en ligne, comptes, listes de recherche, alertes |
| [`05-administration.md`](05-administration.md) | Statistiques et pilotage du catalogue |
| [`06-regles-metier.md`](06-regles-metier.md) | Règles numérotées et vérifiables |
| [`07-exigences-non-fonctionnelles.md`](07-exigences-non-fonctionnelles.md) | Performance, hors-ligne, RGPD, accessibilité, authentification |
| [`08-questions-ouvertes.md`](08-questions-ouvertes.md) | Décisions restant à prendre et risques assumés |
| [`technique/`](technique/README.md) | **Architecture technique** — comment construire ce que décrit ce dossier |
| [`plan/`](plan/README.md) | **Plan d'exécution** — dans quel ordre construire, quels tests manuels, quels déploiements |

## Statut

**Spécification stabilisée, architecture écrite, plan d'exécution écrit. Rien
d'implémenté.** Les décisions structurantes de `08` sont tranchées : `Q-01`, `Q-05`,
`Q-10` et `Q-11` le sont, `Q-02` est reportée hors v1, `Q-04`, `Q-06` et `Q-09` sont des
risques assumés.

**Ce qui attend encore une réponse**, et qui vient de l'association plus que de la
technique :

| Question | Attendue pour |
|---|---|
| `Q-03` — proportion de livres sans ISBN | Se **mesure** au palier 0, ne se décide pas |
| `Q-07` — genres et classement | Pendant le palier 1, parce qu'elle bloque le palier 2 |
| `Q-08` — matériel de scan | Après le palier 0, pas avant |
| Les chiffres cibles du palier 0 | **Avant** de lancer la campagne (`S0-1`) |

Le plan signale par ailleurs quatre arbitrages qui lui sont propres : voir
[`plan/README.md`](plan/README.md), « Les décisions que le plan n'a pas le droit de prendre
seul ».

## Conventions

- Les règles métier sont numérotées `RG-nn` et référencées depuis les autres documents.
- Les questions ouvertes sont numérotées `Q-nn`.
- Les exigences non fonctionnelles sont numérotées `ENF-nn`.
- « v1 » désigne le premier périmètre livré en production ; « v2 » ce qui est
  volontairement reporté mais déjà pensé.
