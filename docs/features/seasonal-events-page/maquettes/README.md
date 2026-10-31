# Maquettes — Page Événements en agenda et thèmes des lotos

- **Canvas Claude Design** : https://claude.ai/artifact/DCADrDqkqKp6tdcPExVMoV (privé).
  Exporté le 2026-10-31 depuis la version publiée `1793454758-9403`.
- **Aperçus** : `preview/index.html` donne le sommaire des écrans et de leurs zones. Sont
  jouables : `Main.html`, `Mobile.html`, `AgendaV2*.html`, `Calendrier*.html` (bascule
  Calendrier / Liste, filtres, clic sur un jour) et `FicheLoto.html`.
- **Sources** : `sources/` contient les `.dc.html` tels quels. Les SVG et `@keyframes` des
  mascottes (`Papillon` variante `a`, `BouleBingo`, `Citrouille`, `Fantome`, `ChauveSouris`)
  sont à reprendre depuis ces fichiers.

## Lire ces maquettes

- **Charte du site** (`docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`, jetons de
  `src/Website/tailwind.config.js`) pour tout ce qui n'est pas thématique.
- **Identités** (`Identites.html`), couleurs à poser en variables CSS du composant, jamais en dur :

  | Identité | Fond | Accents | Mascotte |
  |---|---|---|---|
  | Halloween « Nuit de citrouille » | `#120a1c` | citrouille `#ff8c2e`, braise `#ffb347`, cire `#f4e8cf`, lilas `#b8a5cf` | citrouille, chauve-souris, fantôme |
  | Bourse « Papier et papillons » | papier `#f4ecdc` ligné (lignes `rgba(16,50,77,.08)` pleines), marge `rgba(229,112,42,.35)` | encre `#10324d`, ciel `#8cc7e6`, signet `#e5702a` | papillon A « Marque-page » |
  | Loto sans thème « Boules et carton » | `#0b2438`, trame de points cyan | bleu asso `#1497d6`, orange `#f0801c`, cyan `#7fd8f5` | boule de bingo numérotée |

- `Main.html` et `Mobile.html` : n'en garder que l'en-tête, « À la une », « Ensuite », « Nos
  rendez-vous » et « Nous trouver ». Leur agenda en lignes est remplacé par `AgendaV2*` (Liste)
  et `Calendrier*` (Calendrier, vue par défaut).
- `Papillons.html` : seule la proposition A est retenue.

## Fichiers

| Fichier `preview/` | Écran | Spec |
|---|---|---|
| `Main.html`, `Mobile.html` | E1 en-tête et À la une, E2 Halloween | § 6 E1–E2, RG-41, RG-42 |
| `AlaUneClassique.html` | E1 À la une classique | RG-41 |
| `Calendrier.html`, `CalendrierMobile.html` | E1 agenda, vue Calendrier (défaut) | RG-43, RG-51 |
| `AgendaV2.html`, `AgendaV2Mobile.html` | E1 agenda, vue Liste | RG-43, RG-50 |
| `FicheLoto.html` | E3 fiche d'un loto Halloween | RG-44 |
| `Bandeau.html` | E4 bandeau de J-7 au jour J | RG-45, RG-46 |
| `Backoffice.html` | E5 thème du loto | RG-44 |
| `Etats.html` | E6 états | — |
| `Accueil.html`, `AccueilMobile.html` | E7 cartes de l'accueil | RG-50 |
| `Identites.html`, `Papillons.html` + personnages | identités et mascottes | RG-48, RG-50 |
