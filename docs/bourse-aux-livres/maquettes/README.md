# Maquettes — Bourse aux livres

Maquettes statiques des trois surfaces du module, dessinées à partir des specs de ce
dossier et du design system du site (`src/Website`).

| Canvas | Artboards | Spec de référence |
|---|---|---|
| [Scanette](https://claude.ai/code/artifact/45cfe87a-9e3b-4cc0-9a13-cc6c9c8f34fb) | 12 | `03-parcours-benevole-scan.md` |
| [Catalogue public + administration](https://claude.ai/code/artifact/0545c05d-4473-45c3-bb22-3d136ec23ca0) | 9 + 10 | `04-site-public.md`, `05-administration.md` |

Les liens ouvrent le canvas pan/zoom : on y sélectionne un élément, on l'édite, on
exporte en PNG ou en PDF. Le second canvas a deux pages (« Site public » et
« Administration »), sélecteur dans la barre d'outils.

Les fichiers de ce dossier sont les **sources** de ces canvas. Un `.dc.html` = un
artboard, autonome, ouvrable dans un navigateur. `canvas.json` porte la mise en page,
les titres et les notes.

## Correspondance artboard ↔ spec

### `scanette/`

| Fichier | Écran | Spec |
|---|---|---|
| `Main.dc.html` | Accueil, choix du mode | `03` §2 |
| `SessionMode.dc.html` | Nouvelle session — mise à disposition | `03` §3.1, `RG-20` |
| `TriAttente.dc.html` | Tri — écran d'attente | `03` §3.2 |
| `VerdictTrop.dc.html` | Verdict « inutile d'en garder » | `03` §3.3, `RG-10` |
| `VerdictGarder.dc.html` | Verdict « à garder » | `03` §3.3, `RG-12` |
| `VerdictRare.dc.html` | Verdict « bac livres rares » | `03` §3.3, `RG-14` |
| `VerdictPremier.dc.html` | Verdict « premier exemplaire » | `03` §3.3 |
| `SaisieManuelle.dc.html` | Saisie manuelle du code | `03` §3.5, `RG-01` |
| `FinSession.dc.html` | Fin de session | `03` §3.6, `RG-44` |
| `Caisse.dc.html` | Mode caisse | `03` §5, `RG-50` |
| `Consultation.dc.html` | Mode consultation | `03` §6 |
| `HorsLigne.dc.html` | Variante hors ligne | `03` §7, `ENF-05` |

### `catalogue/` — page « Site public »

| Fichier | Écran | Spec |
|---|---|---|
| `Main.dc.html` | Accueil | `04` §3 |
| `Recherche.dc.html` | Résultats, deux périmètres | `04` §4, `RG-47` |
| `Fiche.dc.html` | Fiche livre | `04` §5 |
| `Catalogue.dc.html` | Catalogue par genre | `04` §2, `RG-26` |
| `MonCompte.dc.html` | Liste de recherche | `04` §6 |
| `PorteeSuivi.dc.html` | Portée du suivi (œuvre ou édition) | `04` §6, `RG-46` |
| `Email.dc.html` | E-mail d'alerte | `04` §7 |
| `AccueilMobile.dc.html` | Accueil, mobile | — |
| `FicheMobile.dc.html` | Fiche livre, mobile | — |

### `catalogue/` — page « Administration »

| Fichier | Écran | Spec |
|---|---|---|
| `AdminTableauDeBord.dc.html` | Tableau de bord | `05` §1 |
| `AdminSessions.dc.html` | Sessions de scan | `05` §4 bis |
| `AdminCorrectionSession.dc.html` | Corriger une session | `05` §4 bis, `RG-45` |
| `AdminCatalogue.dc.html` | Files de travail | `05` §4 |
| `AdminFiche.dc.html` | Fiche en édition + mouvements | `05` §3, §4 |
| `AdminBourse.dc.html` | Statistiques par bourse | `05` §2, `RG-51` |
| `AdminDesengorgement.dc.html` | Désengorgement | `05` §5 |
| `AdminInventaire.dc.html` | Remise à plat | `05` §6, `RG-34` |
| `AdminComptes.dc.html` | Bénévoles et membres | `05` §7, §8 |
| `AdminParametres.dc.html` | Paramètres | `05` §9 |

## Ce qu'il faut savoir avant d'implémenter

Les maquettes reprennent les tokens de `src/Website/tailwind.config.js` sans les
modifier. Trois décisions ne sont **pas** dans le design system existant et doivent
être portées telles quelles :

**1. Deux familles de couleur, jamais mélangées.**

| Rôle | Valeur | Où |
|---|---|---|
| Mode « disponible maintenant » | `#072b45` (ink-2) | Bandeau scanette |
| Mode « prochaine bourse » | `#1497d6` (blue-2) | Bandeau scanette |
| Mode caisse | `#dc6412` (orange-3) | Bandeau scanette |
| Mode consultation | `#4e6c84` (slate-2) | Bandeau scanette |
| Verdict « inutile d'en garder » | `#b02a33` | Bloc de verdict |
| Verdict « à garder » | `#18703a` | Bloc de verdict |
| Verdict « livre rare » | `#5f3a86` | Bloc de verdict, caisse, catalogue |
| Verdict « premier exemplaire » | `#072b45` (ink-2) | Bloc de verdict |

Rouge, vert et violet ne sont pas dans la palette VPD : ils ont été calés sur la même
famille de clarté et de chroma pour ne pas jurer. Le orange VPD **ne dit jamais un
verdict** : il reste la couleur de l'action primaire et du mode caisse.

**2. Les mêmes couleurs disent la même chose sur les trois surfaces.** Vert =
disponible, bleu = annoncé pour la prochaine bourse, ardoise `#9dc2da` = épuisé,
violet = rare. Le bénévole qui trie, le visiteur qui cherche et l'administrateur qui
corrige lisent la même langue. Sur le site public elles sont des pastilles dans une
mise en page éditoriale, jamais les bandeaux pleins de l'outil de terrain.

**3. Répartition typographique.** Newsreader pour les titres d'œuvres et les chiffres
éditoriaux, Libre Franklin pour l'interface, IBM Plex Mono pour les ISBN, compteurs,
horodatages et libellés en petites capitales. Les trois sont déjà déclarées dans
`tailwind.config.js`.

## Limites connues

- **Maquettes statiques.** Aucun `<script>`, aucun état interactif. Les graphiques
  d'administration sont en HTML/CSS : la couche de survol (tooltip, crosshair) reste
  à construire.
- **Icônes redessinées** en SVG au trait, 24 px, `stroke-width` 1,7–1,9. Celles de
  `src/Website/public/icons/` sont des bitmaps encapsulés dans du SVG : elles ne se
  recolorent pas et passent mal en petite taille.
- **Placeholders assumés** : `[ADRESSE À COMPLÉTER]` pour la salle, trames diagonales
  pour les couvertures, pastille « VPD » pour le logo.
- **Non dessinés** : la liste complète des membres (`05` §7, seulement un extrait de
  trois lignes) et une vue agrégée « statistiques par livre » (`05` §3, rendue sous
  forme d'historique des mouvements sur la fiche).
- Les données affichées sont **fictives** (titres, dates, noms de bénévoles, chiffres).

## Reconstruire un canvas depuis ces sources

Les canvas publiés sont assemblés par le script `seed-canvas.mjs` du skill `design`
de Claude Code, qui embarque les artboards dans une page autonome. Pour régénérer :

```
/design  # puis demander de reconstruire le canvas depuis docs/bourse-aux-livres/maquettes/
```

Chaque `.dc.html` reste lisible seul : ouvert dans un navigateur, il rend l'écran tel
quel. C'est la façon la plus simple de s'y référer pendant l'implémentation.
