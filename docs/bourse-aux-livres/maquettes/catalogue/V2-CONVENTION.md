# Catalogue V2 — convention visuelle

Cette convention est la référence du catalogue à partir du 6 septembre 2026. Elle
transpose la maquette `Catalogue V2.dc.html` validée dans la revue « Révision maquettes
catalogue » vers l'application Angular `src/Catalog/`.

## Grammaire visuelle

- Shell centré de 1280 px maximum, gouttières de 24 px sur mobile et 44 px sur desktop.
- Logo réel `public/images/papillon_without_back.png`, en-tête clair, règle de marque
  cyan → bleu → orange et menu genre de 302 px avec règle supérieure et ombre courte.
- `Newsreader` pour les titres éditoriaux, `Libre Franklin` pour les textes et contrôles,
  `IBM Plex Mono` pour les libellés, dates, statuts et identifiants.
- Papier `#f7fbfe`, papier secondaire `#e9f4fb`, encre `#041d30` / `#072b45`, bleus
  `#0c6ea6` / `#1497d6`, cyan `#7fd8f5`, orange `#f0801c` / `#f9a93c` / `#dc6412`,
  lignes `#d9e9f4` / `#e2eef7`.
- Panneaux éditoriaux solides, bordures fines, ombres discrètes et transitions courtes.
  Les interactions importantes donnent un retour visuel, respectent le focus clavier et
  réduisent mouvement et animation avec `prefers-reduced-motion`.

## Écrans couverts

- Accueil : question éditoriale, recherche avec genre, compteur API, prochaine bourse,
  ajout agenda, sélections, genres et appel vers l'espace personnel.
- Recherche/catalogue : barre de recherche, filtres latéraux, cartes de livres, pagination
  et périmètre « pas encore reçu » séparé.
- Fiche livre/œuvre : disponibilité actuelle et annoncée séparées, couverture, édition,
  portée d'alerte et prix communiqué sur place.
- Compte : connexion, liste de suivi, retrait unitaire et double confirmation de suppression.
- Administration : cadre latéral, files de travail et désengorgement branché sur l'API.
- Pages légales et footer : même shell, footer association en quatre colonnes et liens locaux.

## Limites fonctionnelles à préserver

Le visuel ne doit pas inventer de stock, de prix, de rôle, de compte, de rapport ou de
référentiel externe. L'API catalogue actuelle expose les lectures publiques, la watchlist
membre et la lecture administration du dead-stock. Les rubriques sessions de scan,
catalogue/métadonnées, comptes & rôles, rapports et réglages peuvent apparaître dans le
cadre V2 comme zones explicitement « API à connecter », mais ne doivent pas être rendues
interactives avant l'existence de leurs contrats typés.
