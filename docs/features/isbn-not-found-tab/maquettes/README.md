# Maquettes — ISBN non trouvés

Canvas Claude Design (privé, source de référence de la phase 2) :
https://claude.ai/artifact/S9FpHwgqzyR7F2SB8F512J

## Comment les lire

- **Aperçus autonomes** : `preview/<Écran>.html`. Chaque page embarque ses styles
  (`base.css` intégré), sans runtime du canvas. Ouvrir `preview/index.html` donne le
  sommaire avec les zones de chaque écran.
- **Sources** : `sources/` contient les `*.dc.html`, `base.css` et `canvas.json` tels
  qu'exportés du canvas publié. Ne pas les modifier à la main : refaire la maquette dans
  le canvas puis ré-exporter.
- Le fichier `preview/Main.html` est l'écran **E1 desktop** (le nom vient de l'artboard
  principal du canvas).

## Règles de réutilisation

- Les couleurs, espacements et polices passent par les variables CSS `--admin-*` déjà
  définies dans `base.css` (voir `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`).
  Ne pas introduire d'hexadécimaux en dur dans le code de production.
- Chaque élément à implémenter porte un `data-zone="E<n>-<élément>"`. Le plan et le code
  citent ces identifiants ; `check_zones.py` vérifie qu'ils sont tous couverts.
- Les icônes de navigation sont des emplacements génériques : l'application réelle garde
  ses icônes existantes.

## Tableau des écrans

Les numéros de règles (RG-xx) et sections renvoient à `01-fonctionnel.md`.

| Fichier `preview/` | Écran | Taille | Zones (`data-zone`) | Spec |
|---|---|---|---|---|
| `Main.html` | E1 · Analyse des ISBNs, desktop | 1440 × 1040 | E1-titre, E1-intro, E1-lien-non-trouves, E1-compteur, E1-filtres, E1-filtre-toutes/en-cours/echecs, E1-liste, E1-ligne, E1-actions-ligne | §6 E1 · RG-01, RG-02, RG-03 |
| `E1-Mobile.html` | E1 · mobile | 390 × 1180 | E1-titre-mobile, E1-intro-mobile, E1-filtres-mobile, E1-liste-mobile | §6 E1 · RG-02 |
| `E1-Etats.html` | E1 · états | 1100 × 1040 | E1-vide-toutes, E1-vide-en-cours, E1-vide-echecs, E1-chargement, E1-erreur, E1-pastille-independante | §6 E1 · RG-11 |
| `E2-Desktop.html` | E2 · ISBN non trouvés, desktop | 1440 × 1160 | E2-titre, E2-intro, E2-compteur, E2-barre-actions, E2-bouton-exporter, E2-bouton-importer, E2-lien-format, E2-liste, E2-ligne, E2-action-completer/reessayer/supprimer, E2-pagination | §6 E2 · RG-01, RG-05, RG-07, RG-08, RG-23 |
| `E2-Mobile.html` | E2 · mobile | 390 × 1300 | E2-titre-mobile, E2-intro-mobile, E2-compteur-mobile, E2-barre-actions-mobile, E2-bouton-exporter-mobile, E2-bouton-importer-mobile, E2-lien-format-mobile, E2-liste-mobile | §6 E2 · RG-07, RG-23 |
| `E2-Etats.html` | E2 · états | 1100 × 1300 | E2-aide-format, E2-vide, E2-export-desactive, E2-chargement, E2-erreur | §6 E2 · RG-07, RG-08 |
| `E3-Desktop.html` | E3 · Import CSV, aperçu, desktop | 1440 × 900 | E3-titre, E3-nom-fichier, E3-synthese, E3-stat-pretes, E3-stat-sans-auteur, E3-stat-ignorees, E3-stat-rejetees, E3-table-rejets, E3-bouton-annuler, E3-bouton-enregistrer | §6 E3 · RG-14, RG-16, RG-17, RG-18, RG-19, RG-20 |
| `E3-Mobile.html` | E3 · aperçu, mobile | 390 × 1180 | E3-titre-mobile, E3-nom-fichier-mobile, E3-synthese-mobile, E3-table-rejets-mobile, E3-bouton-enregistrer-mobile, E3-bouton-annuler-mobile | §6 E3 · RG-14, RG-19 |
| `E3-Etats.html` | E3 · états | 1180 × 1500 | E3-zone-depot, E3-erreur-fichier, E3-aucune-valide, E3-progression, E3-resume, E3-erreur-enregistrement | §6 E3 · RG-15, RG-19, RG-21, RG-22 |
| `E4-Desktop.html` | E4 · navigation, desktop et compteurs à 0 | 1100 × 1000 | E4-entree-analyse, E4-entree-non-trouves, E4-entree-hors-isbndb, E4-entree-catalogue-sans-pastille, E4-entree-livres-rares-sans-pastille, E4-badge-analyse, E4-badge-non-trouves, E4-badge-hors-isbndb, E4-etat-zero | §6 E4 · RG-09, RG-10, RG-11, RG-12, RG-13 |
| `E4-Mobile.html` | E4 · sélecteur mobile | 390 × 1000 | E4-selecteur-mobile, E4-selecteur-mobile-zero, E4-option-analyse, E4-option-non-trouves, E4-option-hors-isbndb, E4-option-catalogue, E4-option-livres-rares | §6 E4 · RG-09, RG-10 |
| `E5-Desktop.html` | E5 · Livres hors ISBNdb, desktop | 1440 × 1060 | E5-filtre-a-completer, E5-filtre-completees, E5-recherche, E5-liste, E5-ligne-livre | §6 E5 · RG-04, RG-05, RG-06, RG-20 |
| `E5-Mobile.html` | E5 · mobile | 390 × 1300 | E5-filtre-a-completer-mobile, E5-filtre-completees-mobile, E5-recherche-mobile, E5-liste-mobile | §6 E5 · RG-04 |
| `E5-Etats.html` | E5 · états | 1100 × 900 | E5-vide-a-completer, E5-vide-completees, E5-chargement, E5-erreur, E5-sans-titre-inconnu | §6 E5 · RG-04, RG-13 |

## Décisions prises au canvas

- **Import CSV (E3)** : dessiné en boîte de dialogue modale sur la liste (plein écran sur
  mobile), en deux étapes : aperçu, puis enregistrement.
- **Bouton d'export désactivé** quand la liste est vide (E2-Etats).
- **Livres complétés par import** : marqués « complétés » d'office dans E5 (RG-20). Sans
  auteur, la fiche reste « à compléter » (RG-20, question 4 de la spec).
- **E5 sans « Titre inconnu »** : seuls les livres avec un titre restent listés (RG-04,
  `E5-Etats` · « Avant / après »).

## Points à confirmer en implémentation

- Un livre complété à la main ou par import doit être absent d'ISBNdb pour être marqué
  « complété » (contrainte de la spec, section 8).
