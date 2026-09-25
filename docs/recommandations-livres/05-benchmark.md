# 05 — Benchmark des méthodes de livres proches

> **24 septembre 2026.** Demande : « Prépare un vrai benchmark complet avec plusieurs
> possibilités de calcul de livres proches […] avec le modèle Azure OpenAI
> (text-embedding-3-small) […] et un vrai rapport que je puisse lire avec tes remarques et
> conseils. »
>
> **Statut : exécuté le 24 septembre 2026 avec Azure OpenAI** (déploiement
> `text-embedding-3-small` sur `vpd-actuality-title-dev`). Coût total : **0,002 $**.
> **Pour la conclusion, lire directement la section 7.**

Outil : `tools/recommendation-benchmark/` (Python, jetable, hors application).
Rapports générés : `tools/recommendation-benchmark/out/rapport-<fournisseur>.md`.

## 1. La question posée au benchmark

**Quelle façon de calculer « les livres les plus proches » donne les meilleures
suggestions sous une fiche du catalogue, pour un coût et une complexité acceptables ?**

Et plus précisément :

1. Les embeddings font-ils mieux que des règles simples ou qu'une similarité lexicale
   gratuite ?
2. Le **résumé** apporte-t-il quelque chose, et faut-il le **partager entre éditions**
   (`03` §3) ?
3. Le **texte composé** (titre, auteur, collection, série, genres, résumé) bat-il le
   résumé seul (`04` §3) ?
4. Les **filtres** (même œuvre, public) et les **bonus** (série, auteur, forme) sont-ils
   utiles ?
5. Peut-on réduire les vecteurs à **512 ou 256 dimensions** sans perte ? Cela divise le
   stockage par 3 ou par 6.

## 2. Le corpus

- **158 œuvres réelles, 165 éditions**, en **31 familles thématiques** : école de magie,
  dystopie, polar nordique, réalisme du XIXe, BD d'aventure, shōnen, cuisine, féminisme…
  L'éventail couvre ce qu'une bourse reçoit : classiques, poches grand public, jeunesse,
  BD, manga, essais, pratique.
- **Des pièges volontaires** :
  - 7 œuvres en **deux éditions**, pour mesurer la fuite « même œuvre » ;
  - 2 **adaptations en BD** (*1984* de Xavier Coste, *Au revoir là-haut* de Christian
    De Metter) ;
  - des **séries** avec tomes consécutifs (*Harry Potter*, *Millénium*, *La Guerre des
    clans*, *One Piece*, *Fondation*, *Astérix*…) ;
  - des voisinages trompeurs entre publics : *1984* (adulte) face à *Hunger Games* (ado).
- **Les notices sont les vraies**, lues à la BnF (`bib.fuzzyISBN`, résumé 330, collection
  225, série 461, œuvre 500…) et chez Open Library.
  [`journal-constitution.md`](../../tools/recommendation-benchmark/data/journal-constitution.md)
  détaille le choix de chaque édition.

Données disponibles sur le corpus :

| Donnée | Éditions |
|---|---|
| Résumé propre à l'édition | 40 % |
| **Résumé de l'œuvre**, toutes éditions confondues (BnF, sinon Open Library) | **89 %** |
| Collection | 63 % |
| Série et tome | 32 % |
| Identifiant d'œuvre (BnF ou Open Library) | 24 % et 23 % |

**Le partage du résumé entre éditions fait passer la couverture de 40 % à 89 %.** C'est
déjà une réponse à la question 2, avant même de mesurer la qualité.

## 3. La vérité de référence

Chaque œuvre est annotée à la main dans `bench/corpus_spec.py` : public, forme, familles
thématiques, série et tome. **Les méthodes n'y ont jamais accès** : elles ne voient que
ce que disent les notices. La note d'un voisin C proposé pour un livre Q :

| Note | Signification | Exemple |
|---|---|---|
| **3** | Même série | *Harry Potter 2* pour *Harry Potter 1* |
| **2** | Même famille, public compatible, même forme | *Homo deus* pour *Sapiens* |
| **1** | Lien lâche : même famille mais autre public ou autre forme, adaptation, même auteur | *Hunger Games* pour *1984* ; *Le Petit Nicolas* pour *Astérix* |
| **0** | Sans rapport | *Harry Potter* pour *Germinal* |
| **⚠** | Autre édition du même livre : **erreur**, comptée à part | *Dune* (Pocket) pour *Dune* (Robert Laffont) |

