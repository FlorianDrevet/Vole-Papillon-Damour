# Tableau de loto en direct — spécification fonctionnelle

> Statut : validée le 2026-10-13

## 1. Contexte

Le loto annuel de l'association rassemble plus de 500 personnes. Un bénévole tire les boules et
saisit chaque numéro dans le backoffice, sur la page « Tableau de loto » d'un événement de type
Bingo. Cette même page est projetée sur une toile au vidéoprojecteur : c'est le tableau que la
salle regarde. Une page publique du site affiche le même tableau en lecture seule et se met à jour
en direct, pour que les joueurs suivent depuis leur téléphone.

L'état des lieux du 2026-10-13 relève :

- **Projection peu lisible** : cases blanches et chiffres gris foncé, numéros sortis en bleu,
  police manuscrite (Caveat). Sous la lumière de scène, le noir projeté devient gris et les
  teintes se délavent. Distinguer un numéro sorti d'un numéro non sorti repose surtout sur la
  couleur, qui disparaît la première.
- **Contrôles visibles par la salle** : les boutons « annuler », « ligne gagnée » et « bingo »,
  ainsi que le curseur de la souris, sont projetés.
- **Pages qui plantent hors partie** : avant la première partie ou après la dernière, les deux
  pages (backoffice et site) tombent en erreur au lieu d'afficher un état.
- **Direct fragile** :
  - un numéro saisi peut ne jamais atteindre une partie des téléphones quand l'API tourne sur
    plusieurs instances, ce qui arrive précisément quand 500 personnes se connectent ;
  - deux onglets d'un même navigateur peuvent s'évincer mutuellement ;
  - un flux resté silencieux pendant une pause peut être coupé par l'infrastructure ;
  - un événement inexistant laisse la connexion ouverte sans rien renvoyer.
- **Page publique introuvable** : aucun lien du site ne mène au tableau en direct.

Les tests automatisés existants passent (Application 28/28, SSE 5/5, Domain 35/35) : les règles de
jeu côté serveur sont saines. Les défauts sont dans la présentation et dans le transport du direct.

## 2. Objectifs

1. Un tableau projeté **lisible à 20–30 m sous un fort éclairage de scène**, où l'état
   « sorti » se lit par la **luminosité** et non par la seule couleur. Deux palettes (sombre et
   claire) sont disponibles, et l'animateur bascule de l'une à l'autre en direct pour choisir sur
   place celle qui rend le mieux.
2. Une grille qui occupe **le maximum de la surface projetée** pendant le jeu.
3. **Aucun élément de régie visible par la salle** pendant le jeu, sans perdre en rapidité de
   saisie pour l'animateur (un seul écran, contrôles discrets).
4. Un **tableau mobile** centré sur le dernier numéro, qui reste à jour et signale clairement une
   perte de connexion.
5. Le **jour J**, un bandeau sur tout le site annonce l'événement et, pendant le loto, mène au
   tableau en direct.
6. Un direct **fiable** : chaque téléphone reçoit chaque changement en quelques secondes au plus,
   quel que soit le nombre d'instances de l'API, et se resynchronise seul après une coupure.

## 3. Hors périmètre

- Modifier les règles de jeu (types de parties, lignes, lots, bingo, voisins du « +1 / −1 »).
- Un second écran de régie séparé de la projection : la décision du 2026-10-13 garde **un seul
  écran**.
- Les notifications push, l'installation en application et les comptes joueurs.
- La création et l'édition des parties et des lots dans le backoffice (inchangées).
- Une nouvelle ressource Azure (SignalR, Web PubSub, Redis) : le direct doit tenir sans nouvelle
  infrastructure payante.

## 4. Acteurs

| Acteur | Rôle |
|---|---|
| **Animateur** (admin backoffice) | Saisit les numéros, annule, déclare les lignes gagnées et le bingo, lance la pause. Il regarde son écran, que la salle voit projeté. |
| **Salle** | Lit le tableau projeté de loin, sans interaction. |
| **Joueur mobile** (visiteur anonyme du site) | Suit le tableau en direct sur son téléphone, souvent avec un réseau 4G saturé dans la salle. |
| **Visiteur du site** | Voit le bandeau du jour sur n'importe quelle page. |

