# Maquettes — Demandes manuelles

Maquettes de la feature décrite dans [`../01-fonctionnel.md`](../01-fonctionnel.md).

- **Canvas Claude Design (source de vérité, éditable)** :
  <https://claude.ai/artifact/3fvLJibWN6627hDpbhaRW5> — deux pages, « Administration » et
  « Scanette ». Les notes du canvas renvoient aux règles `RG-77` à `RG-89`.
- **Aperçu hors ligne** : ouvrir [`preview/index.html`](preview/index.html) dans un
  navigateur. Chaque écran est un HTML statique autonome (styles en ligne, polices Google
  Fonts), sans le runtime du canvas. **C'est ce dossier que l'implémentation lit**, jamais
  le lien claude.ai.
- **Sources du canvas** : `sources/*.dc.html` + `sources/canvas.json` (format Claude
  Design ; nécessitent le runtime du canvas, lisez plutôt `preview/`).

Convention visuelle :
[`../../../bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`](../../../bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md)
(Newsreader / Libre Franklin / IBM Plex Mono, papier `#f7fbfe`, encre `#072b45`, bleu
`#0c6ea6`, orange `#f0801c` / `#dc6412`). Les maquettes fixent **mise en page, libellés et
états** ; l'implémentation réutilise les variables CSS existantes (Catalog `--catalog-*`,
Scan) plutôt que les hexadécimaux en dur.

**Une seule couleur nouvelle** : le verdict « À mettre de côté » de la scanette, pétrole
`#0d5c63` (fond clair associé `#e3f1f2`). Elle s'ajoute aux couleurs de verdict de
[`../../../bourse-aux-livres/maquettes/README.md`](../../../bourse-aux-livres/maquettes/README.md)
et se déclare comme variable, au même endroit que les autres verdicts.

Les données affichées (noms, téléphones, titres, dates) sont **fictives**.

## Correspondance écran ↔ spec

Les artboards mobiles reprennent les zones de leur équivalent desktop (même écran,
mise en page empilée) ; ils ne portent que les zones propres au mobile.

| Fichier (`preview/`) | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `Main.html` | E1 barre latérale + E2 onglet « Trouvés à remettre » (desktop) | `E1-barre-laterale`, `E1-entree-demandes-manuelles`, `E1-badge-a-remettre`, `E2-bouton-nouveau-demandeur`, `E2-onglets`, `E2-onglet-trouves`, `E2-onglet-en-recherche`, `E2-onglet-closes`, `E2-recherche`, `E2-ligne-trouvee`, `E2-origine-trouvaille`, `E2-demandeur`, `E2-lien-demandeur`, `E2-alerte-anciennete`, `E2-bouton-donne`, `E2-bouton-refuse`, `E2-encart-confidentialite` | §7 E1, E2 · RG-77, RG-79, RG-81, RG-82, RG-89 |
| `AdminEnRecherche.html` | E2 onglet « En recherche » (desktop) | `E2-ligne-en-recherche`, `E2-mention-deja-en-stock`, `E2-bouton-prelever`, `E2-bouton-annuler-demande` | §7 E2 · RG-82, RG-86 |
| `AdminDemandeur.html` | E3 fiche demandeur (desktop) | `E3-retour-liste`, `E3-formulaire-coordonnees`, `E3-champ-nom`, `E3-champ-prenom`, `E3-champ-telephone`, `E3-case-accord`, `E3-bouton-enregistrer`, `E3-liste-demandes`, `E3-bouton-ajouter-livre`, `E3-demande-trouvee`, `E3-statut-demande`, `E3-demande-close`, `E3-supprimer-demandeur` | §7 E3 · RG-78, RG-87, RG-88, RG-89 |
| `AdminAjoutLivre.html` | E4 ajout d'un livre (dialogue, desktop) | `E4-dialogue-ajout`, `E4-recherche-referentiel`, `E4-resultats`, `E4-portee`, `E4-encart-deja-en-stock`, `E4-bouton-prelever`, `E4-bouton-ajouter` | §7 E4 · RG-46, RG-47, RG-78, RG-82 |
| `AdminDonne.html` | E5 confirmation « Donné » | `E5-dialogue-donne`, `E5-bouton-confirmer-donne` | §7 E5 · RG-83 |
| `AdminRefus.html` | E5 « Refusé » : alertes, mise à disposition, suite | `E5-dialogue-refus`, `E5-avertissement-alertes`, `E5-mise-a-disposition`, `E5-choix-suite`, `E5-bouton-confirmer-refus` | §7 E5 · RG-84 |
| `AdminTableauDeBord.html` | E7 tuile « livres à remettre » | `E7-tuile-a-remettre`, `E7-mention-anciennete`, `E7-lien-liste` | §7 E7 · QM-2 |
| `AdminEtats.html` | États E2–E5 : vide, chargement, erreur, Closes, messages, erreurs E4, accord manquant, confirmations de suppression et d'annulation, refus en « prochaine bourse » | `E2-etat-vide`, `E2-etat-chargement`, `E2-etat-erreur`, `E2-ligne-close`, `E2-demandeur-efface`, `E2-dialogue-annulation-trouvee`, `E3-erreur-accord`, `E3-dialogue-suppression`, `E4-erreur-referentiel`, `E4-erreur-doublon`, `E4-edition-seule`, `E5-toast`, `E5-variante-annonce` | §7 E2–E5 · RG-84, RG-86, RG-87, RG-88, RG-89 |
| `AdminDemandesMobile.html` | E1/E2 à 390 px (menu, cartes, téléphone cliquable) | `E1-menu-mobile`, `E2-lien-appeler` | §7 E1, E2 |
| `AdminDemandeurMobile.html` | E3 à 390 px | zones de `AdminDemandeur.html` | §7 E3 |
| `AdminAjoutLivreMobile.html` | E4 à 390 px (plein écran) | zones de `AdminAjoutLivre.html` | §7 E4 |
| `AdminRefusMobile.html` | E5 « Refusé » en feuille mobile | zones de `AdminRefus.html` | §7 E5 |
| `AdminTableauDeBordMobile.html` | E7 à 390 px | zones de `AdminTableauDeBord.html` | §7 E7 |
| `ScanMiseDeCote.html` | E6 verdict « À mettre de côté », en ligne | `E6-verdict-mise-de-cote`, `E6-encart-demandeur`, `E6-recherche-par`, `E6-bouton-ecarter`, `E6-bouton-mettre-de-cote` | §7 E6 · RG-79, RG-80, RG-81 |
| `ScanMiseDeCoteHorsLigne.html` | E6 verdict hors ligne (aucune donnée personnelle) | `E6-encart-hors-ligne` | §7 E6, §10 · RG-80 |
| `ScanCoordonneesChargement.html` | E6 coordonnées en chargement + signal livre rare en complément | `E6-encart-chargement`, `E6-signal-rare` | §7 E6 · RG-79, RG-80 |
| `ScanFinSession.html` | E6 récapitulatif de fin de session | `E6-recap-mis-de-cote`, `E6-recap-non-attribue` | §7 E6, §10 · RG-85 |

La scanette est une application téléphone : elle n'a pas d'artboard 1440 px. Seul son
écran de résultat (et le récapitulatif de fin de session) change ; le reste reprend
[`../../../bourse-aux-livres/maquettes/scanette/`](../../../bourse-aux-livres/maquettes/scanette/)
à l'identique.
