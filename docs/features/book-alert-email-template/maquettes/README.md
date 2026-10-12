# Maquettes — Mail d'alerte livres

- Canvas Claude Design : https://claude.ai/artifact/Nj46cgkoVEM5S19T33m3kK (privé, exporté
  le 2026-10-12 depuis la version publiée `1791791348-6376`).
- Ouvrir `preview/index.html` dans un navigateur : sommaire des écrans et de leurs zones.
  Chaque `preview/*.html` est autonome. Le logo y pointe sur
  `https://volepapillondamour.fr/icons/vpd_icon.png`, la valeur de `BookAlerts:Email:LogoUrl`.
- `sources/` : fichiers `.dc.html` et `canvas.json` du canvas, tels quels (le logo y est un
  `/_blob/…` propre au canvas).

## Lire ces maquettes pour un e-mail

- Ce sont des rendus d'e-mail, pas des pages du catalogue. La charte est celle de
  `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`, mais un e-mail ne peut
  pas charger les feuilles de style du catalogue : les couleurs sont reprises **en valeurs
  littérales dans des styles en ligne**, centralisées dans une palette du code qui génère
  le mail.
- Les maquettes utilisent `div` + flex pour rester éditables. L'implémentation utilise des
  **tableaux de présentation** (`role="presentation"`) et des styles en ligne, largeur
  600 px, pour Outlook.
- La règle de marque = trois cellules pleines (`#7fd8f5`, `#1497d6`, `#f0801c`), jamais un
  dégradé.
- Polices : Newsreader → Georgia, Libre Franklin → Arial, IBM Plex Mono → Courier New.
- Les couvertures dessinées sont des **images d'exemple** : l'implémentation affiche
  l'`<img>` de la couverture réelle, 88×132 (72×108 sur mobile), `alt` = titre du livre.
- La ligne « Boîte de réception » au-dessus du mail illustre le **texte d'aperçu** : il
  n'est pas visible dans le corps du message.
- Données d'exemple : livres, dates, horaires et adresse.

## Correspondance

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `Main.html` | E1 · Alerte, un livre — 600 px | `E1-texte-apercu` `E1-entete` `E1-regle-marque` `E1-accroche` `E1-encart-bourse` `E1-date-bourse` `E1-horaires` `E1-adresse` `E1-carte-livre` `E1-couverture` `E1-titre-livre` `E1-auteur` `E1-edition` `E1-quantite` `E1-lien-fiche` `E1-non-reservation` `E1-bouton-liste` `E1-pied` `E1-lien-site` `E1-raison-envoi` `E1-desabonnement` | E1 ; RG-90, RG-91, RG-92, RG-94, RG-95, RG-96 |
| `E1-Mobile.html` | E1 · Alerte, un livre — mobile | mêmes zones que `Main.html` | E1 ; RG-98 (≤ 390 px, bouton pleine largeur) |
| `E2-Desktop.html` | E2 · Plusieurs livres, deux bourses — 600 px | `E2-texte-apercu` `E2-accroche` `E2-groupe-bourse` `E2-encart-bourse` `E2-carte-livre` `E2-groupe-bourse-suivant` `E2-non-reservation` `E2-raison-envoi` | E2 ; RG-90, RG-93, RG-94, RG-95, RG-96 |
| `E2-Mobile.html` | E2 · Plusieurs livres, deux bourses — mobile | mêmes zones que `E2-Desktop.html` | E2 ; RG-98 |
| `E3-Desktop.html` | E3 · Alerte mixte avec livres rares — 600 px | `E3-encart-disponible` `E3-prochaine-ouverture` `E3-section-rares` `E3-ligne-rare` | E3 ; RG-91, RG-97 |
| `E3-Mobile.html` | E3 · Alerte mixte avec livres rares — mobile | mêmes zones que `E3-Desktop.html` | E3 ; RG-98 |
| `E4-Etats.html` | E4 · États et cas limites | `E4-sans-couverture` `E4-couverture-remplacement` `E4-images-bloquees` `E4-mode-sombre` `E4-sans-prenom` `E4-disponible-sans-ouverture` `E4-rares-seuls` `E4-accroche-rares` `E4-version-texte` | E4 ; RG-90, RG-91, RG-92, RG-97, RG-98, RG-99 |
