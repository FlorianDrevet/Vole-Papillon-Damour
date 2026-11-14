# Gestion des coups de cœur : retrait confirmé, recherche, pagination et calcul en direct

> Demande d'origine (2026-11-14) : « Un coup de cœur retiré disparaît à l'écran mais réapparaît après un rechargement ; ajouter une fenêtre de confirmation avant de retirer ; ajouter une recherche et une pagination à la liste, et retirer la limite du nombre de coups de cœur ; afficher l'avancement du calcul des suggestions pendant qu'il tourne, puis ajouter les livres quand il est fini. »

> Règles nouvelles : `RG-160` à `RG-170`, à la suite de `RG-159`. Remplacent `RG-131` (plafonds) et le délai de `RG-134` (retrait différé). Le reste de [`book-favorites`](../book-favorites/01-fonctionnel.md) reste valable.

## 1. Contexte

La fonctionnalité *Trouver mon prochain livre* est en production depuis les PR
[#392](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/392) à
[#395](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/395). Ce dossier prolonge
la spécification [`book-favorites`](../book-favorites/01-fonctionnel.md).

Constats après le déploiement :

- **Retrait non tenu.** Sur E2, le livre disparaît immédiatement, mais le retrait n'est
  enregistré qu'après un délai de quelques secondes. Un rechargement pendant ce délai fait
  réapparaître le livre. Ce comportement contredit `RG-134` (« retrait immédiat »). Le calcul
  en cours n'est pas en cause : un retrait n'est pas bloqué pendant un calcul.
- **Échec silencieux.** Si l'enregistrement du retrait échoue, le livre revient dans la liste
  sans aucun message.
- **Calcul figé.** E2 vérifie l'avancement toutes les 5 secondes, pendant 60 secondes au plus.
  Ensuite, rien ne se met plus à jour avant un rechargement. Un calcul qui a échoué reste
  affiché comme « en cours ».
- **Liste sans fin.** La liste ne propose ni recherche ni pages. Elle est bornée à 50 coups de
  cœur dans un navigateur et à 200 dans un compte.

## 2. Objectifs

1. Un coup de cœur retiré reste retiré, quel que soit le moment du rechargement.
2. Retirer un coup de cœur se confirme dans une fenêtre dédiée.
3. Un membre retrouve un coup de cœur dans une longue liste, par recherche et par pages.
4. Aucune limite n'empêche d'ajouter un coup de cœur.
5. L'avancement du calcul est visible en direct. Les suggestions apparaissent dès qu'elles
   sont prêtes, sans rechargement.

## 3. Hors périmètre

- Modifier l'algorithme de suggestions, ses seuils ou ses cœurs (`RG-136` à `RG-138`).
- Les plafonds de création de fiches de référence (`RG-135`). Ce ne sont pas des limites de
  coups de cœur.
- Un tri autre que du plus récent au plus ancien (`RG-134`).
- Le partage, l'export ou la réservation d'un livre à la suite d'un retrait.
- Un historique des coups de cœur retirés.
- Un calcul accéléré : le calcul reste fait par le système, comme dans `RG-139`.

## 4. Vocabulaire et acteurs

- **Visiteur** : sans compte. Sa liste est dans son navigateur (`RG-140`).
- **Membre** : avec un compte. Sa liste est dans le compte.
- **Coup de cœur** : une œuvre affichée sur une carte. Elle peut regrouper plusieurs éditions
  (`RG-130`). Le compteur et la pagination comptent des cartes, pas des éditions.
- **Calcul** : pour chaque coup de cœur, les suggestions sont en attente, en cours, prêtes ou
  indisponibles.

## 5. Parcours

### P1 — Retirer un coup de cœur
1. Le membre clique sur le cœur d'un livre (E2, E5 ou E6).
2. La fenêtre E12 s'ouvre avec le titre du livre. Le bouton « Annuler » a le focus.
3. Il confirme avec « Retirer ». Le livre disparaît de la liste et le retrait est enregistré.
4. Il recharge la page : le livre reste absent.
5. Si l'enregistrement échoue, le livre revient à sa place et E11 affiche « Le retrait n'a pas
   pu être enregistré. Réessayez. », jusqu'à ce que le membre ferme ce message.

### P2 — Retrouver un coup de cœur
1. Le membre tape « Hugo » dans le champ de recherche de E2.
2. La liste ne montre que les œuvres correspondantes, avec leur nombre.
3. S'il y en a plus de 10, il passe de page en page.
4. Il efface la recherche : la liste complète revient à la page 1.

### P3 — Attendre les suggestions
1. Le visiteur ou le membre ouvre E2 pendant qu'un de ses coups de cœur est en calcul.
2. E2 affiche « Calcul des suggestions : 0 sur 3 coups de cœur prêts », avec une barre.
3. À mesure que les calculs se terminent, le compteur avance, sans rechargement.
4. Quand le dernier calcul est terminé, la progression disparaît et les suggestions arrivent
   dans la grille.

## 6. Écrans

Numérotation reprise de [`book-favorites`](../book-favorites/01-fonctionnel.md) §6. Sauf
mention contraire, chaque écran est maquetté en desktop 1440 px et en mobile 390 px.

| # | Écran | Nature | Écran d'origine |
|---|---|---|---|
| **E2** | **Trouver mon prochain livre** : « Mes coups de cœur » gagne le champ de recherche, le compteur, la pagination et l'ouverture de E12 au retrait. Au-dessus des suggestions, l'avancement du calcul | *modifié* | book-favorites E2 |
| **E4** | **Création de compte et reprise** : le message de reprise ne connaît plus la variante « plafond atteint » | *modifié* | book-favorites E4 |
| **E5** | **Fiche livre** : le retrait passe par E12 | *modifié* | book-favorites E5 |
| **E6** | **Mes achats** : le retrait passe par E12 | *modifié* | book-favorites E6 |
| **E11** | **États** : calcul avec avancement (remplace « E2 en calcul »), carte dont le calcul est indisponible, recherche sans résultat, erreur de retrait, calcul qui dure. Suppression de « plafond de coups de cœur atteint » | *modifié* | book-favorites E11 |
| **E12** | **Confirmer le retrait** : fenêtre avec le titre de l'œuvre, une question, « Annuler » (focus par défaut) et « Retirer » | *nouveau* | — |

## 7. Règles métier

### Retrait

#### `RG-160` — Retirer se confirme
- Tout retrait (E2, E5, E6) ouvre E12. Rien n'est retiré avant le clic sur « Retirer ».
- E12 porte le titre de l'œuvre. Échap, la croix et « Annuler » ferment la fenêtre sans rien
  changer.
- Le focus reste dans E12 tant qu'elle est ouverte, et revient au bouton d'origine à la
  fermeture.

#### `RG-161` — Le retrait est enregistré à la confirmation
- Confirmer retire le livre et enregistre le retrait tout de suite, côté compte comme côté
  navigateur. Aucun délai ne retarde l'enregistrement. Ce point remplace le délai de `RG-134`.
- Après confirmation, un rechargement de la page ne fait pas réapparaître le livre.
- Si l'enregistrement échoue, le livre revient à sa place et E11 affiche l'erreur jusqu'à ce
  que le membre la ferme. Un échec n'est jamais silencieux.
- Si le navigateur refuse l'écriture de la liste (navigation privée, stockage plein), le
  retrait est signalé comme non enregistré, avec le message de navigateur indisponible déjà
  prévu.
- Après confirmation, aucun bandeau « Annuler » n'est proposé (question `Q-01`).

#### `RG-162` — Suggestions après retrait
- Après un retrait, E2 n'affiche plus une suggestion qui ne s'appuyait que sur le livre retiré
  (`RG-136`).

### Liste sans limite

#### `RG-163` — Plus de limite de coups de cœur (remplace `RG-131`)
- Il n'existe plus de maximum de coups de cœur, ni dans le navigateur ni dans le compte.
- Les messages « Vous avez atteint 50 coups de cœur » et « 200 coups de cœur, le maximum », ainsi
  que le compteur « n / 200 », ne sont plus affichés. Le compteur indique le total :
  « 137 coups de cœur ».
- À la création de compte, tous les coups de cœur du navigateur sont repris. Le message E4
  indique seulement leur nombre.
- `RG-135` (fiches de référence) n'est pas modifié.

### Recherche et pagination

#### `RG-164` — Recherche dans mes coups de cœur
- Un champ « Rechercher dans mes coups de cœur » est placé au-dessus de la liste. Il porte sur
  le titre, l'auteur et l'ISBN de chaque édition d'une œuvre.
- La recherche ignore la casse, les accents et les espaces en début et en fin de saisie.
- Elle se met à jour pendant la saisie, sans rechargement de la page.
- Le compteur indique le nombre de résultats : « 4 résultats sur 137 ».
- Sans résultat, E11 affiche « Aucun coup de cœur ne correspond à « … ». » avec le bouton
  « Effacer la recherche ».
- La recherche ne modifie pas les suggestions.

#### `RG-165` — Pagination
- La liste affiche 10 coups de cœur par page (question `Q-04`). Le tri reste du plus récent au
  plus ancien (`RG-134`).
- La pagination porte sur les résultats de la recherche. Le repère indique la page :
  « Page 2 sur 14 ». Les boutons Précédent et Suivant sont désactivés aux extrémités.
- Modifier la recherche ramène à la page 1.
- Retirer le dernier coup de cœur d'une page ramène à la page précédente. Sinon, la page
  reste la même.
- Changer de page place le focus en haut de la liste.
- Sur mobile (390 px), la pagination reste visible sans défilement horizontal de la page.

### Calcul des suggestions

#### `RG-166` — Avancement du calcul
- Tant qu'un coup de cœur est en calcul, E2 affiche au-dessus des suggestions :
  « Calcul des suggestions : N sur M coups de cœur prêts », avec une barre de progression.
  M compte les œuvres dont le calcul est lancé (`RG-130`).
- L'avancement se met à jour sans rechargement de la page. Il vaut pour le visiteur comme pour
  le membre.
- Chaque carte dont le calcul est en cours garde la mention « Suggestions en cours de calcul… ».
- Quand le dernier calcul est terminé, l'avancement disparaît et les suggestions arrivent dans
  la grille, sans rechargement (`RG-139`). Leur arrivée est annoncée une seule fois aux lecteurs
  d'écran.

#### `RG-167` — Calcul indisponible
- Un calcul qui a échoué, ou qui n'a jamais été lancé, n'est jamais affiché comme « en cours ».
  La carte affiche « Suggestions indisponibles pour ce livre ».
- Ce coup de cœur compte comme terminé dans l'avancement (`RG-166`).
- Le visiteur ou le membre n'a aucune action à faire. Le livre reste dans sa liste.

#### `RG-168` — Calcul qui dure
- Si un calcul dépasse 10 minutes (question `Q-06`), E2 affiche : « Le calcul prend plus de
  temps que prévu. Vos suggestions apparaîtront ici dès qu'elles seront prêtes. »
- Les suggestions déjà prêtes restent affichées. La mise à jour continue tant que la page est
  ouverte.

#### `RG-169` — Suggestions désactivées
- Si le membre a désactivé les suggestions (`RG-154`), E2 n'affiche ni avancement ni mention
  « Calcul en cours ».

#### `RG-170` — Mise à jour pendant que la page est ouverte
- L'avancement et l'arrivée des suggestions ne sont visibles que pendant que E2 est ouvert.
  Quand la page se ferme, rien n'est poussé.

## 8. Critères d'acceptation

| # | Critère |
|---|---|
| CA-18 | Un coup de cœur confirmé dans E12 (depuis E2, E5 ou E6) n'est plus là après un rechargement, même si le rechargement a lieu juste après la confirmation. |
| CA-19 | « Annuler », la croix ou Échap dans E12 ne retirent rien. |
| CA-20 | Si l'enregistrement du retrait échoue, le livre revient dans la liste et l'erreur reste affichée jusqu'à sa fermeture. |
| CA-21 | Un membre peut avoir plus de 200 coups de cœur, et un visiteur plus de 50, sans aucun message de plafond dans E2, E4, E5 ou E6. |
| CA-22 | La recherche « hugo », en minuscules, trouve une œuvre dont l'auteur est Victor Hugo. Elle trouve aussi une œuvre dont une des éditions porte l'ISBN recherché. |
| CA-23 | Avec 25 coups de cœur, la page 1 en affiche 10. « Suivant » mène à la page 2, qui en affiche 10 et indique « Page 2 sur 3 ». |
| CA-24 | Une recherche sans résultat affiche le message. « Effacer la recherche » rétablit la liste complète, à la page 1. |
| CA-25 | Pendant un calcul, pour un visiteur comme pour un membre, l'avancement passe de « 0 sur 3 » à « 3 sur 3 » sans rechargement. Les suggestions apparaissent ensuite et l'avancement disparaît. |
| CA-26 | Un calcul en échec n'affiche jamais « en cours ». La carte affiche « Suggestions indisponibles pour ce livre ». |
| CA-27 | Avec les suggestions désactivées, E2 n'affiche ni avancement ni « Calcul en cours ». |
| CA-28 | À 390 px, la recherche, la pagination et E12 tiennent à l'écran sans défilement horizontal. E12 reste entièrement visible. |
| CA-29 | E12 a un titre accessible, le focus reste à l'intérieur tant qu'elle est ouverte, et Échap la ferme. |

## 9. Contraintes

- **Mise à jour en direct.** L'avancement et l'arrivée des suggestions passent par un flux
  serveur (SSE). Le flux n'est ouvert que pendant qu'E2 affiche un calcul en cours, et il est
  fermé quand la page se ferme. Il se reconnecte seul et reprend l'état à jour, sans perte.
  Le mécanisme SSE déjà en place pour le tableau en direct des événements sert de référence.
- **Membre.** Le flux est protégé comme les autres routes du compte. Aucun jeton ni identifiant
  de compte ne figure dans une adresse.
- **Visiteur.** La liste d'ISBN envoyée pour le calcul n'est pas conservée côté serveur, comme
  pour les suggestions anonymes de `book-favorites`.
- **Pagination côté serveur pour le membre** (question `Q-05`). Une page ou une recherche ne
  recharge pas toute la liste. Les éditions sont regroupées par œuvre (`RG-130`) avant la
  pagination.
- **Volume.** E2 reste fluide avec 1 000 coups de cœur.
- **Aucun appel ISBNdb.** Ni la recherche, ni la pagination, ni le flux n'interrogent ISBNdb
  (`RG-133`).
- **Calcul inchangé.** Le calcul reste fait par le Worker (`RG-139`). Le flux ne déclenche
  aucun calcul.
- **Accessibilité.** Les lecteurs d'écran annoncent l'avancement de façon non intrusive. E12 et
  la pagination sont utilisables au clavier.

## 10. Questions ouvertes

Chaque question porte une recommandation. Les réponses seront reprises dans le plan.

- **`Q-01` — Bandeau « Annuler » après confirmation.** `RG-134` prévoyait un « Annuler » pendant
  quelques secondes. Comme E12 demande déjà une confirmation, je recommande de supprimer ce
  bandeau. Alternative : le garder, avec un nouvel enregistrement du livre.
- **`Q-02` — E12 sur E5 et E6.** Recommandé : oui, un seul comportement pour le même geste.
- **`Q-03` — Plafond du navigateur (visiteur).** Recommandé : le lever aussi. Les messages de
  stockage indisponible restent en place (`RG-161`).
- **`Q-04` — Taille de page.** Recommandé : 10 coups de cœur par page.
- **`Q-05` — Pagination côté serveur pour le membre.** Recommandé : oui. Le visiteur, dont la
  liste est dans le navigateur, est paginé dans le navigateur.
- **`Q-06` — Seuil du message « calcul qui dure ».** Recommandé : 10 minutes.
- **`Q-07` — Visiteur et avancement en direct.** Recommandé : oui, le visiteur voit aussi
  l'avancement et l'arrivée des suggestions.
