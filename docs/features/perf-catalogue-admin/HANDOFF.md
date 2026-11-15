# Handoff Codex — Performance de la recherche publique et du portail d'administration

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Le visiteur filtre le catalogue public par genre (« Roman ») et feuillette les pages en
moins d'une seconde, sans clignotement. L'administrateur ouvre `/administration` et voit
la structure tout de suite, chaque bloc se remplissant dès que sa donnée arrive (moins de
2 s au total). Les pastilles de navigation arrivent en une seule demande, et listes et
statistiques répondent vite. Quatre cartes jugées non pertinentes quittent le tableau de
bord du portail Catalog et du BackOffice.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-perf-catalogue-admin`
- Branche : `fix/perf-catalogue-admin`, qui contient déjà la conception (spec, maquettes,
  plan, scripts de mesure). La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/perf-catalogue-admin/01-fonctionnel.md` : **la spec, elle fait foi**
   (`RG-155` à `RG-168`, `CA-01` à `CA-11`)
2. `docs/features/perf-catalogue-admin/02-plan.md` : **le plan à exécuter** (16 tâches, 0 à 15)
3. `docs/features/perf-catalogue-admin/maquettes/README.md`, puis `maquettes/preview/index.html`
4. `docs/features/perf-catalogue-admin/perf/README.md` : seed local, scénario de parameter
   sniffing, chiffres avant correctif. C'est la base de mesure de `CA-01` et `CA-03`.
5. Skills du repo : `cqrs-feature`, `dotnet-patterns`, `tdd-workflow`, `xunit-unit-testing`,
   `angular-patterns`, `ui-ux-front-saas`, `local-auth-testing`
6. `docs/bourse-aux-livres/05-administration.md` §1 (tableau de bord mis à jour par la
   conception) et `docs/bourse-aux-livres/07-exigences-non-fonctionnelles.md` (`ENF-08`,
   `ENF-23`, `ENF-24`)

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 5 | `E5-Desktop.html`, `E5-Mobile.html` | `E5-filtre-genre`, `E5-indicateur-rechargement`, `E5-resultats-estompes`, `E5-pagination`, `E5-grille-squelette` |
| 6 | `E1-Charge-Desktop.html` | `E1-carte-disponibles`, `E1-carte-tries`, `E1-carte-vendus`, `E1-carte-jamais-vendus` |
| 7 | `E1-Charge-Desktop.html`, `E1-Etats.html` | `E1-heure-calcul`, `E1-pas-encore-calcule` |
| 9 | `Main.html`, `E1-Charge-Desktop.html` | `E2-pastille-squelette`, `E2-pastille`, `E1-tuiles-attention` |
| 10 | `Main.html` | `E1-carte-isbndb-squelette` |
| 11 | `E3-Desktop.html` | `E3-recherche`, `E3-pagination` |
| 12 | `E4-Desktop.html` | `E4-onglets` |
| 13 | `Main.html`, `E1-Mobile.html` | `E1-entete`, `E2-selecteur-mobile` |
| 14 | `Main.html`, `E1-Etats.html`, `E3-Desktop.html`, `E3-Mobile.html`, `E4-Desktop.html`, `E4-Mobile.html` | `E1-carte-squelette`, `E1-alertes-squelette`, `E1-carte-isbndb-squelette`, `E2-pastille-squelette`, `E1-bloc-erreur`, `E1-bouton-reessayer`, `E1-rafraichissement`, `E3-ligne-squelette`, `E3-liste-estompee`, `E3-indicateur-rechargement`, `E3-bloc-erreur`, `E4-bloc-squelette`, `E4-graphique-squelette` |

Largeurs de vérification : 390 et 1440 px. Contrôler aussi avec un réseau ralenti
(DevTools « Fast 4G ») pour voir squelettes et estompages.

## Spécifique à cette feature

- **Constantes non négociables** :
  - recherche publique sous 1 s côté serveur, y compris `genre=Roman` juste après
    `genre=Amour` (`CA-01`) ;
  - au plus 8 appels sans doublon à l'ouverture de `/administration` (`CA-03`) ;
  - pastilles en une demande de moins de 1 s ;
  - « Titres jamais vendus » avec au plus 15 min de retard (Worker toutes les 10 min) ;
  - pastilles et indicateurs courants avec au plus 1 min de retard ;
  - statistiques sous 2 s.
- **Mêmes chiffres (`RG-165`)** : aucune valeur attendue d'un test existant ne change,
  sauf la suppression des champs de `RG-168`. Toute réécriture de requête prouve
  l'égalité par un test.
- **Interdits** :
  - aucun trigger ni procédure stockée pour tenir des compteurs (`ENF-24`) ;
  - aucun cache sur l'exclusion des fiches en cours de scan, qui reste stricte et sans
    délai ;
  - aucun hexadécimal des maquettes en dur : uniquement les variables CSS existantes ;
  - ne pas toucher au rendu chargé des écrans hors des états de chargement, sauf le
    retrait des quatre cartes.
- **Tests sur SQLite** : rien de propre à SQL Server dans un chemin commun. Le genre passe
  par `EF.Constant` (portable). Un éventuel `OPTION (RECOMPILE)` n'est appliqué que sous
  `Database.IsSqlServer()`.
- **Migrations** : deux migrations additives, `OptimizeCatalogAndAdminReadIndexes` et
  `AddAdminDashboardSnapshot`. Aucune migration appliquée à distance.
- **Base locale** : le volume SQL Aspire du checkout principal contient peut-être déjà le
  seed synthétique (ISBN `9781000…`). Le worktree isolé a son propre volume : y lancer
  `perf/01-seed-catalogue.sql`, puis le nettoyer avec `perf/99-nettoyage.sql` si besoin.
- **Azure / infra** : lecture seule autorisée (App Insights `vpd-api-appi-dev-fc`, groupe
  `rg-vpd-dev`) pour comparer avant/après. Aucun déploiement, aucun `az` en écriture,
  aucune modification Bicep.

## PR

- Titre : `fix(perf): fast genre search and responsive admin portal`
- « Risks and follow-up » :
  - conteneur API à 0,5 vCPU / 1 Go (mémoire actuelle à 443 Mo en médiane pour 512 Mo), à
    décider par le responsable ;
  - écart à vérifier entre Bicep (S1, `NEXT.md` L0-7) et Azure (S0 déployé) ;
  - le lazy loading du portail (tâche 13, étape 2) peut être livré séparément s'il
    déborde ;
  - après déploiement, mesurer sur 7 jours les p90 des endpoints du tableau de
    `perf/README.md` (`CA-08`) ;
  - le premier calcul de « Titres jamais vendus » apparaît jusqu'à 10 min après le
    déploiement du Worker.
