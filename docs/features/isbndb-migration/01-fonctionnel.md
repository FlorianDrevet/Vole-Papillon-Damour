# ISBNdb, source principale des fiches livres — spécification fonctionnelle

> Statut : **validée**, 2026-10-23. Étend le module
> [`docs/bourse-aux-livres/`](../../bourse-aux-livres/README.md). Les règles nouvelles
> prennent la suite de la numérotation existante (`RG-100` à `RG-129`).

## 1. Contexte

Une fiche livre est aujourd'hui remplie par une cascade de sources gratuites : la **BnF**,
puis **Open Library**, puis **Google Books**. Ces sources ont trois défauts constatés :

- des **trous** : beaucoup de fiches n'ont ni résumé, ni couverture, ni genre exploitable
  (`docs/recommandations-livres/03-mesure-couverture-resumes.md`) ;
- des **genres médiocres**, hérités des vedettes BnF ou des sujets Open Library, qui
  rendent les filtres du catalogue et les recommandations peu fiables ;
- une **qualité inégale** d'une source à l'autre pour la même édition.

**ISBNdb** (isbndb.com, base commerciale de 122 millions de livres) renvoie en une seule
requête une notice complète : titre long, auteurs, éditeur, date, reliure, pages,
dimensions, résumé en français, sujets, couverture et autres ISBN de la même édition.
L'abonnement retenu est le moins cher : **5 000 requêtes par jour** et **60 requêtes par
minute** (1 par seconde en moyenne). Une montée à 3 par seconde est envisagée.

Chaque requête coûte. La migration doit donc faire d'ISBNdb la source principale **sans
jamais gaspiller de crédit**, et rester utilisable quand le quota est épuisé.

## 2. Objectifs

| # | Objectif |
|---|---|
| O1 | Une fiche est remplie **en priorité par ISBNdb**, et la fiche conserve **toutes** les informations qu'ISBNdb renvoie. |
| O2 | Quand ISBNdb ne peut pas répondre (quota épuisé, service indisponible), la fiche est remplie **provisoirement** par les sources actuelles, puis **remplacée** par ISBNdb lors d'un rattrapage nocturne. |
| O3 | Un livre **absent d'ISBNdb** est rempli par les sources actuelles, signalé comme tel et proposé à l'administration pour être **complété à la main**. |
| O4 | Les recommandations (embedding et voisins) ne sont calculées que sur une fiche **définitive** : issue d'ISBNdb, confirmée absente d'ISBNdb, ou complétée à la main. |
| O5 | Sur le site public, un visiteur qui ne trouve pas son édition peut, **à sa demande**, consulter des propositions supplémentaires venant d'ISBNdb. |
| O6 | L'administration voit le **quota ISBNdb** restant et sa consommation. |
| O7 | Au tri, l'application de scan signale qu'une **autre édition de la même œuvre** est déjà à la bourse aux livres. |
| O8 | Les **genres** des fiches deviennent fiables et alignés sur la liste de genres du catalogue. |
| O9 | Les livres déjà enregistrés sont **conservés** et basculés vers ISBNdb progressivement, sans ressaisie. |

## 3. Hors périmètre

- **Supprimer** la BnF, Open Library ou Google Books : elles restent le **secours** et la
  source de l'identifiant d'œuvre (`RG-115`).
- Utiliser ISBNdb pour la **recherche principale** du site public : elle reste sur les
  sources actuelles (`RG-116`).
- Les **prix** renvoyés par ISBNdb (prix éditeur, prix catalogue) : ils sont stockés mais
  **jamais affichés** (`RG-63`).
- Les endpoints ISBNdb **auteur, éditeur, sujet** et les **prix marchands**
  (`with_prices`) : leur coût en crédits n'est pas justifié en v1.
- L'**estimation de valeur** des livres rares (`RG-14`) : non traitée ici.
- Une interface pour **éditer la table de correspondance** sujets ISBNdb → genres : la
  table est livrée avec l'application et ajustée par mise à jour (voir `Q-05`).
- La **fusion** automatique de fiches de la même œuvre : l'application signale, le
  bénévole décide (`RG-16`).

## 4. Vocabulaire et acteurs

