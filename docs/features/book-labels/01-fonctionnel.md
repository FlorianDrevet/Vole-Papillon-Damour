# Étiquettes des livres sans ISBN et impression — spécification fonctionnelle

> Demande d'origine (2026-11-19) : voir [`README.md`](README.md).
> Relecture 2 (2026-11-19) : photos pour les livres ordinaires sans ISBN, code en chiffres
> sur l'étiquette. Voir §10 (« Tranché »).
> Extension du module bourse aux livres (`docs/bourse-aux-livres/`). Numérotation des règles
> reprise de `06-regles-metier.md` ; les nouvelles règles commencent à `RG-170`, le plus haut
> numéro déjà utilisé dans `docs/` étant `RG-168`.

## 1. Contexte

Une partie des livres de la bourse n'a pas d'ISBN : éditions anciennes, livres
d'occasion sans code, livres rares. Aujourd'hui, un livre sans ISBN ne peut donc pas
être sorti par scan en caisse : le bénévole doit le retrouver à la main. Une fiche
rare peut déjà être créée sans ISBN (`03` §8, fiche rare hors ligne), mais elle n'a
toujours pas de code à scanner.

L'étiqueteuse **T50M Pro** servira à coller sur chaque livre une étiquette avec un
code-barres et son code en chiffres. Ce code doit être lu par la scanette exactement comme
un ISBN, sans réglage particulier. La plateforme génère le code, l'imprime, et la caisse le
scanne pour la sortie.

Une réserve forte : la connexion réelle à l'étiqueteuse n'est pas encore possible. Il
n'existe ni scanette ni protocole documenté à ce jour. Cette spécification couvre donc tout
ce qui doit exister côté application (écrans, boutons, règles, scan), et tout ce qui touche
à l'imprimante est **simulé** tant que le pilote réel n'est pas branché (`RG-178`).

Un livre sans ISBN ordinaire doit aussi être **visible sur le site public** et pouvoir être
ajouté à Ma sélection, comme les autres livres. Il peut donc être créé à deux endroits : dans
l'application de scan (bénévoles au tri) et dans une page d'administration du site.

## 2. Objectifs

- Permettre d'ajouter un livre sans ISBN avec toutes ses informations et ses photos, ordinaire
  ou rare, depuis l'application de scan et depuis une page dédiée de l'administration du site.
- Générer pour chaque livre un code identifiable, lu comme un ISBN par la scanette.
- Imprimer une étiquette depuis l'application de scan, sans passer par l'application du
  fabricant.
- Permettre de réimprimer l'étiquette ISBN d'un livre scanné ou saisi à la main, quand
  l'ISBN imprimé est peu lisible ou n'existe qu'en chiffres.
- Faire sortir de la bourse un livre sans ISBN en le scannant en caisse, comme un ISBN.
- Rendre les livres ordinaires sans ISBN visibles sur le site public et ajoutables à Ma
  sélection.
- Préparer la connexion à l'imprimante (écrans, états, appels simulés) pour brancher le
  pilote réel plus tard, sans modifier les écrans.

## 3. Hors périmètre

| Exclusion | Motif |
|---|---|
| Connexion réelle à l'étiqueteuse (Bluetooth ou réseau) | Pas encore de scanette ni de pilote validé. Phase ultérieure, derrière une interface déjà en place. |
| Impression depuis le site (administration ou public) | Décidé (`Q6`) : l'impression n'existe que dans l'application de scan. |
| Création de livre hors ligne | Décidé (`Q5`, `RG-180`) : aucune création sans connexion, ni dans l'app de scan ni sur le site. |
| Tablette de caisse Android (`MauiCashApp`) | Elle ne scanne pas de sortie et n'imprime rien. |
| Prix sur l'étiquette d'un livre ordinaire | Aucun prix (`RG-50`, `RG-179`). Seul le livre rare porte son prix ferme. |
| Estimation de la valeur d'un livre | Hors sujet ici. |

## 4. Acteurs

- **Bénévole de tri** (rôle `Tri`) : ajoute des livres sans ISBN ordinaires depuis l'app de
  scan, imprime les étiquettes, réimprime l'étiquette ISBN d'un livre scanné.