C'est un **jugement éditorial**, pas une vérité absolue. Il est lisible et modifiable.

## 4. Les onze méthodes en concurrence

| Code | Méthode | Questions éclairées |
|---|---|---|
| **R0** | Règles seules : série, auteur, collection typée, genres et classement **rares** en commun, avec filtres | 1 |
| **L1** | TF-IDF lexical sur le texte composé, sans filtre | 1 |
| **E1** | Embedding du titre et des auteurs | 2 |
| **E2** | Embedding du résumé **de l'édition** (sinon titre + auteur) | 2 |
| **E3** | Embedding du résumé **de l'œuvre** (sinon titre + auteur) | 2 |
| **E4** | Embedding du **texte composé** | 3 |
| **F4** | E4 + filtres (même œuvre, public jeunesse ↔ adulte) | 4 |
| **H3** | E3 + filtres + bonus | 3, 4 |
| **H4** | E4 + filtres + bonus : **la méthode proposée** | 3, 4 |
| **H4-512** | H4 en 512 dimensions | 5 |
| **H4-256** | H4 en 256 dimensions | 5 |

Bonus de H3 et H4, ajoutés au cosinus : même série +0,20, tome suivant +0,10, même
auteur +0,08, même forme +0,03. Ils sont réglables dans `bench/methods.py`.

## 5. Critères de décision, fixés avant les résultats

Pour ne pas interpréter les chiffres après coup, voici **par avance** ce qui décidera :

| Décision | Critère |
|---|---|
| Les embeddings valent leur complexité | La meilleure méthode à embeddings dépasse **à la fois** R0 et L1 d'au moins **0,10** de nDCG@5 |
| Partager le résumé entre éditions | E3 ≥ E2 |
| Composer le texte plutôt que le résumé seul | E4 > E3 **et** H4 ≥ H3 |
| Garder filtres et bonus | H4 > E4, fuite ≈ 0 %, et moins de voisins jeunesse ↔ adulte |
| Réduire à 512 dimensions | H4-512 perd **moins de 0,01** de nDCG@5 par rapport à H4 |
| Qualité suffisante pour publier | H4 : précision@5 ≥ 50 % et tome suivant@5 ≥ 90 % |

Si le dernier critère n'est pas atteint, **ne pas afficher 5 suggestions**. Il vaudra
mieux n'afficher que celles dont le score dépasse un seuil, fixé à partir des données.

## 6. Résultats

### 6.1 Sans IA (exécuté le 24 septembre 2026)

| Méthode | nDCG@5 | Précision@5 | Série@5 | Tome suivant@5 | Fuite même œuvre | Jeunesse ↔ adulte | Couverture |
|---|---|---|---|---|---|---|---|
| **L1** TF-IDF lexical | 0,316 | 22 % | 100 % | 100 % | **100 %** | 12 % | 100 % |
| **R0** Règles seules | 0,216 | 12 % | 100 % | 100 % | 0 % | 11 % | **13 %** |

Rapport complet : `tools/recommendation-benchmark/out/rapport-none.md`.

**Mes remarques :**

1. **Les règles seules ne suffisent pas, et de loin.** R0 est parfait sur les séries
   (100 %) et ne fuit jamais, mais il ne trouve 5 voisins que pour **13 % des livres**.
   Hors série et hors auteur, les métadonnées ne rapprochent presque rien. Exemples :
   - *Germinal* n'obtient **aucun** voisin, alors que 7 romans réalistes du XIXe sont au
     corpus ;
   - *1984* non plus, malgré 10 autres dystopies ;
   - *Sapiens* obtient *Homo deus* et *De l'inégalité parmi les sociétés*, puis rien.

   Les règles sont une excellente **couche de bonus**, pas une méthode à part entière.
