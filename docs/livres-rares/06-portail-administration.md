# 06 — Portail d'administration

Le portail vit dans la **même application** que le catalogue public :
`src/Catalog/src/app/features/administration/`. Maquette de référence :
`AdminFicheRare`.

## 1. Une remarque préalable sur le composant

`catalog-administration-page.component.ts` fait **76 000 caractères** et son gabarit
**108 000**. Toutes les sections y cohabitent. Ajouter la section « Livres rares » dans
ce fichier, c'est le pousser au-delà du maintenable.

**Décision : la section rare est un composant autonome**, monté par le `@switch` de
section, avec sa propre façade d'API :

```
features/administration/rare-books/
├── admin-rare-books.component.{ts,html,scss}
├── admin-rare-book-form.component.{ts,html,scss}
├── admin-rare-book-photos.component.{ts,html,scss}
└── admin-rare-books.facade.ts
```

Le composant parent ne gagne qu'un `@case ('rare-books')`. C'est aussi ce qui rend la
vue restreinte du §5 réalisable sans charger toute la machinerie d'administration.

## 2. Navigation

Dans `catalog-administration-page.component.ts`, `navGroups` (ligne 155) — groupe
« Le fonds de livres », **après « Catalogue »** :

```ts
{id: 'rare-books', label: 'Livres rares', icon: 'rare-books'},
```

`navGroups` est aujourd'hui un `readonly` constant. Il doit devenir **calculé en
fonction du rôle** (§5). Ajouter l'icône au `@switch` du gabarit (lignes 23-47) — un
livre avec une marque, dans le style des autres : `viewBox="0 0 24 24"`, trait 1.8,
extrémités arrondies.

La pastille de compteur (`navBadge`) affiche le nombre de fiches publiées.

Ajouter `'rare-books'` à `CATALOG_ADMIN_SECTIONS`
(`core/catalog-administration-route.ts`) — cela suffit à câbler `/administration/rare-books`,
`catalogAdminSectionFromRoute` et `isKnownCatalogRoute`.

## 3. Liste

En-tête : `Catalogue · Livres rares` en mono, titre `Newsreader` 38px, et les filtres de
[`03 §3`](03-application-et-api.md).

Tableau dense, une ligne par fiche : vignette, titre, mention d'auteur, ISBN — en mono,
ou la mention discrète « sans ISBN » —, prix, statut (`Brouillon` / `Publiée`),
disponibilité, nombre de photos, dernier bénévole ayant modifié.

**Le filtre « sans photo » n'est pas un filtre de confort** : une fiche publiée sans
photo est une vitrine vide. Le compter dans l'en-tête de section.

### File de migration

Encart en haut de la liste, au ton explicitement transitoire, alimenté par
`GetRareBookMigrationQueueQuery` :

> *N* livres étaient marqués rares sous l'ancien système et n'ont pas encore de fiche
> complète. Tant qu'ils n'en ont pas, ils n'apparaissent plus dans la section publique.

Chaque ligne : « Compléter la fiche » (ouvre le formulaire prérempli) et « Ce livre
n'est finalement pas rare » (supprime la fiche brouillon). **L'encart disparaît quand la
file est vide** — il ne doit pas devenir un meuble.

## 4. Fiche d'édition

Gabarit de la maquette `AdminFicheRare` : colonne principale fluide + colonne latérale
de 320px, `gap: 32px`.

**En-tête** : fil d'Ariane mono, titre, puce de statut violette, puis en mono
`/livres-rares/{slug} · créée le 2 mars par Michel`. À droite : « Voir la fiche
publique → » (contour `#9dc2da`) et « Enregistrer » (orange plein, 46px).

**Bandeau pédagogique** sous l'en-tête, filet gauche violet 3px sur fond `#f3edf9` :

> Cette fiche **est** ce qui rend le livre rare : il n'y a plus de case « livre rare »
> sur la fiche catalogue. Tant que la fiche est en brouillon, le livre reste invisible
> côté public et n'est pas signalé en caisse.

### Colonne principale

