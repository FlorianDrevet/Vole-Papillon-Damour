# Benchmark des méthodes de livres proches — résultats

Généré le 2026-09-24 19:11 par `python -m bench.run --provider azure`.
Embeddings : Azure OpenAI, déploiement `text-embedding-3-small` (text-embedding-3-small).

## 1. Le corpus

**165 éditions réelles de 158 œuvres**, notices BnF et Open Library récupérées le 24/09/2026. Chaque édition sert à son tour de livre de départ : 165 requêtes, 5 voisins demandés.

| Donnée disponible | Éditions |
|---|---|
| Résumé propre à l'édition (BnF 330) | 67 (40 %) |
| Résumé de l'œuvre, toutes éditions confondues | 147 (89 %) |
| … dont venu de la BnF | 126 (76 %) |
| … dont venu d'Open Library | 21 (12 %) |
| Collection (225) | 104 (63 %) |
| Série et tome (461) | 53 (32 %) |
| Identifiant d'œuvre BnF (500) | 40 (24 %) |
| Identifiant d'œuvre Open Library | 39 (23 %) |
| Mention CNLJ (livre jeunesse évalué) | 15 (9 %) |
| Public explicite (333) | 3 (1 %) |

## 2. Ce que les méthodes devinent seules

Les filtres et bonus reposent sur des informations **déduites des notices**. Leur justesse, comparée aux étiquettes :

| Déduction | Justesse |
|---|---|
| Public (jeunesse / adulte) | 146/165 (88 %) |
| Forme (texte / illustré / essai) | 153/165 (92 %) |
| Série reconnue, parmi les livres de série | 43/49 |
| Éditions d'une même œuvre reconnues comme telles | 7/7 paires |
| Œuvres différentes confondues à tort | 0 paires |

## 3. Résultats par méthode

Classement par nDCG@5. Lecture : plus c'est haut, mieux c'est, sauf pour les trois colonnes d'erreurs (fuite, public, forme).

| Méthode | nDCG@5 | Précision@5 | Série@5 | Tome suivant@5 | Fuite même œuvre ↓ | Jeunesse↔adulte ↓ | Autre forme ↓ | Couverture | Auteurs distincts | Calcul |
|---|---|---|---|---|---|---|---|---|---|---|
| **H7** H6 sans la collection dans le texte (exploratoire) | 0.592 | 46 % | 100 % | 100 % | 0 % | 3 % | 12 % | 100 % | 4.5 | 0.02 s |
| **H5** E4 + même œuvre exclue + bonus, sans filtre de public (exploratoire) | 0.587 | 45 % | 100 % | 100 % | 0 % | 5 % | 13 % | 100 % | 4.5 | 0.02 s |
| **H6** E4 + même œuvre exclue + bonus + pénalité de public (exploratoire) | 0.586 | 46 % | 100 % | 100 % | 0 % | 3 % | 13 % | 100 % | 4.5 | 0.02 s |
| **H7-512** H7 en 512 dimensions (exploratoire) | 0.580 | 44 % | 100 % | 100 % | 0 % | 4 % | 13 % | 100 % | 4.5 | 0.02 s |
| **H6-512** H6 en 512 dimensions (exploratoire) | 0.579 | 45 % | 100 % | 100 % | 0 % | 4 % | 12 % | 100 % | 4.5 | 0.02 s |
| **H4** E4 + filtres + bonus (candidat retenu) | 0.563 | 44 % | 100 % | 100 % | 0 % | 4 % | 13 % | 100 % | 4.6 | 0.01 s |
| **H4-512** H4 en 512 dimensions | 0.560 | 43 % | 100 % | 100 % | 0 % | 5 % | 13 % | 100 % | 4.6 | 0.01 s |
| **E4** Embedding texte composé | 0.555 | 44 % | 100 % | 100 % | 100 % | 5 % | 15 % | 100 % | 4.4 | 0.00 s |
| **F4** E4 + filtres | 0.553 | 43 % | 100 % | 100 % | 0 % | 5 % | 17 % | 100 % | 4.6 | 0.01 s |
| **H3** E3 + filtres + bonus | 0.541 | 41 % | 100 % | 100 % | 0 % | 4 % | 18 % | 100 % | 4.5 | 0.01 s |
| **E3** Embedding résumé de l'œuvre | 0.514 | 41 % | 86 % | 82 % | 100 % | 5 % | 23 % | 100 % | 4.5 | 0.00 s |
| **H4-256** H4 en 256 dimensions | 0.509 | 38 % | 100 % | 100 % | 0 % | 4 % | 14 % | 100 % | 4.6 | 0.01 s |
| **E1** Embedding titre + auteur | 0.461 | 36 % | 100 % | 100 % | 100 % | 9 % | 16 % | 100 % | 4.5 | 0.00 s |
| **E2** Embedding résumé de l'édition | 0.459 | 36 % | 100 % | 100 % | 100 % | 7 % | 22 % | 100 % | 4.5 | 0.00 s |
| **L1** TF-IDF lexical (sans IA) | 0.316 | 22 % | 100 % | 100 % | 100 % | 12 % | 29 % | 100 % | 4.5 | 0.03 s |
| **R0** Règles seules (sans IA) | 0.216 | 12 % | 100 % | 100 % | 0 % | 11 % | 7 % | 13 % | 1.2 | 0.04 s |

