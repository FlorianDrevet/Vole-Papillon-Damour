# Étiquettes des livres sans ISBN et impression — spécification fonctionnelle

> Demande d'origine (2026-11-19) : voir [`README.md`](README.md).
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
code-barres. Ce code doit être lu par la scanette exactement comme un ISBN, sans réglage
particulier. La plateforme génère le code, l'imprime, et la caisse le scanne pour la
sortie.

Une réserve forte : la connexion réelle à l'étiqueteuse n'est pas encore possible. Il
n'existe ni scanette ni protocole documenté à ce jour. Cette spécification couvre donc tout
ce qui doit exister côté application (écrans, boutons, règles, scan), et tout ce qui touche
à l'imprimante est **simulé** tant que le pilote réel n'est pas branché (`RG-178`).

## 2. Objectifs

- Permettre d'ajouter un livre sans ISBN avec toutes ses informations, ordinaire ou rare.
- Générer pour chaque livre un code identifiable, lu comme un ISBN par la scanette.
- Imprimer une étiquette depuis la plateforme, sans passer par l'application du fabricant.
- Permettre de réimprimer l'étiquette ISBN d'un livre scanné ou saisi à la main, quand
  l'ISBN imprimé est peu lisible ou n'existe qu'en chiffres.
- Faire sortir de la bourse un livre sans ISBN en le scannant en caisse, comme un ISBN.
- Préparer la connexion à l'imprimante (écrans, états, appels simulés) pour brancher le
  pilote réel plus tard, sans modifier les écrans.

## 3. Hors périmètre

| Exclusion | Motif |
|---|---|
| Connexion réelle à l'étiqueteuse (Bluetooth ou réseau) | Pas encore de scanette ni de pilote validé. Phase ultérieure, derrière une interface déjà en place. |
| Impression depuis le BackOffice | Hors v1 (`Q6`). Le besoin exprimé concerne l'app de scan. |
| Modification du site public | Les livres sans ISBN ne sont pas publiés dans cette version (`Q3`). |
| Tablette de caisse Android (`MauiCashApp`) | Elle ne scanne pas de sortie et n'imprime rien. |
| Prix sur l'étiquette | Aucun prix n'est imprimé (`RG-50`, `RG-179`). |
| Estimation de la valeur d'un livre | Hors sujet ici. |

## 4. Acteurs

- **Bénévole de tri** (rôle `Tri`) : ajoute des livres sans ISBN ordinaires, imprime
  les étiquettes, réimprime l'étiquette ISBN d'un livre scanné.
- **Bénévole livres rares** (rôle `LivresRares`) : crée les fiches rares, sans ISBN ou non,
  et imprime leur étiquette.
- **Bénévole de caisse** (rôle `Caisse`) : scanne les codes en sortie. Aucun bouton
  d'impression.
- **Bénévole du matériel** : connecte l'imprimante depuis l'appareil, prend le relais si
  elle se déconnecte. Il peut s'agir d'un bénévole de tri.

## 5. Parcours

### 5.1 Ajouter un livre sans ISBN

1. Depuis l'accueil de la scanette, le bénévole touche **Ajouter un livre sans ISBN**.
2. Il choisit le type : **ordinaire** (rôle `Tri`) ou **rare** (rôle `LivresRares`).
3. Il saisit les informations du livre (titre obligatoire ; auteur, éditeur, année,
   état, quantité pour un ordinaire ; prix ferme et photos pour un rare).
4. Il valide. La plateforme crée la fiche et génère son code interne.
5. L'écran de confirmation montre le code et propose **Imprimer l'étiquette** ou
   **Plus tard**.

### 5.2 Imprimer une étiquette

1. Depuis la confirmation, la fiche rare, la liste des fiches rares, ou le résultat
   d'un scan ISBN, le bénévole touche **Imprimer l'étiquette**.