## 5. Parcours

### P1 — Animer le loto (backoffice, projeté)

1. L'animateur ouvre le tableau de l'événement et passe en plein écran. Le PC est relié au
   vidéoprojecteur en recopie d'écran.
2. Avant la première partie, la toile affiche un écran d'accueil : nom de l'événement, première
   partie et ses lots, et un grand QR code qui mène au tableau mobile. L'animateur choisit la
   palette (sombre ou claire) d'une touche, en regardant la toile.
3. Il saisit un numéro, au clavier (chiffres puis Entrée) ou d'un clic sur sa case. Le numéro
   s'affiche en plein écran environ 2 secondes, puis le tableau revient avec la case allumée et
   le numéro en tête de l'historique.
4. Une erreur de saisie s'annule (touche Retour arrière ou bouton discret). Le numéro retiré
   s'éteint.
5. Quand un joueur crie, l'animateur vérifie le carton, puis déclare la ligne gagnée. La ligne
   suivante, ou la partie suivante, s'affiche.
6. Une partie marquée « pause après » affiche l'écran Pause, avec les numéros du bingo et le QR
   code, jusqu'à ce que l'animateur reprenne. L'écran Pause n'existe que sur la projection : les
   téléphones ne l'affichent pas.
7. Après la dernière partie, la toile affiche « Loto terminé — merci ! ».

### P2 — Suivre le loto sur son téléphone

1. Le jour J, le visiteur voit le bandeau sur le site, ou scanne le QR code projeté avant le
   début ou pendant une pause, et ouvre le tableau en direct.
2. Sur téléphone, il voit en très grand le dernier numéro, les cinq précédents, la grille des
   90 numéros, puis la partie et la ligne en cours, les lots et les numéros du bingo.
3. Chaque nouveau numéro apparaît sans action de sa part. Si la connexion tombe, un message
   discret l'indique et la page se resynchronise seule au retour du réseau.

### P3 — Découvrir l'événement du jour

1. Le jour de l'événement, un bandeau fin apparaît au-dessus de l'en-tête, sur toutes les pages
   du site.
2. Avant le premier numéro, il annonce l'événement (nom, heure d'ouverture des portes, lieu) et
   mène à sa fiche.
3. Dès le premier numéro tiré, il passe à « En direct » avec le dernier numéro et un bouton
   « Suivre le tableau ».
4. Le visiteur peut le fermer. Il ne réapparaît pas de la journée sur cet appareil, sauf au
   passage en direct.

## 6. Écrans

| # | Écran | Nature | Origine |
|---|---|---|---|
| **E1** | Tableau de projection — en jeu | *modifié* | Backoffice `evenement/:id/loto-tableau` (`TableauComponent` backoffice) |
| **E2** | Annonces plein écran de la projection : numéro tiré, pause (avec QR code), avant le début (avec QR code), loto terminé | *modifié* | Modales « Numéro » et « Pause » du même écran ; les états « avant » et « terminé » sont *nouveaux* |
| **E3** | Contrôles discrets de la régie : barre escamotable, saisie clavier, bascule de palette, retour d'erreur | *modifié* | Boutons `undo` / `check` / `trophy` de E1 |
| **E4** | Tableau en direct mobile | *modifié* | Site `evenement/:id/tableau` (`TableauComponent` site) |
| **E5** | Bandeau « Aujourd'hui » du site | *nouveau* | Au-dessus de l'en-tête (`navigation.component`) |
| **E6** | Fiche d'un événement loto : accès au tableau en direct | *modifié* | Site `evenement/:id` (`event-detail`) |

### E1 — Tableau de projection, en jeu

Format de référence : 1920 × 1080 (16:9), plein écran. Il doit rester lisible en 1280 × 720 et en
1440 px de large.

Deux palettes partagent la même mise en page :
- **A, « sombre / tableau lumineux »** : fond sombre, numéros sortis allumés ;
- **B, « claire »** : fond clair, numéros sortis en pavé plein.

La grille occupe **le maximum de la surface** : aucune marge décorative, aucun en-tête de site,
aucun QR code pendant le jeu. Contenu, par ordre d'importance :

1. **Grille 9 × 10** des numéros 1 à 90, sur toute la hauteur et au moins les trois quarts de la
   largeur.
