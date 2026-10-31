# Tableau du loto : mode Halloween animé — spécification fonctionnelle

> Statut : validée le 2026-10-31. Loto prévu le 2026-11-01.
> Étend [`loto-live-board/01-fonctionnel.md`](../loto-live-board/01-fonctionnel.md) : les règles
> RG-01 à RG-24 restent valables, la numérotation continue à RG-25.

## 1. Contexte

Le tableau en direct du site (`/evenement/<id>/tableau`, écran E4 de *loto-live-board*) est
l'écran que les joueurs gardent ouvert sur leur téléphone pendant tout l'après-midi. Le loto du
2026-11-01 tombe au lendemain d'Halloween : l'association veut que ce tableau prenne un habit
d'Halloween, vivant et soigné, sans rien perdre de sa lisibilité.

Le jour du loto, les joueurs n'auront que leur téléphone. Le mode Halloween est donc pensé
**pour le mobile uniquement**. La projection du backoffice ne change pas.

L'écran actuel a aussi quelques tics visuels qui le font paraître généré automatiquement :
petites étiquettes en police à chasse fixe, en capitales très espacées, au-dessus de chaque bloc
(« DERNIER NUMÉRO », « PRÉCÉDENTS », « TOUS LES NUMÉROS »), une pastille « EN DIRECT » avec un
point lumineux, des cartes à bordure fine identiques partout, des phrases d'explication en pied
de page (« Mis à jour automatiquement, pas besoin de recharger la page. »). Le mode Halloween
s'en débarrasse.

## 2. Objectifs

1. Un **mode Halloween** du tableau mobile, choisi par l'organisateur pour un événement.
2. Un décor **vivant en permanence** : les personnages et objets bougent par eux-mêmes (une
   citrouille qui cligne des yeux et dont la bougie vacille, une araignée qui descend au bout de
   son fil, la brume qui ondule), pas seulement quand ils apparaissent.
3. Une **animation propre à chaque moment du jeu** : nouveau numéro, numéro annulé, ligne gagnée,
   bingo, pause, partie suivante (tableau vidé), début et fin du loto.
4. Une **palette Halloween** qui remplace la palette bleu nuit, avec les mêmes garanties de
   lisibilité.
5. Un rendu **fini et maîtrisé** : des personnages dessinés avec soin, des mouvements doux et
   physiques, aucune impression de gadget ni de modèle générique.
6. Un écran qui reste **fluide sur un téléphone d'entrée de gamme** et **sobre en batterie**
   sur tout un après-midi.

## 3. Hors périmètre

- La projection du backoffice (`evenement/:id/loto-tableau`) et le bandeau « Aujourd'hui » : ils
  restent en thème classique.
- Le tableau en largeur tablette et ordinateur : il garde le thème classique (voir RG-26).
- Le son et la vibration : aucun, ni par défaut ni en option (décision du 2026-10-31).
- D'autres thèmes (Noël…) : le réglage est prévu pour en accueillir, mais seul Halloween est
  dessiné.
- Les règles de jeu, le flux en direct et ses garanties (RG-10 à RG-15) : inchangés.
- Le nettoyage des tics visuels du **thème classique** : voir la question ouverte Q1.

## 4. Acteurs

| Acteur | Rôle |
|---|---|
| **Organisateur** (admin backoffice) | Choisit le thème du tableau dans la fiche de l'événement. |
| **Animateur** | Anime le loto comme aujourd'hui ; ses actions déclenchent les animations chez les joueurs. |
| **Joueur mobile** | Suit le tableau sur son téléphone, peut calmer les animations. |

## 5. Parcours

### P1 — Choisir le thème

1. L'organisateur ouvre la création ou la modification d'un événement de type loto au backoffice.
2. Il choisit « Thème du tableau en direct : Classique / Halloween ». Classique par défaut.
3. Les téléphones déjà connectés basculent au prochain état reçu, sans recharger la page.

### P2 — Suivre le loto en mode Halloween

1. Avant le début, le joueur arrive sur une scène de nuit : une citrouille endormie au pied de la
   lune, « Le loto va commencer », la première partie et ses lots.
2. Au premier numéro, la scène se réveille : la citrouille ouvre les yeux, le tableau apparaît.
3. À chaque numéro, une nuée de chauves-souris traverse l'écran ; le grand numéro s'embrase et la
   case correspondante s'allume comme une bougie.
4. Quand une ligne est gagnée, des citrouilles surgissent en riant et des bonbons tombent du haut
   de l'écran, avec le lot gagné.
5. Pendant la pause, un chaudron bouillonne et une silhouette sur un balai passe devant la lune.
6. À la partie suivante, un fantôme traverse le tableau et souffle les cases une à une, puis
   annonce la nouvelle partie.
