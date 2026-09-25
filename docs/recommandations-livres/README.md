# Recommandations de livres — dossier de conception

Ce dossier trace la conception et l'implémentation de la capacité **proposer à un membre
des livres susceptibles de lui plaire**, à partir de ses achats associés. La conception
est validée et l'implémentation est achevée sur `feat/book-recommendations` ; la [PR #239](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/239)
est ouverte vers `main` et attend sa revue.

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
| [`05-benchmark.md`](05-benchmark.md) | Benchmark de 11 méthodes sur 165 éditions réelles : protocole, critères de décision, résultats et analyse |
| [`06-tutoriel-foundry-embeddings.md`](06-tutoriel-foundry-embeddings.md) | Déployer `text-embedding-3-small` sur le compte Foundry existant et lancer le benchmark |
| [`07-specification.md`](07-specification.md) | **Spécification** : surfaces, méthode de calcul, règles d'affichage, RGPD, architecture, critères d'acceptation |
| [`08-plan-implementation.md`](08-plan-implementation.md) | **Plan d'implémentation** en 14 tâches, avec tests d'abord et renvois aux zones des maquettes |
| [`maquettes/index.html`](maquettes/index.html) | Maquettes exportées du canvas [Claude Design](https://claude.ai/artifact/GYWEeUku7qofkTt5pdKtoM) : 8 écrans HTML autonomes, zones `data-zone`, sources `.dc.html`, archive `recommandations-maquettes-html.zip` |
| [`maquettes/recommandations-de-livres-canvas.html`](maquettes/recommandations-de-livres-canvas.html) | Export officiel du canvas depuis Claude Design (fichier unique, hors ligne) : les deux pages et les notes, tel qu'affiché dans l'éditeur |

La spécification et le plan ci-dessus décrivent l'approche retenue et son implémentation.

## Statut

**Implémentation terminée — 25 septembre 2026.** Spécification, maquettes, plan et code
sont réunis dans `feat/book-recommendations` et livrés par la [PR #239](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/239).
La fonctionnalité reste désactivée par défaut ; calibrage du seuil sur 50 fiches, validation
de la base juridique et premier calcul nocturne réussi restent requis avant ouverture publique.
