# Handoff Codex — Template HTML des e-mails d'alerte livres

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Le membre qui reçoit une alerte de liste de recherche (livre disponible ou annoncé pour une
bourse) obtient un e-mail aux couleurs du catalogue : date, horaires et adresse de la bourse
en tête, une carte par livre avec sa couverture, la mention de non-réservation, un bouton
vers sa liste de recherche et un pied de message. La version texte porte le même contenu.
Surface unique : le message envoyé par le Worker via Azure Communication Services.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-book-alert-email-template`
- Branche : `feat/book-alert-email-template`, qui contient déjà la conception (spec,
  maquettes, plan). La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/book-alert-email-template/01-fonctionnel.md` : **la spec, elle fait foi** (`RG-90` à `RG-99`)
2. `docs/features/book-alert-email-template/02-plan.md` : **le plan à exécuter** (tâches 0 à 7).
   Son code a été compilé et testé avant le handoff : reprends-le tel quel.
3. `docs/features/book-alert-email-template/maquettes/README.md`, puis `maquettes/preview/index.html`
4. Skills du repo concernées : `dotnet-patterns`, `tdd-workflow`, `xunit-unit-testing`
5. `docs/email-branding.md` : habillage existant des e-mails (logo, BIMI), complété en tâche 7

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 4 | `Main.html` | `E1-accroche`, `E1-encart-bourse`, `E1-date-bourse`, `E1-horaires`, `E1-adresse`, `E1-texte-apercu` |
| 4 | `E2-Desktop.html` | `E2-accroche`, `E2-texte-apercu`, `E2-groupe-bourse`, `E2-encart-bourse`, `E2-groupe-bourse-suivant` |
| 4 | `E3-Desktop.html` | `E3-encart-disponible`, `E3-prochaine-ouverture` |
| 4 | `E4-Etats.html` | `E4-disponible-sans-ouverture`, `E4-sans-prenom`, `E4-accroche-rares` |
| 5 | `Main.html`, `E1-Mobile.html` | `E1-entete`, `E1-regle-marque`, `E1-carte-livre`, `E1-couverture`, `E1-titre-livre`, `E1-auteur`, `E1-edition`, `E1-quantite`, `E1-lien-fiche`, `E1-non-reservation`, `E1-bouton-liste`, `E1-pied`, `E1-lien-site`, `E1-raison-envoi`, `E1-desabonnement` |
| 5 | `E2-Desktop.html` | `E2-carte-livre`, `E2-non-reservation`, `E2-raison-envoi` |
| 5 | `E3-Desktop.html` | `E3-section-rares`, `E3-ligne-rare` |
| 5 | `E4-Etats.html` | `E4-sans-couverture`, `E4-couverture-remplacement`, `E4-images-bloquees`, `E4-mode-sombre`, `E4-rares-seuls`, `E4-version-texte` |
| 6 | toutes | contrôle visuel des échantillons générés |

Largeurs de vérification : **680 et 390 px**, pas 1440 : c'est un e-mail de 600 px.
Il n'y a pas d'écran Angular. Le contrôle visuel se fait sur les fichiers HTML générés
par le test `BookAlertEmailSamples` (tâche 6). Les maquettes sont faites en `div`/flex
pour l'édition, l'implémentation en tableaux : compare le rendu, pas le balisage.

## Spécifique à cette feature

- **Constantes non négociables** : largeur 600 px ; libellés et palette de la section
  « Contraintes globales » du plan ; dates et heures à l'heure de Paris ; seules les images
  HTTPS sont affichées ; objet des e-mails et en-têtes de désabonnement en un clic inchangés.
- **Interdits** : JavaScript, formulaires, images de fond et dégradés dans le HTML du mail ;
  refondre l'alerte « livre rare vendu » (hors périmètre, `RG-97` : simples lignes) ;
  modifier les règles d'envoi (délai, regroupement, anti-répétition) ; committer les
  échantillons ou captures de la tâche 6.
- **Flags / configuration** : deux options avec valeurs par défaut de production,
  `BookAlerts:Email:CatalogBaseUrl` et `BookAlerts:Email:WebsiteUrl`. Aucun flag.
- **Compatibilité** : les alertes déjà en file au déploiement n'ont pas les nouveaux champs ;
  elles doivent partir sans couverture ni lien de fiche (point de vigilance 1 du plan).
- **Azure / infra** : aucune modification de `infra/`, aucun déploiement, aucune commande
  `az` en écriture. Pas d'envoi d'e-mail réel pendant l'implémentation.

## PR

- Titre : `feat(book-alerts): branded HTML template for watchlist alert emails`
- « Risks and follow-up » :
  - Gmail ignore les polices web et le mode sombre déclaré (repli Georgia/Arial,
    inversion propre à Gmail) ; Apple Mail et Outlook Mac suivent la maquette.
  - Open Library renvoie une image vierge pour certains ISBN sans couverture : si de telles
    URL sont enregistrées au catalogue, la carte affiche un cadre vide au lieu du bloc
    papillon (donnée du catalogue, hors périmètre).
  - À vérifier après déploiement : un vrai envoi DEV lu dans Gmail web, Gmail mobile et
    Outlook.
