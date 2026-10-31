# Page Événements en agenda et thèmes des lotos — spécification fonctionnelle

> Statut : validée le 2026-10-31.
> Prolonge [`halloween-loto-board`](../halloween-loto-board/01-fonctionnel.md), dont elle reprend
> le thème d'un loto (RG-25), le bandeau du jour (E6, RG-39, RG-40) et la palette Halloween.
> La numérotation des règles continue à RG-41.

## 1. Contexte

La page `/evenement` du site présente l'année de l'association :
- un en-tête « Venez nous voir. » avec un encart à deux lignes (prochain loto, prochaine
  bourse aux livres) ;
- deux grandes cartes de présentation, l'une pour le loto, l'autre pour la bourse aux livres ;
- un bloc « Les autres rendez-vous » ;
- un bloc « Nous trouver ».

L'état des lieux du 2026-10-31 relève :

- **Les dates sont difficiles à lire.**
  - La seule vue d'ensemble est l'encart du haut, qui ne montre qu'une date par type.
  - Les autres lotos et bourses sont cachés derrière « + 3 autres dates » et
    « TOUTES LES DATES → ».
  - Rien ne dit « c'est dans 9 jours ».
- **La page est longue avant d'arriver aux dates** : de longs textes de présentation passent
  avant l'agenda.
- **Les tics visuels « générés » sont présents partout** :
  - étiquettes en capitales espacées en police à chasse fixe (« ÉVÈNEMENTS · TOUTE LA
    SAISON », « PROCHAIN LOTO ») ;
  - numérotation « 01 · », « 02 · » ;
  - flèches « → » en texte ;
  - cartes identiques à bordure fine.
- **Le thème d'un loto n'existe que le jour J**, sur le tableau en direct. Le loto
  d'Halloween n'a aucune présence festive avant ce jour-là.
- **Le bandeau du site n'apparaît que le jour même.** C'est trop tard pour donner envie de
  venir.

## 2. Objectifs

1. Voir **en un coup d'œil le prochain rendez-vous**, avec un compte à rebours, et **toutes les
   dates à venir** dans l'ordre, groupées par mois, sur téléphone comme sur ordinateur.
2. Faire vivre le **thème d'un loto** (aujourd'hui Halloween, demain d'autres fêtes) sur toutes
   les pages publiques qui le concernent :
   - la page Événements ;
   - la fiche du loto ;
   - le bandeau du site ;
   - le tableau en direct, déjà fait.
3. Annoncer chaque rendez-vous **une semaine à l'avance** par le bandeau du site.
4. Un rendu soigné et sobre, sans les tics visuels relevés ci-dessus.
5. Un système de thèmes **extensible** : ajouter une fête plus tard ne doit demander qu'un
   nouveau décor, pas de refonte.

## 3. Hors périmètre

- **Les autres fêtes** (Rois mages, muguet, Noël…) : elles seront dessinées une par une, à
  l'approche de chacune. Cette spec ne livre que **Classique** et **Halloween**.
- **La projection du backoffice** : elle reste simple et inchangée (décision du 2026-10-31).
- **Le tableau en direct** : déjà couvert par `halloween-loto-board`.
- **La page `/evenement/all` des éditions passées** : son contenu ne change pas. Seul le lien
  qui y mène est revu.
- **Les textes de présentation** du loto et de la bourse aux livres : ils sont conservés mot
  pour mot.

## 4. Acteurs

| Acteur | Rôle |
|---|---|
| **Visiteur du site** | Cherche la prochaine date, sur téléphone le plus souvent. |
| **Organisateur** (admin backoffice) | Choisit le thème d'un loto. |

## 5. Parcours

### P1 — Trouver la prochaine date

1. Le visiteur ouvre « Événements ». En haut, sans faire défiler : le prochain rendez-vous en
   grand, avec son jour, son mois, « Dans 9 jours », le lieu et l'heure d'ouverture. Juste à
   côté, ou dessous sur téléphone : le suivant.
2. Si ce rendez-vous est un loto à thème, l'en-tête en prend l'habit. Pour Halloween : nuit,
   lune, citrouille vivante, un fantôme qui passe une fois.
3. Plus bas, l'agenda liste toutes les dates à venir, mois par mois. Des filtres
   (« Tout », « Lotos », « Bourses aux livres », « Autres ») réduisent la liste.
4. Un clic sur une date ouvre sa fiche.

### P2 — Être prévenu en naviguant sur le site

1. À partir de 7 jours avant un rendez-vous, un bandeau fin l'annonce au-dessus de l'en-tête,
   sur toutes les pages : « Dans 12 jours · Grand loto d'Halloween · dimanche 1er novembre ».
