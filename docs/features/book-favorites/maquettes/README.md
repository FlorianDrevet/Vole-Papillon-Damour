# Maquettes — Coups de cœur et suggestions

- Canvas Claude Design : https://claude.ai/artifact/B8E7vxXGYMmvF6uRJCsaNw (version
  `1794613217-1d59`, exportée le 2026-11-14).
- Ouvrir [`preview/index.html`](preview/index.html) dans un navigateur : sommaire des
  22 écrans, tailles et zones `data-zone`. Chaque page de `preview/` est autonome.
- `sources/` : les `.dc.html` et `canvas.json` du canvas, tels quels (gabarits
  `sc-for` / `sc-if`). `preview/` en est le rendu, données d'exemple comprises.

## Règles de transposition

- Convention visuelle : [`docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`](../../../bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md).
  Réutiliser les variables CSS et composants existants du catalogue (`app-book-card`,
  pastilles de disponibilité, en-têtes de section) ; **pas d'hexadécimal en dur** dans les
  composants.
- **Nouveaux jetons** introduits par la feature, à déclarer une fois comme variables :

  | Rôle | Valeur | Où |
  |---|---|---|
  | Cœur, accent coup de cœur | `#c8364e` | cœurs, bouton « Coup de cœur », curseur sélectionné, lien de menu actif |
  | Texte sur fond blanc, bouton coup de cœur inactif | `#a3263e` | libellé « Coup de cœur », « Ajouter », « J'ai aimé » |
  | Cœur vide | `#c9d6e1` | contour des cœurs non remplis |
  | Fond doux coup de cœur | `#fdf0f2` / bordure `#f1c9d1` | bouton « Retirer » |
  | Bandeau scanette « a déjà plu » | `#d6304f` | E10 uniquement, distinct du verdict `#b02a33` |

- Les textes des maquettes sont les libellés à reprendre **mot pour mot**. Les titres de
  livres, prénoms, chiffres et l'adresse `[ADRESSE DE LA BOURSE]` sont des exemples.
- Les sections grisées « Sections existantes, inchangées » de E1 ne sont pas à refaire :
  elles indiquent seulement que l'existant descend sous « Pour vous ».
- E8 (e-mail) : tableaux et styles en ligne comme le gabarit des alertes
  (`docs/features/book-alert-email-template/`). Les cœurs y sont du **texte**
  (`♥` / `♡` colorés) suivi de « N cœurs sur 5 », jamais une image.

## Correspondance

| Fichier `preview/` | Écran | Zones | Spec (§, RG) |
|---|---|---|---|
| `E1-Desktop.html`, `E1-Mobile.html` | E1 Accueil | `E1-section-pour-vous`, `E1-carte`, `E1-coeurs`, `E1-raison`, `E1-lien-toutes`, `E1-lien-menu`, `E1-defilement` | §6 E1, RG-143, RG-144 |
| `Main.html` (desktop, sans compte), `E2-Mobile.html` (membre) | E2 Trouver mon prochain livre | `E2-entete`, `E2-lien-menu`, `E2-bandeau-navigateur`, `E2-bouton-creer-compte`, `E2-lien-connexion`, `E2-replier-bandeau`, `E2-coups-de-coeur`, `E2-compteur`, `E2-bouton-ajouter`, `E2-coup-de-coeur`, `E2-pastille-disponibilite`, `E2-calcul-en-cours`, `E2-retirer`, `E2-suggestions`, `E2-nombre-suggestions`, `E2-curseur-exigence`, `E2-carte-suggestion`, `E2-coeurs`, `E2-raison`, `E2-preference-email` | §6 E2, RG-134, RG-136 à RG-140, RG-146 |
| `E3-Desktop.html`, `E3-Mobile.html` | E3 Ajouter un coup de cœur | `E3-panneau`, `E3-fermer`, `E3-recherche`, `E3-aide-edition`, `E3-resultats-bourse`, `E3-resultats-externes`, `E3-resultat`, `E3-bouton-ajouter-edition`, `E3-voir-autres-editions` | §6 E3, RG-130, RG-132, RG-133, RG-117, RG-118 |
| `E4-Desktop.html`, `E4-Mobile.html` | E4 Création de compte et reprise | `E4-etape-compte`, `E4-recap-liste`, `E4-consentement-email`, `E4-bouton-continuer`, `E4-message-reprise`, `E4-message-plafond` | §5 P2, RG-141, RG-146 |
| `E5-Desktop.html`, `E5-Mobile.html` | E5 Fiche livre | `E5-actions`, `E5-bouton-coup-de-coeur`, `E5-confirmation` | RG-134 |
| `E6-Desktop.html`, `E6-Mobile.html` | E6 Mes achats | `E6-explication`, `E6-ligne-achat`, `E6-bouton-jai-aime` | RG-142 |
| `E7-Desktop.html`, `E7-Mobile.html` | E7 Compte et données | `E7-preference-suggestions`, `E7-explication-suggestions`, `E7-preference-email`, `E7-date-consentement`, `E7-exigence` | RG-138, RG-146, RG-154 |
| `E8-Email600.html`, `E8-Email390.html` | E8 E-mail avant la bourse | `E8-texte-apercu`, `E8-objet`, `E8-entete`, `E8-accroche`, `E8-encart-bourse`, `E8-section-nouveautes`, `E8-carte-livre`, `E8-coeurs`, `E8-raison`, `E8-lien-fiche`, `E8-section-de-retour`, `E8-etiquette-tres-demande`, `E8-non-reservation`, `E8-bouton-suggestions`, `E8-pied`, `E8-raison-envoi`, `E8-desabonnement` | RG-145 à RG-152 |
| `E9-Desktop.html`, `E9-Mobile.html` | E9 Administration, e-mail avant la bourse | `E9-lien-menu`, `E9-interrupteur`, `E9-prochain-envoi`, `E9-date-envoi`, `E9-destinataires`, `E9-bouton-test`, `E9-bouton-suspendre`, `E9-apercu`, `E9-compte-rendu` | RG-153, RG-159 |
| `E10-Garder.html`, `E10-Trop.html`, `E10-HorsLigne.html` | E10 Scanette, écran de résultat | `E10-bandeau-a-deja-plu`, `E10-nombre-lecteurs`, `E10-ligne-coups-de-coeur`, `E10-fraicheur` | RG-155 à RG-157 |
| `E11-Etats.html` | E11 États | `E11-encart-entree`, `E11-ne-plus-afficher`, `E11-e2-vide`, `E11-calcul-en-cours`, `E11-aucune-suggestion`, `E11-suggestions-desactivees`, `E11-plafond-liste`, `E11-livre-introuvable`, `E11-stockage-indisponible`, `E11-annuler-retrait`, `E11-email-sans-nouveautes` | RG-131, RG-134, RG-135, RG-138, RG-139, RG-144, RG-151, RG-154 |