- **Bénévole livres rares** (rôle `LivresRares`) : crée les fiches rares sans ISBN, depuis
  l'app de scan ou la page d'administration du site, et imprime leur étiquette depuis l'app
  de scan.
- **Administrateur** : crée des livres sans ISBN ordinaires ou rares depuis la page
  d'administration du site. Il n'imprime rien depuis le site.
- **Bénévole de caisse** (rôle `Caisse`) : scanne les codes en sortie. Aucun bouton
  d'impression.
- **Bénévole du matériel** : connecte l'imprimante depuis l'appareil, prend le relais si
  elle se déconnecte. Il peut s'agir d'un bénévole de tri.
- **Visiteur du site public** : trouve un livre ordinaire sans ISBN dans le catalogue et
  l'ajoute à Ma sélection.

## 5. Parcours

### 5.1 Ajouter un livre sans ISBN depuis l'application de scan

1. Depuis l'accueil de la scanette, le bénévole touche **Ajouter un livre sans ISBN**.
2. Il choisit le type : **ordinaire** (rôle `Tri`) ou **rare** (rôle `LivresRares`).
3. Il saisit les informations du livre (titre obligatoire ; auteur, éditeur, année,
   état, quantité et photos pour un ordinaire ; prix ferme et photos pour un rare).
4. Il valide. Sans connexion, la validation est refusée avec un message clair : rien n'est
   enregistré sur l'appareil (`RG-180`). Avec connexion, la plateforme crée la fiche et
   génère son code interne.
5. L'écran de confirmation montre le code et propose **Imprimer l'étiquette** ou
   **Plus tard**.

### 5.2 Ajouter un livre sans ISBN depuis le site (administration)

1. L'administrateur ouvre **Administration > Le fonds de livres > Livres sans ISBN**.
2. La page liste les livres sans ISBN déjà créés, avec leur type, quantité et code-barres.
3. Il touche **Ajouter un livre sans ISBN** et remplit le même formulaire que dans
   l'application de scan, photos comprises (`RG-183`).
4. Il valide. La fiche est créée avec son code interne, visible sur la page. Aucun bouton
   d'impression n'est proposé sur le site (`Q6`).
5. Un livre ordinaire créé ici apparaît dans le catalogue public dès qu'il est disponible
   (`RG-181`).

### 5.3 Imprimer une étiquette (application de scan seulement)

1. Depuis la confirmation, la fiche rare, la liste des fiches rares, ou le résultat
   d'un scan ISBN, le bénévole touche **Imprimer l'étiquette**.
2. Un aperçu s'affiche : le code-barres et son code en chiffres. Pour un livre rare, le prix
   ferme s'ajoute (`RG-179`). Pour un ordinaire, l'aperçu indique le nombre d'étiquettes ; par
   défaut une par exemplaire.
3. Il touche **Imprimer**. Si aucune imprimante n'est connectée, l'écran renvoie vers
   l'écran de connexion (E6), puis revient à l'aperçu.

### 5.4 Connecter l'imprimante

1. Le bénévole ouvre **Imprimante d'étiquettes** depuis l'accueil.
2. L'écran affiche l'état : non connectée, recherche, connectée, erreur.
3. Il touche **Connecter**, puis **Tester** pour imprimer une étiquette d'essai.
4. Tant que la simulation est active, l'écran l'annonce (`RG-178`).

### 5.5 Réimprimer l'ISBN d'un livre scanné

1. Le bénévole scanne un ISBN, ou saisit un ISBN à la main (`03` §3.5).
2. Le résultat s'affiche comme aujourd'hui (verdict).
3. Il touche **Réimprimer l'étiquette ISBN**. L'aperçu présente le code-barres de l'ISBN-13
   et ses chiffres (`RG-175`).
4. Cette impression ne change ni la quantité ni le verdict.

### 5.6 Sortir un livre sans ISBN en caisse

1. Le caissier scanne l'étiquette. Le code est reconnu comme code interne.
2. L'écran affiche le titre et le type de livre. Un rare garde la mention « LIVRE RARE »
   en grand, comme aujourd'hui (`03` §5.1).
3. `VALIDER` enregistre une sortie d'un exemplaire, comme un ISBN.
4. Un code scanné qui n'est ni un ISBN, ni un code interne connu, est refusé avec un
   message explicite (`RG-172`).