2. Un aperçu s'affiche : code-barres, chiffres lisibles, titre tronqué. Pour un ordinaire
   il indique le nombre d'étiquettes ; par défaut une par exemplaire.
3. Il touche **Imprimer**. Si aucune imprimante n'est connectée, l'écran renvoie vers
   l'écran de connexion (E6), puis revient à l'aperçu.

### 5.3 Connecter l'imprimante

1. Le bénévole ouvre **Imprimante d'étiquettes** depuis l'accueil.
2. L'écran affiche l'état : non connectée, recherche, connectée, erreur.
3. Il touche **Connecter**, puis **Tester** pour imprimer une étiquette d'essai.
4. Tant que la simulation est active, l'écran l'annonce (`RG-178`).

### 5.4 Réimprimer l'ISBN d'un livre scanné

1. Le bénévole scanne un ISBN, ou saisit un ISBN à la main (`03` §3.5).
2. Le résultat s'affiche comme aujourd'hui (verdict).
3. Il touche **Réimprimer l'étiquette ISBN**. L'aperçu présente l'ISBN-13 en code-barres
   et en chiffres.
4. Cette impression ne change ni la quantité ni le verdict.

### 5.5 Sortir un livre sans ISBN en caisse

1. Le caissier scanne l'étiquette. Le code est reconnu comme code interne.
2. L'écran affiche le titre et le type de livre. Un rare garde la mention « LIVRE RARE »
   en grand, comme aujourd'hui (`03` §5.1).
3. `VALIDER` enregistre une sortie d'un exemplaire, comme un ISBN.
4. Un code scanné qui n'est ni un ISBN, ni un code interne connu, est refusé avec un
   message explicite (`RG-172`).

## 6. Écrans

Les identifiants `E<n>` sont repris dans le plan et les zones des maquettes.

- **E1 · Accueil de la scanette** — *modifié*. Écran d'origine : accueil et choix du mode
  (`docs/bourse-aux-livres/maquettes/scanette/Main.dc.html`). Deux entrées s'ajoutent :
  **Ajouter un livre sans ISBN** (rôles `Tri` et `LivresRares`) et **Imprimante
  d'étiquettes** (mêmes rôles).
- **E2 · Formulaire Livre sans ISBN** — *nouveau*. Pas d'écran d'origine. Même grammaire que
  le formulaire de livre rare (`src/Scan/src/app/rare-books/scan-rare-book-form.component.ts`) ;
  bascule ordinaire / rare visible seulement si le rôle le permet.
- **E3 · Fiche créée, impression proposée** — *nouveau*. Affiche le code généré en chiffres
  et en code-barres, et deux actions : **Imprimer l'étiquette**, **Plus tard**.
- **E4 · Résultat d'un ISBN (scan ou saisie)** — *modifié*. Écran d'origine : verdict du tri
  (`scanette/VerdictTrop.dc.html`, `VerdictGarder.dc.html`, `VerdictRare.dc.html`,
  `VerdictPremier.dc.html`) et saisie manuelle (`scanette/SaisieManuelle.dc.html`).
  Un bouton secondaire **Réimprimer l'étiquette ISBN** s'ajoute au même endroit sur chaque
  verdict. Il ne doit jamais devancer le verdict.
- **E5 · Fiche livre rare et liste** — *modifié*. Écran d'origine : formulaire
  `livres-rares/:id` et la liste `livres-rares`. Une action **Imprimer l'étiquette** est ajoutée
  à la fiche et à chaque ligne de la liste ; la liste indique les fiches jamais imprimées.
- **E6 · Imprimante d'étiquettes** — *nouveau*. États : non connectée, recherche en cours,
  connectée (nom de l'appareil, bouton **Tester**), erreur de connexion (message et
  **Réessayer**), simulation (bandeau permanent, `RG-178`).
- **E7 · Aperçu et impression d'étiquette** — *nouveau*, en panneau. Aperçu (code-barres,
  chiffres, titre tronqué), nombre d'étiquettes (ordinaire seulement), **Imprimer**. États :
  en préparation, impression en cours, réussie, échec avec lien vers E6.