7. À la fin, les citrouilles saluent et le fantôme dit au revoir.
8. À tout moment, le joueur peut passer les animations en « calmes ».

## 6. Écrans

| # | Écran | Nature | Origine |
|---|---|---|---|
| **E1** | Réglage « Thème du tableau en direct » | *modifié* | Backoffice, dialogue de création / modification d'événement (`create-update-event-dialog`) |
| **E2** | Tableau Halloween en jeu — mobile 390 px | *modifié* | Site `evenement/:id/tableau`, E4 de *loto-live-board* (état « en jeu ») |
| **E3** | Animations d'événements (storyboards) : nouveau numéro, annulation, ligne gagnée, bingo, partie suivante | *nouveau* | — |
| **E4** | États Halloween : avant le début, pause, terminé, chargement, reconnexion / hors ligne, introuvable | *modifié* | États de E4 de *loto-live-board* ; l'état « pause » est *nouveau* côté mobile |
| **E5** | Bouton « Animations : pleines / calmes » | *nouveau* | Dans l'en-tête de E2 et E4 |

### E1 — Réglage du thème

Un champ « Thème du tableau en direct » avec deux choix, « Classique » et « Halloween », visible
seulement pour un événement de type loto. Une ligne d'aide : « Halloween s'affiche sur les
téléphones des joueurs. La projection ne change pas. »

### E2 — Tableau Halloween en jeu

Même contenu et même ordre que l'écran actuel : retour vers l'événement, état de la connexion,
dernier numéro, cinq précédents, grille des 90, partie et ligne, lots, bingo. Ce qui change :

- **Scène** : ciel de nuit en haut de page, lune voilée derrière le dernier numéro, nuages qui
  glissent lentement devant, brume au bas de l'écran qui ondule.
- **Dernier numéro** : chiffres couleur citrouille avec une lueur chaude qui respire. Une
  citrouille sculptée posée sur le bord du bloc : ses yeux clignent et regardent de côté de temps
  en temps, la lueur de sa bougie vacille, sa bouche s'étire en sourire après chaque numéro.
- **Précédents** : petites étiquettes couleur os, comme des pierres tombales arrondies.
- **Grille** : case non sortie sombre ; case sortie couleur cire de bougie ; dernier numéro en
  citrouille avec une flammèche qui danse au-dessus ; voisins du « +1 / −1 » cerclés de vert
  sorcière.
- **Toile d'araignée** dans un coin de la grille ; l'araignée descend au bout de son fil toutes
  les 20 à 40 s, se balance, puis remonte.
- **Feux follets** : deux ou trois petites lueurs qui dérivent lentement dans le fond.
- **Partie, lots, bingo** : blocs sans bordure fine générique, titres en toutes lettres
  (« Dernier numéro », « Les cinq d'avant », « Partie 2 sur 8 »), sans capitales espacées ni
  police à chasse fixe.
- **État de la connexion** : un mot simple (« En direct », « Reconnexion… », « Hors ligne »)
  sans pastille lumineuse.
- Plus de phrase d'explication en pied de page.

### E3 — Animations d'événements

| Moment | Ce qu'on voit | Durée |
|---|---|---|
| **Nouveau numéro** | 3 à 5 chauves-souris traversent l'écran en vol battu, trajectoires courbes et décalées, de tailles différentes (profondeur). L'ancien numéro part en fumée, le nouveau s'embrase en partant du centre. L'ancien dernier numéro glisse vers les précédents. La case de la grille s'allume avec une flammèche. La citrouille sourit. | ≈ 1,6 s |
| **Numéro annulé** | Le numéro s'éteint en volute de fumée, la case redevient sombre, le précédent reprend sa place. Pas de chauves-souris. | ≈ 0,8 s |
| **Ligne gagnée** | Trois citrouilles surgissent du bas de l'écran en rebondissant, bouches qui rient (mâchoire qui s'ouvre et se ferme), yeux plissés. Des bonbons emballés tombent du haut en tournoyant et rebondissent au bas de l'écran avant de s'évanouir. Bannière « Ligne gagnée ! » avec le nom du lot. Le lot est ensuite barré dans la liste. | ≈ 4 s |
| **Bingo** | Comme la ligne, en plus fort : pluie de bonbons dense, nuée de chauves-souris, bannière « Bingo ! ». | ≈ 5 s |
| **Partie suivante** | Un fantôme entre par la gauche, traverse la grille en ondulant ; sur son passage les cases s'éteignent une à une comme des bougies soufflées. Il ressort à droite en faisant coucou, puis « Partie 3 sur 8 · <nom> » apparaît. | ≈ 3 s |

