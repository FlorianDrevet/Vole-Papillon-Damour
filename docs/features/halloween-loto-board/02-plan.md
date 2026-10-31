# Tableau du loto : mode Halloween animé — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**But.** Ajouter un thème « Halloween » au tableau en direct du site, sur téléphone, réglé par
événement depuis le backoffice, avec un décor vivant et une animation par moment du jeu.

**Délai.** Loto le 2026-11-01. Les tâches sont rangées dans l'ordre de livraison de la spec
(§ 11, Q3) : chaque tâche laisse l'application dans un état livrable. Si le temps manque, on
livre jusqu'à la dernière tâche verte et on note le reste dans la PR.

**Architecture.**

- **API** : une valeur `LiveTheme` (`Classic` | `Halloween`) sur l'agrégat `AssoEvents`,
  persistée en entier, exposée par `EventResponse.LiveTheme` (donc aussi par le flux SSE, qui
  sérialise `EventResponse`). Un changement de thème appelle `MarkLiveChange()` : le
  `LiveTableauSyncService` diffuse alors le nouvel état aux téléphones (RG-25).
- **Backoffice** : un `mat-select` de plus dans `create-update-event-dialog`, seulement pour un
  loto.
- **Site** : `TableauComponent` garde son template classique et délègue à un nouveau
  `TableauHalloweenComponent` quand `liveTheme === 'Halloween'` **et** que la largeur est
  inférieure à 768 px (RG-26). Le décor permanent est en SVG + animations CSS en boucle ; les
  animations d'événement passent par l'API Web Animations (`element.animate()`), qu'on peut
  relancer, interrompre et attendre. Aucune nouvelle dépendance npm.
- **Moments du jeu** : une fonction pure `detectLotoMoment(previous, next)` compare deux états
  successifs et rend au plus un moment (RG-28). La pause mobile se déduit de l'état
  (`LotoTableauView.intermission`, RG-31).

**Fichiers de référence à lire avant de commencer.**

- Maquettes : `maquettes/preview/Main.html` (démo jouable : boutons à droite), `Tableau.html`,
  `Etats.html`, `Backoffice.html`, `Palette.html`. Les dessins et les `@keyframes` à reprendre
  **tels quels** sont dans `maquettes/sources/*.dc.html` (`Citrouille`, `ChauveSouris`,
  `Fantome`, `Chaudron`, `Tableau`, `Main`, `Etats`).
- Code : `src/Website/src/app/feature/tableau/tableau.component.{ts,html,scss}`,
  `src/SharedUi/src/lib/loto/loto-tableau-view.ts`,
  `src/Website/src/app/shared/clients/sse-client.service.ts`,
  `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/AssoEvents.cs`.
- Skills du dépôt : `.github/skills/cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
  `angular-patterns`, `tdd-workflow`, `delivery-workflow`.

---

## Tâche 0 — État initial

1. `dotnet build src/Backend` puis `dotnet test` sur `Vole_Papillon_Damour.Domain.tests`,
   `Vole_Papillon_Damour.Application.tests`, `Vole_Papillon_Damour.Api.tests`.
2. `npm test -- --watch=false --browsers=ChromeHeadless` dans `src/Website` et `src/BackOffice`.
3. Noter dans la PR les échecs déjà présents, sans les corriger.

Pas de commit.

---

## Tâche 1 — Domaine : thème du tableau en direct

**Fichiers**
- Créer `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/ValueObjects/LiveTheme.cs`
  sur le modèle de `EventsType.cs` : `EnumValueObject<LiveTheme.LiveThemeEnum>`, valeurs
  `Classic = 0`, `Halloween = 1`, `CreateFromString(string?)` qui retombe sur `Classic`.
- Modifier `AssoEvents.cs` : propriété `public LiveTheme LiveTheme { get; private set; }`
  initialisée à `Classic` dans `Create` et dans le constructeur sans paramètre ; méthode
  `public bool SetLiveTheme(LiveTheme theme)` qui ne fait rien et rend `false` si la valeur ne
  change pas, sinon affecte, appelle `MarkLiveChange()` et rend `true`.

**Test d'abord** — `src/Backend/Vole_Papillon_Damour.Domain.tests/AssoEventsAggregateTests/AssoEventsTests.cs` :
- un événement créé est en `Classic` ;
- `SetLiveTheme(Halloween)` rend `true` et incrémente `LiveRevision` de 1 ;
- un second `SetLiveTheme(Halloween)` rend `false` et ne change pas `LiveRevision` ;
- `LiveTheme.CreateFromString("inconnu")` donne `Classic`.

**Commit** : `feat(loto): thème du tableau en direct sur l'événement`