### 5.7 Trouver et ajouter un livre sans ISBN sur le site public

1. Le visiteur trouve le livre dans la recherche ou le catalogue, comme un autre livre.
2. La fiche ne montre pas d'ISBN ; elle montre le titre, l'auteur, l'éditeur et sa photo.
3. Le visiteur l'ajoute à Ma sélection. L'entrée est désignée par le code interne
   (`RG-182`).

## 6. Écrans

Les identifiants `E<n>` sont repris dans le plan et les zones des maquettes.

Application de scan (Scanette) :

- **E1 · Accueil de la scanette** — *modifié*. Écran d'origine : accueil et choix du mode
  (`docs/bourse-aux-livres/maquettes/scanette/Main.dc.html`). Deux entrées s'ajoutent :
  **Ajouter un livre sans ISBN** (rôles `Tri` et `LivresRares`) et **Imprimante
  d'étiquettes** (mêmes rôles).
- **E2 · Formulaire Livre sans ISBN (scanette)** — *nouveau*. Pas d'écran d'origine. Même
  grammaire que le formulaire de livre rare (`src/Scan/src/app/rare-books/scan-rare-book-form.component.ts`) ;
  bascule ordinaire / rare visible seulement si le rôle le permet. Photos disponibles pour les
  deux types.
- **E3 · Fiche créée, impression proposée** — *nouveau*. Affiche le code généré (code-barres
  et chiffres) et deux actions : **Imprimer l'étiquette**, **Plus tard**.
- **E4 · Résultat d'un ISBN (scan ou saisie)** — *modifié*. Écran d'origine : verdict du tri
  (`scanette/VerdictTrop.dc.html`, `VerdictGarder.dc.html`, `VerdictRare.dc.html`,
  `VerdictPremier.dc.html`) et saisie manuelle (`scanette/SaisieManuelle.dc.html`).
  Un bouton secondaire **Réimprimer l'étiquette ISBN** s'ajoute au même endroit sur chaque
  verdict. Il ne doit jamais devancer le verdict.
- **E5 · Fiche livre rare et liste (scanette)** — *modifié*. Écran d'origine : formulaire
  `livres-rares/:id` et la liste `livres-rares`. Une action **Imprimer l'étiquette** est ajoutée
  à la fiche et à chaque ligne de la liste ; la liste indique les fiches jamais imprimées.
- **E6 · Imprimante d'étiquettes** — *nouveau*. États : non connectée, recherche en cours,
  connectée (nom de l'appareil, bouton **Tester**), erreur de connexion (message et
  **Réessayer**), simulation (bandeau permanent, `RG-178`).
- **E7 · Aperçu et impression d'étiquette** — *nouveau*, en panneau. Aperçu (code-barres et
  chiffres, plus le prix ferme pour un rare), nombre d'étiquettes (ordinaire seulement),
  **Imprimer**. États : en préparation, impression en cours, réussie, échec avec lien vers E6.
- **E8 · Caisse : code interne reconnu** — *modifié*. Écran d'origine : mode caisse
  (`scanette/Caisse.dc.html`, `03` §5). Un code interne affiche le titre et le type ; un
  code refusé affiche le message de `RG-172`.

Site Catalog, administration :

- **E9 · Livres sans ISBN (liste admin)** — *nouveau*. Section « Livres sans ISBN » dans le
  groupe « Le fonds de livres », à côté de « Livres rares » et « ISBN non trouvés ». Liste
  des fiches (titre, auteur, type, quantité, code-barres), bouton **Ajouter un livre sans
  ISBN**, filtre ordinaire / rare. Écran d'origine : la section « Livres rares » de
  l'administration du site (`src/Catalog/src/app/features/administration/rare-books/admin-rare-books.component.ts`).
  États : vide, chargement, erreur. Aucun bouton d'impression.
- **E10 · Formulaire Livre sans ISBN (admin)** — *nouveau*. Mêmes champs que E2, photos
  comprises. Écran d'origine : formulaire de livre rare de l'administration
  (`src/Catalog/src/app/features/administration/rare-books/admin-rare-book-form.component.ts`).
  Un gestionnaire `LivresRares` ne voit que le type rare.

Site public :