| Terme | Définition |
|---|---|
| **Fiche ISBNdb** | Fiche dont le contenu bibliographique provient d'ISBNdb. Marqueur « issue d'ISBNdb » = vrai. |
| **Fiche de secours** | Fiche remplie par la BnF, Open Library ou Google Books parce qu'ISBNdb n'a pas pu répondre. Marqueur « issue d'ISBNdb » = faux, « absente d'ISBNdb » = faux. Elle est **provisoire**. |
| **Fiche hors ISBNdb** | Fiche d'un ISBN qu'ISBNdb ne connaît pas (réponse « livre introuvable »). Marqueur « absente d'ISBNdb » = vrai. Remplie par le secours, elle est **définitive** jusqu'à ce qu'ISBNdb connaisse le livre. |
| **Fiche complétée** | Fiche hors ISBNdb qu'un administrateur a relue, complétée et marquée « complétée ». |
| **Rattrapage nocturne** | Traitement quotidien, vers 3 h 30 (heure de Paris), qui remplace les fiches de secours par ISBNdb. |
| **Crédit** | Une unité du quota journalier ISBNdb. Le quota se renouvelle à **minuit UTC** (1 h ou 2 h à Paris). |
| **Œuvre** | Ensemble des éditions d'un même texte (`RG-46`). Identifiée par l'identifiant d'œuvre BnF / Open Library ou, à défaut, par la clé d'œuvre (`RG-119`). |

| Acteur | Ce qu'il fait |
|---|---|
| **Bénévole de tri** (rôle `Tri`) | Scanne ; voit la fiche et, nouveau, le signal « autre édition de la même œuvre ». |
| **Administrateur** (rôle `Administration`) | Consulte le quota ; complète les fiches hors ISBNdb et les marque complétées. |
| **Visiteur du site public** | Demande, s'il le souhaite, les propositions supplémentaires ISBNdb à la fin de sa recherche. |
| **Système** | Remplit les fiches, applique le secours, lance le rattrapage nocturne et les recommandations. |

## 5. États d'une fiche vis-à-vis d'ISBNdb

Deux marqueurs **indépendants** sont portés par chaque fiche, plus un marqueur de
complétion :

| Situation | « issue d'ISBNdb » | « absente d'ISBNdb » | « complétée » | Recommandation |
|---|---|---|---|---|
| ISBNdb a répondu | vrai | faux | — | oui |
| Quota épuisé ou ISBNdb indisponible → secours | faux | faux | — | **non, attend le rattrapage** |
| ISBNdb ne connaît pas l'ISBN → secours | faux | vrai | faux | oui |
| Hors ISBNdb, relue par un administrateur | faux | vrai | vrai | oui, **recalculée** |
| Fiche existante avant la migration | faux | faux | — | conservée, recalculée après rattrapage |

```
           scan / import
                │
                ▼
        ┌──────────────┐  trouvé   ┌──────────────┐
        │ appel ISBNdb ├──────────►│ Fiche ISBNdb │──► recommandation
        └──┬────────┬──┘           └──────────────┘
 introuvable│        │quota épuisé / panne
           ▼        ▼
 ┌──────────────┐  ┌──────────────────┐  rattrapage nocturne
 │ Hors ISBNdb  │  │ Fiche de secours ├────────────────────► Fiche ISBNdb ──► recommandation
 │ (secours)    │  └──────────────────┘
 └──┬───────────┘
    │ recommandation
    ▼
 page admin « Hors ISBNdb » ──► complétée ──► nouvel embedding + recommandation
```

## 6. Parcours

### P1 — Un bénévole scanne un livre inconnu (quota disponible)

1. Le bénévole scanne l'ISBN. La fiche est créée (`RG-03`).
2. Le système interroge ISBNdb **une seule fois** pour cet ISBN, quel que soit le nombre
   d'écrans ou de traitements qui en ont besoin (`RG-104`).
3. ISBNdb répond : la fiche est remplie, marquée « issue d'ISBNdb ». Le bénévole voit le
   titre, l'auteur et la couverture comme aujourd'hui.
4. Si une autre édition de la même œuvre est déjà à la bourse, l'écran de résultat le
   signale (`RG-120`).
5. À la clôture de la session, la recommandation du livre est lancée (`RG-112`).

### P2 — Le quota est épuisé pendant une bourse

1. Le système sait que le quota est épuisé (`RG-106`) : il **n'appelle plus** ISBNdb
   jusqu'au renouvellement.
2. La fiche est remplie par la BnF, Open Library puis Google Books, comme aujourd'hui.
   Elle est marquée « issue d'ISBNdb » = faux, « absente d'ISBNdb » = faux.
