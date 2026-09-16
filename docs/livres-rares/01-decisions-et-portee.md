# 01 — Décisions structurantes et périmètre

## 1. Le problème que ce module résout

Aujourd'hui, « livre rare » est un **booléen** posé à la main par un administrateur
(`Book.IsRare`, `src/Backend/Vole_Papillon_Damour.Domain/BookAggregate/Book.cs`). Il
alimente une section publique, un filtre de recherche, un badge de carte et une alerte
en caisse — mais il ne porte **aucune information** : ni prix, ni photo, ni état, ni
description.

Conséquence opérationnelle : un livre expertisé à 60 € est signalé comme « rare » en
caisse, et le bénévole doit lire un prix écrit au crayon sur la page de garde. S'il
n'est pas écrit, ou s'il est mal lu, le livre part à 2 €.

Le module remplace ce booléen par une **fiche** : un objet de première classe, avec un
prix ferme, des photos prises par les bénévoles, une description honnête des défauts, et
une vitrine publique.

## 2. Les dix décisions

### D1 — Nouvel agrégat `RareBook`, identifié par un `Guid`

`Book` est un `AggregateRoot<Isbn13>` : **son identité EST son ISBN**, et
`Book.EnsureIsbn` rejette une valeur vide. Un livre de 1932 sans code-barres ne peut
donc pas être un `Book`. Ce n'est pas un détail d'implémentation contournable : c'est
la raison pour laquelle le module a besoin de son propre agrégat.

`RareBook` porte un `Isbn13?` **facultatif**. Quand il est renseigné, la fiche se
rattache à la fiche catalogue existante ; quand il est absent, la fiche vit seule, sans
quantité ni historique de mouvements.

> Maquette `AdminFicheRare` : « Sans ISBN, la fiche vit seule : elle n'est reliée à
> aucune fiche catalogue et n'a ni quantité ni historique de mouvements. Avec un ISBN,
> elle se rattache à la fiche existante et la caisse la reconnaît au scan. »

### D2 — `Book.IsRare` disparaît

Un livre est rare **parce qu'il a une fiche rare publiée**, pas parce qu'une case est
cochée. La maquette `AdminFicheRare` le dit explicitement : « il n'y a plus de case
“livre rare” sur la fiche catalogue ».

C'est la décision la plus coûteuse du module. Elle touche neuf points de lecture
existants — ils sont énumérés en [`02 §5`](02-modele-de-donnees.md) et repris comme lot
dédié en [`08`](08-lots-et-sequencement.md).

**Les marquages existants sont supprimés en bloc**, sans être convertis en fiches.
L'annotation `rare-derive` du canvas le tranche : « les 12 marquages actuels n'ont ni
photo ni prix, donc ils sont supprimés en bloc. Le marquage seul part, les fiches
catalogue et l'historique des ventes restent. » Il n'y a donc **pas** de file de
reprise à construire.

### D3 — Le prix d'un livre rare est stocké et affiché, jamais compté

Cela **amende `RG-50`** (`docs/bourse-aux-livres/06-regles-metier.md`) sur un point
précis, et un seul : « Aucun prix n'est **stocké, affiché**, calculé ni totalisé par
l'application, ni pour les livres ordinaires ni pour les livres rares. »

L'amendement est **motivé, pas accidentel** : `RG-50` protégeait une caisse à 1–2 € où
le prix n'apportait rien. Elle admettait elle-même que les livres rares sont le cas où
il compte — « c'est la seule protection contre un livre expertisé à 35 € vendu 2 € par
quelqu'un qui l'ignore » — et repoussait la solution sur un montant écrit physiquement
sur le livre. Ce module remplace le crayon par la base.

Le prix est un **prix ferme**, non négociable, fixé par l'association et non par le
bénévole de caisse. Il est stocké sur la fiche rare, affiché en vitrine publique et
affiché en caisse **pour être lu**.

Les mots `calculé` et `totalisé` de `RG-50` restent vrais. Voir `D4`.

### D4 — L'application ne compte aucun argent