### E4 — États Halloween

- **Avant le début** : scène de cimetière sous la lune, citrouille endormie (paupières closes,
  respiration lente, petits « z » qui montent), « Le loto va commencer », première partie et ses
  lots. Au premier numéro, la citrouille ouvre les yeux.
- **Pause** *(nouveau sur mobile)* : chaudron qui bouillonne (bulles qui montent et éclatent,
  vapeur verte), une silhouette sur un balai passe devant la lune toutes les 15 à 25 s. Titre
  « Pause », « On reprend dans quelques minutes », numéros du bingo, partie suivante et ses lots.
- **Loto terminé** : les citrouilles saluent, le fantôme fait au revoir, quelques bonbons
  tombent une seule fois. « Loto terminé », « Merci à tous ! », bingo, lien vers les événements.
- **Chargement** : silhouettes du décor qui apparaissent en fondu, sans squelettes gris.
- **Reconnexion / hors ligne** : une chauve-souris suspendue la tête en bas, endormie, à côté du
  message « Connexion perdue. Le tableau se remettra à jour tout seul. » Le dernier état reste
  affiché.
- **Introuvable** : un petit fantôme qui cherche, lanterne à la main, et le message de RG-15.

### E5 — Animations pleines / calmes

Un petit bouton dans l'en-tête, à côté de l'état de connexion. En « calmes » : décor figé dans une
pose soignée, aucune animation d'événement ; les changements se lisent par un simple fondu des
chiffres et des cases. Le choix est mémorisé sur le téléphone.

## 7. Palette proposée

Remplace la palette bleu nuit en mode Halloween uniquement.

| Rôle | Nom | Teinte |
|---|---|---|
| Fond de page | Nuit d'aubergine | `#120a1c` |
| Fond des blocs | Velours | `#1d1229` |
| Case non sortie | Ardoise violette | `#2a1c3a` |
| Dernier numéro, accent principal | Citrouille | `#ff7b1c` |
| Lueur chaude | Braise | `#ffb347` |
| Case sortie, précédents | Cire de bougie | `#f4e8cf` |
| Voisins « +1 / −1 », chaudron | Vert sorcière | `#9bd85a` |
| Texte secondaire | Lilas fané | `#b8a5cf` |
| Puces de partie | Prune | `#5b3482` |

La maquette présente cette palette et une variante plus « sang et os » pour comparaison.

## 8. Règles métier

