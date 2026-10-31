# Handoff Codex — Tableau du loto, mode Halloween animé (mobile)

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

- **Organisateur :** dans le backoffice, il règle un loto sur « Halloween ».
- **Joueurs :** sur téléphone, le tableau en direct prend un habit d'Halloween. Le décor bouge
  en permanence : citrouille, araignée, lune, nuages, brume, feux follets. Chaque moment du
  jeu a son animation :
  - nouveau numéro : chauves-souris ;
  - ligne gagnée : citrouilles qui rient et pluie de bonbons ;
  - bingo : la même chose, en plus fort ;
  - partie suivante : un fantôme qui souffle la grille ;
  - pause, début et fin : leur scène propre.
- **Calmer :** un bouton fige les animations.
- **Bandeau du jour :** le jour du loto, le bandeau en haut du site passe lui aussi en Halloween,
  sur toutes les largeurs. À l'arrivée, un fantôme le traverse en courant.
- **Ce qui ne change pas :** la projection du backoffice et le tableau sur tablette ou
  ordinateur.

**Le loto a lieu le 2026-11-01.** Livrer les tâches dans l'ordre du plan. Chacune laisse
l'application livrable. Si le temps manque, s'arrêter à la dernière tâche verte et ouvrir la
PR avec ce qui reste à faire.

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-halloween-loto-board`
- Branche : `feat/halloween-loto-board`, qui contient déjà la conception (spec, maquettes,
  plan). La PR livrera conception et implémentation ensemble.

## À lire, dans cet ordre

1. `docs/features/halloween-loto-board/01-fonctionnel.md` : **la spec, elle fait foi**. Elle
   reprend RG-01 à RG-24 de `loto-live-board` et ajoute RG-25 à RG-38.
2. `docs/features/halloween-loto-board/02-plan.md` : **le plan à exécuter** (tâches 0 à 13, plus
   la tâche 12 bis, ajoutée le 2026-10-31 : le bandeau du jour).
3. `docs/features/halloween-loto-board/maquettes/README.md`, puis
   `maquettes/preview/index.html`. Ouvrir `preview/Main.html` et cliquer les boutons : c'est
   la référence de rythme et de mouvement.
4. `maquettes/sources/*.dc.html` : les SVG des personnages et toutes les `@keyframes` à
   reprendre telles quelles. Le markup `<x-dc>`, `<sc-if>`, `<sc-for>` et `{{…}}` y est propre
   à l'outil de maquette : le traduire en Angular, sans le copier.
5. Skills du repo : `.github/skills/cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `angular-patterns`, `tdd-workflow`, `local-auth-testing` (recette du backoffice).
6. `docs/features/loto-live-board/01-fonctionnel.md` § E4 et RG-12 à RG-15 : le comportement
   du direct à préserver.

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 4 | `Backoffice.html` | `E1-champ-theme`, `E1-options-theme`, `E1-aide-theme` |
| 7 | `Tableau.html`, `Palette.html` | `E2-retour`, `E2-etat-connexion`, `E2-dernier-numero`, `E2-precedents`, `E2-grille`, `E2-case-derniere`, `E2-partie`, `E2-lots`, `E2-bingo`, `P-proposition` |
| 8 | `Tableau.html` | `E2-scene-ciel`, `E2-citrouille`, `E2-araignee`, `E2-feux-follets`, `E2-brume`, `E5-bouton-animations` |
| 9 | `Main.html` | `E2-ecran`, `E3-chauves-souris` |
| 10 | `Main.html` | `E3-citrouilles-rire`, `E3-pluie-bonbons`, `E3-banniere` |
| 11 | `Main.html` | `E3-fantome`, `E3-annonce-partie` |
| 12 | `Etats.html`, `Main.html` | `E4-*` |
| 12 bis | `Bandeau.html` | `E6-*` |

Largeurs de vérification : 390 × 844 pour le mode Halloween, et 1024 px pour vérifier que le
thème classique est conservé. Le bandeau E6 se vérifie en 390 et 1440 px : il est en Halloween
sur toutes les largeurs.

## Spécifique à cette feature

- **Constantes non négociables :**
  - Seuil téléphone : `max-width: 767.98px`.
  - Palette : les 10 variables de `maquettes/README.md`, posées sur la racine `.hw`.
  - Durées :
    - nouveau numéro : 1,6 s ;
    - annulation : 0,8 s ;
    - ligne : 4,2 s ;
    - bingo : 5,2 s ;
    - fantôme : 2,6 s ;
    - annonce de partie : 1,8 s, à partir de 2,2 s.
  - 26 bonbons pour une ligne, 40 au plus pour le bingo.
  - Aucune animation pendant 3 s après le retour de la page au premier plan.
  - La grille reste gelée au plus 2 s, ou 6,5 s après une ligne qui termine la partie.
  - Clé `localStorage` du bouton Calmer : `vpd.tableau.calm`. Le tableau et le bandeau
    partagent cette préférence.
  - Fantôme du bandeau : une fois par chargement de page, en 2,8 s (2,2 s sur téléphone).
- **Interdits :**
  - Aucune nouvelle dépendance npm (pas de Motion, Lottie ni canvas).
  - Aucune image matricielle ni vidéo : tout est en SVG et CSS.
  - Ne pas modifier le template classique du tableau ni la projection du backoffice
    (`src/BackOffice/.../tableau/`).
  - N'animer que `transform` et `opacity`. Un `filter: blur` n'est permis que sur le seul
    élément du numéro qui part en fumée.
  - Pas de son, pas de vibration (RG-38).
  - Pas de flash : au plus trois éclats par seconde (RG-36).
- **Accessibilité :**
  - Tout le décor porte `aria-hidden="true"`.
  - `aria-live` reste sur le dernier numéro uniquement.
  - `prefers-reduced-motion` active le mode calme d'office.
- **Polices :** Fraunces auto-hébergée dans `src/Website/public/fonts/` : trois fichiers latins
  statiques, licence OFL, chargés seulement en mode Halloween. Récupérer les fichiers depuis
  le dépôt officiel de Fraunces (`undercase/fraunces`) ou Google Fonts, puis les sous-ensembler
  en latin.
- **Base de données :** une seule migration, `AddEventLiveTheme`, qui ajoute une seule
  colonne. Ne pas la lancer sur un autre environnement que le local.
- **Azure / infra :** aucun déploiement ni commande `az` en écriture. Le déploiement du
  2026-11-01 est fait par l'utilisateur : migration, puis API, puis site et backoffice.

## PR

- Titre : `feat(loto): animated Halloween theme for the mobile live board`
- « Risks and follow-up » :
  - la migration `AddEventLiveTheme` doit être appliquée avant de déployer l'API ;
  - les tâches non livrées, s'il y en a ;
  - le résultat de l'enregistrement Performance avec le processeur ralenti ×4 ;
  - la question Q1 de la spec reste ouverte : faut-il aussi épurer le thème classique ?