2. **Le lexical gratuit fait mieux que les règles**, mais il a deux défauts rédhibitoires
   pour un affichage public :
   - il renvoie l'autre édition du même livre **dans 100 % des cas** ;
   - il se trompe complètement dès que les mots diffèrent. *Percy Jackson* reçoit
     *Le Père Goriot* ; *Le Trône de fer* reçoit *1984* ; *Le Meilleur des mondes*
     reçoit *Madame Bovary*.

   C'est précisément la faiblesse que les embeddings sont censés corriger. C'est aussi
   **la barre à battre** : un embedding qui ne dépasse pas 0,316 ne vaut pas son appel
   d'API.
3. **Les deux méthodes s'effondrent sur les mêmes familles** : feel-good, romance,
   quête initiatique, dystopie, BD de gag. Ce sont des familles définies par le ton ou le
   thème, pas par des mots ni des métadonnées. C'est là que les embeddings devront faire
   la différence ; je lirai la section 4 du rapport en priorité.
4. **Ce que les notices permettent de déduire** (section 2 du rapport) :
   - public jeunesse ou adulte juste à **88 %** ;
   - forme juste à **92 %** ;
   - série reconnue **43 fois sur 49** ;
   - éditions d'une même œuvre reconnues **7 sur 7**, sans aucune fausse fusion.

   Les filtres et bonus reposent donc sur des bases solides. Cela a demandé deux
   correctifs pendant la construction : l'identifiant d'œuvre BnF est parfois celui de
   la **série** (tomes 1 et 2 confondus), et la « clé titre + premier auteur » tombait
   sur le **traducteur**. Ce sont deux pièges que l'implémentation réelle devra éviter.

### 6.2 Avec embeddings (exécuté le 24 septembre 2026)

Rapport complet, avec les exemples côte à côte et les pires cas :
`tools/recommendation-benchmark/out/rapport-azure.md`.

| Méthode | nDCG@5 | Précision@5 | Tome suivant@5 | Fuite même œuvre | Jeunesse ↔ adulte | Autre forme |
|---|---|---|---|---|---|---|
| **H7** H6 sans la collection dans le texte ᵉ | **0,592** | 46 % | 100 % | 0 % | **3 %** | 12 % |
| **H5** E4 + même œuvre exclue + bonus, public ignoré ᵉ | 0,587 | 45 % | 100 % | 0 % | 5 % | 13 % |
| **H6** E4 + même œuvre exclue + bonus + **pénalité** de public ᵉ | 0,586 | 46 % | 100 % | 0 % | **3 %** | 13 % |
| **H6-512** H6 en 512 dimensions ᵉ | 0,579 | 45 % | 100 % | 0 % | 4 % | 12 % |
| **H4** E4 + filtres stricts + bonus (candidat initial) | 0,563 | 44 % | 100 % | 0 % | 4 % | 13 % |
| **H4-512** | 0,560 | 43 % | 100 % | 0 % | 5 % | 13 % |
| **E4** Embedding texte composé | 0,555 | 44 % | 100 % | **100 %** | 5 % | 15 % |
| **F4** E4 + filtres stricts | 0,553 | 43 % | 100 % | 0 % | 5 % | 17 % |
| **H3** Résumé d'œuvre + filtres + bonus | 0,541 | 41 % | 100 % | 0 % | 4 % | 18 % |
| **E3** Embedding résumé de l'œuvre | 0,514 | 41 % | 82 % | 100 % | 5 % | 23 % |
| **H4-256** | 0,509 | 38 % | 100 % | 0 % | 4 % | 14 % |
| **E1** Embedding titre + auteur | 0,461 | 36 % | 100 % | 100 % | 9 % | 16 % |
| **E2** Embedding résumé de l'édition | 0,459 | 36 % | 100 % | 100 % | 7 % | 22 % |
| **L1** TF-IDF lexical | 0,316 | 22 % | 100 % | 100 % | 12 % | 29 % |
| **R0** Règles seules | 0,216 | 12 % | 100 % | 0 % | 11 % | 7 % |

ᵉ **Variantes exploratoires**, ajoutées **après** lecture des premiers résultats pour
vérifier une hypothèse (§6.3). Elles ne faisaient pas partie des critères fixés à
l'avance, et leur avantage doit être confirmé sur le vrai catalogue.