2. Le jour J, le bandeau se comporte comme aujourd'hui : « Aujourd'hui », puis « En direct »
   et « Terminé » pour un loto.
3. Fermer le bandeau le masque jusqu'à l'étape suivante (voir RG-46).

### P3 — Habiller un loto

1. Dans le backoffice, l'organisateur choisit le **thème du loto** : Classique ou Halloween.
2. La page Événements, la fiche, le bandeau et le tableau sur téléphone prennent ce thème.

## 6. Écrans

| # | Écran | Nature | Origine |
|---|---|---|---|
| **E1** | Page Événements en agenda — ordinateur 1440 px et téléphone 390 px | *modifié* | Site `/evenement` (`vpd-events-page`) |
| **E2** | En-tête « À la une » d'un loto Halloween | *nouveau* (variante de E1) | — |
| **E3** | Fiche d'un loto à thème — en-tête Halloween | *modifié* | Site `/evenement/:id` (`event-detail`) |
| **E4** | Bandeau du site, de J-7 au jour J — classique et Halloween | *modifié* | Bandeau du jour (`today-banner`) et sa variante Halloween (E6 de `halloween-loto-board`) |
| **E5** | Réglage « Thème du loto » | *modifié* | Backoffice, champ E1 de `halloween-loto-board` |
| **E6** | États de la page Événements : chargement, aucun rendez-vous, filtre vide | *modifié* | États de `vpd-events-page` |
| **E7** | Cartes « Venez nous voir » de l'accueil | *modifié* | Accueil, section des prochains rendez-vous (`app-vpd-events-section`), seules les cartes |

### E1 — Page Événements en agenda

De haut en bas :

1. **À la une.**
   - Le prochain rendez-vous en grand :
     - le jour en très grand (« 01 ») et le mois (« novembre ») ;
     - le compte à rebours (« Dans 9 jours », « Demain », « Aujourd'hui », « En ce moment »
       pour une bourse sur plusieurs jours) ;
     - le type (« Loto », « Bourse aux livres », ou le nom pour un autre événement) ;
     - le titre, la ville, l'heure d'ouverture ;
     - deux actions : « Toutes les infos » et « Ajouter à mon agenda ».
   - À côté (ordinateur) ou dessous (téléphone), une carte plus petite « Ensuite » pour le
     rendez-vous suivant.
   - Le titre de page devient une phrase simple : « Nos prochains rendez-vous ».
2. **Agenda.** Deux affichages, au choix du visiteur par une bascule « Calendrier / Liste »
   en haut de l'agenda ; **Calendrier par défaut** (RG-51).
   - **Calendrier** : trois mois côte à côte sur ordinateur, un mois avec flèches sur
     téléphone ; les jours d'événement s'allument un à un à l'arrivée, chacun dans son
     identité ; une bourse sur plusieurs jours forme une bande continue ; toucher un jour
     affiche son bandeau dessous (sur téléphone, les bandeaux du mois sont listés sous la grille).
   - **Liste** : un bandeau en longueur par date, groupé par mois, dans l'identité de
     l'événement.
   Ce qui suit décrit le contenu commun aux deux.
   - Toutes les dates à venir, groupées par mois (« Novembre 2026 »), dans l'ordre.
   - Chaque ligne :
     - un bloc de date : jour en grand, jour de la semaine en toutes lettres ; plage
       « 11 → 16 » pour un événement sur plusieurs jours ;
     - une étiquette de type, en couleur **et** avec une icône : loto, livre ou étoile ;
     - le titre, la ville et l'heure d'ouverture ;
     - le compte à rebours si la date tombe dans les 30 jours ;
     - un pictogramme du thème pour un loto à thème (petite citrouille).
   - Filtres en haut de l'agenda : « Tout », « Lotos », « Bourses aux livres », « Autres »,
     avec le nombre de dates de chacun. Un filtre sans aucune date est masqué.
   - En bas : le lien « Voir les éditions passées », vers `/evenement/all`.