Décision de l'association, prise après les maquettes : **aucun panier, aucun total,
aucune recette calculée**, ni pour les livres rares ni pour les livres ordinaires. La
caisse reste exactement ce qu'elle est aujourd'hui — elle enregistre quels livres
sortent, rien d'autre.

Conséquence directe : les livres ordinaires n'ont **aucun prix**, nulle part. Il n'y a
ni prix par fiche, ni prix unitaire par bourse, ni champ à ajouter dans
`AssociationSettings`.

> La maquette `CaisseRare` (artboard A12) montre un panier et un total « 63,00 €
> encaissés ». **Cette partie de la maquette est caduque.** Elle est remplacée par
> l'artboard `CaisseAjoutRare` (S3), page « Scanette » du canvas. Le reste de A12 —
> le bandeau violet, le prix ferme en grand, la recherche par titre — reste valable.

### D5 — La recette d'une bourse reste saisie à la main

`RG-51` est **inchangée**. Un seul montant, saisi par un administrateur à la clôture de
chaque bourse : celui du comptage de caisse que l'association fait de toute façon.

Rien dans ce module ne calcule, ne pré-remplit ni ne contrôle ce montant. Une fiche rare
vendue enregistre **la date de la vente et la bourse concernée** pour la traçabilité,
jamais un montant à additionner.

### D5 bis — La sortie du catalogue se fait à la clôture de la session

Un livre rare s'ajoute à une session de caisse par un bouton, se cherche par son titre,
et **c'est la validation de la sortie de session** qui le retire des livres rares
disponibles — pas son ajout.

Tant que la session est ouverte, le geste est rattrapable sans conséquence publique.
C'est cohérent avec le fonctionnement actuel des sessions de scan.

### D6 — « Me prévenir s'il part » réutilise la liste de suivi

Les deux maquettes de fiche publique portent un bouton « Me prévenir s'il part », et
l'écran de vente validée annonce « Les 2 personnes qui suivaient ce livre reçoivent un
e-mail “il est parti” ».

C'est l'agrégat `WatchlistAggregate` existant, étendu à une cible `RareBookId` en plus
de l'`Isbn13`, et l'`IBookAlertOutbox` existant avec un nouveau motif d'alerte. Ne pas
créer un second mécanisme d'alerte.

Sémantique inverse de l'alerte actuelle : aujourd'hui on prévient quand un livre
**arrive** ; ici on prévient quand il **part**, pour éviter un déplacement inutile.

### D7 — Un nouveau rôle applicatif `LivresRares`

Valeur Entra `LivresRares`, libellé « Bénévole livres rares ». Il s'ajoute à `Tri`,
`Caisse` et `Administration` sans en modifier aucun. Détail complet en
[`04`](04-role-livres-rares.md).

### D8 — Une fiche a un statut de publication

`Brouillon` → invisible côté public, non signalée en caisse. `Publiée` → visible dans
« Livres rares », remontée dans la recherche publique, prix affiché en caisse.

C'est ce qui rend acceptable qu'un bénévole crée une fiche depuis la scanette, debout
devant une table, sans qu'elle parte immédiatement en vitrine.

### D9 — Les photos sont une entité enfant ordonnée

`RareBookPhoto` : ordre explicite, légende facultative (« Dos », « Page de titre »,
« Gravure », « Défaut » dans les maquettes), référence blob. La **première photo est la
vignette** ; l'ordre se change par glisser-déposer.

Nombre illimité. Contrainte affichée dans la maquette : JPEG, 8 Mo par photo.

### D10 — Les fiches rares ont leurs propres URL publiques

`/livres-rares` pour la liste, `/livres-rares/:slug` pour la fiche, le slug étant dérivé
du titre, de l'auteur et de l'année
(`fables-de-la-fontaine-dore-1868` dans la maquette `AdminFicheRare`).

Elles ne réutilisent pas `/livres/:slug`, qui est indexé par ISBN et ne peut pas servir
une fiche sans ISBN.

## 3. Écarts assumés par rapport aux maquettes

Trois points où ce plan s'écarte volontairement des artboards. Ils sont signalés ici
pour que l'écart soit un choix, pas un oubli.