- **E11 · Fiche d'un livre sans ISBN** — *modifié*. Écran d'origine : fiche livre
  (`catalogue/Fiche.dc.html`, `FicheMobile.dc.html`). La ligne ISBN est absente (et non vide).
  La photo du livre remplace la couverture ; à défaut, l'image de repli du site. Le bouton
  **Ajouter à Ma sélection** est le même que pour un autre livre.

## 7. Règles métier

Les règles existantes restent valables, sauf `RG-02` précisé ci-dessous.

- **RG-170 — Code interne.** Chaque livre sans ISBN reçoit, à sa création, un code interne
  généré par la plateforme. C'est un EAN-13 dont le préfixe appartient à la plage réservée à
  un usage interne (2xx), avec un chiffre de contrôle valide. Il est lu par la scanette comme
  un ISBN, sans réglage, et ne peut pas être confondu avec un ISBN réel : jamais de préfixe
  978 ou 979.
- **RG-171 — Unicité.** Un code interne n'est jamais attribué à deux fiches, et jamais
  réattribué, même si la fiche est supprimée ou remplacée.
- **RG-172 — Reconnaissance des codes en scan.** Un code interne valide est reconnu et
  résolu vers sa fiche. Il n'est pas refusé au titre de `RG-02`. Un code qui n'est ni un ISBN
  valide (`RG-01`), ni un code interne connu, reste refusé avec un message explicite. C'est
  la seule exception à `RG-02`.
- **RG-173 — Un code par fiche ou par exemplaire.** Un livre ordinaire sans ISBN a un
  code par fiche, avec une quantité, comme un ISBN. Un livre rare a un code par exemplaire,
  et sa quantité est toujours 1.
