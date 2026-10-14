# Maquettes — Tableau de loto en direct

- **Canvas Claude Design** : https://claude.ai/artifact/UBbiYvymt2pBPJKVfgRnkR (privé).
  Exporté le 2026-10-13 depuis la version publiée `1791891885-42c9`. Deux pages :
  « Projection (backoffice) » et « Site public ».
- **Aperçus** : ouvrir `preview/index.html` dans un navigateur donne le sommaire des écrans et
  de leurs zones. Chaque `preview/*.html` est autonome.
- **Sources** : `sources/` contient les fichiers `.dc.html` et `canvas.json` du canvas, tels
  quels.

## Lire ces maquettes

- **Charte** : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` et les jetons de
  `src/Website/tailwind.config.js`. Pas d'hexadécimal en dur dans les composants. Les couleurs
  passent par les jetons existants (`ink`, `ink-2`, `blue`, `blue-2`, `cyan`, `orange`,
  `orange-2`, `orange-3`, `mist`, `mist-2`, `slate-*`, `line`…) ou, pour la projection, par les
  variables CSS des deux palettes ci-dessous.
- **Projection (E1, E2, E3)** : backoffice, `evenement/:id/loto-tableau`, plein écran en
  1920 × 1080. Les deux palettes partagent **exactement** la même mise en page : seules les
  variables CSS changent, selon la classe racine `pal-a` ou `pal-b` (RG-22). Elles sont définies
  dans le `<style>` de `Main.html` :

  | Variable | A « sombre » | B « claire » | Rôle |
  |---|---|---|---|
  | `--bg` | `#020e18` | `#ffffff` | fond |
  | `--cell` / `--cell-num` | `#0b2438` / `#5f8199` | `#e6eff6` / `#5b7a92` | case non sortie |
  | `--on` / `--on-num` | `#ffffff` / `#041d30` | `#072b45` / `#ffffff` | case sortie, précédents |
  | `--last` / `--last-num` | `#f0801c` / `#041d30` | `#f0801c` / `#041d30` | dernier numéro |
  | `--text` | `#ffffff` | `#041d30` | texte, anneau du voisin |
  | `--muted` | `#9dc2da` | `#33536e` | libellés |
  | `--panel` / `--line` / `--chip` | `#071b2b` / `#1b3c57` / `#123552` | `#f3f8fc` / `#d9e9f4` / `#e9f4fb` | panneaux |
  | `--accent` | `#f9a93c` | `#b4500e` | bingo, compteur « 2ᵉ fois » |

  - **Lisibilité des états (RG-01)** : les quatre états d'une case se distinguent par la
    **luminosité** :
    - non sortie : sombre en A, très claire en B ;
    - sortie : pleine claire en A, pleine encre en B ;
    - dernier numéro : orange ;
    - voisin du +1 / −1 : case non sortie cerclée d'un anneau `--text` de 8 px.
  - **Dernier numéro, en grand dans la colonne de droite** : en A, chiffre orange sur fond
    sombre. En B, pavé orange avec chiffres encre, car l'orange sur blanc ne donne que 2,6:1.
  - **Typographie** : Libre Franklin 800/900 et `font-variant-numeric: tabular-nums` pour tous
    les numéros. Aucune police manuscrite (Caveat). Les tailles en 1920 × 1080 (grille 76 px,
    dernier numéro 300–320 px, annonce 780–820 px) sont des minimums (RG-03). À
    l'implémentation, elles suivent la taille de l'écran (`vh`/`vw`, `clamp`).
  - **Grille (RG-23)** : 1448 px sur 1920, soit 75 % de la largeur, et toute la hauteur
    utile.
  - **QR code** : celui des maquettes est un **dessin**. L'implémentation génère un vrai QR code
    de `https://volepapillondamour.fr/loto` (RG-24), sur fond blanc avec une marge, dans les
    deux palettes.
  - **E3** est la même page que E1, souris active. La barre, le témoin de saisie, le toast et
    le curseur disparaissent après 3 s sans mouvement (RG-04).