| Maquette | Écart | Motif |
|---|---|---|
| `AdminSidebar` ne contient pas d'entrée « Livres rares » ; `AdminFicheRare` s'affiche avec `active="catalogue"` et le fil d'Ariane « Catalogue · Livres rares · Fiche » | Le plan ajoute une **entrée dédiée** « Livres rares » dans le groupe « Le fonds de livres » | Tranché par l'association : « cette administration des livres rares se fasse bien dans un nouvel onglet dans la sidebar ». Une section accessible au seul rôle `LivresRares` a besoin de sa propre entrée de navigation pour que la vue restreinte ait du sens |
| `AdminSidebar` montre encore « Inventaire & cartons » | Ignoré | L'onglet a été supprimé sur `main` par le PR #195 ; la maquette est antérieure |
| `CaisseRare` (A12) montre un panier et un total | Caduque sur ce point | Voir `D4`. Remplacée par `CaisseAjoutRare` (S3) |

## 4. Questions ouvertes — à trancher avec l'association

| # | Question | Ce qu'elle bloque | Défaut retenu si non tranchée |
|---|---|---|---|
| ~~`Q1`~~ | ~~Les livres ordinaires ont-ils un prix ?~~ | — | **Tranchée : non.** L'application ne compte aucun argent (`D4`) |
| `Q2` | « Poser une question » sur une fiche publique — quel canal ? | Lot 5. Aucun backend de messagerie entrante n'existe. | `mailto:` vers l'adresse de contact de l'association, sans backend |
| ~~`Q3`~~ | ~~« Livre rare hors catalogue » en caisse~~ | — | **Sans objet.** Aucun montant n'est saisi en caisse. Un rare sans fiche sort comme un livre ordinaire, et l'administration est prévenue après la bourse |
| `Q4` | Le « rayon d'affichage » (Éditions anciennes, Illustrés, Beaux-arts, Régionalisme) est-il une liste fermée ou libre ? | Lot 1 (modèle). | Liste fermée, éditable dans les paramètres d'association |
| `Q5` | La gestion depuis la scanette est-elle vraiment nécessaire en v1, ou le portail admin sur mobile suffit-il ? | Lot 7 entier — c'est le lot le plus coûteux (hors-ligne, photos, file d'attente). | Nécessaire : c'est le geste du bénévole au tri, debout, sans ordinateur |

Plus aucune question ne bloque un lot. `Q2` et `Q4` ont des défauts raisonnables ; `Q5`
est une question de priorité, pas de conception.

## 5. Hors périmètre

- **L'estimation automatique de la valeur marchande** (`Q-02`, `RG-14`). Reste
  reportée. Le prix est saisi par un humain.
- **La réservation en ligne.** Les maquettes le disent trois fois : « Aucune
  réservation : le livre part au premier qui se présente en caisse. »
- **Tout ce qui compte de l'argent** : panier, total à encaisser, recette calculée,
  prix des livres ordinaires, statistiques financières dérivées des ventes. Voir `D4`
  et `D5`.
- **Le paiement en ligne.**
- **`src/BackOffice`.** C'est une application Angular distincte, sans surface livres
  rares. Ne pas y toucher.
- **`src/MauiCashApp`.** N'est pas référencée par `Vole_Papillon_Damour.slnx`.

## 6. Dettes existantes que ce module ne doit pas hériter

Deux bugs réels du dépôt qui deviendront des bugs du module s'ils ne sont pas corrigés
avec lui. Ils sont affectés à des lots dans [`08`](08-lots-et-sequencement.md).

1. **`BlobService.DeleteFileAsync` est codé en dur sur le conteneur des actualités**
   (`src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BlobService/BlobService.cs`).
   Il compare l'URL au conteneur `ContainerActualityImagesName` et retourne une chaîne
   vide si elle n'y correspond pas. Supprimer une photo de livre rare ne supprimerait
   donc **rien**, silencieusement. À rendre conscient du conteneur avant le lot 2.
2. **La route `/administration` n'a aucun garde**
   (`src/Catalog/src/app/app-routing.module.ts`). Le filtrage se fait dans le composant.
   C'était tenable avec un seul rôle ; avec deux rôles et deux vues différentes, la
   navigation doit devenir dépendante du rôle de façon explicite (lot 5).