- **RG-174 — ISBN ajouté après coup.** Si un ISBN est ajouté à une fiche qui avait un code
  interne (par l'administration du site), le code interne reste valide, car des étiquettes
  sont déjà collées. Les deux codes résolvent vers la même fiche. Une fiche ne porte qu'un
  ISBN (`RG-01`).
- **RG-175 — Étiquette ISBN.** Tout ISBN scanné ou saisi peut être réimprimé. L'étiquette
  porte le code-barres de l'ISBN-13 (`RG-01` : un ISBN-10 est converti) et ce même code en
  chiffres, rien d'autre. Cette impression ne crée aucun mouvement et ne change aucune
  quantité.
- **RG-176 — Qui imprime.** Seule l'application de scan imprime. Les rôles `Tri`
  (livres ordinaires, ISBN et sans ISBN) et `LivresRares` (livres rares) impriment. Le rôle
  `Caisse` ne voit aucun bouton d'impression. Le site (administration comme public) n'a
  aucun bouton d'impression.
- **RG-177 — Trace d'impression.** La date de la dernière impression réelle est conservée sur
  la fiche, pour repérer les fiches sans étiquette. Une impression simulée n'écrit rien.
- **RG-178 — Simulation tant que le pilote n'est pas branché.** Tant que la connexion réelle
  n'existe pas, l'application affiche « Simulation — rien n'est imprimé » sur E6 et E7 et
  n'enregistre aucune impression (`RG-177`). Les appels à l'imprimante passent par une
  interface unique, pour brancher le pilote réel sans modifier les écrans.
- **RG-179 — Contenu de l'étiquette.** L'étiquette porte le code-barres et le code en chiffres
  (ISBN-13 ou code interne). Pour un livre rare, elle porte en plus le prix ferme. Aucun titre
  sur l'étiquette, et aucun prix pour un livre ordinaire.
- **RG-180 — Création en ligne uniquement.** Aucune fiche sans ISBN n'est créée hors ligne,
  ni depuis l'app de scan, ni depuis le site. L'unicité des codes (`RG-171`) est assurée par
  la plateforme. Une fiche déjà connue de l'appareil peut être imprimée hors ligne, si
  l'imprimante est connectée.
- **RG-181 — Visibilité publique.** Un livre ordinaire sans ISBN suit les mêmes règles de
  disponibilité et de date d'annonce que les autres livres. Une fois disponible, il apparaît
  dans la recherche et le catalogue du site public. Sa fiche n'affiche pas d'ISBN.
- **RG-182 — Ma sélection.** Un livre ordinaire sans ISBN peut être ajouté à Ma sélection.
  L'entrée est désignée par son code interne, de la même façon qu'une fiche rare est désignée
  par son identifiant. Les alertes de disponibilité suivent la même règle (`Q9`).
- **RG-183 — Création depuis le site.** Les administrateurs créent des livres sans ISBN
  ordinaires ou rares depuis E10, photos comprises. Un gestionnaire `LivresRares` ne crée que
  des fiches rares. La page ne propose aucune impression et affiche le code-barres de la fiche.

## 8. Critères d'acceptation

- Un bénévole `Tri` crée un livre ordinaire sans ISBN en renseignant au moins le titre ; la
  fiche reçoit un code interne unique (`RG-170`, `RG-171`).
- Un bénévole `Tri` peut joindre des photos à un livre ordinaire sans ISBN (`Q11`).
- Un bénévole `LivresRares` crée une fiche rare sans ISBN ; elle reçoit un code par
  exemplaire, quantité 1 (`RG-173`).
- Un administrateur crée un livre sans ISBN depuis la page **Livres sans ISBN** du site, avec
  ses photos ; la fiche apparaît dans la liste avec son code-barres et aucun bouton
  d'impression (`RG-183`, `RG-176`).
- Un gestionnaire `LivresRares` ne voit que le type rare sur cette page.
- Sans connexion, la création est refusée avec un message ; aucune fiche n'est créée sur
  l'appareil (`RG-180`).
- Le rôle `Caisse` ne voit aucun bouton d'impression.
- Après un scan ISBN réussi, le bouton **Réimprimer l'étiquette ISBN** apparaît ; l'aperçu
  montre le code-barres de l'ISBN-13 et ses chiffres, et rien d'autre ; l'impression ne modifie
  ni quantité ni verdict (`RG-175`).
- Après une saisie manuelle valide, le même bouton apparaît (`RG-175`).
- Une étiquette de livre ordinaire porte le code-barres et son code en chiffres ; celle d'un
  livre rare porte en plus son prix ferme (`RG-179`).
- Le code interne d'un livre sans ISBN, scanné en caisse, résout vers la bonne fiche et
  enregistre une sortie d'un exemplaire (`RG-172`).
- Un code qui n'est ni un ISBN valide ni un code interne connu est refusé avec un message
  explicite, et aucune sortie n'est enregistrée.
- Un ISBN ajouté à une fiche qui avait un code interne ne rend pas ce code invalide : les
  deux scans trouvent la même fiche (`RG-174`).
- Un livre ordinaire sans ISBN disponible apparaît dans la recherche publique ; sa fiche
  n'affiche pas d'ISBN, affiche sa photo ; il s'ajoute à Ma sélection (`RG-181`, `RG-182`).
- Tant que la simulation est active, E6 et E7 l'annoncent et aucune date d'impression
  n'est écrite (`RG-177`, `RG-178`).
- Une fiche rare imprimée affiche sa date d'impression ; une fiche jamais imprimée est
  repérée dans la liste (`RG-177`).
- Sans imprimante connectée, E7 renvoie vers E6 et ne bloque ni la création ni la sortie en
  caisse.

## 9. Contraintes et mises à jour de docs

- **Imprimante.** Étiqueteuse T50M Pro. Aucune connexion réelle pendant cette phase ; le
  code du dépôt tiers qui en rétro-ingénierie le protocole n'est pas intégré à ce stade. Une
  interface d'impression unique est définie, avec une implémentation simulée (`RG-178`).
- **Largeur d'étiquette.** Les informations trouvées en ligne (voir `Q2`) donnent une largeur
  d'impression d'environ 20 à 50 mm et des rouleaux de 2 pouces (environ 50 mm). Un EAN-13
  standard, avec ses chiffres, doit tenir dans une telle largeur ; la taille exacte reste à
  vérifier sur l'appareil.
- **Format du code.** EAN-13 à préfixe interne (`RG-170`). Il doit être lu par la scanette
  comme un ISBN-13. Les codes 978 et 979 ne sont jamais générés.
- **Photos.** Un livre ordinaire sans ISBN accepte des photos, comme un livre rare. Les
  règles de taille et de nombre de photos des fiches rares s'appliquent (à confirmer au plan).
- **Rôles.** Les rôles existants (`Tri`, `Caisse`, `LivresRares`) et le rôle administrateur
  suffisent ; aucun rôle nouveau.
- **Applications concernées.**
  - L'application de scan (`src/Scan`) : création, impression, réimpression, sorties.
  - Le site Catalog (`src/Catalog`) : page d'administration E9-E10, fiche publique E11,
    recherche et Ma sélection.
  - La plateforme serveur (`src/Backend`) : génération et unicité des codes, création des
    fiches, photos, trace d'impression, visibilité publique.
- **Docs à mettre à jour pendant l'implémentation :**
  - `docs/bourse-aux-livres/06-regles-metier.md`, `RG-02` : ajouter « sauf les codes internes
    de `RG-170` ».
  - `docs/bourse-aux-livres/03-parcours-benevole-scan.md` : §3.3 (bouton de réimpression),
    §3.5 (saisie manuelle, même bouton), §5 (sortie d'un code interne), §8 (fiches sans ISBN).
  - `docs/bourse-aux-livres/04-site-public.md` et `05-administration.md` : livre sans ISBN
    visible, page d'administration.
  - `docs/bourse-aux-livres/08-questions-ouvertes.md` : ajouter les questions restées ouvertes
    de ce document (`Q2`, `Q4`, `Q8`, `Q9`, `Q10`).

## 10. Questions ouvertes

Les réponses des relectures 1 et 2 sont notées « Tranché ». Les autres gardent leur
proposition, qui sera retenue si personne ne la conteste.

- **Q1 — Format du code interne.** Tranché : EAN-13 à préfixe interne (plage 2xx), pas un
  faux ISBN. Reste à fixer la plage exacte pendant le plan, et à vérifier qu'elle est lue
  par la scanette.
- **Q2 — Format de l'étiquette.** Partiellement répondu par une recherche en ligne (sources
  non officielles, à confirmer sur le manuel SUPVAN) : la T50M Pro imprime entre 20 et 50 mm de
  large, à 203 dpi, avec des rouleaux de 2 pouces (environ 50 mm) ; les dimensions de
  l'appareil sont incertaines. Le support des codes-barres par la T50M Pro n'a pas été
  confirmé ; c'est à tester sur l'appareil dès réception. Sources trouvées :
  [page de manuel tierce SUPVAN T50M Series](https://fr.manuals.plus/asin/B0C8S8PLT2),
  [fiche T50M Plus](https://takespec.com/printer/supvan-t50m-plus). La première n'a pas été
  lue en détail.
- **Q3 — Visibilité sur le site public.** Tranché : un livre ordinaire sans ISBN est visible
  sur le site public (`RG-181`).
- **Q4 — Code par fiche ou par exemplaire pour un ordinaire.** Non répondu. Proposition
  retenue : un code par fiche avec une quantité (`RG-173`), une étiquette imprimée par
  exemplaire.
- **Q5 — Création hors ligne.** Tranché : aucune création hors ligne, ni dans l'app de scan
  ni sur le site (`RG-180`).
- **Q6 — Impression depuis le site.** Tranché : pas d'impression depuis le site, seulement
  depuis l'application de scan (`RG-176`).
- **Q7 — Contenu de l'étiquette.** Tranché : code-barres et code en chiffres pour tous les
  livres ; prix ferme en plus pour un rare ; pas de titre (`RG-179`).
- **Q8 — Réimpression de l'ISBN.** Non répondu. Proposition retenue : l'ISBN-13 seul, même si
  le livre porte un ISBN-10 (`RG-175`).
- **Q9 — Ma sélection et alertes.** Non demandé explicitement. Proposition retenue : un livre
  sans ISBN s'ajoute à Ma sélection et déclenche les alertes, désigné par son code interne
  (`RG-182`), comme une fiche rare.
- **Q10 — Gestionnaires rares et page admin.** Non demandé. Proposition retenue : un
  gestionnaire `LivresRares` ne crée que des fiches rares depuis la page admin (`RG-183`).
- **Q11 — Photos d'un livre ordinaire sans ISBN.** Tranché : des photos sont possibles, comme
  pour un rare (E2, E10, E11). Les règles de taille et de nombre seront reprises des fiches
  rares au plan.
