# Actualités depuis les réseaux sociaux — spécifications fonctionnelles

Ce dossier décrit **ce que doit faire** l'import automatique des publications de
l'association vers les actualités du site. Il ne décrit pas *comment* le construire :
l'architecture technique est dans [`technique/`](technique/README.md).

Aujourd'hui, chaque actualité du site est ressaisie à la main dans le BackOffice alors
que la présidente a déjà écrit le même contenu sur les réseaux. L'objectif est de
supprimer cette double saisie sans rien retirer au contrôle éditorial.

## Par où commencer

| Vous êtes… | Lisez |
|---|---|
| Membre du bureau de l'association | [`00-note-presidente.md`](00-note-presidente.md) — se lit seul, sans les autres documents |
| En charge de la conception ou du développement | [`01-vision-et-perimetre.md`](01-vision-et-perimetre.md), puis les suivants dans l'ordre |
| **Pressé, et vous voulez savoir si c'est faisable** | [`02-sources-et-contraintes-plateformes.md`](02-sources-et-contraintes-plateformes.md) — tout part de là |
| À la recherche d'une règle précise | [`04-regles-metier.md`](04-regles-metier.md) |
| En train de préparer le chiffrage | [`07-questions-ouvertes.md`](07-questions-ouvertes.md) **en premier** — trois questions sont bloquantes |

## Contenu

| Document | Objet |
|---|---|
| [`00-note-presidente.md`](00-note-presidente.md) | Ce qui change pour la présidente, et les décisions qu'elle seule peut prendre |
| [`01-vision-et-perimetre.md`](01-vision-et-perimetre.md) | Objectifs, acteurs, périmètre, paliers de livraison |
| [`02-sources-et-contraintes-plateformes.md`](02-sources-et-contraintes-plateformes.md) | Ce que les API Meta autorisent réellement, et pourquoi le déclencheur est un minuteur |
| [`03-parcours-import-et-validation.md`](03-parcours-import-et-validation.md) | Le trajet d'une publication, du post au site |
| [`04-regles-metier.md`](04-regles-metier.md) | Règles numérotées et vérifiables |
| [`05-titre-genere.md`](05-titre-genere.md) | Génération du titre par un modèle Azure AI Foundry, garde-fous et repli |
| [`06-exigences-non-fonctionnelles.md`](06-exigences-non-fonctionnelles.md) | Cadence, quotas, jeton, coût, RGPD, droit à l'image, observabilité |
| [`07-questions-ouvertes.md`](07-questions-ouvertes.md) | Décisions restant à prendre et risques assumés |
| [`technique/`](technique/README.md) | **Architecture technique** — fonction, domaine, migration, infrastructure |

## Statut

**Spécification écrite. Rien d'implémenté.** Trois questions bloquantes attendent une
réponse de l'association avant tout développement : `Q-ACT-01`, `Q-ACT-02` et `Q-ACT-04`.
Elles se répondent en dix minutes dans l'application Instagram et dans le Business
Manager de Meta, pas en réunion.

## Conventions

- Les règles métier sont numérotées `RG-ACT-nn`.
- Les exigences non fonctionnelles sont numérotées `ENF-ACT-nn`.
- Les questions ouvertes sont numérotées `Q-ACT-nn`.
- Les décisions techniques déjà tranchées sont numérotées `DT-ACT-nn` et vivent dans
  [`technique/`](technique/README.md).
- Le préfixe `-ACT-` évite toute collision avec la numérotation de
  [`../bourse-aux-livres/`](../bourse-aux-livres/README.md).
- « v1 » désigne le premier périmètre livré en production ; « v2 » ce qui est
  volontairement reporté mais déjà pensé.
