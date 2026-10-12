# Template HTML des e-mails d'alerte livres — spécification fonctionnelle

> Module : bourse aux livres. Étend `docs/bourse-aux-livres/04-site-public.md` §7
> (« Contenu de l'e-mail ») et `06-regles-metier.md` (`RG-28` à `RG-32`, `RG-46`).
> Règles propres à cette évolution : `RG-90` à `RG-99`.

## Contexte

Quand un livre d'une liste de recherche est rendu disponible ou annoncé pour une bourse
datée, le membre reçoit un e-mail (`RG-28`, `RG-29`). Aujourd'hui ce message est un HTML
minimal : un logo de 56 px, « Bonjour, », une liste à puces (titre en gras, ISBN,
quantité, édition, disponibilité dans le texte), une phrase de non-réservation et un lien
de désinscription. Le client mail l'affiche avec sa police par défaut, sans mise en page.

Ce message n'honore pas non plus tout le contenu promis par `04` §7 : l'auteur, la
couverture, les horaires et l'adresse de la bourse, la mise en évidence de la date et le
lien vers la liste de recherche manquent.

## Objectifs

1. Donner aux alertes un gabarit soigné, à la charte du catalogue (logo, règle de marque,
   typographies, palette), lisible sur ordinateur comme sur téléphone.
2. Faire comprendre en un coup d'œil **quel livre est arrivé, quand et où venir le
   chercher**.
3. Livrer le contenu prévu par `04` §7 : titre, auteur, couverture, édition, date mise en
   évidence, dates/horaires/adresse de la bourse, non-réservation, lien vers la liste de
   recherche, désinscription.

## Hors périmètre

- La refonte de l'alerte « livre rare vendu » : pas de photo ni de carte dédiée. Ses
  lignes sont seulement reprises dans le nouveau gabarit (`RG-97`).
- L'objet des e-mails : inchangé.
- Les règles d'envoi : constitution, délai de 2 h, regroupement, anti-répétition,
  suspension (`RG-28` à `RG-31`, `RG-44`) restent identiques.
- Les autres e-mails (compte, Entra, formulaires du site).
- Le logo affiché à côté de l'expéditeur dans la boîte de réception (BIMI, voir
  `docs/email-branding.md`).

## Acteurs

- **Membre** : destinataire de l'alerte, sur ordinateur ou téléphone, dans un client mail
  quelconque (Gmail, Outlook, Apple Mail…), images distantes parfois bloquées, parfois en
  mode sombre.

## Parcours

1. Le membre reçoit l'alerte. Dans sa boîte, il voit l'objet et le **texte d'aperçu**
   (`RG-96`).
2. À l'ouverture, il lit l'accroche, puis l'**encart bourse** : quand et où.
3. Il parcourt les cartes livres : couverture, titre, auteur, édition, quantité.
4. Il peut ouvrir la fiche d'un livre dans le catalogue, ou sa liste de recherche via le
   bouton principal.
5. En pied de message, il comprend pourquoi il reçoit ce message et peut se désabonner.

## Écrans

Les « écrans » sont ici des rendus de l'e-mail. Chacun est maquetté en largeur 600 px
(lecture sur ordinateur) et 390 px (téléphone).

- **E1 — Alerte, un livre** *(modifié — e-mail actuel produit par le Worker)*.
  De haut en bas :
  1. **En-tête** : logo papillon et « Vole Papillon d'Amour · Bourse aux livres », règle
     de marque cyan → bleu → orange.
  2. **Accroche** : « Bonjour Marie, » (ou « Bonjour, » sans prénom), puis un titre
     éditorial « *Le Petit Prince* vous attend ».
  3. **Encart bourse** (`RG-91`) : « Disponible à la bourse du samedi 14 mars 2026 »
     ou « Disponible dès maintenant », avec horaires d'ouverture et adresse.
  4. **Carte livre** (`RG-92`) : couverture, titre, auteur, édition (éditeur · année ·
     format), quantité, lien « Voir la fiche ».
  5. **Mention de non-réservation** (`RG-94`).
  6. **Bouton principal** « Voir ma liste de recherche » (`RG-95`).
  7. **Pied de message** (`RG-95`) : nom de l'association, lien vers le site, raison de
     l'envoi, lien « Se désabonner ».
- **E2 — Alerte, plusieurs livres** *(modifié — même origine)*. Accroche « 3 livres de
  votre liste vous attendent ». Les cartes sont regroupées sous leur encart bourse ; le
  cas de deux bourses dans un même message est montré (`RG-93`).
- **E3 — Alerte mixte** *(modifié — même origine)* : des livres disponibles et des livres
  rares suivis vendus dans le même message (`RG-97`).
- **E4 — États** : sans couverture (`RG-92`), images bloquées (`RG-98`), mode sombre
  (`RG-98`), sans prénom, rares seuls (aucun livre disponible), version texte brut
  (`RG-99`).

## Règles métier

### `RG-90` — Un gabarit unique pour les alertes livres
Toutes les alertes de liste de recherche utilisent le même gabarit : en-tête, accroche,
encarts bourse, cartes, non-réservation, bouton principal, pied de message. La charte
est celle du catalogue V2 (`docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`).

L'accroche dépend du nombre de livres disponibles ou annoncés :
- un livre : « *Titre* vous attend » ;
- plusieurs : « N livres de votre liste vous attendent » ;
- aucun (rares seuls) : « Des nouvelles de vos livres rares suivis ».

### `RG-91` — La date et le lieu passent en premier
L'encart bourse précède les cartes. Il affiche en grand :

