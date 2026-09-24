# Recommandations de livres — dossier de conception

Ce dossier trace la réflexion sur une nouvelle capacité : **proposer à un membre des
livres susceptibles de lui plaire**, à partir de ce qu'il a déjà acheté (et, le cas
échéant, d'autres signaux de son compte).

Rien n'est implémenté. Rien n'est encore décidé : le dossier sert d'abord à confronter
l'idée initiale aux contraintes réelles du projet (données disponibles, coût, RGPD,
maintenance par une personne seule) avant d'écrire une spécification.

## Par où commencer

| Vous voulez… | Lisez |
|---|---|
| Suivre la discussion dans l'ordre | [`00-journal-des-echanges.md`](00-journal-des-echanges.md) |
| Savoir si l'idée « embeddings + base graphe » tient la route, et ce qu'elle coûte | [`01-analyse-critique-idee-initiale.md`](01-analyse-critique-idee-initiale.md) |
| Comparer SQL et base graphe pour stocker les voisins | [`02-etude-stockage-voisins-sql-ou-graphe.md`](02-etude-stockage-voisins-sql-ou-graphe.md) |

## Contenu

| Document | Objet |
|---|---|
| [`00-journal-des-echanges.md`](00-journal-des-echanges.md) | Journal daté : propositions, réponses, décisions, questions ouvertes |
| [`01-analyse-critique-idee-initiale.md`](01-analyse-critique-idee-initiale.md) | Critique argumentée de l'idée initiale, faits vérifiés dans le dépôt, chiffrage, alternatives |
| [`02-etude-stockage-voisins-sql-ou-graphe.md`](02-etude-stockage-voisins-sql-ou-graphe.md) | Étude d'architecture : voisins en table SQL, en SQL Graph ou dans Cosmos DB Gremlin ; prix de Cosmos |
| [`03-mesure-couverture-resumes.md`](03-mesure-couverture-resumes.md) | Mesure sur 138 éditions de livres connus : taux de résumés BnF et Open Library (Google Books à venir) ; défaut ISBN-10 découvert |
| [`04-signaux-de-similarite.md`](04-signaux-de-similarite.md) | Le résumé ne suffit pas : zones BnF réellement remplies, rôle de chaque signal (filtre, bonus, embedding) |

Les documents suivants (spécification retenue, puis plan d'implémentation) seront
ajoutés ici une fois l'approche validée.

## Statut

**Exploration — 24 septembre 2026.** Branche `docs/book-recommendations-design`.
