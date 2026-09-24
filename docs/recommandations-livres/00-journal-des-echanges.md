# 00 — Journal des échanges

Journal chronologique de la conception. Chaque échange consigne la demande, la réponse,
les décisions prises et les questions laissées ouvertes. Les analyses longues vivent
dans les documents numérotés ; ce journal y renvoie.

---

## Échange 1 — 24 septembre 2026 : l'idée initiale

### Demande

Maintenant que les membres disposent de la liste des livres qu'ils ont achetés
(`Mes achats`), leur mettre en avant des livres susceptibles de leur plaire.

Piste technique proposée :

- un **embedding** calculé sur le résumé ou d'autres informations du livre ;
- une **base graphe** où chaque nœud est un livre portant son embedding, relié aux
  livres les plus proches ;
- l'embedding calculé **une seule fois** par livre, et les relations calculées **une
  fois**, à l'entrée ou à la sortie d'un livre, pour réduire au maximum le coût et la
  latence.

Consigne explicite : ne pas aller dans ce sens pour faire plaisir, **challenger au
maximum** ; une solution totalement différente est acceptable ; l'objectif est une
solution qui fonctionne à coût modéré.

### Réponse — résumé

Analyse complète : [`01-analyse-critique-idee-initiale.md`](01-analyse-critique-idee-initiale.md).

- ✅ **À garder** : un embedding calculé une fois par livre, hors du chemin de la
  requête ; des voisins **pré-calculés** pour une lecture instantanée.
- ❌ **À écarter** : la base graphe. Parcours à un seul saut sur environ 20 000 fiches :
  c'est une table SQL `BookNeighbors`. Une base de plus contredit les principes du
  projet et peut coûter jusqu'à ~65 $/mois sans aucun gain.
- ❌ **À corriger** : pas de recalcul à la sortie d'un livre. La similarité dépend du
  contenu, pas du stock ; la disponibilité se filtre à la lecture. À l'entrée, un
  recalcul complet périodique est plus simple et plus sûr qu'un recalcul incrémental.
- ⚠️ **Prémisse à vérifier** : le dépôt **ne stocke aucun résumé** et ne le demande à
  aucune source. Sans résumé, l'embedding apporte peu par rapport à des règles simples.
- ⚠️ **Signal rare** : `Mes achats` ne contient que les passages associés, et la
  fonction vient d'être livrée. Il faut aussi utiliser la liste de recherche et Ma
  sélection, et prévoir un repli.
- ⚠️ **RGPD** : c'est du profilage, une finalité nouvelle à documenter dans `09`.
  L'affichage dans le compte et l'envoi par e-mail n'ont pas la même base légale.
- 💶 **Coût IA** : ≈ 0,16 $ pour 20 000 fiches, puis quelques millièmes de dollar par
  mois. **Le coût réel est le temps de développement**, pas la facture Azure.

Recommandation provisoire : mesurer d'abord (taux de résumés, comptes ayant au moins un
signal), puis construire par couches — règles SQL, puis voisins sémantiques si la mesure
le justifie.

### Décisions

Aucune pour l'instant.

### Questions ouvertes

- **Q1** — Où et quand le membre voit-il ces suggestions, et dans quel but ? *(posée)*
- **Q2** — Quels signaux utiliser : achats seuls, ou aussi liste de recherche et Ma
  sélection ?
- **Q3** — Accepte-t-on une sonde de mesure (taux de résumés sur un échantillon d'ISBN
  réels, nombre de comptes ayant au moins un signal) avant de choisir l'approche ?
- **Q4** — Le compte Azure OpenAI de génération de titres
  (`TITLE_GENERATION_ENABLED`) est-il activé en production ? Il conditionne l'absence
  de nouvelle ressource.