3. Le tri continue sans aucune différence visible pour le bénévole.
4. À la clôture de la session, la recommandation de ce livre est **reportée**.
5. La nuit suivante, le rattrapage remplit la fiche depuis ISBNdb, **écrase** les
   données de secours (sauf corrections manuelles, `RG-05`) et lance la recommandation.

### P3 — Le livre n'existe pas dans ISBNdb

1. ISBNdb répond « livre introuvable ». Cette réponse ne coûte pas de crédit.
2. La fiche est remplie par le secours et marquée « absente d'ISBNdb ».
3. La recommandation est lancée normalement à la clôture de la session.
4. La fiche apparaît dans la page d'administration « Livres hors ISBNdb » (E2).

### P4 — Un administrateur complète une fiche hors ISBNdb

1. Il ouvre « Livres hors ISBNdb », filtre sur « À compléter ».
2. Il voit, pour chaque fiche, les informations manquantes (résumé, couverture, genre…).
3. Il ouvre la fiche (E3), complète les champs (ces champs deviennent des corrections
   manuelles, `RG-05`) et clique « Marquer comme complétée ».
4. La fiche quitte la liste « À compléter ». Un **nouvel embedding** et un nouveau calcul
   des voisins sont lancés (`RG-113`).

### P5 — Le rattrapage nocturne

1. Vers 3 h 30 (heure de Paris), après le renouvellement du quota, le rattrapage
   démarre.
2. Il traite, par lots, les fiches de secours et les fiches d'avant la migration,
   dans l'ordre de priorité de `RG-108`.
3. Il peut consommer **tout** le quota du jour. Il s'arrête **immédiatement** dès que
   le quota est épuisé (`RG-109`), sans relancer de requête.
4. Il produit un compte rendu visible dans l'administration (E1).

### P6 — Un visiteur ne trouve pas son édition

1. Le visiteur cherche « Les Misérables » sur le site public. Les résultats du catalogue
   et la section « Pas encore dans la bourse aux livres » (BnF / Open Library)
   s'affichent comme aujourd'hui.
2. Sous la **dernière page** de cette section, un encart propose : « Vous n'avez pas
   trouvé l'édition que vous cherchez ? » avec le bouton « Voir d'autres éditions ».
3. Au clic, jusqu'à 100 éditions en français proposées par ISBNdb s'affichent, sans les
   éditions déjà listées au-dessus (`RG-117`).
4. Le visiteur peut suivre une de ces éditions comme les autres références.
5. S'il relance la même recherche dans l'heure, le résultat est resservi sans nouvelle
   requête ISBNdb (`RG-116`).

## 7. Écrans

| # | Écran | Nature | Écran d'origine |
|---|---|---|---|
| **E1** | Administration — Tableau de bord : carte « ISBNdb » (crédits restants, consommation du jour, état, dernier rattrapage) | *modifié* | `/administration` section « Tableau de bord » |
| **E2** | Administration — « Livres hors ISBNdb » : liste des fiches absentes d'ISBNdb, filtres « À compléter / Complétées », indicateurs de champs manquants | *nouveau* | même grammaire que « ISBN en cours d'analyse » |
| **E3** | Administration — Fiche livre : provenance (ISBNdb / secours / hors ISBNdb / complétée), nouveaux champs ISBNdb, action « Marquer comme complétée », action « Revérifier dans ISBNdb » | *modifié* | fiche Catalog d'administration (édition des métadonnées) |
| **E4** | Site public — Recherche : encart « Voir d'autres éditions » sous la dernière page des références externes, et bloc de résultats ISBNdb (chargement, vide, erreur, quota) | *modifié* | `catalog-search-page`, section « Pas encore dans la bourse aux livres » |
| **E5** | Application de scan — Écran de résultat : signal « Autre édition déjà à la bourse » avec le nombre d'exemplaires et les éditions concernées | *modifié* | `03` §3.3 Écran de résultat |

La fiche publique (`04` §5) n'est pas modifiée en v1 ; voir `Q-06`.

**Pas de maquettes.** Chaque écran reprend la grammaire de l'écran existant le plus proche :
mêmes composants, mêmes variables CSS et même ton de libellés. E1 suit les tuiles du tableau
de bord, E2 la section « ISBN en cours d'analyse », E3 la fiche d'administration, E4 la
section « Pas encore dans la bourse aux livres » et E5 l'écran de résultat de la scanette.

## 8. Règles métier

### Données de la fiche