<details><summary>Définition des indicateurs</summary>

- **nDCG@5** : qualité du classement des 5 voisins (1 = parfait), avec la grille : même série 3, même famille + public + forme 2, lien lâche 1, sans rapport 0.
- **Précision@5** : part des 5 voisins notés au moins 2.
- **Série@5** : pour un livre dont un autre tome est au corpus, part des cas où un tome de la série est proposé. **Tome suivant@5** : idem pour le tome n+1 exactement.
- **Fuite même œuvre** : pour une œuvre présente en deux éditions, part des cas où l'autre édition est proposée. C'est une erreur : ce n'est pas une recommandation.
- **Jeunesse↔adulte** : part des voisins d'un public opposé. **Autre forme** : BD proposée sous un roman, ou l'inverse (indicatif : ce n'est pas toujours faux).
- **Couverture** : part des livres qui obtiennent 5 voisins. **Auteurs distincts** : diversité du top 5.

</details>

- **R0** — Même série (+4, tome suivant +1), même auteur (+2), même collection typée (+1), genres, sujets et classement BnF rares en commun (+1 chacun, 2 au plus). Filtres : même œuvre et public incompatible exclus. Un livre sans aucun point commun n'est pas proposé.
- **L1** — Mots partagés entre textes composés, pondérés par leur rareté. Aucun filtre.
- **E1** — Vecteur du seul titre et des auteurs. Aucun filtre.
- **E2** — Vecteur du résumé propre à l'édition, sinon titre + auteur. Aucun filtre.
- **E3** — Vecteur du meilleur résumé parmi toutes les éditions de l'œuvre (BnF, sinon Open Library), sinon titre + auteur. Aucun filtre.
- **E4** — Vecteur de : titre, auteurs, collection, série, genres, sujets, public, résumé de l'œuvre. Aucun filtre.
- **F4** — E4, puis exclusion de la même œuvre et des publics incompatibles (jeunesse / adulte).
- **H3** — Résumé de l'œuvre, filtres, puis bonus : même série +0,20, tome suivant +0,10, même auteur +0,08, même forme +0,03.
- **H4** — Texte composé, filtres et bonus. C'est la méthode proposée pour la production.
- **H5** — Comme H4, mais le public déduit n'intervient pas. Ajoutée après lecture des premiers résultats : le public est mal déduit pour 11 % des éditions et le filtre strict amplifie ces erreurs.
- **H6** — Comme H5, avec une pénalité de 0.10 (au lieu d'une exclusion) quand les publics déduits s'opposent (jeunesse / adulte). Ajoutée après lecture des premiers résultats.
- **H6-512** — H6 avec des vecteurs réduits à 512 dimensions.
- **H7** — Comme H6, mais le texte composé ne mentionne pas la collection, qui semblait rapprocher les livres d'un même éditeur plutôt que d'un même thème.
- **H7-512** — H7 avec des vecteurs réduits à 512 dimensions.
- **H4-512** — H4 avec des vecteurs réduits à 512 dimensions (stockage divisé par 3).
- **H4-256** — H4 avec des vecteurs réduits à 256 dimensions (stockage divisé par 6).

## 4. Par famille thématique (nDCG@5)

| Famille | R0 | L1 | E3 | E4 | H4 | H6 |
|---|---|---|---|---|---|---|
| absurde | 0.335 | 0.287 | 0.327 | 0.499 | 0.668 | 0.668 |
| animaux | 0.408 | 0.438 | 0.656 | 0.596 | 0.594 | 0.594 |
| aventure-classique | 0.254 | 0.601 | 0.723 | 0.593 | 0.609 | 0.609 |
| bd-aventure | 0.369 | 0.639 | 0.879 | 0.679 | 0.711 | 0.711 |
| bd-gag | 0.028 | 0.042 | 0.050 | 0.250 | 0.195 | 0.191 |
| bd-historique | 0.000 | 0.204 | 0.000 | 0.360 | 0.231 | 0.310 |
| bd-polar | 0.329 | 0.393 | 0.182 | 0.564 | 0.576 | 0.576 |
| conte-philosophique | 0.235 | 0.000 | 0.264 | 0.342 | 0.404 | 0.404 |
| cuisine | 0.883 | 0.578 | 0.866 | 1.000 | 1.000 | 1.000 |
| developpement-personnel | 0.303 | 0.268 | 0.561 | 0.773 | 0.799 | 0.799 |
| dystopie | 0.062 | 0.197 | 0.508 | 0.528 | 0.516 | 0.516 |
| ecole-de-magie | 0.592 | 0.603 | 0.803 | 0.750 | 0.836 | 0.862 |
| enquete-classique | 0.170 | 0.405 | 0.444 | 0.480 | 0.531 | 0.531 |
| fantasy-adulte | 0.040 | 0.313 | 0.415 | 0.415 | 0.455 | 0.528 |
| fantasy-jeunesse | 0.294 | 0.355 | 0.746 | 0.619 | 0.622 | 0.661 |
| feel-good | 0.000 | 0.068 | 0.537 | 0.545 | 0.578 | 0.578 |
| feminisme | 0.000 | 0.381 | 0.609 | 0.566 | 0.611 | 0.611 |
| guerre-14-18 | 0.159 | 0.276 | 0.428 | 0.378 | 0.286 | 0.339 |
| histoire-humanite | 0.992 | 0.385 | 0.517 | 0.687 | 0.834 | 0.834 |
| humour-enfance | 0.217 | 0.385 | 0.328 | 0.448 | 0.330 | 0.411 |
| mythologie | 0.000 | 0.000 | 0.684 | 0.592 | 0.000 | 0.000 |
| polar-nordique | 0.253 | 0.468 | 0.647 | 0.762 | 0.876 | 0.876 |
| quete-initiatique | 0.056 | 0.028 | 0.337 | 0.378 | 0.402 | 0.402 |
| realisme-xixe | 0.151 | 0.287 | 0.594 | 0.672 | 0.655 | 0.748 |
| roman-historique-xxe | 0.522 | 0.606 | 0.445 | 0.418 | 0.418 | 0.418 |
| romance | 0.127 | 0.188 | 0.542 | 0.501 | 0.492 | 0.492 |
| romance-classique | 0.226 | 0.420 | 0.503 | 0.552 | 0.552 | 0.552 |
| sf | 0.249 | 0.386 | 0.635 | 0.681 | 0.759 | 0.759 |
| shonen | 0.145 | 0.275 | 0.393 | 0.738 | 0.453 | 0.552 |
| thriller | 0.177 | 0.303 | 0.429 | 0.584 | 0.632 | 0.632 |
| vulgarisation-scientifique | 0.000 | 0.807 | 0.950 | 0.488 | 0.567 | 0.567 |

## 5. Exemples côte à côte

Notes : ★★★ même série · ★★ proche · ★ lien lâche · ✗ sans rapport · ⚠ autre édition du même livre.

### Harry Potter à l'école des sorciers — Rowling

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Harry Potter à l'école des sorciers — Rowling | ⚠ Harry Potter à l'école des sorciers — Rowling | ★★★ Harry Potter et la chambre des secrets — Rowling | ★★★ Harry Potter et la chambre des secrets — Rowling |
| 2 | ★★★ Harry Potter et la chambre des secrets — Rowling | ★★★ Harry Potter et la chambre des secrets — Rowling | ★★★ Harry Potter et le prisonnier d'Azkaban — Rowling | ★★★ Harry Potter et le prisonnier d'Azkaban — Rowling |
| 3 | ★★★ Harry Potter et le prisonnier d'Azkaban — Rowling | ★★★ Harry Potter et le prisonnier d'Azkaban — Rowling | ★★ Le lion, la sorcière blanche et l'armoire magique — Lewis | ★★ Le lion, la sorcière blanche et l'armoire magique — Lewis |
| 4 | ★★ Bilbo le Hobbit — Tolkien | ★★ Percy Jackson et les Olympiens — Riordan | ★★ Les sortceliers — Audouin-Mamikonian | ★★ Les sortceliers — Audouin-Mamikonian |
| 5 | ★★ Percy Jackson et les Olympiens — Riordan | ★★ Le lion, la sorcière blanche et l'armoire magique — Lewis | ✗ Matilda — Dahl | ✗ Matilda — Dahl |

### Percy Jackson et les Olympiens — Riordan

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★ La Passe-miroir — Dabos | ★★ Harry Potter à l'école des sorciers — Rowling | ✗ Le passeur — Lowry | ✗ Le passeur — Lowry |
| 2 | ★★ Harry Potter à l'école des sorciers — Rowling | ★ Les royaumes du Nord — Pullman | ✗ Hypérion — Simmons | ✗ Hypérion — Simmons |
| 3 | ★★ Harry Potter à l'école des sorciers — Rowling | ✗ Le passeur — Lowry | ✗ L'île au trésor — Stevenson | ✗ L'île au trésor — Stevenson |
| 4 | ✗ Death note — Ohba | ★★ Harry Potter à l'école des sorciers — Rowling | ✗ Vingt mille lieues sous les mers — Verne | ✗ Vingt mille lieues sous les mers — Verne |
| 5 | ★★ Harry Potter et la chambre des secrets — Rowling | ✗ Rodrick fait sa loi — Kinney | ✗ Et après — Musso | ✗ Et après — Musso |

### 1984 — Orwell

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★ 1984 — Coste | ★ 1984 — Coste | ★ 1984 — Coste | ★ 1984 — Coste |
| 2 | ★★ Le meilleur des mondes — Huxley | ★★ Le meilleur des mondes — Huxley | ★★ Le meilleur des mondes — Huxley | ★★ Le meilleur des mondes — Huxley |
| 3 | ✗ Le tour du monde en quatre-vingts jours — Verne | ✗ Le tour du monde en quatre-vingts jours — Verne | ✗ Le tour du monde en quatre-vingts jours — Verne | ✗ Le tour du monde en quatre-vingts jours — Verne |
| 4 | ✗ La nuit des temps — Barjavel | ✗ La horde du contrevent — Damasio | ✗ La horde du contrevent — Damasio | ✗ La horde du contrevent — Damasio |
| 5 | ✗ À l'Ouest rien de nouveau — Remarque | ✗ À l'Ouest rien de nouveau — Remarque | ✗ À l'Ouest rien de nouveau — Remarque | ✗ À l'Ouest rien de nouveau — Remarque |

### 1984 — Coste

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★ 1984 — Orwell | ★ 1984 — Orwell | ★ 1984 — Orwell | ★ 1984 — Orwell |
| 2 | ★ Le meilleur des mondes — Huxley | ★ Le meilleur des mondes — Huxley | ★ Le meilleur des mondes — Huxley | ★ Le meilleur des mondes — Huxley |
| 3 | ★ Ravage — Barjavel | ★ Ravage — Barjavel | ✗ Au revoir là-haut — De Metter | ✗ Au revoir là-haut — De Metter |
| 4 | ✗ Le liseur du 6h27 — Didierlaurent | ✗ La horde du contrevent — Damasio | ★ Ravage — Barjavel | ★ Ravage — Barjavel |
| 5 | ★ Le passeur — Lowry | ✗ À l'Ouest rien de nouveau — Remarque | ✗ La horde du contrevent — Damasio | ✗ La horde du contrevent — Damasio |

### Les hommes qui n'aimaient pas les femmes — Larsson

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Les hommes qui n'aimaient pas les femmes — Larsson | ⚠ Les hommes qui n'aimaient pas les femmes — Larsson | ★★★ La fille qui rêvait d'un bidon d'essence et d'une allumette — Larsson | ★★★ La fille qui rêvait d'un bidon d'essence et d'une allumette — Larsson |
| 2 | ★★ Le Bonhomme de neige — Nesbø | ★★★ La fille qui rêvait d'un bidon d'essence et d'une allumette — Larsson | ★★ Miséricorde — Adler-Olsen | ★★ Miséricorde — Adler-Olsen |
| 3 | ★★ Miséricorde — Adler-Olsen | ★★ Miséricorde — Adler-Olsen | ★★ Le Bonhomme de neige — Nesbø | ★★ Le Bonhomme de neige — Nesbø |
| 4 | ★★ Da Vinci code — Brown | ★★ Le Bonhomme de neige — Nesbø | ★★ La femme en vert — Indridason | ★★ La femme en vert — Indridason |
| 5 | ★★ La femme en vert — Indridason | ★★ La femme en vert — Indridason | ★★ Le silence des agneaux — Harris | ★★ Le silence des agneaux — Harris |

### Germinal — Zola

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Germinal — Zola | ⚠ Germinal — Zola | ✗ Au revoir là-haut — De Metter | ★★ L'Assommoir — Zola |
| 2 | ★★ Le Rouge et le Noir — Stendhal | ★★ L'Assommoir — Zola | ✗ Belle et Sébastien — Aubry | ★★ Une vie — Maupassant |
| 3 | ★★ Une vie — Maupassant | ★★ Une vie — Maupassant | ✗ Tout ça finira mal — Tan | ✗ Le liseur du 6h27 — Didierlaurent |
| 4 | ✗ Le liseur du 6h27 — Didierlaurent | ✗ Le liseur du 6h27 — Didierlaurent | ✗ La vie suspendue — Fombelle | ★★ Bel-Ami — Maupassant |
| 5 | ★★ Les misérables — Hugo | ★★ Bel-Ami — Maupassant | ✗ Matilda — Dahl | ★★ Les misérables — Hugo |

### L'étranger — Camus

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ L'étranger — Camus | ⚠ L'étranger — Camus | ★★ La peste — Camus | ★★ La peste — Camus |
| 2 | ★★ La peste — Camus | ✗ Au revoir là-haut — Lemaitre | ✗ Au revoir là-haut — Lemaitre | ✗ Au revoir là-haut — Lemaitre |
| 3 | ✗ Au revoir là-haut — De Metter | ✗ Et après — Musso | ✗ Et après — Musso | ✗ Et après — Musso |
| 4 | ✗ Et après — Musso | ★★ La peste — Camus | ★★ La nausée — Sartre | ★★ La nausée — Sartre |
| 5 | ✗ Une vie — Maupassant | ★★ La nausée — Sartre | ★★ Le procès — Kafka | ★★ Le procès — Kafka |

### Le Petit Prince — Saint-Exupéry

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Le Petit prince — Saint-Exupéry | ⚠ Le Petit prince — Saint-Exupéry | ★★ Le prophète — Gibran | ★★ Le prophète — Gibran |
| 2 | ✗ La serpe d'or — Goscinny | ★★ Le prophète — Gibran | ✗ Le liseur du 6h27 — Didierlaurent | ✗ Le liseur du 6h27 — Didierlaurent |
| 3 | ✗ Astérix le Gaulois — Goscinny | ✗ Le liseur du 6h27 — Didierlaurent | ✗ Le petit Nicolas — Goscinny | ✗ Le petit Nicolas — Goscinny |
| 4 | ★★ Le prophète — Gibran | ✗ Le petit Nicolas — Goscinny | ✗ Astérix le Gaulois — Goscinny | ✗ Astérix le Gaulois — Goscinny |
| 5 | ✗ À l'Ouest rien de nouveau — Remarque | ✗ Astérix le Gaulois — Goscinny | ✗ Le grand troupeau — Giono | ✗ Le grand troupeau — Giono |

### Dune — Herbert

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Dune — Herbert | ⚠ Dune — Herbert | ★★ Hypérion — Simmons | ★★ Hypérion — Simmons |
| 2 | ★★ Ravage — Barjavel | ★★ Hypérion — Simmons | ★★ La horde du contrevent — Damasio | ★★ La horde du contrevent — Damasio |
| 3 | ★★ Le meilleur des mondes — Huxley | ★★ La horde du contrevent — Damasio | ★★ Ravage — Barjavel | ★★ Ravage — Barjavel |
| 4 | ✗ Les royaumes du Nord — Pullman | ★★ Ravage — Barjavel | ✗ Le comte de Monte-Cristo — Dumas | ✗ Le comte de Monte-Cristo — Dumas |
| 5 | ★★ Hypérion — Simmons | ✗ Les royaumes du Nord — Pullman | ★★ Le meilleur des mondes — Huxley | ★★ Le meilleur des mondes — Huxley |

### Astérix le Gaulois — Goscinny

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★★ La serpe d'or — Goscinny | ★★★ La serpe d'or — Goscinny | ★★★ La serpe d'or — Goscinny | ★★★ La serpe d'or — Goscinny |
| 2 | ★★ "Z comme Zorglub" — Franquin | ✗ Gala de gaffes à gogo — Franquin | ★ Le petit Nicolas — Goscinny | ★ Le petit Nicolas — Goscinny |
| 3 | ★★ Le secret de l'Espadon — Jacobs | ★ Le petit Nicolas — Goscinny | ★★ Dalton City — Morris | ★★ Dalton City — Morris |
| 4 | ✗ Gala de gaffes à gogo — Franquin | ★★ "Z comme Zorglub" — Franquin | ✗ Gala de gaffes à gogo — Franquin | ✗ Gala de gaffes à gogo — Franquin |
| 5 | ★★ Tintin au Tibet — Hergé | ★★ Objectif lune — Hergé | ✗ Le Petit Prince — Saint-Exupéry | ✗ Le Petit Prince — Saint-Exupéry |

### One piece — Oda

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★★ One piece — Oda | ★★★ One piece — Oda | ★★★ One piece — Oda | ★★★ One piece — Oda |
| 2 | ✗ La serpe d'or — Goscinny | ★★ Dragon Ball — Toriyama | ★★ Death note — Ohba | ★★ Death note — Ohba |
| 3 | ✗ Astérix le Gaulois — Goscinny | ★★ Death note — Ohba | ✗ Dieu, le sexe et les bretelles — Zep | ★★ Dragon Ball — Toriyama |
| 4 | ✗ Le trône de fer — Martin | ★★ Journée portes ouvertes — Horikoshi | ✗ Maus — Spiegelman | ✗ Dieu, le sexe et les bretelles — Zep |
| 5 | ★★ Death note — Ohba | ✗ "Z comme Zorglub" — Franquin | ✗ 1984 — Coste | ★★ Journée portes ouvertes — Horikoshi |

### Retour à l'état sauvage — Hunter

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★★ À feu et à sang — Hunter | ★★★ À feu et à sang — Hunter | ★★★ À feu et à sang — Hunter | ★★★ À feu et à sang — Hunter |
| 2 | ✗ L'apprenti assassin — Hobb | ★★ Les sortceliers — Audouin-Mamikonian | ★★ Les sortceliers — Audouin-Mamikonian | ★★ Les sortceliers — Audouin-Mamikonian |
| 3 | ★★ D'un Monde à l'Autre — Bottero | ★ Eragon — Paolini | ★ Eragon — Paolini | ★ Eragon — Paolini |
| 4 | ★★ Le bon gros géant — Dahl | ★★ D'un Monde à l'Autre — Bottero | ✗ Divergente — Roth | ✗ Divergente — Roth |
| 5 | ★★ Croc-Blanc — London | ✗ L'apprenti assassin — Hobb | ★★ Le lion, la sorcière blanche et l'armoire magique — Lewis | ★★ Le lion, la sorcière blanche et l'armoire magique — Lewis |

### Sapiens — Harari

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ⚠ Sapiens — Harari | ⚠ Sapiens — Harari | ★★ Homo deus — Harari | ★★ Homo deus — Harari |
| 2 | ★★ Homo deus — Harari | ★★ Homo deus — Harari | ✗ Cosmos — Sagan | ✗ Cosmos — Sagan |
| 3 | ✗ Le meilleur des mondes — Huxley | ✗ Cosmos — Sagan | ★★ De l'inégalité parmi les sociétés — Diamond | ★★ De l'inégalité parmi les sociétés — Diamond |
| 4 | ✗ Ravage — Barjavel | ✗ Le meilleur des mondes — Huxley | ✗ Le meilleur des mondes — Huxley | ✗ Le meilleur des mondes — Huxley |
| 5 | ✗ La nuit des temps — Barjavel | ★★ De l'inégalité parmi les sociétés — Diamond | ✗ Le gène égoïste — Dawkins | ✗ Le gène égoïste — Dawkins |

### Le Deuxième sexe — Beauvoir

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★ Une chambre à soi — Woolf | ★★ Une chambre à soi — Woolf | ★★ Une chambre à soi — Woolf | ★★ Une chambre à soi — Woolf |
| 2 | ✗ Madame Bovary — Flaubert | ✗ Ta deuxième vie commence quand tu comprends que tu n'en as qu'une — Giordano | ✗ Ta deuxième vie commence quand tu comprends que tu n'en as qu'une — Giordano | ✗ Ta deuxième vie commence quand tu comprends que tu n'en as qu'une — Giordano |
| 3 | ★★ King Kong théorie — Despentes | ★★ King Kong théorie — Despentes | ★★ King Kong théorie — Despentes | ★★ King Kong théorie — Despentes |
| 4 | ★ La servante écarlate — Atwood | ✗ Une vie — Maupassant | ✗ Une vie — Maupassant | ✗ Une vie — Maupassant |
| 5 | ✗ Une vie — Maupassant | ✗ Madame Bovary — Flaubert | ✗ Madame Bovary — Flaubert | ✗ Madame Bovary — Flaubert |

### Je sais cuisiner — Mathiot

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★ La cuisine de référence — Maincent-Morel | ★★ La cuisine de référence — Maincent-Morel | ★★ La cuisine de référence — Maincent-Morel | ★★ La cuisine de référence — Maincent-Morel |
| 2 | ★★ Pâtisserie ! — Felder | ★★ Pâtisserie ! — Felder | ★★ Pâtisserie ! — Felder | ★★ Pâtisserie ! — Felder |
| 3 | ✗ Gala de gaffes à gogo — Franquin | ★★ Jérusalem — Ottolenghi | ★★ Jérusalem — Ottolenghi | ★★ Jérusalem — Ottolenghi |
| 4 | ★★ Jérusalem — Ottolenghi | ✗ Gala de gaffes à gogo — Franquin | ✗ Gala de gaffes à gogo — Franquin | ✗ Gala de gaffes à gogo — Franquin |
| 5 | ✗ Madame Bovary — Flaubert | ✗ La liste de mes envies — Delacourt | ✗ La liste de mes envies — Delacourt | ✗ La liste de mes envies — Delacourt |

### L'élégance du hérisson — Barbery

| # | E3 | E4 | H4 | H6 |
|---|---|---|---|---|
| 1 | ★★ La liste de mes envies — Delacourt | ✗ Le grand troupeau — Giono | ✗ Le grand troupeau — Giono | ✗ Le grand troupeau — Giono |
| 2 | ★★ Les gratitudes — Vigan | ✗ La Passe-miroir — Dabos | ★★ Ensemble, c'est tout — Gavalda | ★★ Ensemble, c'est tout — Gavalda |
| 3 | ✗ Le grand troupeau — Giono | ★★ Ensemble, c'est tout — Gavalda | ✗ Bel-Ami — Maupassant | ✗ Bel-Ami — Maupassant |
| 4 | ✗ Couleurs de l'incendie — Lemaitre | ✗ Bel-Ami — Maupassant | ★★ Les gratitudes — Vigan | ★★ Les gratitudes — Vigan |
| 5 | ✗ Les croix de bois — Dorgelès | ★★ Les gratitudes — Vigan | ★★ Le liseur du 6h27 — Didierlaurent | ★★ Le liseur du 6h27 — Didierlaurent |

## 6. Les dix livres où H6 se trompe le plus

| Livre | nDCG@5 | Trois premiers voisins |
|---|---|---|
| Percy Jackson et les Olympiens — Riordan | 0.000 | ✗ Le passeur — Lowry ; ✗ Hypérion — Simmons ; ✗ L'île au trésor — Stevenson |
| Le Nom de la Rose — Eco | 0.000 | ✗ Da Vinci code — Brown ; ✗ Ravage — Barjavel ; ✗ D'un Monde à l'Autre — Bottero |
| Les rivières pourpres — Grangé | 0.000 | ✗ Les croix de bois — Dorgelès ; ✗ Les gratitudes — Vigan ; ✗ Couleurs de l'incendie — Lemaitre |
| Da Vinci code — Brown | 0.000 | ✗ Le comte de Monte-Cristo — Dumas ; ✗ Le Nom de la Rose — Eco ; ✗ Et après — Musso |
| Jonathan Livingston le goéland — Bach | 0.000 | ✗ Carnet de bord de Greg Heffley — Kinney ; ✗ La vie suspendue — Fombelle ; ✗ Eragon — Paolini |
| Le grand troupeau — Giono | 0.000 | ✗ L'élégance du hérisson — Barbery ; ✗ Ensemble, c'est tout — Gavalda ; ✗ Le liseur du 6h27 — Didierlaurent |
| Nos étoiles contraires — Green | 0.000 | ✗ Carnet de bord de Greg Heffley — Kinney ; ✗ Divergente — Roth ; ✗ Rodrick fait sa loi — Kinney |
| Les malheurs de Sophie — Ségur | 0.000 | ✗ Les misérables — Hugo ; ✗ Un long dimanche de fiançailles — Japrisot ; ✗ Belle et Sébastien — Aubry |
| Belle et Sébastien — Aubry | 0.000 | ✗ Les sortceliers — Audouin-Mamikonian ; ✗ La vie suspendue — Fombelle ; ✗ Les malheurs de Sophie — Ségur |
| Premières classes — Cauvin | 0.000 | ✗ Ensemble, c'est tout — Gavalda ; ✗ Le liseur du 6h27 — Didierlaurent ; ✗ Le petit Nicolas — Goscinny |

## 7. Coût

Tokens envoyés pour ce corpus (compté par l'API) : **82 930**  soit **0.00166 $** à 0.02 $ par million. 0 requête(s)  0.0 s.

| Texte vectorisé | Tokens (textes uniques) | Tokens moyens par livre | Projection 20 000 livres |
|---|---|---|---|
| titre_auteur | 1 983 | 12 | 0.005 $ |
| resume_edition | 14 790 | 90 | 0.036 $ |
| resume_oeuvre | 27 175 | 165 | 0.066 $ |
| compose | 31 785 | 193 | 0.077 $ |
| compose_sans_collection | 30 441 | 184 | 0.074 $ |

Stockage des vecteurs pour 20 000 livres (float32) : 1536 dimensions ≈ 123 Mo · 512 ≈ 41 Mo · 256 ≈ 20 Mo.

## 8. Limites du protocole

- **La vérité de référence est un jugement éditorial** (`bench/corpus_spec.py`) : familles, publics et séries sont posés à la main. Elle est lisible et modifiable ; les méthodes n'y ont pas accès.
- **Le corpus est fait de livres connus.** Les métadonnées y sont meilleures qu'en moyenne ; un catalogue réel de bourse aura moins de résumés.
- **Corpus de 165 éditions.** Dans un catalogue de 20 000 livres, il y aura beaucoup plus de voisins plausibles, et les écarts entre méthodes se creuseront plutôt qu'ils ne se réduiront.
- **Pas de signal de comportement** (co-achats, co-sélection) : il n'existe pas encore.

