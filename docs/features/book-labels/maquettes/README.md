# Maquettes — Étiquettes des livres sans ISBN

- Canevas Claude Design (relecture) : https://claude.ai/artifact/AEzgNYCDYr9KuxomHmk3Nx
- Pages autonomes : [`preview/index.html`](preview/index.html) (sommaire et zones `data-zone`).
- Sources : [`sources/`](sources/) (`*.dc.html` et `canvas.json`, tels qu'exportés depuis le canevas).
- Convention visuelle : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` (variables CSS existantes).

| Fichier `preview/` | Écran | Zones | Spec |
|---|---|---|---|
| `Main.html` | E1 · Accueil de la scanette (modifié) | E1-entree-ajouter-sans-isbn, E1-entree-imprimante | §6 E1 |
| `E2-FormulaireSansIsbn.html` | E2 · Formulaire livre sans ISBN (nouveau) | 10 zones E2-* | §6 E2, RG-170, RG-180 |
| `E3-FicheCreee.html` | E3 · Fiche créée, impression proposée (nouveau) | E3-code-chiffres, E3-bouton-imprimer, E3-bouton-plus-tard | §6 E3 |
| `E4-ResultatIsbn.html` | E4 · Résultat d'un ISBN, réimpression (modifié) | E4-bouton-reimprimer-isbn, E4-isbn-lisible | §6 E4, RG-175 |
| `E5-FicheRare.html` | E5 · Fiche livre rare, impression (modifié) | E5-bouton-imprimer, E5-bouton-plus-tard, E5-prix-ferme | §6 E5, RG-179 |
| `E6-Imprimante.html` | E6 · Imprimante d'étiquettes (nouveau) | E6-etat, E6-appareil, E6-bandeau-simulation, E6-bouton-connecter, E6-bouton-tester | §6 E6, RG-178 |
| `E7-ApercuImpression.html` | E7 · Aperçu et impression (nouveau) | E7-apercu-etiquette, E7-nombre-etiquettes, E7-bouton-imprimer, E7-statut-simulation, E7-bouton-annuler | §6 E7, RG-179 |
| `E8-CaisseCodeInterne.html` | E8 · Caisse, code interne reconnu (modifié) | E8-ligne-code-interne | §6 E8, RG-172 |
| `E9-ListeAdmin.html` | E9 · Livres sans ISBN, liste admin (nouveau) | E9-bouton-ajouter | §6 E9, RG-183 |
| `E10-FormulaireAdmin.html` | E10 · Formulaire admin (nouveau) | aucune zone dans le canevas | §6 E10, RG-183 |
| `E11-FichePublique.html` | E11 · Fiche publique (modifié) | aucune zone dans le canevas | §6 E11, RG-181 |
| `E11-FichePubliqueMobile.html` | E11 · Fiche publique, mobile | aucune zone dans le canevas | §6 E11, RG-181 |

Les zones non implémentées et leur raison sont dans [`../02-plan.md`](../02-plan.md#zones-non-implémentées).