1. **Photos** — grille de 5 colonnes, vignettes de 196px. La première porte le badge
   « Vignette » en violet et une bordure `#5f3a86` ; les autres une bordure `#d9e9f4`.
   Poignée de glisser-déposer en bas à droite. Dernière case : zone en tirets
   `1.5px dashed #9dc2da` avec « + Ajouter » et « JPEG · 8 Mo max ».
   Sous la grille : `5 photos · 14,2 Mo · conteneur « livres-rares »` et « Supprimer la
   photo sélectionnée » en `#b02a33`.
2. **Identité du livre** — grille 2 colonnes : Titre, Auteur ou mention, Éditeur, Année,
   Rayon d'affichage (liste déroulante), ISBN facultatif. Le champ ISBN vide se dessine
   en tirets avec le texte `aucun — livre d'avant 1970` et une action « Scanner ».
   Note sous la grille :
   > Sans ISBN, la fiche vit seule : elle n'est reliée à aucune fiche catalogue et n'a ni
   > quantité ni historique de mouvements. Avec un ISBN, elle se rattache à la fiche
   > existante et la caisse la reconnaît au scan.
3. **Prix et état** — champ prix de 200px, hauteur 56px, bordé `#d9c8ea`, valeur en mono
   24px violet avec le `€` en `Newsreader` ; à côté, l'état en quatre pavés de 56px, le
   sélectionné bordé `1.5px #5f3a86` sur `#f3edf9`.
4. **Description publique** — zone de 132px, compteur `412 / 1200`, et sous le champ :
   « Dites les défauts. Un acheteur déçu en caisse, c'est un don qui revient. »
5. **Grille de 4 champs** : Reliure, Format, Pages, Emplacement.

### Colonne latérale

- **Publication** — bascule Brouillon / Publiée en deux pavés de 42px, le second violet
  plein quand actif ; puis l'interrupteur « Encore disponible » (vert `#18703a`) avec
  « Basculé automatiquement par la caisse à la vente. À la main si le livre part
  autrement. »
- **En caisse** — rappel de fonctionnement et lien vers l'écran de caisse.
- **Traçabilité** — Créée, Modifiée, Prix fixé par ; puis « Supprimer la fiche rare » en
  `#b02a33` avec « Le livre quitte les livres rares. S'il a un ISBN, la fiche catalogue et
  son historique restent intacts. »

## 5. Vue restreinte au rôle `LivresRares`

`navGroups` devient calculé :

```ts
readonly navGroups = computed<CatalogAdminNavGroup[]>(() =>
  this.auth.isAdministrator() ? FULL_NAV_GROUPS : RARE_ONLY_NAV_GROUPS);
```

`RARE_ONLY_NAV_GROUPS` : un groupe « Le fonds de livres », un item « Livres rares ».

La carte « Votre rôle » en bas de barre latérale
(`catalog-administration-page.component.html:60-66`) affiche déjà un texte conditionné
par `isAdministrator()`. L'étendre à trois cas : `Administration`, `Livres rares —
accès limité à la gestion des livres rares`, et le compte sans droits.

**Trois gardes, pas un seul** — la route `/administration` n'en a aucune aujourd'hui
([`01 §6.2`](01-decisions-et-portee.md)) :

1. Un utilisateur sans aucun des deux rôles voit l'écran d'accès réservé existant.
2. Un utilisateur `LivresRares` seul qui atteint `/administration/accounts` par URL est
   redirigé sur `/administration/rare-books`, pas sur un écran vide.
3. `/administration` sans section redirige vers la première section **autorisée** :
   `overview` pour un administrateur, `rare-books` sinon.

Le backend refuse déjà ces appels (`03 §4`) : ces gardes sont une question de
présentation, pas de sécurité. Les deux sont nécessaires.

## 6. Fiche catalogue d'un livre ordinaire

Dans la carte latérale « État de la fiche »
(`catalog-administration-page.component.html:410`), l'interrupteur « Livre rare —
marquage manuel pour expertise » est **remplacé** par une action :

- si aucune fiche rare ne porte cet ISBN : « Créer la fiche livre rare → », qui ouvre le
  formulaire prérempli avec les métadonnées du livre ;
- si une fiche existe : « Voir la fiche livre rare → » avec son statut.

L'interrupteur « Masquer du public » reste inchangé.
