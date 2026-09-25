# 03 — Mesure : les résumés existent-ils dans les sources ?

> **24 septembre 2026.** Sonde jetable (script Python hors dépôt). Question posée :
> « Prends des livres connus et regarde s'ils ont des résumés sur la BnF et Google
> Books. » Open Library, troisième source déjà utilisée par l'application, a été
> mesurée en plus, car elle ne demande pas de clé.
>
> **Statut : BnF et Open Library mesurées. Google Books en attente d'une clé d'API**
> (l'accès anonyme renvoie `429`, quota à 0).

## 1. Protocole

- **46 titres connus** en 7 catégories : classiques, romans grand public, policier,
  jeunesse, BD et manga, SF et fantasy, essais et pratique.
- Les éditions sont prises **dans le catalogue de la BnF** (dépôt légal), pas chez
  Google, pour ne pas favoriser une source dans l'échantillon.
- Pour chaque titre, trois éditions **étalées dans le temps** : la plus ancienne, la
  médiane et la plus récente. Au total, **138 ISBN**.
- Chaque ISBN est ensuite interrogé **comme le fait l'application** :
  - BnF : `bib.isbn all "<ISBN-13>"`, premier enregistrement, zone UNIMARC **330**
    (résumé) ;
  - Open Library : description de l'édition, sinon de l'œuvre ;
  - Google Books : `volumeInfo.description` avec contrôle de l'ISBN exact (à faire).

**Biais assumé** : ce sont des livres **connus**. Des titres obscurs auront une
couverture plus faible. Inversement, l'échantillon sur-représente les éditions très
anciennes et très récentes, là où une bourse reçoit surtout des éditions des années 1990
à 2020.

## 2. Résultats par édition

| | Éditions | Résumé BnF | Résumé Open Library | BnF **ou** Open Library |
|---|---|---|---|---|
| **Total** | 138 | **48 (34 %)** | 30 (21 %) | **71 (51 %)** |
| Éditions 2017 et après | 55 | 32 (58 %) | | |
| Éditions 2007-2016 | 27 | 8 (29 %) | | |
| Éditions avant 2007 | 55 | 8 (14 %) | | |

Par catégorie, résumé BnF : jeunesse 54 %, SF-fantasy 50 %, policier 44 %, BD 33 %,
essais 33 %, classiques 30 %, **romans grand public 16 %**.

Environ 12 des 30 résumés Open Library semblent en français ; les autres sont en
anglais, décrivant l'œuvre.

## 3. Résultat par œuvre : c'est celui qui compte

Pour trouver des livres proches, **le résumé de l'œuvre suffit** : que ce soit celui
de l'édition Folio ou de l'édition Pocket importe peu. Si une seule édition d'un titre a
un résumé, toutes peuvent l'utiliser.

| Au moins une des 3 éditions a un résumé | Titres |
|---|---|
| BnF | 31 / 46 (67 %) |
| Open Library | 22 / 46 (48 %) |
| **BnF ou Open Library** | **37 / 46 (80 %)** |

Titres sans aucun résumé trouvé : *Madame Bovary*, *L'Écume des jours*, *Et après*,
*L'Alchimiste*, *Les Rivières pourpres*, *Le Club des cinq*, *Astérix le Gaulois*,
*Blacksad*, *Largo Winch*. Avec seulement trois éditions interrogées par titre, 80 %
est un **plancher** pour des livres connus.

**Condition** : il faut savoir regrouper les éditions d'une même œuvre. La colonne
`WorkId` existe (`RG-46`), mais dans le code actuel **seul Open Library la remplit**
(`BnfSruClient` et `GoogleBooksClient` passent `WorkId: null`). Or Open Library ne
trouve que **60 éditions sur 138** de l'échantillon. `WorkId` ne suffira donc pas : il
faudra une clé de regroupement de repli, par exemple titre et premier auteur
normalisés. Sa fiabilité est à évaluer au moment de la spécification.

## 4. Qualité des résumés

Médiane : **443 caractères** (environ 100 à 150 tokens), ce qui suffit pour un embedding.
Mais tout ce qui est en zone 330 n'est pas un résumé :

| Défaut observé | Exemple |
|---|---|
| Titre recopié | *Le Comte de Monte-Cristo* : « Le Comte de Monte-Cristo » (24 caractères) |
| Extrait du texte plutôt que résumé | *Germinal* 2025 : une réplique de dialogue |
| Texte d'**édition**, pas d'œuvre | « Une édition abrégée… Les points forts de cette édition », « À l'occasion des 90 ans de l'auteur, ce coffret rassemble… » |
| Très court mais juste | *Tintin au Tibet* : « Tintin part au Tibet pour sauver son ami Tchang. » |

Il faudra donc un **filtre minimal** : une longueur plancher, rejeter un texte égal au
titre, et préférer le résumé le plus long parmi les éditions d'une œuvre.

## 5. Découverte hors sujet : un défaut existant dans l'application

**`BnfSruClient` ne retrouve aucune édition antérieure à 2007.**

- Il interroge la BnF avec `bib.isbn all "<ISBN-13>"` uniquement.
- Les notices BnF des éditions d'avant 2007 portent un **ISBN-10**, que cette requête ne
  retrouve pas.
- Sur l'échantillon : **0 édition d'avant 2007 sur 55** trouvée par ISBN-13, **55 sur
  55** trouvées en ajoutant un repli ISBN-10.
- Toutes catégories confondues, l'application rate donc la BnF sur **40 % de
  l'échantillon**. Elle se replie alors sur Open Library et Google Books, qui ont moins
  souvent des métadonnées françaises.

Pour une bourse de livres d'occasion, où les éditions anciennes sont nombreuses, c'est
un vrai manque. **Il est indépendant des recommandations** et mérite un correctif à
part : repli ISBN-10 quand l'ISBN-13 commence par `978`.

## 6. Ce que la mesure change

1. **La prémisse « embedding sur le résumé » est viable**, à trois conditions : lire
   la zone 330 de la BnF (et la description Open Library), corriger le repli ISBN-10,
   et **partager le résumé entre les éditions d'une même œuvre**.
2. Pour environ **20 %** des œuvres connues, et sans doute davantage pour les autres, il
   n'y aura pas de résumé : l'embedding retombera sur titre, auteur, genre et collection.
   Il faut aussi les règles simples, et prévoir que ces livres aient des voisins de
   moindre qualité.
3. **Les BD et les romans grand public** sont les moins couverts. Pour les séries de BD,
   la règle « même série / même auteur » compte plus que l'embedding.
4. **Google Books** reste à mesurer : il pourrait combler une partie du trou, surtout
   pour les romans grand public.
