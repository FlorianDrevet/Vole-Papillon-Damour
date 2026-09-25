# Benchmark des méthodes de livres proches — résultats

Généré le 2026-09-24 17:57 par `python -m bench.run --provider none`.

> **Exécution sans embeddings.** Seules les méthodes sans IA (R0, L1) sont évaluées. Relancer avec `--provider azure` pour les autres.

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
| **L1** TF-IDF lexical (sans IA) | 0.316 | 22 % | 100 % | 100 % | 100 % | 12 % | 29 % | 100 % | 4.5 | 0.11 s |
| **R0** Règles seules (sans IA) | 0.216 | 12 % | 100 % | 100 % | 0 % | 11 % | 7 % | 13 % | 1.2 | 0.11 s |

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

## 4. Par famille thématique (nDCG@5)

| Famille | R0 | L1 |
|---|---|---|
| absurde | 0.335 | 0.287 |
| animaux | 0.408 | 0.438 |
| aventure-classique | 0.254 | 0.601 |
| bd-aventure | 0.369 | 0.639 |
| bd-gag | 0.028 | 0.042 |
| bd-historique | 0.000 | 0.204 |
| bd-polar | 0.329 | 0.393 |
| conte-philosophique | 0.235 | 0.000 |
| cuisine | 0.883 | 0.578 |
| developpement-personnel | 0.303 | 0.268 |
| dystopie | 0.062 | 0.197 |
| ecole-de-magie | 0.592 | 0.603 |
| enquete-classique | 0.170 | 0.405 |
| fantasy-adulte | 0.040 | 0.313 |
| fantasy-jeunesse | 0.294 | 0.355 |
| feel-good | 0.000 | 0.068 |
| feminisme | 0.000 | 0.381 |
| guerre-14-18 | 0.159 | 0.276 |
| histoire-humanite | 0.992 | 0.385 |
| humour-enfance | 0.217 | 0.385 |
| mythologie | 0.000 | 0.000 |
| polar-nordique | 0.253 | 0.468 |
| quete-initiatique | 0.056 | 0.028 |
| realisme-xixe | 0.151 | 0.287 |
| roman-historique-xxe | 0.522 | 0.606 |
| romance | 0.127 | 0.188 |
| romance-classique | 0.226 | 0.420 |
| sf | 0.249 | 0.386 |
| shonen | 0.145 | 0.275 |
| thriller | 0.177 | 0.303 |
| vulgarisation-scientifique | 0.000 | 0.807 |

## 5. Exemples côte à côte

Notes : ★★★ même série · ★★ proche · ★ lien lâche · ✗ sans rapport · ⚠ autre édition du même livre.

### Harry Potter à l'école des sorciers — Rowling

| # | R0 |
|---|---|
| 1 | ★★★ Harry Potter et la chambre des secrets — Rowling |
| 2 | ★★★ Harry Potter et le prisonnier d'Azkaban — Rowling |
| 3 |  |
| 4 |  |
| 5 |  |

### Percy Jackson et les Olympiens — Riordan

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### 1984 — Orwell

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### 1984 — Coste

| # | R0 |
|---|---|
| 1 | ✗ Dieu, le sexe et les bretelles — Zep |
| 2 | ✗ Au revoir là-haut — De Metter |
| 3 | ✗ One piece — Oda |
| 4 | ✗ Death note — Ohba |
| 5 | ✗ Maus — Spiegelman |

### Les hommes qui n'aimaient pas les femmes — Larsson

| # | R0 |
|---|---|
| 1 | ★★★ La fille qui rêvait d'un bidon d'essence et d'une allumette — Larsson |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### Germinal — Zola

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### L'étranger — Camus

| # | R0 |
|---|---|
| 1 | ★★ La peste — Camus |
| 2 | ✗ L'élégance du hérisson — Barbery |
| 3 |  |
| 4 |  |
| 5 |  |

### Le Petit Prince — Saint-Exupéry

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### Dune — Herbert

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### Astérix le Gaulois — Goscinny

| # | R0 |
|---|---|
| 1 | ★ Le petit Nicolas — Goscinny |
| 2 | ★★★ La serpe d'or — Goscinny |
| 3 | ★★ Dalton City — Morris |
| 4 |  |
| 5 |  |

### One piece — Oda

| # | R0 |
|---|---|
| 1 | ★★★ One piece — Oda |
| 2 | ✗ 1984 — Coste |
| 3 | ✗ Dieu, le sexe et les bretelles — Zep |
| 4 | ✗ Au revoir là-haut — De Metter |
| 5 | ✗ Maus — Spiegelman |