- **Site public (E4, E5, E6)** : charte du site, avec le fond sombre du tableau public actuel
  pour E4.
  - **Bandeau E5** : il s'insère **au-dessus** de l'en-tête existant (`navigation.component`).
    En-tête et page d'accueil apparaissent en arrière-plan pour le contexte, ils ne changent
    pas.
  - **Données d'exemple** : noms de parties, lots, numéros, horaires, ainsi que `[Salle]`,
    `[Ville]` et `[date]`, qui viennent de l'événement.

## Correspondance

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `Main.html` | E1 · Tableau projeté — palette A, partie standard | `E1-tableau` `E1-grille` `E1-case-sortie` `E1-case-non-sortie` `E1-case-derniere` `E1-partie` `E1-ligne` `E1-compteur-fois` `E1-dernier-numero` `E1-precedents` `E1-lots` `E1-bingo` | §6 E1 ; RG-01, RG-02, RG-03, RG-21, RG-22, RG-23 |
| `E1-Claire.html` | E1 · Tableau projeté — palette B, partie +1 / −1 | mêmes zones que `Main.html` + `E1-case-voisin` `E1-type-partie` | §6 E1 ; RG-01, RG-22 |
| `E3-Regie.html` | E3 · Contrôles visibles (souris active) | `E3-barre-regie` `E3-bouton-annuler` `E3-bouton-ligne` `E3-bouton-bingo` `E3-bouton-pause` `E3-bouton-palette` `E3-bouton-plein-ecran` `E3-aide-raccourcis` `E3-saisie-clavier` `E3-toast-erreur` | §6 E3 ; RG-04, RG-05, RG-06, RG-08, RG-22 |
| `E2-Numero.html` | E2 · Numéro tiré — A | `E2-annonce-numero` | §6 E2 ; RG-03, RG-07 |
| `E2-NumeroClaire.html` | E2 · Numéro tiré — B | `E2-annonce-numero` | §6 E2 ; RG-07, RG-22 |
| `E2-AvantDebut.html` | E2 · Avant le début — A | `E2-avant-debut` `E2-premiere-partie` `E2-qr-code` `E2-adresse-courte` | §6 E2 ; RG-09, RG-21, RG-24 |
| `E2-AvantDebutClaire.html` | E2 · Avant le début — B | mêmes zones que `E2-AvantDebut.html` | §6 E2 ; RG-22 |
| `E2-Pause.html` | E2 · Pause — A | `E2-pause` `E2-bingo-pause` `E2-partie-suivante` `E2-qr-code` | §6 E2 ; RG-08, RG-21 |
| `E2-PauseClaire.html` | E2 · Pause — B | mêmes zones que `E2-Pause.html` | §6 E2 ; RG-22 |
| `E2-Termine.html` | E2 · Loto terminé — A | `E2-termine` `E2-bilan-bingo` | §6 E2 ; RG-09 |
| `E4-Mobile.html` | E4 · Tableau en direct — 390 px | `E4-etat-connexion` `E4-dernier-numero` `E4-precedents` `E4-partie` `E4-lots` `E4-grille` `E4-bingo` | §6 E4 ; RG-10, RG-13 |
| `E4-Desktop.html` | E4 · Tableau en direct — 1440 px | mêmes zones que `E4-Mobile.html` | §6 E4 |
| `E4-Etats.html` | E4 · États | `E4-reconnexion` `E4-avant-debut` `E4-termine` `E4-introuvable` | §6 E4 ; RG-09, RG-12, RG-13, RG-15 |
| `E5-Desktop.html` | E5 · Bandeau « En direct » — desktop | `E5-bandeau` `E5-statut-direct` `E5-dernier-numero` `E5-bouton-suivre` `E5-fermer` | §6 E5 ; RG-16 à RG-20 |
| `E5-Mobile.html` | E5 · Bandeau « En direct » — mobile | mêmes zones que `E5-Desktop.html` | §6 E5 ; RG-20 |
| `E5-Etats.html` | E5 · Bandeau — avant, terminé, autre événement | `E5-variante-avant` `E5-lien-evenement` `E5-variante-termine` `E5-variante-autre` | §6 E5 ; RG-17, RG-18 |
| `E6-Desktop.html` | E6 · Fiche loto le jour J — desktop | `E6-encart-direct` `E6-bouton-suivre` | §6 E6 ; RG-21 |
| `E6-Mobile.html` | E6 · Fiche loto le jour J — mobile | mêmes zones que `E6-Desktop.html` | §6 E6 |
