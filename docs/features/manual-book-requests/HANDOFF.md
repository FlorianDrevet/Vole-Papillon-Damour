# Handoff Codex — Demandes de livres saisies par l'administration

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

L'administration (rôle `Administration`) enregistre des personnes sans compte (nom,
prénom, téléphone, accord) et les livres qu'elles cherchent. Au tri, un exemplaire
correspondant est mis de côté : verdict « À mettre de côté », coordonnées affichées en
ligne, aucun stock ni alerte membre. L'administration le marque ensuite donné ou refusé ;
un refus le remet en stock et alerte immédiatement les membres qui le suivent. Surfaces :
backend (.NET), `src/Catalog` (administration), `src/Scan` (scanette), Worker.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-manual-book-requests`
- Branche : `feat/manual-book-requests`, qui contient déjà la conception (spec, maquettes,
  plan). La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/manual-book-requests/01-fonctionnel.md` : **la spec, elle fait foi**
   (règles `RG-77` à `RG-89`)
2. `docs/features/manual-book-requests/02-plan.md` : **le plan à exécuter** (25 tâches,
   0 à 24), y compris ses « Points d'attention » et « Arbitrages »
3. `docs/features/manual-book-requests/maquettes/README.md`, puis
   `maquettes/preview/index.html`
4. Skills du repo : `cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `angular-patterns`, `ui-ux-front-saas`
5. Le slice « livre introuvable », modèle à calquer partout :
   `Domain/NotFoundReportAggregate/`, `Application/NotFoundReports/`,
   `Api/Controllers/NotFoundReportAdministrationController.cs`,
   `src/Catalog/src/app/features/administration/not-found-reports/`
6. `Application/Books/Commands/ScanBook/ScanBookCommandHandler.cs`,
   `Infrastructure/Services/BookAlerts/BookAlertOutbox.cs`,
   `src/Scan/src/app/scanner/scanner.component.*` : les trois fichiers existants les plus
   sensibles de la feature

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 16 | `Main.html`, `AdminEnRecherche.html`, `AdminEtats.html`, `AdminDemandesMobile.html` | `E1-*`, `E2-*`, `E5-toast` |
| 17 | `AdminDemandeur.html`, `AdminDemandeurMobile.html`, `AdminEtats.html` | `E3-*` |
| 18 | `AdminAjoutLivre.html`, `AdminAjoutLivreMobile.html`, `AdminEtats.html` | `E4-*` |
| 19 | `AdminDonne.html`, `AdminRefus.html`, `AdminRefusMobile.html`, `AdminEtats.html` | `E5-*`, `E2-dialogue-annulation-trouvee` |
| 20 | `AdminTableauDeBord.html`, `AdminTableauDeBordMobile.html` | `E7-*` |
| 21 | `ScanMiseDeCote.html`, `ScanMiseDeCoteHorsLigne.html`, `ScanCoordonneesChargement.html` | `E6-verdict-mise-de-cote`, `E6-encart-*`, `E6-bouton-*`, `E6-signal-rare`, `E6-recherche-par` |
| 22 | `ScanFinSession.html` | `E6-recap-*` |

Le détail des zones par tâche est dans les lignes `**Maquette :**` du plan.

Largeurs de vérification : 390 et 1440 px pour l'administration (E1–E5, E7) ; **390 px
seulement** pour la scanette (E6, application téléphone).

## Spécifique à cette feature

- **Constantes non négociables** :
  - effacement des coordonnées **90 jours** après la clôture de la dernière demande
    (`RG-89`) ;
  - mention d'ancienneté au-delà de **14 jours** (`QM-2`) ;
  - alertes de refus dues **immédiatement** (`DueAt = now`), sans `AlertDelayMinutes` ;
  - `BookMovementType.ManualRequestHold` ajouté **en dernier** dans l'enum (valeur 7) ;
  - couleur du verdict `#0d5c63`, fond `#e3f1f2`, déclarés en variables CSS.
- **Interdits** :
  - aucune donnée personnelle de demandeur (nom, prénom, téléphone) dans le delta
    catalogue, IndexedDB, les exports de diagnostic de la scanette ou les logs
    (`RG-80`) ; seul `GET /scan/manual-requests/candidate` les renvoie, pour l'ISBN
    scanné ;
  - ne pas toucher à `ReassignSessionModeCommandHandler` (le plan le fige par un test) ;
  - ne pas modifier le code du domaine donné à la tâche 1 au-delà de ce que le plan
    décrit : il a été compilé et testé tel quel ;
  - aucun envoi de SMS ni d'appel, aucune visibilité sur le site public.
- **Flags / configuration** : aucun flag. La section admin n'est visible que du rôle
  `Administration`.
- **Azure / infra** : aucun déploiement, aucune commande `az` en écriture. La migration
  `AddManualBookRequests` est générée et relue, **pas appliquée** à une base distante.

## PR

- Titre : `feat(manual-requests): track books wanted by people without an account`
- « Risks and follow-up » :
  - migration `AddManualBookRequests` à appliquer au prochain `Books runtime - deploy`
    (`run_migrations: true`), puis déployer Catalog et Scan ;
  - alertes de refus : envoi immédiat best effort depuis l'API, sinon au Sweep horaire ;
  - registre des traitements RGPD de l'association à mettre à jour hors code ;
  - arbitrages du plan (prélèvement, « Recherché par » inchangé, nombre de membres
    prévenus non mémorisé) et tout écart constaté.
