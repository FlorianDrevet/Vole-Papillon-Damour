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

## Statut

**Brouillon soumis à validation.** Trois éléments doivent être arbitrés avant tout
développement, dont un est structurant pour l'architecture : voir
[`08-questions-ouvertes.md`](08-questions-ouvertes.md).

## Conventions

- Les règles métier sont numérotées `RG-nn` et référencées depuis les autres documents.
- Les questions ouvertes sont numérotées `Q-nn`.
- Les exigences non fonctionnelles sont numérotées `ENF-nn`.
- « v1 » désigne le premier périmètre livré en production ; « v2 » ce qui est
  volontairement reporté mais déjà pensé.
