# Page Événements en agenda et thèmes des lotos — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**But.** Refaire la page `/evenement` autour d'un agenda : « À la une », puis une bascule
« Calendrier / Liste », avec le calendrier par défaut. Donner à chaque événement son identité
visuelle (Halloween, loto sans thème, bourse aux livres) sur la page, la fiche du loto, le
bandeau du site (dès J-7) et les cartes de l'accueil.

**Dépendance.** Cette branche part de `feat/halloween-loto-board`, qui apporte :
- le thème du loto (`LiveTheme` dans l'API, le backoffice et les modèles du site) ;
- les personnages citrouille, fantôme et chauve-souris ;
- la variante Halloween du bandeau (tâches 8 et 12 bis de son plan).

**Tâche 0** : vérifier que ces éléments sont présents. S'ils manquent, rebaser sur
`origin/feat/halloween-loto-board` ou sur `main` après sa fusion. Ne jamais les réécrire ici.

**Architecture.**

- **API :** la requête du bandeau `GET /asso-events/today` devient « le prochain rendez-vous
  dans les 7 jours ». La route, la forme de la réponse et la règle de priorité restent les
  mêmes (RG-45).
- **Site, logique pure testée** (`src/Website/src/app/shared/events/`) :
  - l'identité d'un événement : `halloween | loto | books | other` (RG-44, RG-50) ;
  - le compte à rebours (RG-42) ;
  - l'étape du bandeau (RG-45, RG-46) ;
  - le regroupement par mois (RG-43) ;
  - la grille d'un mois de calendrier ;
  - le fichier `.ics` (RG-47).
- **Mascottes** (`src/Website/src/app/shared/components/mascots/`) : composants autonomes en
  SVG et CSS, sans dépendance npm :
  - `vpd-papillon` (variante A, « Marque-page ») et `vpd-boule-bingo`, nouveaux ;
  - `vpd-citrouille`, `vpd-fantome` et `vpd-chauve-souris`, déplacés ici depuis le tableau.
- **Animations :** elles suivent RG-48. La préférence « Calmer » (`vpd.tableau.calm`) et
  `prefers-reduced-motion` arrêtent tout décor. Le fantôme ne court qu'une fois par
  chargement de page, via un service racine partagé avec le bandeau.

**À lire avant de commencer.**
- `maquettes/preview/index.html`. `Calendrier.html`, `AgendaV2.html` et `Main.html` sont
  jouables.
- `maquettes/sources/Papillon.dc.html` : variante `a` seulement. `BouleBingo.dc.html` : SVG et
  `@keyframes` à reprendre tels quels.
- Code :
  - `src/Website/src/app/feature/vpd-events/vpd-events-page.component.*`
  - `feature/home/components/vpd-events/vpd-event-sections.*`
  - `core/layouts/today-banner/*`
  - `feature/event-detail/event-detail.component.*`
  - `src/Backend/Vole_Papillon_Damour.Application/Events/Queries/GetTodayEvent/GetTodayEventQueryHandler.cs`
- Skills : `.github/skills/cqrs-feature`, `dotnet-patterns`, `xunit-unit-testing`,
  `angular-patterns`, `tdd-workflow`, `delivery-workflow`.

---

## Tâche 0 — État initial

1. `git log` : les commits `feat(loto): thème du tableau…` et les personnages Halloween sont
   présents (voir **Dépendance**).
2. `dotnet build src/Backend` et les tests Application et Api ; `npm test -- --watch=false
   --browsers=ChromeHeadless` dans `src/Website` et `src/BackOffice`.
3. Noter dans la PR les échecs déjà présents, sans les corriger.

Pas de commit.

---

## Tâche 1 — API : le bandeau annonce le prochain rendez-vous à 7 jours

**Fichiers**
- `Application/Events/Queries/GetTodayEvent/GetTodayEventQueryHandler.cs` : candidats non
  annulés dont le début est au plus tard **aujourd'hui + 7 jours** (Paris) et qui ne sont pas
  terminés. Les dates se calculent avec `ParisCalendar`.
  - Ordre de choix : d'abord ce qui a lieu aujourd'hui, ou une bourse déjà commencée ; puis le
    début le plus proche ; à égalité, le loto.
  - `LiveStatus` et `LastNumero` restent renseignés pour un loto du jour seulement.
  - `LiveTheme` est renseigné pour tout loto.
- Renommer la requête et le gestionnaire n'est pas demandé ; mettre à jour le commentaire de
  `TodayEventResponse` (« announced up to 7 days ahead »).

**Tests d'abord** — `Application.tests` :
- un loto à J-7 est renvoyé, mais pas à J-8 ;
- une bourse commencée la veille et finissant dans 3 jours est renvoyée ;
- une bourse à J-2 et un loto à J-5 : c'est la bourse qui est renvoyée ;
- un loto et une bourse le même jour : c'est le loto ;
- un événement annulé n'est jamais renvoyé.

**Commit** : `feat(events): annoncer le prochain rendez-vous une semaine à l'avance`

---

## Tâche 2 — Logique pure : identité, compte à rebours, étapes, agenda

**Maquette :** preview/Identites.html — zones ID-halloween, ID-bourse, ID-loto

**Fichiers** — créer `src/Website/src/app/shared/events/` :
- `event-identity.ts` : `eventIdentity(event): 'halloween' | 'loto' | 'books' | 'other'`.
  - Un loto avec `liveTheme === 'Halloween'` → `halloween`.
  - Un autre loto → `loto`.
  - `Books` → `books`.
  - Le reste → `other` (RG-50).
- `countdown.ts` : `countdownLabel(event, now, maxDays?)` → « Aujourd'hui », « Demain »,
  « Dans {n} jours », « En ce moment » ou `null` au-delà de `maxDays` (30 par défaut).
  Les jours sont des jours calendaires à Paris (RG-42) : réutiliser `parisDay()` de
  `today-banner-state.ts`, en le déplaçant ici.
- `agenda.ts` :
  - `groupByStartMonth(events)` (RG-43) ;
  - `calendarMonth(year, month, events)` : les cellules lundi → dimanche, avec pour chaque jour
    l'événement et sa position dans une plage (`one | start | middle | end`) ;
  - `agendaMonths(events)` : du mois du prochain rendez-vous au mois du dernier (RG-51).
- `ics.ts` : `toIcs(event, url)`. Un `VCALENDAR` avec un `VEVENT` (UID = id, DTSTART/DTEND en
  date ou en date et heure, SUMMARY, LOCATION, URL). Les caractères spéciaux sont échappés
  (RG-47).

**Tests d'abord** — un `*.spec.ts` par fichier :
- 31 octobre 2026 → « Demain » pour le 1er novembre ;
- la bourse du 11 au 16 novembre vue le 13 → « En ce moment » ;
- novembre 2026 commence un dimanche : 6 cellules vides avant le 1er ;
- la bourse du 11 au 16 donne `start`, `middle` ×4 et `end` ;
- le `.ics` contient `DTSTART;VALUE=DATE:20261111` et `DTEND;VALUE=DATE:20261117`.

**Commit** : `feat(events): identité, compte à rebours et agenda des événements`

---

## Tâche 3 — Mascottes partagées

**Maquette :** preview/Identites.html — zones ID-papillon, ID-boule ; preview/Papillons.html — zones PA-a

**Fichiers**
- Créer `src/Website/src/app/shared/components/mascots/papillon.component.ts` (`vpd-papillon`,
  entrée `size`, `mood: 'vol' | 'pose'`). Reprendre **seulement la variante `a`** de
  `sources/Papillon.dc.html` :
  - lignes de page **droites et complètes**, coupées par le contour de l'aile ;
  - liseré orange, ailes basses bleu ciel à queues avec un œil orange ;
  - antennes à bout rond ;
  - battement en deux coups puis plané (`pa-flap`, 1,9 s).
- Créer `boule-bingo.component.ts` (`vpd-boule-bingo`). Entrées :
  - `size` ;
  - `num` (chaîne) ;
  - `color: 'bleu' | 'orange' | 'cyan'` ;
  - `mood: 'saute' | 'pose'`.
  Reprendre le SVG de `sources/BouleBingo.dc.html` : sphère pleine, pastille blanche cerclée,
  yeux au-dessus, sourire en dessous, saut et clin d'œil. Couleurs : bleu `#1497d6`,
  orange `#f0801c`, cyan `#7fd8f5`.
  **Le numéro est obligatoire et toujours visible.** Il s'écrit en texte HTML posé sur la
  pastille, pas en `<text>` SVG : Libre Franklin 900, `#041d30`, taille égale à 27 % de la
  boule (22 % pour deux chiffres), centré à 56 % de la hauteur. Il saute avec la boule.
- Déplacer ici `vpd-citrouille`, `vpd-fantome` et `vpd-chauve-souris` depuis
  `feature/tableau/halloween/characters/`, puis mettre à jour les imports du tableau. Les ids
  de dégradés sont suffixés par instance.
- `mascots.css` partagé, ou styles dans chaque composant :
  - `.calm` et `prefers-reduced-motion` coupent les animations ;
  - `animation-play-state: paused` quand la page est cachée.

**Test d'abord** — `boule-bingo.component.spec.ts` : `num="03"` s'affiche ; la couleur change le
dégradé. `papillon.component.spec.ts` : le rendu est `aria-hidden`, et `mood="pose"` n'a pas
d'animation de battement.

**Commit** : `feat(site): mascottes papillon et boule de bingo`

---

## Tâche 4 — Bandeau du site, de J-7 au jour J (E4)

**Maquette :** preview/Bandeau.html — zones E4-bandeau-classique, E4-bandeau-halloween, E4-bandeau-mobile, E4-etape

**Maquette :** preview/Mobile.html — zones E4-bandeau-mobile

**Fichiers**
- `core/layouts/today-banner/today-banner-state.ts` :
  - nouvelle étape `todayBannerStage(today, now)` : `upcoming | today | live | finished |
    ongoing` ;
  - la fermeture est mémorisée par événement **et** par étape (RG-46) : `upcoming` couvre
    J-7 à J-1 ; `today`, `live` et `ongoing` sont chacune une étape.
- `today-banner.component.html` (classique) et la variante Halloween :
  - « Dans {n} jours » ou « Demain » + titre + date (« dimanche 1er novembre ») + ville ;
  - « Aujourd'hui », « En direct », « Terminé » : comme aujourd'hui ;
  - « En ce moment » + « jusqu'au {date} ».
  Le classique perd ses capitales espacées et sa pastille lumineuse : l'étape s'écrit en
  Newsreader italique cyan.

**Test d'abord** — `today-banner-state.spec.ts` : J-7 donne `upcoming` ; un bandeau fermé à J-5
reste fermé à J-1, puis réapparaît le jour J (RG-46). `today-banner.component.spec.ts` :
« Dans 5 jours » et les libellés de chaque étape.

**Commit** : `feat(site): bandeau du prochain rendez-vous dès J-7`

---

## Tâche 5 — Page Événements : en-tête et « À la une » (E1, E2)

**Maquette :** preview/Main.html — zones E1-titre, E1-a-la-une, E1-date-a-la-une, E1-compte-a-rebours, E1-actions-a-la-une, E1-ajouter-agenda, E1-ensuite, E2-a-la-une-halloween, E2-decor, E2-citrouille, E2-fantome-course, E1-nos-rendez-vous, E1-nous-trouver

**Maquette :** preview/AlaUneClassique.html — zones E1-a-la-une-classique

**Maquette :** preview/Mobile.html — zones E1-titre, E1-a-la-une, E1-date-a-la-une, E1-compte-a-rebours, E1-actions-a-la-une, E1-ajouter-agenda, E1-ensuite, E2-a-la-une-halloween, E2-decor, E2-citrouille, E2-fantome-course, E1-nos-rendez-vous

**Fichiers** — `feature/vpd-events/vpd-events-page.component.{ts,html}` et nouveaux
sous-composants dans `feature/vpd-events/components/` :
- `a-la-une` : prochain rendez-vous (RG-41) dans l'identité Halloween (E2) ou classique.
- `ensuite` : le suivant, plus « Puis » et les deux d'après sur ordinateur.

Libellés mot pour mot :
- « Nos prochains rendez-vous » ;
- « Toutes les infos », « Ajouter à mon agenda » (télécharge le `.ics` de la tâche 2) ;
- « Ensuite », « Puis » ;
- « Nos rendez-vous », « Nous trouver ».

Les textes de présentation du loto et de la bourse sont conservés mot pour mot : premier
paragraphe, puis « Lire la suite » qui déplie le reste. La boîte « prochaine date » de
`special-event` disparaît.

**Test d'abord** — spec de la page :
- 31 octobre : « Demain » et le titre du loto à la une ;
- E2 seulement si le loto est Halloween ;
- « Ensuite » montre la bourse ;
- aucune étiquette en capitales espacées.

**Commit** : `feat(site): à la une de la page Événements`

---

## Tâche 6 — Agenda, affichage Liste

**Maquette :** preview/AgendaV2.html — zones E1-agenda, E1-bascule-vue, E1-filtres, E1-mois, E1-bandeau-loto-theme, E1-bandeau-bourse, E1-bandeau-loto, E1-editions-passees

**Maquette :** preview/AgendaV2Mobile.html — zones E1-agenda, E1-bascule-vue, E1-filtres, E1-mois, E1-bandeau-loto-theme, E1-bandeau-bourse, E1-bandeau-loto, E1-editions-passees

**Fichiers** — `feature/vpd-events/components/agenda-liste/` :
- un bandeau en longueur par date, groupé par mois. Sur ordinateur, le mois est en grand
  italique dans une colonne gauche.
- identité de chaque bandeau :
  - **Halloween :** nuit, lune, citrouille, chauve-souris ;
  - **bourse :** papier ligné aux lignes complètes, marge orange, `vpd-papillon` ;
  - **loto sans thème :** nuit bleue, trame de carton, `vpd-boule-bingo` dont le numéro est
    le **jour** du loto, plus deux petites boules ;
  - **autre :** charte du site.
- les filtres sont des onglets soulignés « Tout », « Lotos », « Bourses aux livres »,
  « Autres », sans compteur ; un filtre vide est masqué.
- le compte à rebours s'affiche dans les 30 jours ;
- en bas, « Voir les éditions passées ».

**Test d'abord** — spec : « Novembre » regroupe le loto et la bourse ; la bourse 11 → 16
apparaît une seule fois ; le filtre « Lotos » masque les bourses ; le bandeau du loto
Halloween contient la citrouille.

**Commit** : `feat(site): agenda en bandeaux thématiques`

---

## Tâche 7 — Agenda, affichage Calendrier (par défaut) et bascule

**Maquette :** preview/Calendrier.html — zones E1-calendrier, E1-bascule-vue, E1-legende, E1-navigation-mois, E1-mois-calendrier, E1-jour-allume, E1-detail-jour

**Maquette :** preview/CalendrierMobile.html — zones E1-calendrier, E1-bascule-vue, E1-navigation-mois, E1-mois-calendrier, E1-jour-allume, E1-detail-jour

**Fichiers** — `feature/vpd-events/components/agenda-calendrier/` et la bascule :
- **Bascule « Calendrier / Liste »** : calendrier par défaut, choix mémorisé dans
  `localStorage` `vpd.agenda.vue` (lecture dans un `try/catch`). Les filtres sont partagés par
  les deux vues (RG-51).
- **Ordinateur :**
  - trois mois côte à côte, flèches « Mois précédents » / « Mois suivants » ;
  - légende « Loto d'Halloween », « Loto », « Bourse aux livres » ;
  - les jours d'événement sont de vrais `<button>` avec un `aria-label` complet ;
  - ils s'allument un à un à l'arrivée et à chaque changement de page : `light`, décalage
    0,12 s, décor coupé en mode calme.
  - bourse sur plusieurs jours : bande continue, avec un petit papillon sur le premier jour ;
  - loto Halloween : nuit, chiffre orange qui luit, mini-citrouille ;
  - loto sans thème : nuit bleue et pastille de boule ;
  - cliquer un jour affiche dessous son grand bandeau (même composant que la tâche 6).
    Par défaut, le prochain rendez-vous est sélectionné.
- **Téléphone :** un mois avec flèches. Sous la grille, « Ce mois-ci » et les bandeaux du
  mois ; celui du jour sélectionné est cerclé d'orange.

**Test d'abord** — spec :
- la vue par défaut est le calendrier ;
- le choix « Liste » est conservé au rechargement ;
- novembre 2026 allume le 1er et le 11 → 16 ;
- cliquer le 3 janvier affiche « Loto de Maxence ».

**Commit** : `feat(site): agenda en calendrier`

---

## Tâche 8 — États de la page (E6)

**Maquette :** preview/Etats.html — zones E6-chargement, E6-aucun-rendez-vous

- **Chargement** : silhouettes de « À la une », d'« Ensuite » et de deux lignes.
  `role="status"` et « Chargement des rendez-vous ».
- **Aucun rendez-vous** : « Pas de nouvelle date pour l'instant. », « Les prochains
  rendez-vous apparaîtront ici dès qu'ils seront fixés. », « Voir les éditions passées ».

**Test d'abord** — spec : chaque état avec son libellé.

**Commit** : `feat(site): états de la page Événements`

---

## Tâche 9 — Fiche d'un loto à thème (E3)

**Maquette :** preview/FicheLoto.html — zones E3-entete-halloween, E3-entete-halloween-mobile, E3-decor, E3-citrouilles, E3-fantome-course, E3-encart-direct, E3-infos

`feature/event-detail/event-detail.component.html` : si `eventIdentity(event) === 'halloween'`,
l'en-tête sombre prend la scène Halloween. Le contenu et l'encart « Tableau en direct »
restent les mêmes.
- Libellés : « Tous les événements », « Loto », le compte à rebours, « Date », « Ouverture »,
  « Lieu ».
- Les capitales espacées et la flèche « ← » en texte disparaissent aussi de la fiche classique.

**Test d'abord** — spec : un loto Halloween rend la scène ; un loto Classique n'a aucun décor.

**Commit** : `feat(site): fiche d'un loto à thème`

---

## Tâche 10 — Cartes « Venez nous voir » de l'accueil (E7)

**Maquette :** preview/Accueil.html — zones E7-section, E7-cartes, E7-carte-halloween, E7-carte-bourse, E7-carte-loto

**Maquette :** preview/AccueilMobile.html — zones E7-section, E7-cartes, E7-carte-halloween, E7-carte-bourse

`feature/home/components/vpd-events/vpd-event-sections.{html,scss,ts}` : **seules les cartes
changent**. L'en-tête de section et « Tout l'agenda » ne bougent pas, et l'inclinaison de la
première carte est conservée.
- Contenu de chaque carte : jour, mois, titre, ville · heure, « Détails ».
- Identité de la carte : nuit et petite citrouille pour Halloween ; papier ligné et papillon
  pour la bourse ; nuit bleue et boule de bingo pour un loto sans thème.
- Le compte à rebours s'affiche dans les 30 jours.

**Test d'abord** — spec : trois cartes ; la première porte « Demain » et la citrouille, la
deuxième le papillon.

**Commit** : `feat(site): cartes des prochains rendez-vous sur l'accueil`

---

## Tâche 11 — Backoffice : libellé « Thème du loto » (E5)

**Maquette :** preview/Backoffice.html — zones E5-champ-theme-loto, E5-aide-theme-loto

Dans `create-update-event-dialog`, le champ de `halloween-loto-board` change :
- son libellé devient « Thème du loto » ;
- son aide devient « Habille la page Événements, la fiche, le bandeau du site et le tableau
  sur téléphone. La projection ne change pas. »

**Test d'abord** — mettre à jour la spec du dialogue.

**Commit** : `feat(backoffice): thème du loto`

---

## Tâche 12 — Validation complète et PR

1. Les tests de la tâche 0 : aucun nouvel échec.
2. `npm run build` sur Website (SSR compris) et BackOffice. Dans le HTML rendu par le serveur,
   la page Événements et l'accueil gardent leurs textes ; le décor n'apparaît qu'une fois la
   page chargée dans le navigateur.
3. Recette manuelle en 390 et 1440 px :
   - le calendrier est la vue par défaut, la bascule fonctionne ;
   - les jours s'allument, le clic sur un jour fonctionne ;
   - le bandeau apparaît à J-7 ;
   - le fantôme court une seule fois en passant du bandeau à la page ;
   - le mode calme et `prefers-reduced-motion` coupent les animations ;
   - le `.ics` s'importe dans un agenda de téléphone.
4. Captures 390 et 1440 px de chaque écran dans la PR.
5. PR vers `main`, ou vers `feat/halloween-loto-board` si celle-ci n'est pas encore fusionnée :
   le préciser dans la description.

---

## Zones non implémentées

- **E1-ligne-agenda**, **E1-pictogramme-theme** (preview/Main.html, preview/Mobile.html) :
  première version de l'agenda en lignes, remplacée par la Liste en bandeaux (tâche 6) et le
  Calendrier (tâche 7), à la suite du retour du 2026-10-31.
- **PA-b**, **PA-c**, **PA-d** (preview/Papillons.html) : papillons proposés mais non retenus.
  Seul **PA-a** est implémenté.