**Écarts testés statistiquement** (bootstrap apparié sur les 165 requêtes, 10 000
tirages, intervalle à 95 %) :

| Comparaison | Écart de nDCG@5 | Intervalle à 95 % | Conclusion |
|---|---|---|---|
| E4 (texte composé) − L1 (lexical) | +0,239 | [+0,193 ; +0,283] | **Significatif** |
| E3 (résumé d'œuvre) − E2 (résumé d'édition) | +0,055 | [+0,010 ; +0,100] | **Significatif** |
| E4 (texte composé) − E3 (résumé seul) | +0,041 | [+0,005 ; +0,077] | **Significatif** |
| H4 (filtres + bonus) − E4 | +0,008 | [−0,019 ; +0,036] | Non significatif sur la note, mais fuite de 100 % à 0 % |
| H6 (pénalité de public) − H4 (exclusion) | +0,022 | [+0,010 ; +0,038] | **Significatif** |
| H6 − H5 (public ignoré) | −0,002 | [−0,020 ; +0,016] | Non significatif, mais H6 divise par deux les voisins jeunesse ↔ adulte |
| H7 (sans collection) − H6 | +0,006 | [−0,004 ; +0,016] | Non significatif |
| H6-512 − H6 | −0,006 | [−0,020 ; +0,007] | Non significatif : **512 dimensions suffisent** |
| H4-256 − H4 | −0,055 | [−0,077 ; −0,033] | **Significatif : 256 dimensions dégradent** |

### 6.3 Verdict sur les critères fixés à l'avance

| Critère (§5) | Résultat | Verdict |
|---|---|---|
| Embeddings > R0 **et** L1 de 0,10 au moins | +0,25 sur L1, +0,35 sur R0 | ✅ **Largement** |
| Partager le résumé entre éditions : E3 ≥ E2 | 0,514 contre 0,459, significatif | ✅ |
| Texte composé : E4 > E3 et H4 ≥ H3 | 0,555 > 0,514 et 0,563 ≥ 0,541 | ✅ |
| Filtres et bonus : H4 > E4, fuite ≈ 0, moins de jeunesse ↔ adulte | Fuite 100 % → 0 % ✅ ; jeunesse ↔ adulte 5 % → 4 % ✅ ; note +0,008, non significatif ⚠️ | ✅ **pour la fuite**, ⚠️ pour le filtre de public (voir ci-dessous) |
| 512 dimensions : perte < 0,01 | −0,003 (H4), −0,006 (H6), non significatif | ✅ |
| Publiable : précision@5 ≥ 50 % et tome suivant ≥ 90 % | Tome suivant 100 % ✅ ; précision@5 **46 %** ❌ | ❌ **Ne pas afficher systématiquement 5 voisins** |

**Le filtre strict de public est une fausse bonne idée.** Il est appliqué sur un public
*déduit* des notices, qui se trompe pour 18 éditions sur 165 (11 %). Chaque erreur exclut
alors tous les bons voisins :

- *Percy Jackson* (collection « Wiz », Albin Michel) est déduit adulte : tous les
  *Harry Potter* disparaissent ;
- *Dragon Ball* est déduit adulte et *One Piece* jeunesse : ils ne se voient plus.

D'où les variantes exploratoires. **Une pénalité (H6) plutôt qu'une exclusion** gagne
+0,022, de façon significative, et divise par deux les voisins de public opposé par
rapport à l'absence de filtre (H5). *Germinal* montre bien l'écart : H4 lui propose
*Matilda* et *Belle et Sébastien*, H6 *L'Assommoir*, *Une vie*, *Bel-Ami* et *Les
Misérables*.

### 6.4 Ce qu'il faut retenir de la lecture des exemples

1. **Les embeddings font ce qu'aucune règle ne fait.** Pour *Les hommes qui n'aimaient
   pas les femmes*, H6 propose le tome 2, puis *Miséricorde*, *Le Bonhomme de neige*,
   *La Femme en vert* et *Le Silence des agneaux*. Pour *Dune* : *Hypérion*, *La Horde
   du contrevent*, *Ravage*. Pour *Je sais cuisiner* : les trois autres livres de
   cuisine. Pour *L'Étranger* : *La Peste*, *La Nausée*, *Le Procès*.