2. **Dernier numéro** en très grand dans une colonne latérale, avec les **cinq précédents**
   dessous.
3. **Partie en cours** : nom, ligne en cours (« 1 ligne », « 2 lignes », « Carton plein »),
   compteur « 1ʳᵉ fois » / « 2ᵉ fois » en une ligne, type spécial (« Américaine », « +1 / −1 »).
4. **Lots** de la ligne en cours, avec les lots déjà gagnés barrés.
5. **Bingo** : ses numéros, ou « Bingo gagné ! ».

Pas de QR code sur cet écran : il n'apparaît qu'avant le début et pendant la pause (E2).

### E2 — Annonces plein écran

- **Numéro tiré** : le numéro occupe la hauteur de l'écran pendant environ 2 s, puis le tableau
  revient. Pendant ce temps, une nouvelle saisie remplace l'annonce.
- **Pause** : titre « Pause », numéros du bingo, partie suivante et **QR code** « Suivez le
  tableau sur votre téléphone ». Il dure jusqu'à la reprise.
- **Avant le début** : nom de l'événement, « Le loto va commencer », première partie, ses lots,
  et un **grand QR code** avec l'adresse courte en clair.
- **Loto terminé** : « Loto terminé — merci ! », bingo gagné ou non.

### E3 — Contrôles discrets

- La barre d'aide de régie (annuler, ligne gagnée, bingo, pause / reprise, palette sombre / claire,
  plein écran et raccourcis) est **masquée par défaut**. Elle apparaît quand une touche du clavier
  est pressée, puis se masque après 3 s sans activité clavier. Le mouvement de la souris révèle
  uniquement le curseur, qui se masque après 3 s sans mouvement.
- Une case numérotée du tableau est également cliquable pour tirer ce numéro, comme la saisie
  clavier chiffres puis Entrée.
- Saisie clavier : chiffres puis Entrée. La saisie en cours apparaît dans un petit témoin, dans
  un coin, et non au centre de l'écran.
- Bascule de palette : bouton de la barre et touche **P**, sans recharger la page ni perdre
  l'annonce en cours. Le choix est mémorisé sur le poste.
- Toute action est **désactivée pendant son envoi**, pour éviter les doublons au double-clic.
- Une erreur (numéro déjà sorti, réseau) s'affiche dans un toast discret, en bas de l'écran.

### E4 — Tableau en direct mobile (390 px, également lisible sur ordinateur)

De haut en bas sur téléphone :

1. Une pastille d'état : « En direct », « Reconnexion… » ou « Hors ligne ».
2. Le **dernier numéro** en très grand.
3. Les **cinq précédents**.
4. La **grille 90** compacte.
5. La partie, la ligne et le compteur en cours.
6. Les **lots**.
7. Les numéros du **bingo**.

Les états « avant le début » et « terminé » reprennent les contenus de E2.

### E5 — Bandeau « Aujourd'hui »

- **Avant le jeu** : « Aujourd'hui · Loto de Vole Papillon d'Amour · portes 13 h 30 · <ville> »,
  avec le lien « Voir l'événement ».
- **En direct** : indicateur animé « En direct », « Dernier numéro : 42 », bouton « Suivre le
  tableau ».
- **Terminé** : « Le loto est terminé, merci à tous ! », avec le lien « Voir le tableau final ».
- **Autre type d'événement** (bourse aux livres, autre) : « Aujourd'hui · <nom> · <horaires> »,
  avec le lien « Voir l'événement ». Pas d'état « En direct ».
- Bouton de fermeture. En mobile, le bandeau tient sur une ou deux lignes.

### E6 — Fiche d'un événement loto

Le jour J, un encart « Tableau en direct » avec le bouton « Suivre le tableau » apparaît en haut de
la fiche. Les autres jours, rien ne change.

## 7. Règles métier

