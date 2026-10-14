# Handoff Codex — Tableau de loto en direct

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

Le jour du loto :
- **Animateur** : il projette au backoffice un tableau lisible de loin sous la lumière de
  scène. Deux palettes, sombre et claire, se basculent avec la touche P. Les contrôles
  restent discrets. Des annonces plein écran couvrent le numéro tiré, la pause et l'avant
  début (ces deux-là avec un QR code), ainsi que la fin du loto.
- **Joueurs** : ils suivent un tableau mobile fiable en direct.
- **Site** : il annonce l'événement du jour par un bandeau sur toutes les pages, un encart
  sur la fiche et l'adresse courte `/loto`.

Le direct SSE est refait pour fonctionner avec plusieurs réplicas de l'API.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-loto-live-board`
- Branche : `feat/loto-live-board`, qui contient déjà la conception (spec, maquettes, plan). La PR
  livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/loto-live-board/01-fonctionnel.md` : **la spec, elle fait foi** (RG-01 à RG-24)
2. `docs/features/loto-live-board/02-plan.md` : **le plan à exécuter** (tâches 0 à 12). Le
   code y est complet et a déjà été compilé, testé et comparé aux maquettes : le reprendre
   tel quel, tâche par tâche.
3. `docs/features/loto-live-board/maquettes/README.md` (dont le tableau des variables des
   palettes A et B), puis `maquettes/preview/index.html`
4. Skills du repo : `.github/skills/cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `angular-patterns`, `ui-ux-front-saas`, `tdd-workflow`, `local-auth-testing` (recette
   BackOffice avec le persona Administration)
5. `.github/memory/03-domain-model.md` et `10-api-endpoints.md` : sections à mettre à jour
   à la tâche 12

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 7 | `Main.html`, `E1-Claire.html` | `E1-tableau`, `E1-grille`, `E1-case-*`, `E1-partie`, `E1-ligne`, `E1-compteur-fois`, `E1-type-partie`, `E1-dernier-numero`, `E1-precedents`, `E1-lots`, `E1-bingo` |
| 7 | `E3-Regie.html` | `E3-barre-regie`, `E3-bouton-*`, `E3-aide-raccourcis`, `E3-saisie-clavier`, `E3-toast-erreur` |
| 7 | `E2-*.html` (A et B) | `E2-annonce-numero`, `E2-avant-debut`, `E2-premiere-partie`, `E2-qr-code`, `E2-adresse-courte`, `E2-pause`, `E2-bingo-pause`, `E2-partie-suivante`, `E2-termine`, `E2-bilan-bingo` |
| 9 | `E4-Mobile.html`, `E4-Desktop.html`, `E4-Etats.html` | `E4-*` |
| 10 | `E5-Desktop.html`, `E5-Mobile.html`, `E5-Etats.html` | `E5-*` |
| 11 | `E6-Desktop.html`, `E6-Mobile.html` | `E6-encart-direct`, `E6-bouton-suivre` |

Largeurs de vérification :
- projection du backoffice : 1920 × 1080, et un contrôle à 1280 × 720 ;
- site : 390 et 1440 px.

## Spécifique à cette feature

- **Constantes non négociables :**
  - Délais : heartbeat SSE 15 s, `retry` 3 s, synchronisation entre réplicas 1 s ; contrôles
    masqués après 3 s ; « Reconnexion… » après 5 s.
  - Projection : grille sur 75 % de la largeur ; chiffres de la grille 76 px, dernier numéro
    300 à 320 px, annonce 780 px, sur la scène 1920 × 1080.
  - Palette mémorisée sous la clé `vpd.loto.palette` ; adresse courte
    `https://volepapillondamour.fr/loto`.
- **Interdits :**
  - pas de nouvelle ressource Azure (SignalR, Web PubSub, Redis), pas de changement de
    `maxReplicas` ni d'infra ;
  - pas de dépendance npm de QR code : le SVG statique est fourni par le plan ;
  - ne pas modifier les règles de jeu (`Partie`, `LinePartie`, logique des handlers) au-delà
    de l'appel `MarkLiveChange()` ;
  - pas de police manuscrite sur les tableaux.
- **Migration :** `AddAssoEventLiveRevision`, à générer avec `dotnet ef` (tâche 2). Elle ne
  doit ajouter que la colonne `LiveRevision`. Ne l'appliquer à aucun environnement distant.
- **Styles de la projection :** ils vont dans la feuille globale
  `src/BackOffice/src/scss/_loto-projection.scss`, préfixée `.loto-projection`. Le budget
  de 4 Ko par composant est dépassé sinon : le build de production échoue.
- **Azure / infra :** aucun déploiement, aucun `az` en écriture.

## PR

- Titre : `feat(loto): reliable live board, stage projection and today banner`
- « Risks and follow-up » :
  - **Migration** `AddAssoEventLiveRevision` : à appliquer au prochain déploiement de l'API.
  - **Ancien flux SSE** : les clients ouverts au moment du déploiement se reconnectent seuls.
  - **Recette en salle** : faire un essai au vidéoprojecteur avant le jour J pour choisir la
    palette. La spec le prévoit avec la bascule à la touche P.
  - **Vidéoprojecteur 4:3 ou 16:10** : la scène 16:9 est réduite, avec des bandes sur les
    côtés.
  - **Bandeau de cookies** : il couvre aussi le tableau mobile au premier passage
    (comportement du site inchangé).
  - **Budget de style** : `tableau.component.scss` du Website dépasse le seuil
    d'avertissement de 2 Ko (2,9 Ko) ; le seuil d'erreur est à 4 Ko.