3. **Nos rendez-vous.** Les deux présentations, loto et bourse aux livres : mêmes photos et
   textes qu'aujourd'hui, mise en page resserrée, sans la boîte « prochaine date »
   (l'agenda la donne déjà).
4. **Nous trouver.** Inchangé dans son contenu, avec des titres sans capitales espacées.

### E2 — À la une, loto Halloween

Quand le prochain rendez-vous est un loto Halloween, le bloc « À la une » prend la palette
Halloween :
- nuit, lune, étoiles et brume ;
- la citrouille vivante qui cligne des yeux, posée près de la date ;
- l'araignée qui descend de temps en temps ;
- à l'arrivée sur la page, le fantôme qui court le traverse une fois.

Le contenu ne change pas. Le reste de la page reste dans la charte du site : seule la ligne
du loto dans l'agenda porte sa petite citrouille.

### E3 — Fiche d'un loto à thème

L'en-tête sombre de la fiche (nom, dates, lieu, encart « Tableau en direct ») prend le décor du
thème : pour Halloween, la palette nuit d'aubergine, la lune, la citrouille, l'araignée et le
fantôme qui passe une fois. Le reste de la fiche (photos, lots, plan) ne change pas. Un loto
« Classique » garde la fiche actuelle.

### E4 — Bandeau du site, de J-7 au jour J

| Étape | Texte | Lien |
|---|---|---|
| J-7 à J-2 | « Dans {n} jours » · titre · « dimanche 1er novembre » · ville | « Voir l'événement » |
| J-1 | « Demain » · titre · heure d'ouverture · ville | « Voir l'événement » |
| Jour J, loto pas commencé | « Aujourd'hui » · titre · « portes {heure} » · ville | « Voir l'événement » |
| Jour J, loto en direct | « En direct » · titre · « Dernier numéro » + pastille | « Suivre le tableau » |
| Jour J, loto terminé | « Le loto est terminé, merci à tous ! » | « Voir le tableau final » |
| Bourse sur plusieurs jours, en cours | « En ce moment » · titre · « jusqu'au {date} » | « Voir l'événement » |

- **Thème Classique** : la mise en page du bandeau actuel, sans capitales espacées ni pastille
  lumineuse.
- **Thème Halloween** : la variante E6 de `halloween-loto-board`. Le fantôme court une fois par
  chargement de page, à toutes les étapes.

### E5 — Réglage « Thème du loto »

Le champ du backoffice s'appelle désormais « Thème du loto », avec la même liste
(« Classique », « Halloween »). Sa ligne d'aide devient : « Habille la page Événements, la fiche,
le bandeau du site et le tableau sur téléphone. La projection ne change pas. »

### E7 — Cartes « Venez nous voir » de l'accueil

Seules les trois cartes de la section changent : l'en-tête de section, le lien vers l'agenda
et le reste de l'accueil restent tels quels. Chaque carte garde son contenu (jour, mois, titre,
ville et heure, « Détails ») et prend l'identité de son événement (RG-50) : nuit et petite
citrouille pour le loto d'Halloween, papier ligné et papillon pour la bourse aux livres, nuit
bleue et boule de bingo pour un loto sans thème. Le compte à rebours s'ajoute dans les 30 jours
(RG-42). Sur téléphone, les cartes s'empilent.

### E6 — États

- **Chargement** : silhouettes de la date à la une et de trois lignes d'agenda.
- **Aucun rendez-vous à venir** : « Pas de nouvelle date pour l'instant. Les prochains
  rendez-vous apparaîtront ici dès qu'ils seront fixés. » et le lien « Voir les éditions
  passées ».
- **Filtre vide** : impossible, puisqu'un filtre sans date est masqué (voir E1).

## 7. Règles métier