#### `RG-100` — La fiche conserve tout ce qu'ISBNdb renvoie
Chaque champ de la notice ISBNdb est enregistré sur la fiche : titre, titre long,
ISBN-13, ISBN-10, auteurs, éditeur, date de publication (telle que fournie, année ou date
complète), langue, reliure, édition et numéro d'édition, nombre de pages, dimensions
(texte et mesures structurées : hauteur, largeur, épaisseur, poids, avec leurs unités),
classification Dewey, résumé (*synopsis*), présentation (*overview*), extrait, sujets,
autres ISBN (avec leur reliure), éditions liées, prix éditeur et prix catalogue (montant,
devise). La notice brute reçue est aussi conservée telle quelle, pour qu'un champ ajouté
plus tard par ISBNdb ne soit pas perdu.

#### `RG-101` — Seule la couverture permanente est retenue
ISBNdb fournit deux images : une vignette à **adresse permanente** et une image
« originale » dont l'adresse **expire** après quelques heures. Seule l'adresse
permanente est enregistrée et affichée. L'adresse expirante n'est jamais stockée.

#### `RG-102` — Les champs historiques restent alimentés
Les champs déjà affichés par les applications (titre, auteurs, éditeur, année, format,
langue, résumé, genres, couverture) sont remplis depuis ISBNdb avec ces conversions :
auteurs affichés « Prénom Nom » (« Hugo, Victor » → « Victor Hugo ») ; année tirée de la
date ; format traduit en français (« Paperback » → « Broché », « Hardcover » → « Relié »,
« Mass Market Paperback » → « Poche », etc.) ; résumé = *synopsis*, à défaut
*overview*, nettoyé de toute balise.

#### `RG-103` — Les prix ne sont jamais affichés
Les prix ISBNdb sont stockés pour mémoire et ne sont montrés nulle part (`RG-63`).

### Consommation des crédits

#### `RG-104` — Un ISBN, une requête
Pour un même ISBN, une seule requête ISBNdb est faite par période de 24 heures, quel que
soit le nombre d'écrans, de bénévoles ou de traitements qui en ont besoin. Une fiche déjà
issue d'ISBNdb n'est **jamais** redemandée automatiquement.
Dès que plusieurs ISBN attendent une fiche en même temps (import CSV de l'administration,
file d'enrichissement après un scan, rattrapage), ils sont regroupés en **requêtes
groupées** de 100 ISBN au plus plutôt qu'envoyés un par un.

#### `RG-105` — Débit maîtrisé, nouvelle tentative en cas de refus
Les appels respectent le débit de l'abonnement (1 par seconde aujourd'hui, réglable
sans modifier l'application pour passer à 3). Un refus pour excès de débit est retenté
automatiquement, avec une attente croissante, **3 fois au plus** ; au-delà, l'appel est
traité comme une panne (`RG-106`).

#### `RG-106` — Quota épuisé : on arrête d'appeler
Dès qu'ISBNdb indique que le quota du jour est épuisé, le système le mémorise avec
l'heure de renouvellement et **n'envoie plus aucune requête** avant cette heure. Les
fiches passent par le secours (P2). Une panne d'ISBNdb (erreur serveur, délai dépassé)
mène aussi au secours, mais sans bloquer les appels suivants.

#### `RG-107` — Une requête abandonnée est annulée
Si l'appelant abandonne (bénévole qui change d'écran, visiteur qui quitte la page,
arrêt du traitement), la requête ISBNdb en cours est annulée et ne fait l'objet d'aucune
nouvelle tentative. Une requête attendue par plusieurs appelants n'est annulée que si
**tous** l'abandonnent.

### Rattrapage nocturne

#### `RG-108` — Ce que le rattrapage traite, et dans quel ordre
Le rattrapage traite les fiches « issue d'ISBNdb » = faux et « absente d'ISBNdb » = faux,
non écartées (`Discarded`), dans cet ordre :
1. fiches **en stock** (disponibles ou annoncées) ;
2. fiches suivies par un membre ou par une demande manuelle ;
3. toutes les autres, des plus récemment scannées aux plus anciennes.

Il interroge ISBNdb **par lots** : une requête groupée porte jusqu'à 100 ISBN (taille
réglable, 1 000 sur les abonnements supérieurs). Le lot réduit le **nombre de requêtes**
(débit) ; côté **quota**, ISBNdb décompte un crédit par livre trouvé, les absents sont
gratuits (mesuré, voir §10).