- **E8 · Caisse : code interne reconnu** — *modifié*. Écran d'origine : mode caisse
  (`scanette/Caisse.dc.html`, `03` §5). Un code interne affiche le titre et le type ; un
  code refusé affiche le message de `RG-172`.

## 7. Règles métier

Les règles existantes restent valables, sauf `RG-02` précisé ci-dessous.

- **RG-170 — Code interne.** Chaque livre sans ISBN reçoit, à sa création, un code interne
  généré par la plateforme. Le code est lu par la scanette comme un ISBN, sans réglage, et
  ne peut pas être confondu avec un ISBN réel. Le format exact reste à valider (`Q1`).
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
  interne, le code interne reste valide, car des étiquettes sont déjà collées. Les deux codes
  résolvent vers la même fiche. Une fiche ne porte qu'un ISBN (`RG-01`).
- **RG-175 — Étiquette ISBN.** Tout ISBN scanné ou saisi peut être réimprimé. L'étiquette
  porte l'ISBN-13 (`RG-01` : un ISBN-10 est converti) en code-barres et en chiffres. Cette
  impression ne crée aucun mouvement et ne change aucune quantité.
- **RG-176 — Qui imprime.** Les rôles `Tri` (livres ordinaires, ISBN et sans ISBN) et
  `LivresRares` (livres rares) impriment. Le rôle `Caisse` ne voit aucun bouton d'impression.
- **RG-177 — Trace d'impression.** La date de la dernière impression réelle est conservée sur
  la fiche, pour repérer les fiches sans étiquette. Une impression simulée n'écrit rien.
- **RG-178 — Simulation tant que le pilote n'est pas branché.** Tant que la connexion réelle
  n'existe pas, l'application affiche « Simulation — rien n'est imprimé » sur E6 et E7 et
  n'enregistre aucune impression (`RG-177`). Les appels à l'imprimante passent par une
  interface unique, pour brancher le pilote réel sans modifier les écrans.
- **RG-179 — Contenu de l'étiquette.** L'étiquette porte le code-barres, les chiffres lisibles
  et le titre tronqué. Elle ne porte jamais de prix (`RG-50`), y compris pour un livre rare.
- **RG-180 — Création en ligne.** La création d'une fiche sans ISBN demande une connexion,
  car l'unicité des codes (`RG-171`) est assurée par la plateforme. L'impression d'une fiche
  déjà créée reste possible hors ligne si l'imprimante est connectée (`Q5`).

## 8. Critères d'acceptation

- Un bénévole `Tri` crée un livre ordinaire sans ISBN en renseignant au moins le titre ; la
  fiche reçoit un code interne unique (`RG-170`, `RG-171`).
- Un bénévole `LivresRares` crée une fiche rare sans ISBN ; elle reçoit un code par
  exemplaire, quantité 1 (`RG-173`).
- Le rôle `Caisse` ne voit aucun bouton d'impression.
- Après un scan ISBN réussi, le bouton **Réimprimer l'étiquette ISBN** apparaît, avec
  l'ISBN-13 ; l'impression ne modifie ni quantité ni verdict (`RG-175`).
- Après une saisie manuelle valide, le même bouton apparaît (`RG-175`).
- Le code interne d'un livre sans ISBN, scanné en caisse, résout vers la bonne fiche et
  enregistre une sortie d'un exemplaire (`RG-172`).
- Un code qui n'est ni un ISBN valide ni un code interne connu est refusé avec un message
  explicite, et aucune sortie n'est enregistrée.
- Un ISBN ajouté à une fiche qui avait un code interne ne rend pas ce code invalide : les
  deux scans trouvent la même fiche (`RG-174`).
- Tant que la simulation est active, E6 et E7 l'annoncent et aucune date d'impression
  n'est écrite (`RG-177`, `RG-178`).