| N° | Règle |
|---|---|
| **RG-41** | « À la une » montre le rendez-vous à venir **le plus proche**, y compris celui du jour et une bourse en cours. « Ensuite » montre le suivant. Les événements annulés et passés n'apparaissent ni à la une ni dans l'agenda. |
| **RG-42** | Compte à rebours, calculé en jours calendaires à l'heure de Paris : « Aujourd'hui » (J), « Demain » (J-1), « Dans {n} jours » (n ≥ 2) ; « En ce moment » pour un événement sur plusieurs jours déjà commencé et pas terminé. Dans l'agenda, il n'apparaît que dans les 30 jours. |
| **RG-43** | L'agenda groupe par mois **de début**. Un événement sur plusieurs jours apparaît une seule fois, au mois de son début, avec sa plage. |
| **RG-44** | Le thème d'un loto (RG-25, Classique par défaut) s'applique à E2, E3, E4 et au tableau. Un événement d'un autre type n'a pas de thème. La projection du backoffice ne change jamais. |
| **RG-45** | Le bandeau du site annonce **le prochain rendez-vous à venir dans les 7 jours**, tous types confondus. S'il y en a plusieurs, il montre le plus proche ; à égalité, le loto. Il reprend RG-16 à RG-20 de `loto-live-board` pour le jour J. |
| **RG-46** | La fermeture du bandeau est mémorisée sur l'appareil pour l'événement **et l'étape** : « avant » (J-7 à J-1), « jour J », « en direct ». Le bandeau réapparaît au changement d'étape. |
| **RG-47** | « Ajouter à mon agenda » télécharge un fichier `.ics` de l'événement (titre, début, fin, lieu, lien vers la fiche), sans compte ni service tiers. |
| **RG-48** | Les animations d'un thème suivent RG-34, RG-36 et RG-37 : elles respectent le réglage « réduire les animations » et le bouton Calmer, ne clignotent jamais plus de trois fois par seconde et s'arrêtent quand la page est cachée. Le fantôme court une fois par chargement de page, quel que soit l'endroit (bandeau ou à la une). |
| **RG-50** | Chaque type d'événement a une **identité visuelle** utilisée dans l'agenda (liste et calendrier) et les cartes de l'accueil : la bourse aux livres a toujours « Papier et papillons » (papier ligné aux lignes droites et complètes, papillon aux ailes de pages — dessin A « Marque-page ») ; un loto sans thème a « Boules et carton » (nuit bleue, vraies boules de bingo aux couleurs de l'association, pastille blanche et gros numéro, la bleue saute et cligne de l'œil) ; un loto à thème prend l'identité de son thème (Halloween : « Nuit de citrouille »). Un événement « Autre » garde la charte du site. |
| **RG-51** | L'agenda s'ouvre en **Calendrier**. La bascule « Calendrier / Liste » est mémorisée sur l'appareil ; les filtres s'appliquent aux deux affichages. Le calendrier commence au mois du prochain rendez-vous et va jusqu'au mois du dernier rendez-vous connu. |
| **RG-49** | Un thème est un **habillage** : palette, personnages, décor, animations. Il ne change jamais le contenu, l'ordre des informations ni les libellés. Ajouter un thème ne modifie ni la page ni ses règles. |

## 8. Critères d'acceptation

1. Sur téléphone (390 px), le prochain rendez-vous, son compte à rebours et le suivant sont
   visibles sans faire défiler (RG-41, RG-42).
2. L'agenda liste toutes les dates à venir par mois ; une bourse du 11 au 16 novembre apparaît
   une fois, en novembre, avec « 11 → 16 » (RG-43). Les filtres affichent leur nombre et
   masquent ceux qui sont vides.
3. Un loto Halloween à la une affiche E2, sa ligne d'agenda porte la citrouille, sa fiche
   affiche E3 ; le même loto en Classique n'affiche aucun décor (RG-44).
4. Le bandeau apparaît à J-7 avec « Dans 7 jours », passe à « Demain », « Aujourd'hui », puis
   « En direct » pour un loto ; fermé à J-5, il reste fermé jusqu'à J-1, puis réapparaît le
   jour J (RG-45, RG-46).
5. « Ajouter à mon agenda » produit un `.ics` que l'agenda du téléphone importe (RG-47).
6. Avec « réduire les animations » ou le mode calme, aucun décor ne bouge ; le fantôme ne court
   qu'une fois par chargement, même en passant du bandeau à la page Événements (RG-48).
7. Aucune étiquette en capitales espacées ni flèche « → » en texte sur la page Événements.

## 9. Contraintes

- **Dépend de `feat/halloween-loto-board`**, qui apporte le thème du loto, ses personnages et
  le bandeau Halloween. Cette branche part de la sienne et devra être rebasée sur `main` après
  sa fusion.
- **Pas de changement de schéma** : le thème existe déjà. Seule la requête du bandeau change :
  « le prochain rendez-vous dans les 7 jours » remplace « l'événement du jour ».
- **Charte** : `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md` et les jetons de
  `src/Website/tailwind.config.js` pour tout ce qui n'est pas thématique. Polices du site
  (Newsreader, Libre Franklin) ; Fraunces seulement dans les habillages Halloween.
- **Pages rendues côté serveur (SSR)** : la page Événements et la fiche sont indexées. Les
  décors sont décoratifs, sans contenu, et ne retardent pas l'affichage. Les animations ne
  démarrent qu'une fois dans le navigateur.
- **Accessibilité** : les filtres sont de vrais boutons avec état ; le compte à rebours est du
  texte ; tout décor porte `aria-hidden`.

## 10. Questions ouvertes

- ~~Q4~~ — Papillon retenu le 2026-10-31 : **A « Marque-page »**.

- **Q1** — Le bandeau à J-7 pour **tous** les types : faut-il aussi l'afficher pour un
  événement « Autre » (par exemple une marche), ou seulement pour les lotos et les bourses ?
- **Q2** — Faut-il un lien vers la page Événements dans le bandeau (« Tout l'agenda ») en plus
  du lien vers la fiche ?