---

## Tâche 2 — Persistance et migration

**Fichiers**
- `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/AssoEventConfiguration.cs` :
  conversion `LiveTheme` ↔ `int` comme `EventsType`, `HasDefaultValue(0)` sur la valeur
  convertie (passer par `HasConversion` puis `HasDefaultValueSql("0")` si EF refuse le défaut
  d'un objet valeur), colonne non nulle.
- Migration `AddEventLiveTheme` dans `src/Backend/Vole_Papillon_Damour.Infrastructure/Migrations/`
  (`dotnet ef migrations add AddEventLiveTheme` depuis le projet Infrastructure, startup Api).
  Elle ne doit ajouter **que** la colonne `AssoEvents.LiveTheme int NOT NULL DEFAULT 0`.

**Test d'abord** — `src/Backend/Vole_Papillon_Damour.Infrastructure.tests` : un aller-retour
en base (même mécanique que les tests existants du dossier) conserve `Halloween`.

**Commit** : `feat(loto): persister le thème du tableau`

---

## Tâche 3 — Application, contrat et API

**Fichiers**
- `Application/Events/Commands/Events/UpdateEvent/UpdateEventCommand.cs` et
  `CreateEvent/CreateEventCommand.cs` : paramètre `LiveTheme LiveTheme` (création : défaut
  `Classic`).
- `UpdateEventCommandHandler.cs` : après `vpdEvent.Update(...)`, `vpdEvent.SetLiveTheme(command.LiveTheme)`.
  `CreateEventCommandHandler.cs` : idem après `Create`.
- `Application/Events/Common/AssoEventResult.cs` : champ `LiveTheme LiveTheme` (en fin de record).
- `Contracts/Events/Requests/UpdateEventRequest.cs` et `CreateEventRequest.cs` :
  `public string? LiveTheme { get; set; }`.
- `Contracts/Events/Responses/EventResponse.cs` : `public string LiveTheme { get; set; } = "Classic";`
- `Api/Common/Mapping/EventMappingConfig.cs` : `string → LiveTheme` via `CreateFromString`,
  `LiveTheme → string` via `Value.ToString()`, et dans `AssoEventResult → EventResponse`
  `.Map(dest => dest.LiveTheme, src => src.LiveTheme.Value.ToString())`.

**Tests d'abord**
- `Application.tests/Events/...` : `UpdateEventCommandHandler` avec `LiveTheme = Halloween`
  sur un loto `Classic` → résultat `Halloween` et `LiveRevision` incrémentée ; même thème →
  `LiveRevision` inchangée.
- `Api.tests/LiveTableau/LiveTableauEndpointTests.cs` : l'état complet renvoyé par
  `GET asso-events/{id}` et le premier message SSE contiennent `"liveTheme":"Halloween"` après
  modification (RG-25).

**Commit** : `feat(loto): exposer le thème du tableau dans l'API et le direct`

---

## Tâche 4 — Backoffice : champ « Thème du tableau en direct » (E1)

**Maquette :** preview/Backoffice.html — zones E1-champ-theme, E1-options-theme, E1-aide-theme

**Fichiers**
- `src/BackOffice/src/app/shared/models/vpdEvent.model.ts` : `liveTheme?: 'Classic' | 'Halloween'`.
- `src/BackOffice/src/app/shared/components/dialogs/create-update-event-dialog/create-update-event-dialog.component.ts` :
  contrôle `liveTheme` (défaut `'Classic'`), prérempli en modification, envoyé par
  `formData.append("LiveTheme", …)` **seulement** si le type est `Bingo`.
- Même dossier, `.html` : sous « Titre », un `mat-form-field` avec `mat-select`, visible
  seulement si `type() === VpdEventEnum.Bingo`. Libellés mot pour mot :
  - étiquette : « Thème du tableau en direct » ;
  - options : « Classique », « Halloween » ;
  - `mat-hint` : « Halloween s'affiche sur les téléphones des joueurs. La projection ne change pas. »

**Test d'abord** — spec du dialogue : le champ est absent pour « Bourse aux livres », présent
pour « Loto » ; l'envoi d'un loto contient `LiveTheme=Halloween` après sélection.

**Commit** : `feat(backoffice): choisir le thème du tableau en direct`

---

## Tâche 5 — Moments du jeu et pause mobile (logique pure)

**Fichiers**
- `src/SharedUi/src/lib/loto/loto-tableau-view.ts` : ajouter à `LotoTableauView`
  `intermission: boolean` = vrai quand la partie courante a un index > 0, qu'elle n'a encore
  aucun numéro (`lastNumeros.length === 0`) et que la partie précédente a `pauseAfter` (RG-31).
  La projection du backoffice n'utilise pas ce champ.
- Créer `src/Website/src/app/feature/tableau/halloween/loto-moment.ts` :

  ```ts
  export type LotoMoment =
    | {kind: 'bingo'}
    | {kind: 'line'; lotName: string; endsPartie: boolean}
    | {kind: 'partie'}
    | {kind: 'numero'; value: number}
    | {kind: 'undo'; value: number};
  export function detectLotoMoment(previous: LotoEventSnapshot, next: LotoEventSnapshot): LotoMoment | null;
  ```

  Règles (RG-28, RG-32, RG-33), dans cet ordre de priorité :
  1. `bingo` : `bingoHasBeenWon` passe de faux à vrai.
  2. `line` : un lot de n'importe quelle partie passe de `isWon === null` à non nul ; `lotName`
     = son nom ; `endsPartie` = `currentPartieIndex` a augmenté dans le même message.
  3. `partie` : `currentPartieIndex` a augmenté.
  4. `numero` : même partie, `lastNumeros` plus long ; `value` = dernier numéro.
  5. `undo` : même partie, `lastNumeros` plus court ; `value` = numéro retiré.
  Événements différents (`id`) ou état identique → `null`.

**Tests d'abord**
- `src/Website/src/app/feature/tableau/loto-tableau-view.spec.ts` : `intermission` vrai après
  une partie `pauseAfter` terminée, faux au premier numéro suivant, faux à la partie 1.
- Créer `src/Website/src/app/feature/tableau/halloween/loto-moment.spec.ts` : un cas par
  moment ; trois numéros d'un coup → `numero` avec le dernier ; ligne + partie suivante dans le
  même message → `line` avec `endsPartie: true` ; ligne + bingo → `bingo`.

**Commit** : `feat(tableau): détecter les moments du jeu et la pause mobile`

---

## Tâche 6 — Activation du thème et préférence « Calmer »

**Fichiers**
- `src/Website/src/app/shared/models/vpdEvent.model.ts` : `liveTheme?: 'Classic' | 'Halloween'`.
- Créer `src/Website/src/app/feature/tableau/halloween/halloween-preferences.ts` (service
  fourni par `TableauComponent`, comme `SseClientService`) :
  - `phone` : signal suivant `matchMedia('(max-width: 767.98px)')` ;
  - `reducedMotion` : signal suivant `matchMedia('(prefers-reduced-motion: reduce)')` ;
  - `calm` : `reducedMotion() || choixMémorisé`, clé `localStorage` `vpd.tableau.calm`
    (lecture et écriture dans `try/catch`, la page doit fonctionner sans) ; `toggleCalm()` ;
  - `visible` : signal suivant `document.visibilityState` ; `visibleSince` : horodatage du
    dernier retour visible.
  Tout est inerte côté serveur (`isPlatformBrowser`).
- `tableau.component.ts` : `halloween = computed(() => live.eventAsso()?.liveTheme === 'Halloween' && prefs.phone())`.
- `tableau.component.html` : `@if (halloween()) { <app-tableau-halloween …/> } @else { …template actuel… }`.
  Le template classique ne change pas (spec § 3, Q1).

**Test d'abord** — `tableau.component.spec.ts` (à créer à côté) : thème `Halloween` + largeur
téléphone → `app-tableau-halloween` rendu ; thème `Halloween` + largeur 1024 → template
classique ; thème absent → classique. `halloween-preferences.spec.ts` : `calm` vrai si
`prefers-reduced-motion`, mémorisé après `toggleCalm()`, sans erreur si `localStorage` lève.

**Commit** : `feat(tableau): activer le thème Halloween sur téléphone`

---

## Tâche 7 — E2 : tableau Halloween en jeu (mise en page et palette)

**Maquette :** preview/Tableau.html — zones E2-retour, E2-etat-connexion, E2-dernier-numero, E2-precedents, E2-grille, E2-case-derniere, E2-partie, E2-lots, E2-bingo

**Fichiers**
- Créer `src/Website/src/app/feature/tableau/halloween/tableau-halloween.component.{ts,html,scss}`
  (standalone, importé par `FeatureModule`). Entrées : `view: LotoTableauView`,
  `event: VpdEventModel | null`, `connection: LiveTableauConnection`.
- Palette (zone P-proposition de preview/Palette.html) en variables CSS sur la racine
  `.hw` du composant, **pas** dans `tailwind.config.js` (thème local) :
  `--nuit #120a1c`, `--velours #1d1229`, `--ardoise #2a1c3a`, `--citrouille #ff8c2e`,
  `--braise #ffb347`, `--cire #f4e8cf`, `--sorciere #9bd85a`, `--lilas #b8a5cf`,
  `--prune #5b3482`, `--encre #1a0b22`. Fond du bloc dernier numéro `#170e23`.
- Police **Fraunces** auto-hébergée comme les autres (`src/Website/public/fonts/`) : trois
  fichiers latins statiques `fraunces-latin-500-italic.woff2`, `fraunces-latin-800.woff2`,
  `fraunces-latin-900.woff2` (licence OFL), déclarés par `@font-face` dans le SCSS du
  composant avec `font-display: swap`. Ils ne se chargent qu'en mode Halloween. Les chiffres
  de la grille et des pierres tombales restent en Libre Franklin.
- Libellés mot pour mot : « Dernier numéro », « Les cinq d'avant », « Tous les numéros »,
  « {n} sortis », « Partie {n} sur {total} », « À gagner sur cette ligne », « gagné », « Bingo »,
  « Gagné ! », états « En direct », « Reconnexion… », « Hors ligne », « Connecté », « Terminé ».
  Plus d'étiquettes en capitales espacées, plus de pastille lumineuse, plus de phrase de pied
  de page.
- Classes de case : non sortie, `on` (cire), `last` (citrouille + lueur), `nb` (voisin, anneau
  vert) : mêmes règles que `cell.state` de la vue. Les précédents sont des « pierres
  tombales » inclinées de −2°, 1,5°, −1°, 2°, −1,5°.
- `aria-live="polite"` reste sur le bloc du dernier numéro ; tout le décor porte
  `aria-hidden="true"`.

**Test d'abord** — `tableau-halloween.component.spec.ts` : rend le dernier numéro, 5 précédents,
90 cases dont une `last` ; les voisins ont `nb` ; un lot gagné est barré avec « gagné » ;
aucun élément ne contient « EN DIRECT » en capitales.

**Commit** : `feat(tableau): tableau Halloween en jeu`

---

## Tâche 8 — Décor vivant et bouton « Calmer » (E2, E5)

**Maquette :** preview/Tableau.html — zones E2-scene-ciel, E2-citrouille, E2-araignee, E2-feux-follets, E2-brume, E5-bouton-animations

**Fichiers**
- Créer `src/Website/src/app/feature/tableau/halloween/characters/` avec quatre composants
  standalone `vpd-citrouille`, `vpd-chauve-souris`, `vpd-fantome`, `vpd-chaudron`. Chacun
  reprend **à l'identique** le SVG et les `@keyframes` de `maquettes/sources/<Nom>.dc.html`,
  avec une entrée `size` (px) et une entrée `mood` :
  - citrouille : `idle | sourire | rire | dort | salut` ;
  - chauve-souris : `vol | dort` ;
  - fantôme : `flotte | coucou | lanterne`.
  Les `id` de dégradés SVG (`pkSkin`, `pkFire`…) sont suffixés d'un identifiant d'instance
  pour rester uniques dans la page.
- Dans `tableau-halloween.component.html`, reprendre de `sources/Tableau.dc.html` : ciel
  (étoiles, lune, deux nuages), lueur derrière le numéro, citrouille posée sur le bord du bloc
  (taille 104), toile + araignée sur la grille (cycle 26 s), trois feux follets, brume en bas.
  Toutes les durées et courbes sont celles de la maquette.
- Bouton E5 dans l'en-tête : texte « Calmer » / « Animer », `aria-pressed`, `aria-label`
  « Calmer les animations » / « Remettre les animations », 44 px de haut, branché sur
  `HalloweenPreferences.toggleCalm()` (RG-34).
- Mode calme : classe `calm` sur la racine → `animation: none` sur tout le décor (même règle
  que la maquette : `.calm .deco, .calm .deco *`). Onglet caché : classe `paused` →
  `animation-play-state: paused` sur tout le composant (RG-37).

**Test d'abord** — spec du composant : la classe `calm` est posée quand
`prefs.calm()` est vrai ; le bouton bascule `aria-pressed` et le texte ; la classe `paused`
suit `prefs.visible()`.

**Commit** : `feat(tableau): décor Halloween vivant et bouton Calmer`

---

## Tâche 9 — Moteur d'animation et « nouveau numéro » / « annulation » (E3)

**Maquette :** preview/Main.html — zones E2-ecran, E3-chauves-souris

**Fichiers**
- Créer `src/Website/src/app/feature/tableau/halloween/halloween-fx.ts` (service du composant) :
  - garde l'état précédent ; à chaque nouvel `event`, appelle `detectLotoMoment`, **sauf** :
    premier état reçu, mode calme, page cachée, et pendant 3 s après un retour visible
    (`prefs.visibleSince`) (RG-27) ;
  - un seul moment à la fois : un moment de rang supérieur interrompt le courant
    (`animation.cancel()` sur tout ce qui joue, fondu de 150 ms), un moment de même rang
    l'interrompt aussi et se joue (RG-29) ;
  - expose un signal `moment` et, pour la grille, un signal `frozenCells` (voir tâche 11).
- Couche d'effets : un `<div class="fx">` en `position: fixed; inset: 0; pointer-events: none`
  au-dessus du tableau, rempli seulement pendant un moment.
- **Nouveau numéro** (1,6 s) : 5 chauves-souris `vpd-chauve-souris` tailles 78, 54, 112, 40 et
  60 px, trajectoires `fly-1` à `fly-5` et délais de `sources/Main.dc.html`, converties en
  `element.animate()` avec des positions relatives à la fenêtre (`vw`/`vh`).
  En même temps, sur le tableau :
  - l'ancien numéro en fumée : `num-smoke`, 0,55 s ;
  - le nouveau qui s'embrase : `num-ign`, 0,75 s, départ à 0,16 s ;
  - la nouvelle pierre tombale qui tombe : `tomb-in` ;
  - la case qui s'allume : `ign`, puis la lueur en boucle ;
  - la citrouille en `sourire` pendant 1,3 s.
- **Annulation** (0,8 s) : `num-smoke` sur le numéro retiré, `num-back` sur celui qui revient,
  `unlit` sur la case. Pas de chauves-souris.
- Les données s'affichent dès réception : seules les couches décoratives attendent (RG-29).
  Les chauves-souris passent au-dessus du dernier numéro en moins de 0,3 s chacune (RG-30).

**Test d'abord** — `halloween-fx.spec.ts` (horloge simulée `fakeAsync`) : aucun moment au
premier état ; `numero` déclenché au second ; aucun en mode calme ; aucun dans les 3 s qui
suivent un retour visible ; un `line` interrompt un `numero` en cours.

**Commit** : `feat(tableau): chauves-souris et numéro embrasé`

---

## Tâche 10 — « Ligne gagnée » et « Bingo » (E3)

**Maquette :** preview/Main.html — zones E3-citrouilles-rire, E3-pluie-bonbons, E3-banniere

**Fichiers** — `tableau-halloween.component.html` et `halloween-fx.ts`.
- **Ligne** (4,2 s) :
  - trois `vpd-citrouille` en `rire` (130, 156 et 130 px), bas de l'écran, keyframes
    `pk-rise`, décalées de 0,12 s ;
  - 26 bonbons (emballé, bonbon maïs, sucette, couleurs de `CANDY_COLORS`), keyframes
    `candy-fall` / `candy-spin`, sol = bas de la fenêtre ;
  - bannière couleur cire sous le dernier numéro (RG-30) : titre « Ligne gagnée ! » en Fraunces
    900 italique `#a83a06`, puis le nom du lot.
- **Bingo** (5,2 s) : citrouilles, 40 bonbons, chauves-souris, bannière « Bingo ! ».
- Positions des bonbons pseudo-aléatoires mais déterministes (formule de `renderVals()` dans
  `sources/Main.dc.html`), **40 au plus** (RG-37), animés uniquement en `transform`/`opacity`.

**Test d'abord** — spec : un `line` rend 3 citrouilles, 26 bonbons et le nom du lot ; un
`bingo` rend 40 bonbons et « Bingo ! » ; tout disparaît après la durée.

**Commit** : `feat(tableau): citrouilles et pluie de bonbons`

---

## Tâche 11 — « Partie suivante » : fantôme et grille soufflée (E3)

**Maquette :** preview/Main.html — zones E3-fantome, E3-annonce-partie

**Fichiers** — `halloween-fx.ts`, composant.
- Sur `partie` : `vpd-fantome` en `coucou` (120 px), trajet `gh-cross` 2,6 s à mi-hauteur de
  la grille. Les cases sorties de l'**ancienne** partie restent affichées via `frozenCells`
  et s'éteignent colonne par colonne (`cell-out`, délai `0,45 s + 0,164 s × colonne`). Ensuite
  la grille vide de la nouvelle partie prend le relais : 2 s au plus, seule exception à RG-29.
  Enfin l'annonce plein écran « Partie {n} sur {total} » + nom, en fondu 1,8 s à partir de
  2,2 s.
- **Ligne qui termine la partie** (`line` avec `endsPartie`) : la fête de la ligne d'abord,
  sur l'ancienne grille gelée, puis le fantôme ; la grille reste gelée 6,5 s au plus. Si la
  nouvelle partie est une pause (RG-31), l'écran Pause s'affiche après le fantôme (RG-32).
- Un nouveau numéro reçu pendant ce temps dégèle immédiatement la grille et joue son moment
  (RG-29).

**Test d'abord** — spec : `partie` gèle les anciennes cases puis les libère à 2 s ;
`line`+`endsPartie` enchaîne bingo/ligne puis fantôme ; un `numero` pendant le gel libère
la grille.

**Commit** : `feat(tableau): le fantôme souffle la grille à la partie suivante`

---

## Tâche 12 — E4 : états Halloween

**Maquette :** preview/Etats.html — zones E4-avant, E4-cimetiere, E4-citrouille-endormie, E4-pause, E4-balai, E4-chaudron, E4-termine, E4-salut, E4-chargement, E4-reconnexion, E4-chauve-souris-endormie, E4-introuvable, E4-fantome-lanterne

**Maquette :** preview/Main.html — zones E4-pause, E4-balai, E4-chaudron, E2-brume

Dans `tableau-halloween.component.html`, selon `view.phase`, `view.intermission` et
`connection`. Libellés mot pour mot :

- **Avant** : cimetière, citrouille `dort` (150 px), « {nom} · aujourd'hui »,
  « Le loto va commencer », « Gardez cette page ouverte : les numéros s'afficheront ici dès le
  premier tirage. », carte « Première partie ».
- **Pause** (`intermission`) : lune, silhouette sur balai (cycle 20 s), chaudron (190 px),
  « Pause », « On reprend dans quelques minutes. Gardez la page ouverte, le tableau repartira
  tout seul. », carte « Bingo », carte « Ensuite, partie {n} sur {total} ». L'en-tête affiche
  « Pause ».
- **Terminé** : deux citrouilles `salut` + fantôme `coucou`, cinq bonbons qui tombent **une
  fois**, « Loto terminé », « Merci à tous ! », « Les bénéfices de cet après-midi soutiennent
  Maxence et sa famille. », carte « Bingo », bouton « Voir les prochains événements ».
- **Chargement** : silhouettes du tableau en pulsation lente (2,6 s), sans blocs gris
  génériques, `role="status"` + « Chargement du tableau de loto ».
- **Reconnexion / hors ligne** : chauve-souris `dort` (44 px) + « Connexion perdue. Le
  tableau se remettra à jour tout seul dès le retour du réseau. », dernier état gardé (RG-13).
- **Introuvable** : fantôme `lanterne` (180 px), « Ce tableau n'existe pas ou n'est plus
  disponible. », « Vérifiez le lien, ou retrouvez le loto du jour depuis la page des
  événements. », « Voir les événements ». Ce cas ne connaît pas le thème : il s'affiche en
  Halloween seulement si un thème a déjà été reçu ; sinon, l'écran classique.
- Au premier numéro, la citrouille endormie de « Avant » passe en `idle` (elle ouvre les
  yeux) avant que le tableau en jeu s'affiche.

**Test d'abord** — spec : un cas par état, avec le libellé principal ; `intermission` affiche
« Pause » et pas la grille.

**Commit** : `feat(tableau): états Halloween avant, pause, fin et connexion`

---

## Tâche 13 — Validation complète et PR

1. Tests backend et frontend de la tâche 0 : aucun nouvel échec.
2. `npm run build` dans `src/Website` et `src/BackOffice`. Le premier chargement du tableau
   n'augmente pas de plus de 150 Ko (RG-37) ; les polices Fraunces ne se chargent qu'en
   Halloween.
3. Vérification à la main, Chrome DevTools en 390 × 844, processeur ralenti ×4, avec un loto
   local réglé sur Halloween et le backoffice dans un second onglet :
   - chaque moment de E3 se joue une fois, au bon déclencheur (critères 3 à 7 de la spec) ;
   - enregistrement Performance pendant un bingo : pas d'image au-delà de 50 ms ;
   - onglet caché : animations en pause ;
   - mode calme et `prefers-reduced-motion` émulé : rien ne bouge en dehors des fondus ;
   - en 1024 px : thème classique ; projection du backoffice inchangée.
4. Captures d'écran 390 px de E2, des cinq moments de E3 et des états de E4 dans la PR.
5. Ouvrir la PR vers `main` en suivant `delivery-workflow`. La migration `AddEventLiveTheme`
   est à appliquer avant le déploiement du site.

**Commit** : aucun hors corrections.

---

## Zones non implémentées

- **P-variante** (preview/Palette.html) : palette « Sang et os », présentée pour comparaison et
  non retenue (contrastes insuffisants, spec § 7).
