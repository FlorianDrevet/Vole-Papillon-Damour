# 05 — Catalogue public

Application : `src/Catalog`. Maquettes de référence : `LivresRares` (desktop),
`LivresRaresMobile` (liste + fiche), `FicheRare` (fiche desktop).

## 1. Routes

Dans `src/Catalog/src/app/app-routing.module.ts` :

```ts
{path: 'livres-rares', component: CatalogRareBooksPageComponent},
{path: 'livres-rares/:slug', component: CatalogRareBookDetailPageComponent},
```

Et dans `core/catalog-route.ts`, ajouter `/livres-rares` à `KNOWN_STATIC_ROUTE_PATHS`
puis étendre `isKnownCatalogRoute` pour accepter `livres-rares/:slug` — la fonction
teste aujourd'hui `segments[0] === 'livres' || segments[0] === 'oeuvre'`.

Vérifier `app.routes.server.ts` (rendu serveur) et `core/catalog-robots.ts`
(indexation) : les fiches rares **doivent** être indexables, c'est une vitrine.

Ajouter les deux routes au sitemap. Précédent : la branche `fix/website-sitemap-routes`.

## 2. Page liste — `/livres-rares`

Structure reprise de la maquette `LivresRares` :

1. **Fil d'Ariane** `Catalogue / Livres rares`, le dernier segment en `#5f3a86`.
2. **Bandeau sombre** `#072b45` avec un dégradé radial violet en haut à droite
   (`radial-gradient(70% 120% at 88% -10%, rgba(95,58,134,.55) 0%, rgba(7,43,69,0) 62%)`),
   titre `Newsreader` 62px, et un encart « Prochaine bourse » alimenté par
   `/asso-events` — la logique existe déjà sur la page d'accueil.
3. **Barre de filtres** : puces par rayon avec compteur (`Tous · 23`,
   `Éditions anciennes · 9`…), la puce active en violet plein ; à droite, le tri, dont
   **`prix décroissant` est la valeur par défaut**.
4. **Grille 3 colonnes**, `gap: 30px`, cartes bordées `#d9e9f4` sans rayon.
5. **Encart de fin** « Vous avez un livre ancien à donner ? » avec bouton orange.

### Carte rare

Distincte de `book-card` : elle ne réutilise pas le composant existant, dont l'anatomie
(genre, disponibilité, quantité annoncée) ne correspond pas. Créer
`shared/rare-book-card/`.

| Élément | Valeur |
|---|---|
| Zone photo | `height: 320px`, motif `repeating-linear-gradient(135deg, #ece3f6 0 8px, #f7f3fc 8px 16px)` en attente d'image |
| Badge « Rare » | Haut gauche, fond `#5f3a86`, mono 9.5px, `letter-spacing: .14em`, majuscules |
| Compteur photos | Bas droite, fond `rgba(7,43,69,.78)`, « 5 photos » |
| Titre | `Newsreader` 25px/1.12 |
| Mention d'auteur | 14.5px `#4e6c84` |
| Ligne technique | mono 11.5px `#6d8ba2` — `Hachette · 1868 · demi-reliure cuir`, et `· sans ISBN` quand il n'y en a pas |
| Prix | `Newsreader` 32px `#5f3a86`, séparé par un filet `#e2eef7` |
| Action | `Voir la fiche →` en `#0c6ea6` |

**État vendu** : badge gris `#6d8ba2` « Parti », textes en `#4e6c84`/`#9dc2da`, prix
barré, et « Vendu le 8 février » à la place de l'action. La carte reste dans la liste —
c'est une décision de la maquette, elle montre ce que l'association a vendu.

> Le badge « Rare » des cartes ordinaires (`shared/book-card/book-card.component.scss`,
> `.rare-badge`) est aujourd'hui **orange**, alors que le violet est la couleur du rare
> partout ailleurs (verdict de tri, alerte caisse, filtre de recherche). Avec la
> suppression d'`IsRare`, ce badge disparaît des cartes ordinaires : l'incohérence se
> résout d'elle-même.

## 3. Fiche publique — `/livres-rares/:slug`

Desktop : deux colonnes `620px | 1fr`, `gap: 56px`.

**Colonne gauche** — galerie : image principale 700px bordée `#d9c8ea`, bouton
« Agrandir » en bas à droite, puis 4 vignettes de 130px en grille ; la quatrième porte un
voile `+N` s'il reste des photos. Sous la galerie, une phrase qui vaut engagement :
« Photos prises par les bénévoles au moment du tri. Elles montrent l'exemplaire réel,
défauts compris. »

