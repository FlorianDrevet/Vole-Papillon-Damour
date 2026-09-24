# Maquettes — Signalement « livre introuvable »

Maquettes de la feature décrite dans
[`../../11-signalement-livre-introuvable.md`](../../11-signalement-livre-introuvable.md)
(plan d'implémentation : [`../../plan/07-signalement-livre-introuvable.md`](../../plan/07-signalement-livre-introuvable.md)).

- **Canvas Claude Design (source de vérité, éditable)** :
  <https://claude.ai/artifact/KDX9jHZrcRnb2H6qGccnDZ> — deux pages, « Membre — Ma sélection »
  et « Administration ».
- **Archive autonome** : [`../signalement-introuvable.zip`](../signalement-introuvable.zip)
  (même contenu que ce dossier).
- **Aperçu hors ligne** : ouvrir [`preview/index.html`](preview/index.html) dans un
  navigateur. Chaque écran est un HTML statique autonome (styles en ligne, polices Google
  Fonts), sans le runtime du canvas.
- **Sources du canvas** : `sources/*.dc.html` + `sources/canvas.json` (format Claude Design ;
  nécessitent le runtime `support.js` du canvas pour s'afficher, lisez plutôt `preview/`).

Convention visuelle : [`../catalogue/V2-CONVENTION.md`](../catalogue/V2-CONVENTION.md)
(Newsreader / Libre Franklin / IBM Plex Mono, papier `#f7fbfe`, encre `#072b45`, bleu
`#0c6ea6`, orange `#f0801c` / `#dc6412`). Les maquettes fixent **mise en page, libellés et
états** ; l'implémentation Angular réutilise les variables CSS existantes du Catalog
(`--catalog-*`) plutôt que les hexadécimaux en dur.

## Correspondance écran ↔ spec

| Fichier (`preview/`) | Écran | Spec |
|---|---|---|
| `Main.html` | E1 — Ma sélection desktop : bouton « Je ne l'ai pas trouvé », tous les états de ligne (disponible, signalé, retrouvé, retiré, rare, annoncé, acheté) et nouveaux filtres | §6.2, §6.4, §6.6 |
| `AvantApres.html` | Référence avant / après sur une ligne (ce qui disparaît) | §6.1, RG-76 |
| `ConfirmationSignalement.html` | E2 — Modale de confirmation (desktop) | §6.3 |
| `SignalementEnvoye.html` | E3 — Ligne juste après l'envoi + toast | §6.3, §6.4 |
| `SelectionMobile.html` | E1/E3 — Ma sélection à 390 px | §6, CA-11 |
| `ConfirmationMobile.html` | E2 — Feuille de confirmation mobile | §6.3 |
| `ConnexionRequise.html` | Visiteur non connecté : invitation à se connecter | §6.5, RG-67 |
| `AdminIntrouvables.html` | E4/E5 — Barre latérale (entrée + compteur) et file « À vérifier » | §7.2, §7.3 |
| `AdminIntrouvablesTraites.html` | E5 — Onglet « Traités » (historique) | §7.3 |
| `AdminIntrouvablesMobile.html` | E5 — File à 390 px | CA-11 |
| `AdminRetrait.html` | E6 — Retirer du stock (saisie des exemplaires retrouvés) | §7.4, RG-72 |
| `AdminRetrouve.html` | E6 — Confirmer « Retrouvé » | §7.4 |
| `AdminSansSuite.html` | E6 — Classer sans suite | §7.4 |
| `AdminRetraitRare.html` | E6 — Retirer une fiche rare | §7.4, Q-SIG-5 |
| `AdminTableauDeBord.html` | E7 — Tuile « À vérifier en rayon » | §7.5 |
| `AdminFiche.html` | E8 — Encadré « Signalée introuvable » + mouvement « suite à signalement » | §7.6 |

Les données affichées (titres, dates, compteurs) sont des exemples.
