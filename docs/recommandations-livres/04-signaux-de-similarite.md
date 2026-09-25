# 04 — Le résumé suffit-il ? Les autres signaux de similarité

> **24 septembre 2026.** Question : « Pour trouver les livres les plus proches, est-ce
> que le résumé suffira ? Est-ce qu'il y a d'autres infos qui pourraient être
> pertinentes ? » Réponse fondée sur une seconde sonde jetable : les zones UNIMARC
> remplies dans les notices BnF des 138 éditions de [`03`](03-mesure-couverture-resumes.md),
> interrogées par `bib.fuzzyISBN`.

## 1. Réponse courte

**Non, le résumé seul ne suffit pas**, pour quatre raisons :

1. **Couverture** : au mieux 80 % des œuvres connues ont un résumé (`03` §3), moins pour
   les autres.
2. **Le résumé dit « de quoi ça parle », pas « pour qui » ni « sous quelle forme ».**
   L'échantillon contient des adaptations en **bande dessinée** de *1984*, *Les
   Misérables* et *Au revoir là-haut*. Leur résumé est presque celui du roman : un
   embedding les jugera quasi identiques, alors qu'un lecteur de BD pour adolescents et
   un lecteur du roman en poche ne cherchent pas la même chose.
3. **Il ignore l'ordre des séries** : le tome 2 de *Millénium* est la meilleure
   suggestion pour qui a acheté le tome 1, et aucun embedding ne le « sait ».
4. **Il fait remonter les autres éditions de la même œuvre** comme « plus proches
   voisins ». Or proposer *L'Étranger* en Folio à qui a acheté *L'Étranger* en Folio
   Plus n'est pas une recommandation.

## 2. Ce que les notices BnF contiennent réellement

| Zone UNIMARC | Contenu | Remplie | Utile pour |
|---|---|---|---|
| `700` | Auteur principal | **91 %** | Même auteur |
| `101` | Langue du texte, et langue originale des traductions | **100 %** | Filtre de langue |
| `686` | Cadre de classement de la Bibliographie nationale | **73 %** | Grand domaine (code) |
| `225` | Collection (*Folio junior*, *Folio*, *Pocket*…) | **61 %** | Public et genre, par la collection |
| `500` | Titre uniforme = **identifiant d'œuvre** BnF | **41 %** | Regrouper les éditions |
| `461` | **Série ou ensemble, avec numéro de tome** | **34 %** (71 % des BD) | Tome suivant |
| `330` | Résumé | 34 % | Proximité thématique |
| `608` | Forme ou genre (souvent posé par le CNLJ) | 25 % | Forme (BD, roman, poésie) |
| `676` | Dewey | 15 % | Domaine, pour les essais |
| `606` | Sujets RAMEAU | 7 % (surtout les essais) | Thèmes précis |
| `333` | Public (« À partir de 11 ans ») | 6 % | Âge, rare |

Exemples réels : `461` donne « "Millénium" / 1 » et « Le comte de Monte-Cristo / 1 » ;
`500` donne le même identifiant d'œuvre `11958514` pour deux éditions de *L'Étranger* ;
`333` donne « À partir de 11 ans » pour *Harry Potter à l'école des sorciers* (1998) ;
`608` suivi de la mention CNLJ signale un livre évalué par le Centre national de la
littérature pour la jeunesse, donc destiné à la jeunesse.

**Aujourd'hui, la table `Books` ne conserve aucune de ces zones**, sauf l'auteur, la
langue et un genre unique. `BnfSruClient` lit `608`, `606` et `610` pour ce genre, mais
ni le résumé, ni la collection, ni la série, ni l'identifiant d'œuvre.

## 3. Rôle de chaque information : filtrer, favoriser, ou nourrir l'embedding

La bonne question n'est pas « quelles données mettre dans l'embedding », mais **quel
rôle donner à chacune**. Mettre toutes les informations dans un seul texte vectorisé
laisse le résumé écraser le reste ; certaines décisions doivent être des règles
explicites.

### Règles dures (filtres)

| Règle | Données | Pourquoi |
|---|---|---|
| **Jamais une autre édition de la même œuvre** | `500` (identifiant d'œuvre BnF), `WorkId` Open Library, sinon titre et auteur normalisés | Sinon, les « meilleurs voisins » sont des doublons |
| **Même public** : jeunesse ou adulte | Collection (`225`), mention CNLJ (`608`), `333`, classement (`686`) | Ne pas proposer un roman adulte sous un album jeunesse, ni l'inverse |
| **Même langue de lecture** | `101` / `Books.Language` | Un livre en anglais sous un livre en français déçoit presque toujours |
| **Visible et disponible ou annoncé** | `Books` | Décidé en `D2` |

### Bonus explicites (règles de classement)

| Bonus | Données | Force |
|---|---|---|
| **Tome suivant de la série** | `461` (série + numéro) | Le plus fort : quasi certain de plaire à qui a lu le précédent |
| **Même auteur** | `700` | Fort, simple, sans IA |
| **Même forme** (BD, manga, roman, poésie) | `608`, collection | Moyen |
| **Même collection** | `225` | Faible, sauf pour les collections très typées (SF, policier, jeunesse) |

### Texte de l'embedding (proximité thématique)

Un texte composé, pas le résumé seul :

```
Titre. Auteur(s). Collection. Forme ou genre. Sujets. Résumé.
```

- Sans résumé, l'embedding garde encore titre, auteur, collection et genre : il dégrade
  doucement au lieu de tomber à zéro.
- L'auteur dans le texte rapproche les livres d'un même auteur, ce qui est voulu.
- Le résumé est **nettoyé** avant d'entrer (`03` §4 : titre recopié, extrait, texte
  d'édition).

### Ce qui n'aide pas

Éditeur (sauf quelques éditeurs jeunesse très typés), année, format, nombre de pages,
prix. Ils ajoutent du bruit.

## 4. Signaux qui viendront plus tard

Quand le site sera public, des **signaux de comportement** apparaîtront : livres ajoutés
ensemble à Ma sélection, présents dans la même liste de recherche, achetés dans le même
passage associé. Ce sont eux qui captent ce qu'aucune métadonnée ne dit (« les gens qui
aiment ceci aiment aussi cela »). La table `BookNeighbors` (`D2`) pourra les accueillir
comme une deuxième source de score. Rien à construire maintenant : le volume est nul.

## 5. Conséquences pour la spécification

1. **Nouvelles colonnes** à lire depuis la BnF, en plus du résumé : identifiant d'œuvre
   (`500`), série et tome (`461`), collection (`225`), public (`333`, mention CNLJ),
   classement (`686`), formes et sujets (`608`, `606`).
2. **Le regroupement par œuvre** a enfin une clé solide pour 41 % des éditions (`500`),
   complétée par `WorkId` Open Library et par titre et auteur normalisés.
3. **Score final** = similarité d'embedding, **après** filtres (même œuvre, public,
   langue, disponibilité), **plus** bonus (tome suivant, même auteur, même forme).
4. **À valider avant de coder** : les pondérations ne se décident pas sur le papier.
   Une sonde de qualité (embedding des 138 éditions, avec et sans champs enrichis, et
   lecture des 5 plus proches voisins de chacune) dira si l'enrichissement change
   réellement les résultats.