| Mode | Ligne principale | Détails |
|---|---|---|
| `PROCHAINE BOURSE` | « Disponible à la bourse du samedi 14 mars 2026 » | horaires d'ouverture, adresse |
| `DISPONIBLE MAINTENANT` avec ouverture connue | « Disponible dès maintenant » | « Prochaine ouverture : samedi 14 mars 2026 », horaires, adresse |
| `DISPONIBLE MAINTENANT` sans ouverture connue | « Disponible dès maintenant » | aucun détail |

Les dates et heures sont exprimées à l'heure de Paris, en français. Un horaire ou une
adresse inconnus sont omis, sans libellé vide.

### `RG-92` — Une carte par livre
Chaque livre disponible ou annoncé a sa carte :
- **couverture** à gauche, si le catalogue en connaît une ; sinon un bloc de
  remplacement au papillon, aux couleurs de la charte ;
- **titre** (« Titre non renseigné » s'il manque), **auteur(s)** s'ils sont connus ;
- **édition** : éditeur · année · format, éléments connus seulement (`RG-46`) ;
- **quantité** : « 1 exemplaire » / « 2 exemplaires » ;
- **lien « Voir la fiche »** vers la fiche publique du livre au catalogue, si elle
  existe.

L'ISBN n'est plus affiché en clair. Les cartes s'empilent sans limite de nombre : un
message de douze titres affiche douze cartes.

### `RG-93` — Regroupement par bourse
Dans un même message, les livres sont regroupés par bourse et par mode. Chaque groupe
a son encart (`RG-91`) suivi de ses cartes. Les groupes sont triés par date, la plus
proche d'abord ; un groupe `DISPONIBLE MAINTENANT` sans date vient en premier.

### `RG-94` — Non-réservation
La mention figure dans chaque message, sous les cartes, sur fond distinct :
« Ces livres ne sont pas mis de côté. Premier arrivé, premier servi. » (au singulier
pour un seul livre : « Ce livre n'est pas mis de côté. Premier arrivé, premier
servi. »). Elle remplace la formulation actuelle (`RG-32`).

### `RG-95` — Liens
- Bouton principal « Voir ma liste de recherche » → page compte du catalogue.
- « Voir la fiche » → fiche publique du livre (`RG-92`).
- Pied de message : lien vers le site de l'association, phrase « Vous recevez cet
  e-mail parce que ces livres figurent dans votre liste de recherche. », lien
  « Se désabonner ». L'en-tête de désabonnement en un clic reste en place.

### `RG-96` — Texte d'aperçu
Un texte d'aperçu, invisible dans le corps, accompagne l'objet dans la boîte de
réception. Il reprend la ligne principale du premier encart et le nombre de titres :
« À la bourse du samedi 14 mars 2026 · 3 titres », « Disponible dès maintenant ·
1 titre ».

### `RG-97` — Livres rares suivis dans le gabarit
Les livres rares vendus présents dans un message apparaissent dans une section
« Livres rares suivis », après les cartes et avant la mention de non-réservation, sous
forme de lignes simples : « *Titre* — auteur : n'est plus disponible. » Pas de photo,
pas de carte. Un message ne contenant que des rares garde l'en-tête, l'accroche
(`RG-90`), cette section, le pied de message, mais ni encart bourse, ni mention de
non-réservation, ni bouton principal.

### `RG-98` — Lisibilité garantie
- Le message reste complet et compréhensible si les images sont bloquées : titre,
  date, lieu et liens ne sont jamais portés par une image ; chaque image a un texte
  alternatif (nom de l'association pour le logo, titre du livre pour une couverture).
- Il reste lisible en mode sombre, le logo conservant un fond clair.
- Sur téléphone (≤ 390 px), tout tient sur une colonne sans défilement horizontal ; les
  liens et le bouton restent confortables au doigt.

### `RG-99` — Version texte
La version texte brut, envoyée avec le HTML, reprend le même contenu dans le même ordre :
accroche, encarts (ligne principale, horaires, adresse), livres (titre, auteur, édition,
quantité, lien de fiche), rares, non-réservation, lien vers la liste de recherche,
raison de l'envoi, désabonnement.

## Critères d'acceptation

1. Une alerte d'un livre annoncé pour une bourse datée montre, avant toute carte, la date
   de la bourse, ses horaires et son adresse.
2. Une carte affiche la couverture connue du livre, son titre, son auteur, son édition et
   sa quantité ; sans couverture, le bloc de remplacement apparaît.
3. Un message de plusieurs livres répartis sur deux bourses montre deux encarts, chacun
   suivi de ses cartes, dans l'ordre chronologique.
4. Un message mixte affiche la section « Livres rares suivis » après les cartes.
5. Un message rares seuls n'affiche ni encart, ni non-réservation, ni bouton principal.
6. Images bloquées : le titre, la date et le lieu restent lisibles.
7. Le message s'affiche sur une seule colonne à 390 px, sans défilement horizontal, dans
   Gmail (web et mobile), Outlook (bureau Windows et web) et Apple Mail.
8. La version texte contient les mêmes informations que le HTML.
9. L'objet et les règles d'envoi sont inchangés ; le désabonnement en un clic fonctionne
   toujours.

## Contraintes

- Clients mail : mise en page par tableaux et styles en ligne, largeur 600 px, polices
  web avec repli (Georgia pour les titres, Arial/Helvetica pour le texte), pas de
  JavaScript ni de formulaire.
- Les couvertures sont des URL externes (BnF, Open Library, Google Books) ou des images
  téléversées ; seules les URL HTTPS sont affichées.
- Données affichées : uniquement celles déjà connues de l'association (catalogue,
  bourses). Le message ne contient aucune donnée personnelle autre que le prénom du
  destinataire.

## Questions ouvertes

Aucune.