| N° | Règle |
|---|---|
| **RG-01** | Le tableau projeté distingue un numéro **sorti** d'un numéro **non sorti** par un écart de **luminosité** fort, et pas seulement par la teinte. Le dernier numéro et les voisins du « +1 / −1 » ont chacun un troisième et un quatrième état, qui restent lisibles en niveaux de gris. |
| **RG-02** | Les textes du tableau projeté ont un contraste d'au moins 7:1 avec leur fond (WCAG AAA) et utilisent une police sans empattement, grasse, à chiffres à chasse fixe. Pas de police manuscrite pour les données de jeu. |
| **RG-03** | Taille minimale, en 1920 × 1080 : chiffres de la grille ≥ 56 px de haut, dernier numéro ≥ 320 px, annonce plein écran ≥ 60 % de la hauteur d'écran. |
| **RG-04** | La barre d'aide de régie n'apparaît qu'après une touche du clavier et se masque après 3 s sans activité clavier. Le curseur apparaît au mouvement de la souris et se masque après 3 s sans mouvement. |
| **RG-05** | Une action de régie (numéro, annulation, ligne gagnée, bingo) est traitée l'une après l'autre : tant qu'une action est en cours d'envoi, les autres sont refusées côté écran. |
| **RG-06** | Un numéro déjà sorti dans la partie est refusé avec le message « Le numéro <n> est déjà sorti. », et le tableau ne change pas. |
| **RG-07** | L'annonce plein écran du numéro dure 2 s, configurable. Une nouvelle saisie la remplace immédiatement. |
| **RG-08** | L'écran Pause s'affiche après une ligne gagnée qui termine une partie marquée « pause après » (comportement actuel conservé), et se ferme par l'action « Reprendre ». |
| **RG-09** | Hors partie (avant la première, après la dernière, événement sans partie), les pages affichent l'état E2 correspondant, jamais une erreur ni un chargement infini. |
| **RG-10** | Chaque changement saisi par l'animateur parvient à tous les joueurs connectés en **2 s au plus** dans des conditions normales, quel que soit le nombre d'instances de l'API. |
| **RG-11** | Le flux en direct reste ouvert pendant une pause prolongée : au moins une heure sans changement, sans coupure due à l'infrastructure. |
| **RG-12** | Après une coupure, la page mobile se reconnecte seule (délai croissant, plafonné à quelques secondes) et affiche l'**état complet** à jour, même si des numéros ont été tirés pendant la coupure. |
| **RG-13** | La pastille passe à « Reconnexion… » après 5 s sans connexion, et à « Hors ligne » quand le téléphone signale l'absence de réseau. Le dernier état connu reste affiché. |
| **RG-14** | Plusieurs onglets ou appareils derrière la même connexion reçoivent chacun le direct. |
| **RG-15** | Le flux en direct d'un événement inexistant répond par une erreur « introuvable » au lieu d'une connexion ouverte et muette. La page mobile affiche alors « Ce tableau n'existe pas ou n'est plus disponible. » |
| **RG-16** | Un événement est « du jour » quand la date du jour, à l'heure de Paris, est comprise entre son début et sa fin (ou égale au jour de son début s'il n'a pas de fin). Un événement annulé n'est jamais « du jour ». |
| **RG-17** | Si plusieurs événements sont du jour, le bandeau montre le loto en priorité, sinon le premier qui commence. |
| **RG-18** | Le bandeau d'un loto passe à « En direct » dès qu'un premier numéro est sorti et que toutes les parties ne sont pas terminées. Il passe à « Terminé » une fois la dernière partie gagnée. |
| **RG-19** | La fermeture du bandeau est mémorisée sur l'appareil pour l'événement et la journée. Le passage de « avant » à « En direct » le fait réapparaître une fois. |
| **RG-20** | Le bandeau n'apparaît pas sur la page du tableau en direct elle-même. Il ne doit pas décaler la mise en page de façon visible au chargement (place réservée ou apparition sans saut). |
| **RG-21** | Le QR code et le bouton « Suivre le tableau » mènent à la même adresse publique, `/evenement/<id>/tableau`. Le QR code n'apparaît que sur les écrans « Avant le début » et « Pause » de la projection, jamais sur le tableau en jeu. |
| **RG-22** | Le tableau projeté propose deux palettes, A « sombre » et B « claire », avec la même mise en page ; chacune respecte RG-01 à RG-03. L'animateur bascule de l'une à l'autre à tout moment (bouton de régie ou touche P). La bascule est instantanée et n'affecte ni la saisie ni les téléphones. Le dernier choix est mémorisé sur le poste. |
| **RG-23** | Pendant le jeu, la grille occupe toute la hauteur disponible et au moins les trois quarts de la largeur de l'écran projeté, en laissant juste la place nécessaire aux informations de partie. |
| **RG-24** | L'adresse courte `volepapillondamour.fr/loto`, affichée en clair sous le QR code, redirige vers le tableau en direct du loto du jour (RG-16). Sans loto ce jour-là, elle mène à la fiche du prochain loto, ou à la page des événements s'il n'y en a pas. Le QR code encode cette même adresse courte. |

