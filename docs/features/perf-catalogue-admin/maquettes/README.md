# Maquettes — Perf catalogue & admin (états de chargement)

- Canvas Claude Design (référence, validé le 15 novembre 2026) :
  https://claude.ai/artifact/KiRVcyebJkrcLo75HZMXVb
- Ouvrir `preview/index.html` dans un navigateur : sommaire des écrans, tailles et zones.
  Chaque page de `preview/` est autonome (styles en ligne).
- `sources/` : les `*.dc.html` et `canvas.json` exportés tels quels.

## Règles de lecture

- Convention visuelle : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`.
  Réutiliser les variables CSS existantes du portail (`--admin-paper`, `--admin-line`,
  `--admin-ink`, `--admin-slate`, `--admin-blue`, `--admin-orange`…) et du catalogue
  public, **jamais** les hexadécimaux des maquettes en dur.
- **Seuls les états de chargement changent**, plus le retrait des quatre cartes du
  tableau de bord (`RG-168`). Le contenu chargé des écrans existants (carte ISBNdb,
  tuiles d'attention, lignes de liste, cartes livres) reste celui du code actuel. Leur
  rendu dans les maquettes est approximatif et ne doit pas être recopié.
- Les squelettes animés respectent `prefers-reduced-motion` (fond fixe sans animation).
- Les mobiles E4 et E5 n'ont pas de zone propre : ils appliquent les zones desktop du
  même écran à 390 px.

## Correspondance

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `Main.html` | E1·E2 — premier chargement, desktop | `E1-entete`, `E1-carte-squelette`, `E1-carte-isbndb-squelette`, `E1-alertes-squelette`, `E2-pastille-squelette` | §5 P2, `RG-159`, `RG-160`, `RG-161`, `RG-162` |
| `E1-Charge-Desktop.html` | E1·E2 — chargé (4 cartes), desktop | `E1-carte-disponibles`, `E1-carte-tries`, `E1-carte-vendus`, `E1-carte-jamais-vendus`, `E1-heure-calcul`, `E1-tuiles-attention`, `E2-pastille` | `RG-162`, `RG-163`, `RG-164`, `RG-168` |
| `E1-Mobile.html` | E1·E2 — chargement, mobile | `E2-selecteur-mobile` | `RG-161`, `RG-162` |
| `E1-Etats.html` | E1 — erreur, pas encore calculé, rafraîchissement | `E1-bloc-erreur`, `E1-bouton-reessayer`, `E1-pas-encore-calcule`, `E1-rafraichissement` | `RG-159`, `RG-161`, `RG-163` |
| `E3-Desktop.html` | E3 — premier chargement et rechargement, desktop | `E3-recherche`, `E3-ligne-squelette`, `E3-liste-estompee`, `E3-indicateur-rechargement`, `E3-pagination` | §5 P3, `RG-156`, `RG-157`, `RG-167` |
| `E3-Mobile.html` | E3 — rechargement et erreur, mobile | `E3-bloc-erreur` | `RG-156`, `RG-161` |
| `E4-Desktop.html` | E4 — onglet en chargement, desktop | `E4-onglets`, `E4-bloc-squelette`, `E4-graphique-squelette` | §5 P4, `RG-157`, `RG-161`, `RG-166` |
| `E4-Mobile.html` | E4 — onglet en chargement, mobile | (zones E4 desktop) | `RG-161` |
| `E5-Desktop.html` | E5 — rafraîchissement, desktop | `E5-filtre-genre`, `E5-indicateur-rechargement`, `E5-resultats-estompes`, `E5-pagination`, `E5-grille-squelette` | §5 P1, `RG-155` à `RG-158` |
| `E5-Mobile.html` | E5 — rafraîchissement, mobile | (zones E5 desktop) | `RG-156`, `RG-158` |
