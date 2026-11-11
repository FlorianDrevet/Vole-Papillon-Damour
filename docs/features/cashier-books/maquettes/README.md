# Maquettes — Livres dans la caisse Android

- Canvas Claude Design : <https://claude.ai/artifact/PM852dSJ4h46j5578w46Uw> (privé : seul son propriétaire y accède)
- Pages à implémenter : `preview/Main.html`, `preview/Panier.html`, `preview/Etats.html`. Sommaire : `preview/index.html`.
- Sources Claude Design : `sources/*.dc.html` et `sources/canvas.json`.

## Format et écarts assumés

- **Tablette paysage 1280 × 720** pour les trois écrans. L'application est une caisse tablette
  dont la mise en page est à deux colonnes (`src/MauiCashApp/MainPage.xaml`, colonnes
  `7*,4,3*`). La skill prévoit un artboard téléphone 390 px : il n'a pas été dessiné, car l'app
  n'a pas de mise en page téléphone définie.
- **Couleurs et polices reprises de l'app**, pas de `V2-CONVENTION.md` (charte de la
  Scanette et du catalogue web) : `Primary #012F5F`, `Secondary #1ACDF8`, `Tertiary #F67700`
  (`src/MauiCashApp/Resources/Styles/Colors.xaml`), polices Caveat Brush (titres, onglets,
  total) et Open Sans (textes).
- **Pas de design system** : le compte n'en a pas de défaut, et l'app a déjà son style.
- **Icônes tracées en SVG simple** (verre, grille, livre, caisse). Les icônes réelles
  (`Resources/Images/book.svg`, `drink.svg`, `loto.svg`, `caisse.svg`) embarquent des bitmaps
  et ne se prêtent pas à la maquette : Codex les réutilise depuis le dépôt.
- **Couvertures et logo** : zones grises libellées `couverture`, `image`, et cercle en pointillés
  pour le logo. Les noms et prix des livres sont des **exemples** (`Roman 1,00€`…) ; en
  production ils viennent de la fiche produit (CL-04).
- **Sélection des onglets** : l'onglet actif a un fond blanc et du texte navy. L'app ne
  montre pas encore cet état dans ses captures : à valider à l'implémentation.

## Écrans

| Fichier `preview/` | Écran | Zones | Spec (§, règles) |
|---|---|---|---|
| `Main.html` | E1 · E2 — Onglet Livres sélectionné, grille de livres, panier avec deux livres | `E1-onglet-livres`, `E2-grille-livres`, `E2-carte-livre`, `E2-prix-livre`, `E3-ligne-livre`, `E3-total` | §6 E1, E2 · CL-01, CL-03, CL-04 |
| `Panier.html` | E3 — Panier mixte : buvette et livre, onglet Buvette ouvert | `E1-onglet-livres`, `E3-ligne-livre`, `E3-total` | §6 E3 · CL-02, CL-04 |
| `Etats.html` | E4 — États vide, chargement, hors ligne | `E2-carte-livre`, `E4-etat-vide`, `E4-etat-chargement`, `E4-bandeau-hors-ligne` | §6 E4 · CL-01, CL-07 |

Les zones `E2-carte-livre` et `E3-ligne-livre` apparaissent plusieurs fois : une par
élément répété. Les éléments inchangés de l'app (onglets Buvette et Loto, sous-barre
Salé/Sucré/Froid/Chaude, boutons de quantité du panier) n'ont pas de zone : ils ne changent pas.

## Lecture par Codex

- Le total du panier inclut les livres (CL-04). Aucun écran ne montre de sortie de livre ni
  d'enregistrement : la tablette n'enregistre rien (CL-05, dérogation à RG-50 expliquée dans la spec).