## 8. Critères d'acceptation

1. Sur un vidéoprojecteur, ou en simulation (capture désaturée à 50 % avec noirs relevés),
   numéros sortis, non sortis et dernier numéro restent distinguables en niveaux de gris, dans
   les deux palettes (RG-01, RG-22). La touche P bascule de palette sans perte d'état, et un
   rechargement conserve la palette choisie.
2. Tableau projeté et annonces respectent RG-02 et RG-03 en 1920 × 1080 et restent sans
   débordement en 1280 × 720.
3. Une touche clavier affiche la barre d'aide, sans mouvement de souris; elle se masque après 3 s
   sans frappe. Le curseur suit séparément le mouvement de la souris. La saisie de « 4 », « 2 »,
   puis Entrée tire le 42; cliquer une case tire le même numéro (RG-04).
4. Un double-clic sur un numéro ou sur « ligne gagnée » n'enregistre qu'une action (RG-05).
5. Avant la première partie et après la dernière, backoffice et site affichent les états E2, sans
   erreur dans la console (RG-09).
6. Avec deux instances d'API : un numéro saisi sur l'une apparaît en moins de 2 s chez un client
   connecté à l'autre (RG-10). Un test automatisé couvre la diffusion entre instances.
7. Un client resté connecté 70 minutes sans changement reçoit encore le numéro suivant (RG-11).
8. En coupant le réseau 30 s pendant que trois numéros sont tirés, la page affiche ces trois
   numéros au retour, sans recharger la page (RG-12, RG-13).
9. Deux onglets du même navigateur reçoivent chacun les numéros (RG-14).
10. `/evenement/<id inconnu>/tableau` affiche le message de RG-15.
11. Le jour d'un loto, le bandeau suit les états avant → en direct → terminé (RG-16 à RG-18). Il
    est absent de la page du tableau (RG-20) et absent les autres jours.
12. Scanné avec un téléphone, le QR code projeté avant le début et pendant la pause ouvre E4. Il
    est absent du tableau en jeu, où la grille respecte RG-23 (RG-21, RG-23).

## 9. Contraintes

- **Un seul écran** au backoffice, en recopie sur le vidéoprojecteur (décision du 2026-10-13).
- **Direct sans nouvelle infrastructure** (décision du 2026-10-13) : la diffusion entre
  instances de l'API passe par la base existante. Une latence d'environ une seconde entre
  instances est acceptable, mais la base ne doit être interrogée que pour les événements qui ont
  des spectateurs connectés.
- L'API peut tourner sur 1 ou 2 instances (`maxReplicas: 2`). Le site et le backoffice aussi.
- Le flux public est anonyme et doit supporter environ 600 connexions simultanées pendant le loto.
- La page publique du tableau est rendue côté navigateur (pas de rendu serveur).
- Charte : jetons de `src/Website/tailwind.config.js` (encre `ink-2`, bleu `blue-2`, orange
  `orange`, etc.). Le tableau projeté peut définir une palette « projection » dédiée, à fort
  contraste, documentée dans les maquettes.
- Les règles de jeu côté serveur et leurs tests existants restent inchangés.

## 10. Décisions du 2026-10-13

1. **Palettes** : A « sombre » et B « claire », basculables en direct le jour J (RG-22).
2. **Bandeau** : sur tout le site, pour tous les types d'événements du jour ; seul le loto a un
   état « En direct ».
3. **Pause** : propre à la projection, non diffusée aux téléphones.
4. **QR code** : affiché avant le début et pendant la pause, jamais pendant le jeu, pour laisser
   toute la place à la grille (RG-21, RG-23).