### Retour à l'état sauvage — Hunter

| # | R0 |
|---|---|
| 1 | ★★★ À feu et à sang — Hunter |
| 2 | ★★ Harry Potter à l'école des sorciers — Rowling |
| 3 | ✗ Le seigneur des anneaux — Tolkien |
| 4 | ★ Les royaumes du Nord — Pullman |
| 5 | ✗ Fifi Brindacier — Lindgren |

### Sapiens — Harari

| # | R0 |
|---|---|
| 1 | ★★ Homo deus — Harari |
| 2 | ★★ De l'inégalité parmi les sociétés — Diamond |
| 3 | ✗ Sorcières — Chollet |
| 4 |  |
| 5 |  |

### Le Deuxième sexe — Beauvoir

| # | R0 |
|---|---|
| 1 |  |
| 2 |  |
| 3 |  |
| 4 |  |
| 5 |  |

### Je sais cuisiner — Mathiot

| # | R0 |
|---|---|
| 1 | ★★ Jérusalem — Ottolenghi |
| 2 | ★★ Pâtisserie ! — Felder |
| 3 | ★★ La cuisine de référence — Maincent-Morel |
| 4 |  |
| 5 |  |

### L'élégance du hérisson — Barbery

| # | R0 |
|---|---|
| 1 | ✗ L'étranger — Camus |
| 2 | ✗ L'étranger — Camus |
| 3 |  |
| 4 |  |
| 5 |  |

## 6. Les dix livres où L1 se trompe le plus

| Livre | nDCG@5 | Trois premiers voisins |
|---|---|---|
| Percy Jackson et les Olympiens — Riordan | 0.000 | ✗ Le père Goriot — Balzac ; ✗ Jonathan Livingston le goéland — Bach ; ✗ Nos étoiles contraires — Green |
| La vie suspendue — Fombelle | 0.000 | ✗ Le meilleur des mondes — Huxley ; ✗ Death note — Ohba ; ✗ Le grand troupeau — Giono |
| Les royaumes du Nord — Pullman | 0.000 | ✗ Dune — Herbert ; ✗ Dune — Herbert ; ✗ Germinal — Zola |
| Le trône de fer — Martin | 0.000 | ✗ 1984 — Orwell ; ✗ Le liseur du 6h27 — Didierlaurent ; ✗ Le secret de l'Espadon — Jacobs |
| Le meilleur des mondes — Huxley | 0.000 | ✗ Madame Bovary — Flaubert ; ✗ Rodrick fait sa loi — Kinney ; ✗ La liste de mes envies — Delacourt |
| Divergente — Roth | 0.000 | ✗ À feu et à sang — Hunter ; ✗ Carnet de bord de Greg Heffley — Kinney ; ✗ Belle et Sébastien — Aubry |
| Le labyrinthe — Dashner | 0.000 | ✗ Nos étoiles contraires — Green ; ✗ Jonathan Livingston le goéland — Bach ; ✗ Le Nom de la Rose — Eco |
| Le passeur — Lowry | 0.000 | ✗ Croc-Blanc — London ; ✗ Les misérables — Hugo ; ✗ Fifi Brindacier — Lindgren |
| Dune — Herbert | 0.000 | ✗ Dune — Herbert ; ✗ Les royaumes du Nord — Pullman ; ✗ Le tour du monde en quatre-vingts jours — Verne |
| Dune — Herbert | 0.000 | ✗ Dune — Herbert ; ✗ Les royaumes du Nord — Pullman ; ✗ Le tour du monde en quatre-vingts jours — Verne |

## 7. Coût

Aucun appel d'IA dans cette exécution.

Stockage des vecteurs pour 20 000 livres (float32) : 1536 dimensions ≈ 123 Mo · 512 ≈ 41 Mo · 256 ≈ 20 Mo.

## 8. Limites du protocole

- **La vérité de référence est un jugement éditorial** (`bench/corpus_spec.py`) : familles, publics et séries sont posés à la main. Elle est lisible et modifiable ; les méthodes n'y ont pas accès.
- **Le corpus est fait de livres connus.** Les métadonnées y sont meilleures qu'en moyenne ; un catalogue réel de bourse aura moins de résumés.
- **Corpus de 165 éditions.** Dans un catalogue de 20 000 livres, il y aura beaucoup plus de voisins plausibles, et les écarts entre méthodes se creuseront plutôt qu'ils ne se réduiront.
- **Pas de signal de comportement** (co-achats, co-sélection) : il n'existe pas encore.

