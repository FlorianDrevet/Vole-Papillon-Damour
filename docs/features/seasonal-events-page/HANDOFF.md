# Handoff Codex — Page Événements en agenda et thèmes des lotos

> À exécuter avec la skill `implement-handoff` (`.agents/skills/implement-handoff/SKILL.md`).
> Ce fichier ne contient que ce qui est propre à cette feature.

## Objectif

- **Page `/evenement`** :
  - en haut, le prochain rendez-vous « À la une », avec un compte à rebours, puis le suivant ;
  - dessous, l'agenda avec une bascule « Calendrier / Liste », **calendrier par défaut** ;
  - chaque date porte l'identité de son événement : Halloween pour un loto à thème, boules de
    bingo pour un loto sans thème, papier et papillon pour la bourse aux livres.
- **Fiche d'un loto Halloween** : son en-tête prend la scène du thème.
- **Bandeau du site** : il annonce le prochain rendez-vous **7 jours avant**.
- **Accueil** : les cartes « Venez nous voir » prennent l'identité de leur événement.
- **Backoffice** : le champ devient « Thème du loto ».

## Espace de travail (déjà créé, ne pas en créer d'autre)

- Worktree : `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-seasonal-events-page`
- Branche : `feat/seasonal-events-page`. Elle contient déjà la conception (spec, maquettes,
  plan) et part de `feat/halloween-loto-board` (voir « Dépendance »). La PR livrera conception
  et implémentation ensemble.

## Dépendance

Cette feature réutilise le thème du loto (`LiveTheme`), les personnages Halloween et la
variante Halloween du bandeau de `feat/halloween-loto-board`.
1. **Tâche 0 :** `git fetch` ; si `origin/feat/halloween-loto-board` a avancé, rebaser
   dessus, ou sur `origin/main` si elle est déjà fusionnée.
2. Si les personnages (tâche 8 de ce plan-là) ou la variante Halloween du bandeau (tâche 12 bis)
   ne sont pas encore livrés, s'arrêter après la tâche 2 de ce plan-ci et le signaler. Ne pas
   les réécrire ici.

## À lire, dans cet ordre

1. `docs/features/seasonal-events-page/01-fonctionnel.md` : **la spec, elle fait foi**
   (RG-41 à RG-51 ; elle prolonge RG-25 et RG-34 à RG-40 de `halloween-loto-board`).
2. `docs/features/seasonal-events-page/02-plan.md` : **le plan à exécuter** (tâches 0 à 12).
3. `docs/features/seasonal-events-page/maquettes/README.md`, puis
   `maquettes/preview/index.html`. Pages jouables : `Calendrier.html`, `AgendaV2.html` et
   leurs versions mobiles, `Main.html`, `FicheLoto.html`.
4. `maquettes/sources/Papillon.dc.html` (**variante `a` seulement**) et
   `maquettes/sources/BouleBingo.dc.html` : le SVG et les `@keyframes` à reprendre. Le markup
   `<x-dc>`, `<sc-if>`, `<sc-for>` et `{{…}}` est propre à l'outil de maquette : le traduire
   en Angular.
5. Skills du repo : `.github/skills/cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
   `angular-patterns`, `tdd-workflow`, `local-auth-testing` (recette du backoffice).

## Maquettes ↔ tâches

| Tâche | Page `maquettes/preview/` | Zones `data-zone` |
|---|---|---|
| 2 | `Identites.html` | `ID-halloween`, `ID-bourse`, `ID-loto` |
| 3 | `Identites.html`, `Papillons.html` | `ID-papillon`, `ID-boule`, `PA-a` |
| 4 | `Bandeau.html`, `Mobile.html` | `E4-*` |
| 5 | `Main.html`, `Mobile.html`, `AlaUneClassique.html` | `E1-titre`, `E1-a-la-une*`, `E1-date-a-la-une`, `E1-compte-a-rebours`, `E1-actions-a-la-une`, `E1-ajouter-agenda`, `E1-ensuite`, `E1-nos-rendez-vous`, `E1-nous-trouver`, `E2-*` |
| 6 | `AgendaV2.html`, `AgendaV2Mobile.html` | `E1-agenda`, `E1-bascule-vue`, `E1-filtres`, `E1-mois`, `E1-bandeau-*`, `E1-editions-passees` |
| 7 | `Calendrier.html`, `CalendrierMobile.html` | `E1-calendrier`, `E1-bascule-vue`, `E1-legende`, `E1-navigation-mois`, `E1-mois-calendrier`, `E1-jour-allume`, `E1-detail-jour` |
| 8 | `Etats.html` | `E6-*` |
| 9 | `FicheLoto.html` | `E3-*` |
| 10 | `Accueil.html`, `AccueilMobile.html` | `E7-*` |
| 11 | `Backoffice.html` | `E5-*` |

Largeurs de vérification : 390 et 1440 px.

## Spécifique à cette feature

- **Constantes non négociables :**
  - Le bandeau annonce à **J-7**.
  - Le compte à rebours de l'agenda et des cartes s'affiche dans les **30 jours**.
  - Les jours se comptent en jours calendaires à Paris.
  - Clés `localStorage` :
    - `vpd.agenda.vue` (`calendrier` | `liste`, `calendrier` par défaut) ;
    - `vpd.tableau.calm`, préférence partagée avec le tableau.
  - Allumage des jours du calendrier : décalage de 0,12 s entre deux jours.
  - Le fantôme court une seule fois par chargement de page, sur tout le site.
- **Mascottes :**
  - **Papillon :** seule la variante A « Marque-page ». Ses ailes portent des lignes de cahier
    **droites et complètes**, coupées par le contour de l'aile, jamais des lignes courbes qui
    suivent le bord.
  - **Boule de bingo :**
    - sphère pleine aux couleurs de l'association (`#1497d6`, `#f0801c`, `#7fd8f5`) ;
    - pastille blanche cerclée ;
    - **numéro toujours visible**, en texte HTML et non en `<text>` SVG. Dans l'agenda, la
      grande boule porte le jour du loto.
- **Interdits :**
  - aucune dépendance npm d'animation ;
  - aucune image matricielle pour les décors ;
  - ne pas modifier la projection du backoffice (`src/BackOffice/.../tableau/`) ;
  - pas de capitales espacées en police à chasse fixe ni de flèche « → » / « ← » en texte
    sur les écrans touchés ;
  - sur l'accueil, ne changer **que les cartes** : l'en-tête de section reste tel quel.
- **SSR** : les textes et dates sont rendus côté serveur ; les décors et animations ne
  démarrent que dans le navigateur (`isPlatformBrowser`).
- **Azure / infra** : aucun déploiement ni commande `az` en écriture. Aucune migration : le
  thème existe déjà.

## PR

- Titre : `feat(events): agenda calendar, event identities and 7-day banner`
- Base : `main` si `feat/halloween-loto-board` est fusionnée, sinon
  `feat/halloween-loto-board`. Le préciser dans la description.
- « Risks and follow-up » :
  - la dépendance à `halloween-loto-board` ;
  - les identités des prochaines fêtes (Noël, Rois mages, muguet) restent à dessiner, une par
    une ;
  - le changement de fenêtre du bandeau (jour J → J-7) est visible sur tout le site.
