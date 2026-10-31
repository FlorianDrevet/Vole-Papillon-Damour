# Maquettes — Tableau du loto, mode Halloween (mobile)

- **Canvas Claude Design** : https://claude.ai/artifact/TPeyvSzwE6VHyT3ARtTUC5 (privé).
  Exporté le 2026-10-31 depuis la version publiée `1793451544-7dfa` (ajout du bandeau E6 et de
  l'humeur `course` du fantôme).
- **Aperçus** : ouvrir `preview/index.html` dans un navigateur donne le sommaire des écrans et
  de leurs zones. Chaque `preview/*.html` est autonome. `preview/Main.html` est **jouable** :
  les boutons à droite du téléphone simulent les messages du direct.
- **Sources** : `sources/` contient les `.dc.html` et `canvas.json` tels quels. Les SVG des
  personnages et toutes les `@keyframes` sont à reprendre depuis ces fichiers.

## Lire ces maquettes

- **Charte** : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` pour le reste du
  site. Le mode Halloween est un thème local au tableau : ses couleurs passent par les
  variables CSS de la racine `.hw` (tableau ci-dessous), pas par des hexadécimaux en dur dans
  les règles, et pas par `tailwind.config.js`.

  | Variable | Valeur | Rôle |
  |---|---|---|
  | `--nuit` | `#120a1c` | fond de page |
  | `--velours` | `#1d1229` | blocs, bouton Calmer |
  | `--ardoise` | `#2a1c3a` | case non sortie |
  | `--citrouille` | `#ff8c2e` | dernier numéro, case du dernier |
  | `--braise` | `#ffb347` | état « En direct », lueurs |
  | `--cire` | `#f4e8cf` | case sortie, pierres tombales, texte |
  | `--sorciere` | `#9bd85a` | anneau des voisins +1 / −1, chaudron |
  | `--lilas` | `#b8a5cf` | texte secondaire, chiffres non sortis |
  | `--prune` | `#5b3482` | puces de partie, bouton Calmer actif |
  | `--encre` | `#1a0b22` | texte sur cire et citrouille |

- **Polices** : Fraunces (titres, dernier numéro, étiquettes en italique) et Libre Franklin
  (texte, chiffres de la grille).
- **Personnages** : `Citrouille`, `ChauveSouris`, `Fantome`, `Chaudron` sont des composants
  réutilisés par les autres artboards, avec une humeur (`mood`) et une taille (`size`).
- **Durées** : voir les notes du canvas et le panneau de `Main.html` (nouveau numéro 1,6 s,
  annulation 0,8 s, ligne 4,2 s, bingo 5,2 s, partie suivante 3 à 4 s).

## Fichiers

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `Backoffice.html` | E1 · Réglage du thème | E1-champ-theme, E1-options-theme, E1-aide-theme | § 6 E1, RG-25 |
| `Tableau.html` | E2 · Tableau en jeu + E5 | E2-retour, E2-etat-connexion, E2-dernier-numero, E2-scene-ciel, E2-citrouille, E2-precedents, E2-grille, E2-case-derniere, E2-araignee, E2-partie, E2-lots, E2-bingo, E2-feux-follets, E2-brume, E5-bouton-animations | § 6 E2, E5, RG-34, RG-35 |
| `Main.html` | E3 · Démo jouable des moments | E2-ecran, E2-brume, E3-chauves-souris, E3-citrouilles-rire, E3-pluie-bonbons, E3-banniere, E3-fantome, E3-annonce-partie, E4-pause, E4-balai, E4-chaudron | § 6 E3, RG-27 à RG-33 |
| `Etats.html` | E4 · États hors jeu | E4-avant, E4-cimetiere, E4-citrouille-endormie, E4-pause, E4-balai, E4-chaudron, E4-termine, E4-salut, E4-chargement, E4-reconnexion, E4-chauve-souris-endormie, E4-introuvable, E4-fantome-lanterne | § 6 E4, RG-13, RG-15, RG-31 |
| `Bandeau.html` | E6 · Bandeau du jour (jouable) | E6-bandeau, E6-bandeau-mobile, E6-citrouille, E6-etat, E6-dernier-numero, E6-bouton-suivre, E6-araignee, E6-fantome-course, E6-chauves-souris | § 6 E6, RG-39, RG-40 |
| `Palette.html` | Palette | P-proposition, P-variante (non retenue) | § 7 |
| `Citrouille.html`, `ChauveSouris.html`, `Fantome.html`, `Chaudron.html` | Personnages | — | § 6 E2 à E4 |