- Une étiquette ne contient aucun prix (`RG-179`).
- Une fiche rare imprimée affiche sa date d'impression ; une fiche jamais imprimée est
  repérée dans la liste (`RG-177`).
- Sans imprimante connectée, E7 renvoie vers E6 et ne bloque ni la création ni la sortie en
  caisse.

## 9. Contraintes et mises à jour de docs

- **Imprimante.** Étiqueteuse T50M Pro. Aucune connexion réelle pendant cette phase ; le
  code du dépôt tiers qui en rétro-ingénierie le protocole n'est pas intégré à ce stade. Une
  interface d'impression unique est définie, avec une implémentation simulée (`RG-178`).
- **Format du code.** Le code interne doit être lisible par la scanette comme un ISBN-13. Un
  format EAN-13 à préfixe réservé à un usage interne est la piste retenue, à confirmer
  (`Q1`). Les codes ISBN (préfixes 978 et 979) ne doivent jamais être générés.
- **Rôles.** Les rôles existants (`Tri`, `Caisse`, `LivresRares`) suffisent ; aucun rôle
  nouveau.
- **Application concernée.** L'application de scan (`src/Scan`) pour toutes les saisies,
  impressions et sorties. La plateforme serveur (`src/Backend`) pour la génération, l'unicité
  et la trace d'impression.
- **Docs à mettre à jour pendant l'implémentation :**
  - `docs/bourse-aux-livres/06-regles-metier.md`, `RG-02` : ajouter « sauf les codes internes
    de `RG-170` ».
  - `docs/bourse-aux-livres/03-parcours-benevole-scan.md` : §3.3 (bouton de réimpression),
    §3.5 (saisie manuelle, même bouton), §5 (sortie d'un code interne), §8 (fiches sans ISBN).
  - `docs/bourse-aux-livres/08-questions-ouvertes.md` : ajouter les `Q1` à `Q8` de ce
    document si elles restent ouvertes après la validation.

## 10. Questions ouvertes

Chaque question donne la réponse proposée. Elle sera tranchée à la relecture de la spec.

- **Q1 — Format du code interne.** Proposition : EAN-13, préfixe réservé à un usage interne
  (plage 2xx), séquence numérique et chiffre de contrôle. Cela évite toute collision avec un
  ISBN et se lit comme un ISBN-13 sans réglage. À confirmer avec la scanette et l'étiqueteuse
  (types de code acceptés, lecture des chiffres).
- **Q2 — Format de l'étiquette.** Dimensions du rouleau de la T50M Pro et hauteur minimale du
  code-barres. Non connues ; à mesurer sur l'étiqueteuse avant de figer l'aperçu.
- **Q3 — Visibilité sur le site public.** Proposition : un livre ordinaire sans ISBN n'apparaît
  pas sur le site public en v1 ; il ne sort que par la caisse. À confirmer, car cela change
  la fiche publique.
- **Q4 — Code par fiche ou par exemplaire pour un ordinaire.** Proposition : un code par fiche
  avec une quantité (`RG-173`), comme un ISBN, et une étiquette imprimée par exemplaire.
- **Q5 — Création hors ligne.** Proposition : la création demande une connexion en v1
  (`RG-180`), car l'unicité des codes doit être assurée par la plateforme. Une version
  hors ligne demanderait une réservation de plages de codes, à décider si le besoin apparaît.
- **Q6 — Impression depuis le BackOffice.** Proposition : non en v1. L'impression reste dans
  l'application de scan, là où les livres sont ajoutés.
- **Q7 — Texte de l'étiquette.** Proposition : code-barres, chiffres lisibles et titre tronqué
  à 30 caractères environ. Aucun prix (`RG-179`). À confirmer une fois la taille connue
  (`Q2`).
- **Q8 — Réimpression de l'ISBN.** Proposition : toujours l'ISBN-13, même si le livre est
  imprimé en ISBN-10 (`RG-175`). La réimpression ne crée aucun mouvement.