2. **Les séries sont parfaites** (tome suivant dans le top 5 : 100 %), mais grâce au
   **bonus**, pas à l'embedding seul : E3 n'en trouve que 82 %.
3. **Le résumé compte.** Avec un résumé d'œuvre, H6 obtient 0,601 ; sans, 0,458. Les
   livres sans résumé sont les pires cas : *Les Rivières pourpres* reçoit *Les Croix de
   bois*, *Le Grand Troupeau* reçoit des feel-good.
4. **La note sous-estime un peu la qualité réelle.** La grille est stricte : un voisin
   hors de la famille annotée vaut 0. Or *Da Vinci Code* pour *Le Nom de la rose*, ou
   *Le Nom de la rose* pour *Da Vinci Code*, n'ont rien d'absurde pour un lecteur. Le
   « 48 % hors sujet » du top 5 est donc un **maximum**, pas une mesure exacte.
5. **Les points faibles restent visibles.**
   - **BD de gag** (0,19) : un gag n'a pas de résumé, et le texte composé ne dit presque
     rien de *Kid Paddle* ou de *Cédric*.
   - **Livres jeunesse entre eux** : *Nos étoiles contraires* (romance ado) reçoit
     *Journal d'un dégonflé*. *Les Malheurs de Sophie* reçoit *Les Misérables*.
   - **Titres trompeurs** : *Le Deuxième Sexe* reçoit *Ta deuxième vie commence…* ; le
     mot du titre pèse.
6. **Petit corpus, grands effets.** Avec environ cinq livres par famille, le plafond de
   précision@5 atteignable est de 88 %, et H6 en atteint un peu plus de la moitié. Dans
   un catalogue de 20 000 livres, chaque livre aura bien plus de vrais voisins proches :
   les scores absolus monteront. **Les seuils de score calculés ici ne sont pas
   transposables tels quels.**

### 6.5 Afficher moins, mais mieux

Précision des voisins de H6 selon un seuil minimal de score (score = cosinus + bonus −
pénalité) :

| Seuil | Voisins affichés | Dont bons (≥ 2) | Dont hors sujet | Livres gardant au moins 3 voisins |
|---|---|---|---|---|
| aucun | 825 | 46 % | 48 % | 100 % |
| 0,50 | 551 | 53 % | 41 % | 66 % |
| 0,55 | 259 | 67 % | 27 % | 22 % |
| 0,60 | 118 | 78 % | 10 % | 3 % |

Un seuil améliore nettement la qualité affichée, au prix du nombre de voisins. Sur ce
petit corpus, le compromis est défavorable. Sur le vrai catalogue, bien plus dense, il le
sera beaucoup moins. **Le seuil doit être calibré sur le catalogue réel**, pas ici.

## 7. Conclusion et recommandation pour la spécification

**Le benchmark valide l'approche par embeddings**, avec une marge qui ne laisse pas de
doute (+0,24 de nDCG@5 sur la meilleure méthode gratuite). Voici la méthode que je
recommande de spécifier :

