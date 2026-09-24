# 05 — Benchmark des méthodes de livres proches

> **24 septembre 2026.** Demande : « Prépare un vrai benchmark complet avec plusieurs
> possibilités de calcul de livres proches […] avec le modèle Azure OpenAI
> (text-embedding-3-small) […] et un vrai rapport que je puisse lire avec tes remarques et
> conseils. »
>
> **Statut : benchmark prêt et exécuté pour les méthodes sans IA. Les méthodes à
> embeddings attendent l'accès Azure OpenAI** (tutoriel :
> [`06-tutoriel-foundry-embeddings.md`](06-tutoriel-foundry-embeddings.md)). La section 6
> sera complétée après l'exécution.

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

### 6.2 Avec embeddings

**En attente de l'exécution avec Azure OpenAI.** Coût estimé : environ 58 000 tokens, soit
**0,0012 $** pour tout le benchmark. En production, **0,07 $** pour vectoriser 20 000
livres avec le texte composé.

Après exécution, cette section donnera :

- le tableau complet des onze méthodes ;
- le verdict sur chaque critère de la section 5 ;
- les familles où les embeddings gagnent et celles où ils échouent ;
- l'analyse des dix pires cas de H4 ;
- ma recommandation pour la spécification : texte vectorisé, dimension, poids des
  bonus, seuil d'affichage.

## 7. Découvertes annexes, utiles à l'implémentation

| Constat | Conséquence |
|---|---|
| Les notices récentes placent l'auteur en zone **702** (souvent code `070`, parfois sans code) | Les lire, sinon une notice sur cinq perd son auteur. Au passage, `BnfSruClient.ReadAuthors` lit aussi les 702 **traducteurs** |
| Un titre de série cherché seul renvoie des produits dérivés (*Moi, Fadi*, spin-off de *L'Arabe du futur*) ou d'autres auteurs | Tout rapprochement « même série » doit vérifier aussi l'auteur |
| Les adaptations en BD ne portent pas toujours la forme « bande dessinée » (608) | Le filtre de forme ne peut pas reposer sur la seule zone 608 ; l'éditeur et la collection aident |