#### `RG-109` — Le rattrapage peut consommer tout le quota
Le rattrapage n'a **pas de réserve** : il peut utiliser tous les crédits disponibles. Il
s'arrête dès que le quota est épuisé (`RG-106`), et reprend la nuit suivante là où il
s'était arrêté. Les scans du jour qui suivent un quota épuisé passent par le secours
(P2) et sont rattrapés la nuit suivante.

#### `RG-110` — Le rattrapage écrase le secours, pas les corrections
Quand ISBNdb répond, toutes les données de secours sont remplacées par celles d'ISBNdb,
**sauf** les champs corrigés à la main par un administrateur (`RG-05`). La fiche devient
« issue d'ISBNdb ».

#### `RG-111` — Un livre absent n'est pas redemandé en boucle
Un ISBN introuvable lors du rattrapage est **confirmé** par une recherche individuelle
(gratuite s'il est vraiment absent), puis marqué « absente d'ISBNdb ». Les fiches hors
ISBNdb **non complétées** sont revérifiées une fois par mois (`Q-03`) ; si ISBNdb les
connaît enfin, elles deviennent des fiches ISBNdb (`RG-110`).

### Recommandations

#### `RG-112` — Recommandation sur fiche définitive seulement
À la clôture d'une session de scan, la recommandation (embedding puis voisins dans le
fonds de la bourse) est lancée pour chaque livre de la session **dont la fiche est
définitive** : issue d'ISBNdb ou absente d'ISBNdb. Une fiche de secours attend le
rattrapage, qui lance sa recommandation dès qu'il l'a remplie. Une fiche encore en cours
d'analyse lance sa recommandation dès qu'elle devient définitive.

#### `RG-113` — Recalcul quand la fiche change
Un nouvel embedding et de nouveaux voisins sont calculés quand :
- le rattrapage remplace une fiche de secours ou d'avant la migration ;
- un administrateur marque une fiche hors ISBNdb « complétée », ou modifie le résumé, le
  titre, les auteurs ou les genres d'une fiche complétée.

#### `RG-114` — La recommandation s'appuie sur la fiche enregistrée
Pour une fiche ISBNdb, le texte qui sert à l'embedding est composé à partir des données
ISBNdb **déjà enregistrées** (titre long, auteurs, sujets, genres, résumé), sans nouvelle
requête ISBNdb. Les informations gratuites propres à la BnF (public visé, collection,
série, identifiant d'œuvre) continuent de compléter ce texte quand elles existent.

### Identifiant d'œuvre

#### `RG-115` — L'œuvre reste identifiée par la BnF ou Open Library
ISBNdb ne fournit pas d'identifiant d'œuvre. L'identifiant d'œuvre d'une fiche (pages
œuvre du site, suivi d'une œuvre, demandes manuelles) continue d'être obtenu
**gratuitement** auprès de la BnF ou d'Open Library, même pour une fiche ISBNdb. Ce
complément ne modifie aucun autre champ de la fiche.

### Recherche sur le site public

#### `RG-116` — ISBNdb seulement à la demande, avec cache
La recherche publique reste servie par le catalogue et par les sources actuelles. ISBNdb
n'est interrogé **que** sur clic du bouton « Voir d'autres éditions ». La requête demande
**100 résultats** (le maximum), **en français uniquement**. Le résultat est conservé
**1 heure** pour la même recherche (texte normalisé : casse, accents et espaces
ignorés) : relancer la recherche dans ce délai ne consomme aucun crédit.

#### `RG-117` — Bouton sous la dernière page, résultats dédoublonnés
Le bouton n'apparaît que sous la **dernière page** de la section « Pas encore dans la
bourse aux livres », y compris quand cette section est vide. Les résultats ISBNdb déjà
présents dans le catalogue ou dans les références affichées au-dessus (même ISBN-13) sont
retirés. Chaque résultat se suit à l'édition, comme une référence externe.

#### `RG-118` — Plafond contre les abus
La recherche ISBNdb publique est limitée par visiteur *(valeur de départ : 10 recherches
par heure)* et au total *(valeur de départ : 300 crédits par jour)*. Au-delà, ou si le
quota est épuisé, l'encart l'indique sans erreur : « Les propositions supplémentaires
sont momentanément indisponibles. Réessayez plus tard. »

### Même œuvre au tri

#### `RG-119` — Clé d'œuvre
Deux éditions sont de la **même œuvre** si elles ont le même identifiant d'œuvre BnF ou
Open Library, ou à défaut la même **clé d'œuvre** : titre normalisé (casse, accents,
articles initiaux, ponctuation et sous-titre ignorés) **et** au moins un nom d'auteur en
commun **et** le même numéro de tome. Les ISBN qu'ISBNdb déclare comme « autres ISBN »
d'une fiche rattachent aussi les éditions entre elles. Un titre trop générique
(« Œuvres complètes », « Poèmes », « Théâtre », moins de 4 caractères…) ne suffit jamais à
lui seul.

