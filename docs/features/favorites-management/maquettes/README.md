# Maquettes — Gestion des coups de cœur

- Canvas Claude Design : https://claude.ai/artifact/BQpNeD5rGKVDvVNXTdgytq (version
  `1794693555-76fc`, exportée le 2026-11-14).
- Ouvrir [`preview/index.html`](preview/index.html) dans un navigateur : sommaire des
  5 artboards, tailles et zones `data-zone`. Chaque page de `preview/` est autonome.
- `sources/` : les `.dc.html` et `canvas.json` du canvas, tels quels (gabarits
  `sc-for` / `sc-if`). `preview/` en est le rendu, données d'exemple comprises.
- Écrans non redessinés : E4, E5 et E6. Leurs maquettes sont celles de
  [`book-favorites`](../../book-favorites/maquettes/README.md), avec les changements listés
  plus bas.

## Règles de transposition

- Convention visuelle : [`docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`](../../../bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md).
  Réutiliser les variables CSS et composants existants du catalogue (en-têtes de section,
  pastilles de disponibilité, boutons pleine largeur) ; **pas d'hexadécimal en dur** dans
  les composants.
- Jetons déjà introduits par `book-favorites` (cœur `#c8364e`, texte coup de cœur
  `#a3263e`, cœur vide `#c9d6e1`, fond doux `#fdf0f2` / bordure `#f1c9d1`) : voir
  [sa table](../../book-favorites/maquettes/README.md#règles-de-transposition).
  Ajout de cette feature : bouton « Retirer » de E12 en `#a3263e` plein, avec texte blanc.
- Les textes des maquettes sont les libellés à reprendre **mot pour mot**. Les titres de
  livres et les chiffres (137 coups de cœur, page 2 sur 14, 2 sur 3) sont des exemples.
- Cette feature n'utilise pas de système de design externe : le canvas suit la convention
  V2 du catalogue.

## Changements d'écrans existants à reporter sur E4, E5 et E6

- **E4 (reprise de compte)** : le message de reprise ne connaît plus la variante « plafond
  atteint » (`RG-163`). Il indique seulement le nombre de coups de cœur repris.
- **E5 (fiche livre) et E6 (Mes achats)** : le bouton cœur qui retire ouvre la fenêtre de
  confirmation E12 au lieu de retirer immédiatement (`RG-160`). Le bouton « Coup de cœur »
  pour ajouter ne change pas.

## Correspondance

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `E2-Desktop.html` | E2 Trouver mon prochain livre, desktop (visiteur, bandeau navigateur) | `E2-entete`, `E2-lien-menu`, `E2-bandeau-navigateur`, `E2-bouton-creer-compte`, `E2-lien-connexion`, `E2-replier-bandeau`, `E2-coups-de-coeur`, `E2-compteur`, `E2-recherche`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-pastille-disponibilite`, `E2-calcul-en-cours`, `E2-retirer`, `E2-pagination`, `E2-suggestions`, `E2-avancement-calcul`, `E2-nombre-suggestions`, `E2-curseur-exigence`, `E2-carte-suggestion`, `E2-coeurs`, `E2-raison` | §6 E2 ; RG-134, RG-160, RG-163, RG-164, RG-165, RG-166, RG-136, RG-138 |
| `E2-Mobile.html` | E2 Mes coups de cœur, mobile (membre) | `E2-entete`, `E2-coups-de-coeur`, `E2-compteur`, `E2-recherche`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-pastille-disponibilite`, `E2-calcul-en-cours`, `E2-retirer`, `E2-pagination`, `E2-suggestions`, `E2-avancement-calcul`, `E2-curseur-exigence`, `E2-nombre-suggestions`, `E2-carte-suggestion`, `E2-coeurs`, `E2-raison` | §6 E2 ; RG-160, RG-163, RG-164, RG-165, RG-166 |
| `E2-Etats.html` | E11 États de E2 | `E11-calcul-en-cours`, `E11-calcul-indisponible`, `E11-calcul-indisponible-texte`, `E11-calcul-long`, `E11-recherche-vide`, `E11-erreur-retrait` | §6 E11 ; RG-161, RG-164, RG-166, RG-167, RG-168 |
| `E12-Desktop.html` | E12 Confirmer le retrait, desktop | `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer`, `E12-fermer` | §6 E12 ; RG-160, RG-161, RG-162 ; CA-18, CA-19, CA-29 |
| `E12-Mobile.html` | E12 Confirmer le retrait, mobile (feuille basse) | `E12-fenetre`, `E12-titre`, `E12-livre`, `E12-texte`, `E12-annuler`, `E12-retirer` | §6 E12 ; RG-160, RG-161 ; CA-28, CA-29 |

## Écarts connus

- **`E2-Mobile` ne reprend pas la préférence e-mail du membre** (`E2-preference-email` de
  `book-favorites`). Cette zone n'est pas modifiée par la feature : le plan ne la
  reconstruit pas, et elle reste telle que livrée. Le faire figurer ici serait une
  rétrocession à décider séparément.
- **E4, E5 et E6 ne sont pas redessinés** (voir plus haut). Le plan les traite comme des
  modifications de l'écran existant.