| Élément | Recommandation | Fondement |
|---|---|---|
| Modèle | `text-embedding-3-small`, déjà déployé sur le compte Foundry existant | §6.2 |
| Texte vectorisé | **Texte composé** : titre, auteurs, série et tome, genres, sujets, public, **résumé de l'œuvre**. Collection facultative (effet non significatif) | E4 > E3 ; H7 ≈ H6 |
| Résumé | Le plus long parmi toutes les éditions de l'œuvre (BnF 330, sinon Open Library), **nettoyé** | E3 > E2 ; couverture 40 % → 89 % |
| Dimension | **512** (41 Mo pour 20 000 livres) ; pas 256 | Perte non significative à 512, significative à 256 |
| Exclusion stricte | **Autres éditions de la même œuvre** (identifiant BnF 500, identifiant Open Library, sinon titre + recoupement d'auteurs, avec garde-fou sur les tomes) | Fuite 100 % → 0 % |
| Public | **Pénalité** de 0,10, pas d'exclusion. Améliorer la déduction : liste de collections jeunesse (*Wiz*, *Fj poche*…), et public décidé par la majorité des éditions de l'œuvre | H6 > H4, significatif |
| Bonus | Même série +0,20, tome suivant +0,10, même auteur +0,08, même forme +0,03 | Tome suivant 100 % |
| Affichage | **Jusqu'à 5 voisins au-dessus d'un seuil**, pas 5 d'office. Seuil calibré sur le vrai catalogue par une relecture de 50 fiches | Précision@5 46 % < 50 % |
| Coût | ≈ **0,08 $** pour 20 000 livres, puis quelques millièmes par mois | §7 du rapport |

**Deux conditions de réussite, à inscrire dans la spécification :**

1. **Lire les bonnes zones BnF** : résumé 330 partagé par œuvre, série 461, auteurs en
   702, identifiant d'œuvre 500, avec le garde-fou sur les tomes. Sans cela, on retombe
   à E1 (0,46) ou pire.
2. **Traiter le public comme une information incertaine** : pénaliser, ne jamais exclure
   sur une déduction.

**Ce que le benchmark ne dit pas** : la qualité sur le vrai catalogue d'une bourse
(livres moins connus, moins de résumés), et l'effet des signaux de comportement, qui
n'existent pas encore. La première se mesurera avec le seuil d'affichage ; la seconde
viendra avec le trafic.

## 8. Second benchmark : ISBNdb améliore-t-il encore ?

> **Statut : prêt, en attente d'une clé ISBNdb active.** La clé fournie le 24 septembre
> est refusée par l'API (`401 Api key is not active`).

**Question.** Une source payante, [ISBNdb](https://isbndb.com/), apporte-t-elle des
résumés ou des sujets qui améliorent les voisins, au-delà de la BnF et d'Open Library ?

**Méthodes ajoutées**, toutes construites sur H6 pour isoler l'effet de la source :

| Code | Texte vectorisé | Question |
|---|---|---|
| **I1** | Résumé BnF, puis Open Library, puis **ISBNdb en dernier recours**, plus les sujets ISBNdb | ISBNdb comble-t-il les trous ? |
| **I2** | Résumé **ISBNdb en priorité**, plus les sujets ISBNdb | Qui résume le mieux ? |
| **I3** | **ISBNdb seul** (titre, auteurs, sujets, résumé ISBNdb) | ISBNdb peut-il remplacer les sources gratuites ? |
| **I1-512** | I1 en 512 dimensions | — |

**Critère fixé à l'avance.** ISBNdb vaut son abonnement si I1 ou I2 dépasse H6 d'un
écart **significatif** (bootstrap apparié, comme en §6.2), et si les éditions sans résumé
en gagnent un en nombre.

**Deux points de licence à peser, quel que soit le résultat :**

- ISBNdb est **payant** : de 14,99 $ à 299,99 $ par mois selon le plan.
- Ses conditions imposent de **supprimer les données si l'abonnement s'arrête**. En
  production, cela crée une dépendance permanente. La question se pose aussi pour les
  vecteurs dérivés des résumés ISBNdb. Pour le benchmark, les notices restent dans
  `cache/` (ignoré par git) : seules des statistiques agrégées sont écrites.

## 9. Découvertes annexes, utiles à l'implémentation

| Constat | Conséquence |
|---|---|
| Les notices récentes placent l'auteur en zone **702** (souvent code `070`, parfois sans code) | Les lire, sinon une notice sur cinq perd son auteur. Au passage, `BnfSruClient.ReadAuthors` lit aussi les 702 **traducteurs** |
| Un titre de série cherché seul renvoie des produits dérivés (*Moi, Fadi*, spin-off de *L'Arabe du futur*) ou d'autres auteurs | Tout rapprochement « même série » doit vérifier aussi l'auteur |
| Les adaptations en BD ne portent pas toujours la forme « bande dessinée » (608) | Le filtre de forme ne peut pas reposer sur la seule zone 608 ; l'éditeur et la collection aident |