#### `RG-120` — Signal « autre édition déjà à la bourse »
Au tri, si l'ISBN scanné n'a pas d'exemplaire disponible ni annoncé mais qu'une autre
édition de la même œuvre en a, l'écran de résultat affiche le signal **« Autre édition
déjà à la bourse »** avec le nombre d'exemplaires et, pour chaque édition, l'éditeur et
l'année. Le signal s'affiche aussi en complément quand l'ISBN scanné a déjà des
exemplaires.

#### `RG-121` — Le seuil de doublon compte l'œuvre
Le verdict « inutile d'en garder » (`RG-10`) compte les exemplaires disponibles et
annoncés de **toutes les éditions de la même œuvre**. La priorité des verdicts (`RG-15`)
est inchangée : « Recherché » et « ça se vend » l'emportent. Voir `Q-01`.

#### `RG-122` — Hors ligne, le signal reste disponible
L'application de scan connaît la clé d'œuvre des livres déjà enregistrés et l'applique
hors ligne. Pour un ISBN jamais vu, le signal n'est calculé qu'une fois la fiche obtenue ;
sans réseau, seul le verdict par ISBN s'affiche.

### Genres

#### `RG-123` — Genres alignés sur la liste du catalogue
Les genres d'une fiche sont choisis **dans la liste des genres du catalogue** (Roman,
Jeunesse, Bande dessinée, Manga, Policier, Thriller, Science-fiction, Fantasy, Romance,
Histoire, Biographie, Documentaire, Essai, Théâtre, Poésie, Cuisine), par une table de
correspondance depuis les sujets ISBNdb en français et en anglais (« Romans » → Roman,
« Science Fiction & Fantasy » → Science-fiction et Fantasy, « Bandes dessinées » → Bande
dessinée…), complétée par des indices d'éditeur (Dargaud → Bande dessinée, Kana →
Manga…). Les sujets sans correspondance restent enregistrés comme **sujets** mais ne
deviennent pas des genres. Les sujets parasites (« Self Service », « Arborist
Merchandising Root », nom de l'auteur pris pour un sujet) sont ignorés.

#### `RG-124` — Une fiche sans genre trouvé reste sans genre
Si aucun sujet ne correspond, la fiche n'a pas de genre plutôt qu'un genre faux. Une
fiche hors ISBNdb garde les genres de secours jusqu'à sa complétion manuelle.

### Administration

#### `RG-125` — Quota visible
La carte « ISBNdb » du tableau de bord affiche : crédits restants sur le quota du jour,
heure du prochain renouvellement (heure de Paris), consommation du jour répartie par
usage (scan, rattrapage, recherche publique, administration), état (« Opérationnel »,
« Quota épuisé jusqu'à … », « Service indisponible »), nombre de fiches en attente de
rattrapage et compte rendu du dernier rattrapage (fiches remplies, absentes,
restantes). Le nombre de crédits restants vient de la dernière réponse d'ISBNdb : il
peut retarder d'une requête.

#### `RG-126` — Page « Livres hors ISBNdb »
La page liste les fiches « absente d'ISBNdb », avec deux filtres : **À compléter**
(défaut) et **Complétées**. Chaque ligne montre la couverture, le titre, les auteurs,
l'ISBN, la source de secours, la quantité en stock, et des pastilles pour les champs
manquants (résumé, couverture, genre, éditeur, année, pages). Le tri par défaut place
d'abord les livres en stock. Le compteur de la navigation donne le nombre de fiches à
compléter.

#### `RG-127` — Marquer comme complétée
« Marquer comme complétée » n'est possible que si la fiche a au moins un titre et un
auteur. L'action est tracée (qui, quand) et réversible par « Rouvrir ». Elle relance la
recommandation (`RG-113`).

#### `RG-128` — Revérifier dans ISBNdb
Sur une fiche de secours ou hors ISBNdb, l'action « Revérifier dans ISBNdb » interroge
ISBNdb pour cet ISBN, dans la limite du quota. Si ISBNdb
connaît le livre, la fiche devient une fiche ISBNdb (`RG-110`).

### Reprise de l'existant

#### `RG-129` — Les fiches existantes sont conservées
Toutes les fiches déjà enregistrées sont conservées avec leurs quantités, mouvements,
corrections manuelles et recommandations. Elles reçoivent « issue d'ISBNdb » = faux et
« absente d'ISBNdb » = faux : le rattrapage nocturne les reprend progressivement
(`RG-108`, `RG-109`). Aucune fiche n'est vidée en attendant.

## 9. Critères d'acceptation

| # | Critère |
|---|---|
| CA-01 | Un ISBN connu d'ISBNdb, scanné quota disponible, donne une fiche « issue d'ISBNdb » portant tous les champs de `RG-100`, et la couverture enregistrée est l'adresse permanente. |
| CA-02 | Le même ISBN consulté par trois bénévoles et par l'enrichissement dans l'heure ne génère qu'**une** requête ISBNdb. |
| CA-03 | Quota épuisé : aucun appel ISBNdb n'est émis avant l'heure de renouvellement ; les fiches sont remplies par le secours et marquées « issue d'ISBNdb » = faux, « absente » = faux. |
| CA-04 | Un ISBN inconnu d'ISBNdb donne une fiche « absente d'ISBNdb », remplie par le secours, visible dans E2, et sa recommandation est lancée à la clôture de session. |
| CA-05 | Une fiche de secours n'a pas de recommandation avant le rattrapage ; après le rattrapage, elle est « issue d'ISBNdb » et a une recommandation. |
| CA-06 | Le rattrapage interroge ISBNdb par lots de 100 ISBN au plus, s'arrête dès que le quota est épuisé sans requête supplémentaire, et reprend la nuit suivante. |
| CA-07 | Un champ corrigé à la main n'est pas modifié par le rattrapage. |
| CA-08 | « Marquer comme complétée » retire la fiche de « À compléter » et relance embedding et voisins. |
| CA-09 | Sur le site public, le bouton « Voir d'autres éditions » n'apparaît que sous la dernière page des références externes ; un clic affiche au plus 100 éditions françaises dédoublonnées ; la même recherche relancée dans l'heure ne consomme pas de crédit. |
| CA-10 | Quitter la page pendant le chargement des propositions ISBNdb annule la requête ISBNdb. |
| CA-11 | Un refus pour excès de débit est retenté au plus 3 fois avec attente croissante. |
| CA-12 | E1 affiche les crédits restants et l'heure de renouvellement issus de la dernière réponse ISBNdb. |
| CA-13 | Au tri, scanner une édition de *Les Misérables* absente du stock alors qu'une autre édition y est affiche « Autre édition déjà à la bourse » avec éditeur, année et nombre d'exemplaires, y compris hors ligne pour des fiches déjà synchronisées. |
| CA-14 | Une fiche ISBNdb dont les sujets sont « Literature & Fiction, Science Fiction & Fantasy, Fantasy » reçoit les genres Science-fiction et Fantasy (ou Fantasy seul selon la table), jamais « Self Service ». |
| CA-15 | Après migration, toutes les fiches existantes sont présentes, inchangées, avec les deux marqueurs à faux. |

## 10. Contraintes

Mesures faites le 2026-10-23 avec la clé de test sur `api2.isbndb.com` (abonnement
Basic) :

| Mesure | Constat |
|---|---|
| Authentification | En-tête `Authorization: <clé>`. |
| Débit | En-tête `ratelimit-policy: "rate";q=60;w=60` : **60 requêtes par fenêtre glissante de 60 s** ; une rafale de 70 requêtes n'a pas été refusée. Le débit local doit rester réglable (1 → 3 par seconde). |
| Quota | En-tête `ratelimit: "daily";r=<restant>;t=<secondes avant renouvellement>` sur **chaque** réponse ; `q=5000;w=86400`. Renouvellement à **00:00 UTC**. La valeur peut retarder d'une requête (décompte différé). |
| `GET /book/{isbn}` | 1 crédit si trouvé. **404 « Book not found » : 0 crédit.** ISBN invalide (400) : 1 crédit → toujours valider l'ISBN avant l'appel. |
| `POST /books` (lot) | Corps `{"isbns":[…]}`, jusqu'à 100 ISBN (Basic). **1 crédit par livre trouvé**, les absents sont gratuits ; la réponse liste seulement les trouvés (`total`, `requested`, `data`). |
| `GET /books/{texte}` | `page`, `pageSize` (≤ 1000, on prend 100), `language=fr` (vérifié : 100/100 en `fra`). **1 crédit par page**, quelle que soit la taille. |
| `GET /stats` | 1 crédit → ne pas l'utiliser pour lire le quota ; lire les en-têtes des réponses utiles. |
| Images | `image` (`images.isbndb.com/covers/…jpg`) : publique et permanente. `image_original` : jeton qui expire (~2 h), 403 sans jeton. |
| Champs observés | `title`, `title_long`, `isbn`, `isbn13`, `isbn10`, `binding`, `publisher`, `language` (ISO 639-2, ex. `fra`), `date_published` (`1999` ou `2015-06-09`), `edition`, `edition_number`, `pages`, `dimensions`, `dimensions_structured`, `image`, `image_original`, `msrp`, `list_price`, `synopsis`, `overview`, `excerpt`, `dewey_decimal`, `authors` (« Nom, Prénom »), `subjects`, `other_isbns`, `related`. |
| Sujets | Très hétérogènes : catégories anglaises (« Literature & Fiction », « Classics »), vedettes françaises (« Romans », « Bandes dessinées »), bruit (« Self Service », « Arborist Merchandising Root », nom de l'auteur). D'où `RG-123`. |
| Œuvre | Aucun identifiant d'œuvre. `other_isbns` est rarement rempli. D'où `RG-115` et `RG-119`. |

Autres contraintes :

- La clé ISBNdb est un **secret** : jamais dans le dépôt, jamais côté navigateur, jamais
  dans les journaux.
- L'API et le traitement de fond (Azure Functions) appellent ISBNdb : le respect du débit
  et l'état « quota épuisé » doivent être **partagés** entre eux.
- Le délai d'affichage du verdict au scan (`ENF-01`) ne doit pas s'allonger : le signal
  « même œuvre » se calcule localement dans l'application de scan.
- La migration de base de données ne doit perdre aucune fiche ni aucun ISBN scanné.

## 11. Décisions

Questions tranchées le 2026-10-23 : les propositions ont été retenues, sauf `Q-02`
(pas de réserve, `RG-109`).

| # | Question | Décision |
|---|---|---|
| `Q-01` | Le seuil « inutile d'en garder » doit-il compter toute l'œuvre (`RG-121`) ou seulement l'ISBN, le signal restant informatif ? | Compter l'œuvre **uniquement** quand le lien vient d'un identifiant d'œuvre BnF/Open Library ou d'`other_isbns` ; quand il ne vient que de la clé titre + auteur, signal informatif seulement (moins de faux positifs). |
| `Q-02` | Faut-il garder une réserve de crédits pour la journée (`RG-109`) ? | **Non** : le rattrapage peut consommer tout le quota. |
| `Q-03` | Fréquence de revérification des fiches hors ISBNdb (`RG-111`) ? | Mensuelle : gratuite tant que le livre reste absent. |
| `Q-04` | Une fiche **complétée** à la main doit-elle être remplacée si ISBNdb connaît enfin le livre ? | Non : les champs corrigés sont protégés (`RG-05`) ; seuls les champs vides sont remplis, et la fiche devient « issue d'ISBNdb ». |
| `Q-05` | La table sujets → genres doit-elle être modifiable dans l'administration ? | Pas en v1 : livrée avec l'application, enrichie au fil des sujets rencontrés (un rapport « sujets sans correspondance » dans E1 aide à la compléter). |
| `Q-06` | La fiche publique doit-elle afficher les nouvelles informations (pages, reliure, édition, dimensions, sujets) ? | Oui pour pages, reliure et édition, dans une feature suivante, pour ne pas alourdir celle-ci. |
| `Q-07` | La recherche ISBNdb publique doit-elle proposer une page suivante (100 de plus, 1 crédit) ? | Non en v1 : au-delà de 100, inviter à préciser la recherche. |
| `Q-08` | Faut-il réserver la recherche ISBNdb aux membres connectés pour limiter les abus ? | Non : plafonds `RG-118` suffisants ; à revoir si la consommation publique dépasse 10 % du quota. |
| `Q-09` | Passage à 3 requêtes/seconde : faut-il aussi relever la taille des lots (1 000 ISBN par requête groupée sur les abonnements supérieurs) ? | Oui : débit et taille de lot sont deux réglages indépendants. |