**Colonne droite**, dans l'ordre :

1. Pastille violette « Livre rare · exemplaire unique ».
2. Titre `Newsreader` 50px, mention d'auteur 19px, ligne technique mono.
3. **Carte prix** bordée `#d9c8ea` : libellé mono violet « Prix ferme », montant
   `Newsreader` 52px `#5f3a86`, filet vertical, puis « Prix fixé par l'association, non
   négociable. Il s'applique en caisse, sur présentation du livre. »
4. **Carte disponibilité** : pastille verte `#18703a` + « Disponible à la bourse du
   14 mars » + l'emplacement à droite en mono.
5. **« Ce qu'en dit le bénévole »** — la description publique, 16px/1.7, `max-width: 64ch`.
6. **Tableau de caractéristiques** en 2 colonnes : État, Reliure, Format, Pages, Entré au
   tri, ISBN. L'ISBN absent s'affiche en `#9dc2da` : « aucun (avant 1970) ».
7. **Actions** : « Me prévenir s'il part » (orange, 60px, icône cloche) et « Poser une
   question » (contour).
8. **Encart bleu pâle** : « Aucune réservation : le livre part au premier qui se
   présente en caisse. L'alerte vous dit seulement qu'il n'est plus disponible, pour ne
   pas vous déplacer pour rien. »

**Bas de page** : « Autres livres rares de cette bourse », 3 cartes horizontales
compactes.

Mobile (maquette `LivresRaresMobile` B) : galerie en carrousel plein cadre 420px avec
pastilles de progression violettes, puis le même contenu en une colonne. Le prix reste
dans sa carte, haut placé.

### « Me prévenir s'il part »

Branché sur la liste de suivi ([`03 §7`](03-application-et-api.md)). Réutiliser
`shared/components/auth-prompt/` pour l'invite de connexion : le motif existe déjà,
introduit par `feat/catalog-watchlist-signin-prompt`.

### « Poser une question »

Voir `Q2` ([`01 §4`](01-decisions-et-portee.md)). Par défaut : `mailto:` vers l'adresse
de contact de l'association, avec le titre du livre en objet. Aucun backend.

## 4. Intégration dans les pages existantes

| Page | Modification |
|---|---|
| Accueil (`features/home/catalog-home-page.component.html:293`) | La section « Livres rares » lit désormais `/catalog/rare-books` au lieu de la recherche filtrée, et affiche la carte rare. Son bouton « Voir la sélection » pointe sur `/livres-rares` au lieu d'appeler `showRare()` |
| Recherche (`features/search/catalog-search-page.component.html:128`) | La case « Uniquement les livres rares » est conservée ; elle renvoie désormais les fiches rares publiées. Le tri par prix devient disponible **quand ce filtre est actif** — il n'a pas de sens sur les livres ordinaires |
| Navigation (`core/layouts/navigation/catalog-nav-items.ts`) | Ajouter « Livres rares » à la navigation principale |
| Fiche livre ordinaire | Si un `RareBook` publié porte le même ISBN, afficher un renvoi vers la fiche rare |

## 5. Pastille d'en-tête « Livres rares »

`core/layouts/navigation/catalog-navigation.component.html`, à côté du bloc existant des
lignes 97-103.

```html
@if (isAuthenticated() && isRareBookManager()) {
  <a class="header-rare-pill" routerLink="/administration/rare-books" ...>Livres rares</a>
}
```

Style calqué sur `.header-admin-pill`
(`catalog-navigation.component.scss:233`) : `min-height: 42px`, `min-width: 124px`,
`border-radius: 999px`, mono 600 `.54rem`, `letter-spacing: .16em`, majuscules. Fond
`#5f3a86`, texte `#ffffff`. À l'état actif et au survol : fond `#4a2d6a`.

Un utilisateur portant les deux rôles voit **deux pastilles**. Vérifier que l'en-tête ne
déborde pas à 1024px : `min-width: 124px` × 2 plus le bloc compte (`min-width: 146px`)
tient, mais c'est juste — prévoir un repli qui masque le libellé au profit d'une icône
sous 1100px.

Ajouter également l'entrée dans le menu déroulant du compte, à côté de « Ouvrir
l'administration » (ligne 85), et dans le menu mobile.