| N° | Règle |
|---|---|
| **RG-25** | Chaque événement de type loto porte un **thème du tableau en direct** : Classique (par défaut) ou Halloween. Il se règle dans le backoffice et parvient au site avec l'état de l'événement, y compris par le flux en direct. Un changement de thème s'applique au téléphone sans rechargement. |
| **RG-26** | Le mode Halloween ne s'affiche que sur le tableau public du site, et seulement en largeur téléphone (moins de 768 px). Au-delà, et sur la projection du backoffice, le thème classique s'affiche. |
| **RG-27** | Les animations d'événement (E3) ne se jouent que sur un **changement reçu pendant que la page est ouverte**. Le premier affichage et le retour d'un téléphone sorti de veille montrent l'état directement, sans rejouer ce qui s'est passé. |
| **RG-28** | Après une reconnexion qui rattrape plusieurs changements, une seule animation est jouée, la plus importante, dans cet ordre : bingo, ligne gagnée, partie suivante, nouveau numéro, annulation. |
| **RG-29** | Les données ont toujours la priorité sur le décor : un nouvel état s'affiche **immédiatement**, même si une animation est en cours. Une animation en cours est écourtée en fondu quand une animation plus importante arrive, et laisse la suivante se jouer quand elle est de même rang. Les animations ne s'empilent jamais. Seule exception : à la partie suivante, les cases sorties s'éteignent derrière le fantôme, en 2 s au plus. |
| **RG-30** | Aucun élément animé ne masque le dernier numéro plus de 0,3 s d'affilée, ni la case qui vient de s'allumer. Les bannières « Ligne gagnée ! » et « Bingo ! » se placent sous le dernier numéro. |
| **RG-31** | **Pause** sur mobile : le tableau est en pause quand la partie précédente, marquée « pause après », est terminée et qu'aucun numéro n'est encore sorti dans la partie en cours. Il revient en jeu au premier numéro. *(Remplace, pour le mobile, la décision 3 de* loto-live-board *: la pause se déduit de l'état existant, sans nouvelle information envoyée.)* |
| **RG-32** | **Partie suivante** : l'animation du fantôme se joue quand la partie en cours change et que le tableau se vide. Si la nouvelle partie commence par une pause (RG-31), le fantôme passe d'abord, puis l'écran Pause s'affiche. |
| **RG-33** | **Ligne gagnée** : se déclenche quand un lot de la partie en cours passe à « gagné ». La bannière nomme ce lot. Le bingo gagné déclenche l'animation Bingo à la place. |
| **RG-34** | Le bouton « Animations : pleines / calmes » (E5) est mémorisé sur le téléphone. Si le téléphone demande de réduire les animations, le mode calme s'applique d'office et le bouton l'indique. |
| **RG-35** | Lisibilité : le dernier numéro garde au moins sa taille actuelle ; tous les textes ont un contraste d'au moins 4,5:1 (7:1 pour les numéros) avec leur fond réel, lueurs et brume comprises ; sorti, non sorti et dernier numéro restent distincts en niveaux de gris (comme RG-01). |
| **RG-36** | Sécurité visuelle : aucun clignotement de plus de trois éclats par seconde, aucun flash plein écran. |
| **RG-37** | Fluidité et batterie : le décor tient 60 images par seconde sur un téléphone Android d'entrée de gamme de trois ans ; il s'arrête quand la page n'est pas visible ; une pluie de bonbons compte au plus 40 bonbons à la fois. La page n'ajoute pas plus de 150 Ko au premier chargement du tableau. |
| **RG-38** | Le mode Halloween n'a ni son ni vibration. |

## 9. Critères d'acceptation

1. Au backoffice, un loto peut passer de Classique à Halloween ; un téléphone ouvert sur son
   tableau bascule au prochain état reçu, sans rechargement (RG-25).
2. Sur un téléphone (390 px), le thème Halloween s'affiche ; à 1024 px, le thème classique
   (RG-26). La projection du backoffice ne change pas.
3. À l'ouverture de la page pendant le jeu, aucune animation d'événement ne se joue ; le décor
   bouge (RG-27, objectif 2).
4. Chaque moment du tableau E3 se déclenche sur le bon changement et une seule fois (RG-29,
   RG-32, RG-33).
5. Trois numéros tirés pendant une coupure de 30 s : au retour, une seule animation « nouveau
   numéro » (RG-28).
6. Deux numéros tirés à 0,5 s d'intervalle : le second s'affiche tout de suite, sans attendre la
   fin de la première nuée de chauves-souris (RG-29).
7. Après une partie marquée « pause après », l'écran Pause s'affiche et disparaît au premier
   numéro de la partie suivante (RG-31).
8. En mode calme, ou avec « réduire les animations » activé sur le téléphone, rien ne bouge en
   dehors des fondus de données ; le choix survit au rechargement (RG-34).
9. Contrastes, niveaux de gris et clignotements vérifiés sur les maquettes et la page réelle
   (RG-35, RG-36).
10. Enregistrement de performance sur un profil « téléphone moyen » (processeur ralenti ×4) :
    pas d'image au-delà de 50 ms pendant une pluie de bonbons ; décor arrêté en onglet caché
    (RG-37).

## 10. Contraintes

- **Délai** : loto le 2026-11-01. Le travail doit pouvoir être livré par morceaux : décor et
  palette, puis nouveau numéro et ligne gagnée, puis le reste. Le plan ordonne les tâches ainsi.
- Le thème est une donnée de l'événement : elle demande une évolution de la base et du contrat
  de l'API (migration), déployée avec le site.
- Le tableau est rendu dans le navigateur (pas de rendu serveur), avec les jetons de
  `src/Website/tailwind.config.js` pour le thème classique ; le mode Halloween définit ses
  propres jetons, documentés dans les maquettes.
- Les illustrations sont dessinées en vectoriel (pas d'images lourdes ni de vidéos), pour rester
  nettes sur tous les écrans et légères.
- Le décor et les animations ne doivent pas interférer avec le lecteur d'écran : ils sont
  ignorés par les technologies d'assistance, et les annonces de numéro restent celles
  d'aujourd'hui.

## 11. Questions ouvertes

- **Q1** — Les tics visuels « générés » (étiquettes en capitales espacées, pastille lumineuse,
  phrase de pied de page) sont retirés du mode Halloween. Faut-il aussi les retirer du **thème
  classique** du tableau mobile, dans cette même livraison ?
- **Q2** — Palette : la proposition « nuit d'aubergine / citrouille / cire » ou la variante
  « sang et os » présentée dans les maquettes ?
- **Q3** — Délai : si tout ne tient pas pour demain, l'ordre de priorité proposé est
  (1) réglage + palette + décor vivant, (2) nouveau numéro, (3) ligne gagnée et bingo,
  (4) partie suivante, (5) pause, début, fin. D'accord ?
