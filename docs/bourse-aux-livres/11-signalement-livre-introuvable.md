# 11 — Signaler un livre introuvable et faire le ménage du stock

> **Statut : proposition fonctionnelle à valider.** Rien n'est implémenté. Ce document
> modifie la section 6.3 à 6.5 de
> [`10-evolution-compte-selection-achats.md`](10-evolution-compte-selection-achats.md)
> (états personnels de Ma sélection) et ajoute une file de vérification dans
> l'administration du catalogue.
>
> **Maquettes :** canvas Claude Design <https://claude.ai/artifact/KDX9jHZrcRnb2H6qGccnDZ> ;
> export dans [`maquettes/signalement-introuvable/`](maquettes/signalement-introuvable/README.md)
> et [`maquettes/signalement-introuvable.zip`](maquettes/signalement-introuvable.zip).
> **Plan d'implémentation :** [`plan/07-signalement-livre-introuvable.md`](plan/07-signalement-livre-introuvable.md).

## 1. Le problème

Aujourd'hui, chaque ligne de **Ma sélection** porte un sélecteur à quatre valeurs :
À PRENDRE, ACHETÉ, PAS TROUVÉ, À REVOIR. Ce sélecteur a deux défauts :

1. **Il ne sert qu'au membre.** Quand un membre coche PAS TROUVÉ, l'association n'en
   sait rien. Or c'est une information précieuse : si un livre est marqué disponible
   au catalogue mais que personne ne le trouve sur place, le stock est probablement
   faux (livre perdu, vendu sans passage en caisse, mal rangé, abîmé et jeté…).
2. **Il ajoute de la charge sans bénéfice clair.** À PRENDRE / À REVOIR sont deux
   nuances d'un même souhait, et ACHETÉ est déjà posé automatiquement par la caisse
   lorsque le passage est rattaché au compte (RG-65).

## 2. Ce que l'on veut

1. **Retirer** le sélecteur d'état personnel de Ma sélection.
2. **Ajouter** sur chaque livre de Ma sélection un bouton **« Je ne l'ai pas trouvé »**
   qui envoie un **signalement** à l'association.
3. **Lister** ces signalements dans l'administration, dans une file de travail où un
   bénévole vérifie physiquement la présence du livre et, s'il ne le trouve pas,
   le **sort du stock** en un geste.

## 3. Vocabulaire

| Terme | Définition |
|---|---|
| **Signalement** | Déclaration d'un membre : « j'ai cherché ce livre sur place et je ne l'ai pas trouvé ». Un signalement vise une fiche précise (ISBN ou fiche rare). |
| **Fiche signalée** | Fiche qui a au moins un signalement ouvert. Plusieurs signalements sur la même fiche sont regroupés en une seule ligne de travail. |
| **Vérification** | Recherche physique du livre par un bénévole. |
| **Clôture** | Décision prise par le bénévole après vérification : *retrouvé*, *retiré du stock* ou *classé sans suite*. |

On évite « réclamation », « litige » ou « erreur » : le membre ne se plaint pas, il
**aide** à tenir le catalogue à jour. Le ton de l'interface doit le remercier.

## 4. Acteurs et droits

| Acteur | Peut | Ne peut pas |
|---|---|---|
| Visiteur anonyme | Voir le bouton ; il est invité à se connecter pour signaler | Envoyer un signalement |
| Membre connecté | Signaler une fiche de sa sélection ; annuler son propre signalement tant qu'il est ouvert | Voir les signalements des autres, modifier le stock |
| Administrateur (rôle `Administration`) | Voir la file, vérifier, clôturer, retirer du stock | — |
| Gestionnaire livres rares (rôle `LivresRares`) | Voir et clôturer les signalements visant une **fiche rare** | Traiter les éditions ordinaires |
| Bénévole de tri / caisse | *Voir question Q-SIG-1* | — |

## 5. Écrans modifiés — vue d'ensemble

| # | Écran | Application | Nature du changement |
|---|---|---|---|
| E1 | Mon compte › **Ma sélection** (liste) | Catalogue public | Suppression du sélecteur d'état, ajout du bouton de signalement, refonte des filtres |
| E2 | **Confirmation de signalement** (feuille / modale) | Catalogue public | Nouveau |
| E3 | Ligne de Ma sélection **après signalement** | Catalogue public | Nouvel état visuel |
| E4 | Administration › barre latérale | Catalogue admin | Nouvelle entrée **« Livres introuvables »** avec compteur |
| E5 | Administration › **Livres introuvables** (file) | Catalogue admin | Nouvel écran |
| E6 | Administration › **Vérification d'une fiche signalée** (panneau) | Catalogue admin | Nouveau |
| E7 | Administration › Tableau de bord | Catalogue admin | Nouvelle tuile « À vérifier » |
| E8 | Administration › Catalogue › **Fiche** | Catalogue admin | Encadré « Signalée introuvable » + mouvements |

La fiche publique du livre (page détail) **n'est pas modifiée** dans cette version :
voir l'idée I-2.

## 6. Parcours membre — Ma sélection

### 6.1 E1 — Ligne de Ma sélection (avant)

~~~text
┌─────────────────────────────────────────────────────────┐
│ [couv] Le Horla                                          │
│        Maupassant · Folio · 2004                         │
│        ISBN 9782070…   (Disponible)                      │
│        Mon état : [À prendre][Acheté][Pas trouvé][À revoir]
│        Ajouté le 12 septembre 2026        Retirer        │
└─────────────────────────────────────────────────────────┘
~~~

### 6.2 E1 — Ligne de Ma sélection (après)

~~~text
┌─────────────────────────────────────────────────────────┐
│ [couv] Le Horla                                          │
│        Maupassant · Folio · 2004                         │
│        ISBN 9782070…   (Disponible)                      │
│        Ajouté le 12 septembre 2026                       │
│                                                          │
│        [ Je ne l'ai pas trouvé ]       Retirer           │
└─────────────────────────────────────────────────────────┘
~~~

Règles d'affichage du bouton **« Je ne l'ai pas trouvé »** :

| Situation de la ligne | Bouton |
|---|---|
| Disponibilité = *Disponible* | Affiché, actif |
| Disponibilité = *Annoncé* (bourse future) | Masqué — le livre n'est pas encore en rayon |
| Disponibilité = *Épuisé*, *Fiche vendue*, *Plus visible* | Masqué — le catalogue dit déjà qu'il n'y est plus |
| Ligne ACHETÉ (posé par la caisse) | Masqué |
| Signalement déjà ouvert par ce membre | Remplacé par l'état E3 |
| Ligne locale (visiteur non connecté) | Affiché ; un appui ouvre l'invitation à se connecter (voir 6.5) |

Le bouton est une action **secondaire** (style contour), placée à côté de « Retirer ».
Il ne doit pas concurrencer visuellement la disponibilité.

### 6.3 E2 — Confirmation

Un appui ouvre une feuille (bas d'écran sur mobile, modale sur bureau) :

~~~text
┌──────────────────────────────────────────────┐
│  Vous n'avez pas trouvé ce livre ?           │
│                                              │
│  Le Horla — Maupassant                       │
│                                              │
│  Un bénévole ira vérifier en rayon. Si le    │
│  livre a disparu, il sera retiré du          │
│  catalogue. Merci de nous aider !            │
│                                              │
│  Où l'avez-vous cherché ? (facultatif)       │
│  ( ) À la bourse       ( ) Au local          │
│                                              │
│  Un commentaire ? (facultatif, 280 car.)     │
│  [ ex. rayon polar vide                   ]  │
│                                              │
│  [ Annuler ]        [ Envoyer le signalement ]│
└──────────────────────────────────────────────┘
~~~

- Les deux champs sont facultatifs : le signalement doit pouvoir partir en deux
  appuis, sur place, avec un téléphone.
- Le choix « À la bourse / Au local » n'est proposé que si l'association a
  effectivement deux lieux de consultation (voir Q-SIG-4). Sinon le bloc disparaît.
- Le commentaire est libre, sans donnée personnelle attendue. Il est affiché tel quel
  aux administrateurs (échappé, jamais interprété).

Après envoi : message de confirmation discret (toast) « Merci, un bénévole va vérifier. »

### 6.4 E3 — Ligne après signalement

~~~text
│        ⚑ Signalé introuvable le 24 septembre      │
│          Un bénévole va vérifier.  Annuler le signalement
~~~

- La ligne **reste** dans Ma sélection : le membre peut encore vouloir ce livre si
  un bénévole le retrouve.
- « Annuler le signalement » est disponible tant que le signalement est **ouvert**
  (membre qui s'est trompé ou qui l'a finalement trouvé).
- Lorsque le signalement est clôturé, la ligne affiche le résultat :

| Clôture | Message sur la ligne du membre |
|---|---|
| Retrouvé | « Un bénévole l'a retrouvé le 26 septembre. » — le bouton de signalement redevient disponible |
| Retiré du stock | La disponibilité passe à *Épuisé* (ou *Fiche vendue* pour un rare) ; mention « Retiré après votre signalement, merci ! » |
| Classé sans suite | Aucun message particulier ; le bouton redevient disponible |

### 6.5 Visiteur non connecté

La sélection locale (non synchronisée) n'est pas une donnée membre (RG-55). Le
signalement, lui, doit être rattaché à un compte pour éviter les abus et dédoublonner.
Un appui sur le bouton ouvre donc :

> « Pour signaler ce livre, connectez-vous : un bénévole saura qu'il s'agit d'un
> signalement vérifié. » [ Se connecter ] [ Plus tard ]

Après connexion et fusion de la sélection (section 6.6 du document 10), le
membre retrouve la ligne et renouvelle son geste. On ne conserve pas de
« signalement en attente » dans le navigateur (simplicité, voir idée I-5).

### 6.6 Filtres de Ma sélection

Les filtres dépendaient des états personnels. Nouvelle liste :

| Avant | Après |
|---|---|
| Tout | Tout |
| Prochaine visite (À prendre + À revoir) | **Supprimé** — remplacé par « Encore disponible » |
| Encore disponible | Encore disponible |
| Acheté | Acheté (état posé par la caisse, conservé) |
| Pas trouvé | **Signalés** (signalement ouvert ou clôturé récemment) |
| Indisponibles | Indisponibles |

## 7. Parcours bénévole — administration

### 7.1 Où placer la file ? (décision proposée)

La barre latérale de l'administration a trois groupes : *Pendant la bourse*
(Tableau de bord, Sessions de scan, Désengorgement), *Le fonds de livres*
(Catalogue, Livres rares, Statistiques), *Administration* (Comptes, Paramètres).

**Proposition : nouvelle entrée « Livres introuvables » dans le groupe
*Pendant la bourse*, juste après *Désengorgement*.**

Justification :

- c'est une **file de travail** à vider, comme Désengorgement et les sessions à
  corriger, pas une vue de consultation du fonds ;
- elle est surtout alimentée pendant et juste après une bourse, quand les
  bénévoles sont sur place pour vérifier ;
- elle réutilise le même geste de sortie de stock (mouvement RETRAIT) que
  Désengorgement, donc la même logique mentale.

Alternative écartée : un onglet dans *Catalogue* — la file s'y noierait derrière la
recherche de fiches et on perdrait le compteur dans la barre latérale.

### 7.2 E4 — Barre latérale

~~~text
PENDANT LA BOURSE
  ▦ Tableau de bord
  ⌗ Sessions de scan        3
  ⤓ Désengorgement
  ⚑ Livres introuvables     7    ← nouveau, compteur = fiches à vérifier
~~~

Le compteur n'apparaît que s'il est > 0. Pour le rôle *LivresRares*, l'entrée est
visible et ne compte que les fiches rares.

### 7.3 E5 — Écran « Livres introuvables »

~~~text
Travail
Livres introuvables.
Des membres ont cherché ces livres sans les trouver. Vérifiez en rayon,
puis retirez-les du stock ou confirmez qu'ils sont bien là.

[ À vérifier 7 ] [ Traités 42 ]            Trier : [ Plus signalés ▾ ]

┌───────────────────────────────────────────────────────────────────────┐
│ [couv] Le Horla — Maupassant · Folio 2004           ⚑ 3 signalements  │
│        ISBN 9782070…  · Stock : 2 dispo · Genre : Classiques          │
│        1er signalement le 21 sept. · dernier le 24 sept.              │
│        « rayon polar vide » (+1 commentaire)                          │
│                              [ Retrouvé ]  [ Retirer du stock… ]  ⋯   │
├───────────────────────────────────────────────────────────────────────┤
│ [couv] ★ Livre rare — Les Fleurs du mal, 1861       ⚑ 1 signalement   │
│        …                                                              │
└───────────────────────────────────────────────────────────────────────┘

[ Télécharger la liste à vérifier (CSV) ]
~~~

**Une ligne = une fiche**, pas un signalement. Chaque ligne montre :

- couverture, titre, auteur, éditeur, année ; badge *Livre rare* le cas échéant ;
- ISBN, **quantité disponible actuelle**, genre (aide à savoir dans quel rayon
  chercher) ;
- nombre de signalements ouverts et **nombre de membres distincts** ;
- date du premier et du dernier signalement ;
- commentaires des membres (anonymisés : jamais de nom ni d'e-mail) ;
- les actions de clôture.

Tri proposé : *plus signalés* (défaut), *plus anciens*, *plus récents*, *genre*
(pratique pour faire la tournée des rayons). Filtre *Éditions / Livres rares*.

Onglet **Traités** : historique des clôtures (date, bénévole, décision, quantité
retirée, note) — sert d'audit et de mesure de la fiabilité du stock.

L'export CSV reprend le modèle de Désengorgement : une feuille à imprimer pour la
tournée en rayon (titre, auteur, ISBN, genre, quantité, case à cocher).

### 7.4 E6 — Vérification et clôture

Les actions ouvrent un panneau de confirmation.

**« Retrouvé »** — le livre est bien en rayon :

~~~text
Le Horla est bien là ?
Les 3 signalements seront clôturés. Le stock ne change pas.
Note (facultative) : [ était rangé en Poésie ]
[ Annuler ]  [ Confirmer : retrouvé ]
~~~

**« Retirer du stock… »** — le livre n'est pas (ou pas entièrement) là :

~~~text
Retirer Le Horla du stock
Stock disponible actuel : 2
Combien d'exemplaires avez-vous retrouvés en rayon ?  [ 0 ]
→ 2 exemplaires seront retirés (mouvement RETRAIT).
Motif : (•) Introuvable en rayon  ( ) Abîmé  ( ) Autre
Note : [ Vérifié le 25/09 par l'équipe du samedi ]  (pré-remplie, modifiable)
[ Annuler ]  [ Retirer 2 exemplaires ]
~~~

- On demande **combien d'exemplaires sont présents** plutôt que combien retirer :
  c'est ce que le bénévole constate en rayon, et on évite de sortir tout le stock
  quand un seul exemplaire manque.
- Le retrait produit le mouvement **RETRAIT** existant, attribué au compte connecté,
  avec la note (obligatoire, pré-remplie « Introuvable en rayon — N signalements »).
- Si le stock disponible tombe à 0, la fiche passe *Épuisée* au catalogue, comme
  pour tout retrait.
- Pour une **fiche rare** (exemplaire unique), l'action devient « Retirer la fiche
  rare » : la fiche est dépubliée / marquée retirée (voir Q-SIG-5). Pas de quantité.

**« ⋯ › Classer sans suite »** — signalement manifestement erroné (livre vendu entre
temps, signalement en doublon, test…). Note obligatoire. Stock inchangé.

Toute clôture clôt **tous** les signalements ouverts de la fiche en même temps.

### 7.5 E7 — Tableau de bord

Nouvelle tuile à côté de « Alertes en attente d'envoi » :

~~~text
⚑ 7 livres à vérifier en rayon     → Ouvrir la liste
   dont 2 signalés plus de 7 jours
~~~

### 7.6 E8 — Fiche du catalogue (admin)

Si la fiche a des signalements ouverts, un encadré en haut de la colonne latérale :

~~~text
⚑ Signalée introuvable
  3 signalements depuis le 21 septembre
  [ Retrouvé ]  [ Retirer du stock… ]
~~~

L'historique des mouvements affiche, pour un RETRAIT issu de cette file, la mention
« suite à signalement ».

## 8. Cycle de vie d'un signalement

~~~text
           Membre : « Je ne l'ai pas trouvé »
                          │
                          ▼
                     ┌─────────┐   Membre : « Annuler »   ┌─────────┐
                     │ OUVERT  │ ───────────────────────▶ │ ANNULÉ  │
                     └─────────┘                          └─────────┘
          ┌───────────────┼──────────────────┬──────────────────────┐
          ▼               ▼                  ▼                      ▼
   ┌───────────┐   ┌─────────────┐   ┌──────────────┐   ┌────────────────────┐
   │ RETROUVÉ  │   │ RETIRÉ DU   │   │ SANS SUITE   │   │ CADUC (automatique)│
   │           │   │ STOCK       │   │              │   │ stock tombé à 0 par│
   └───────────┘   └─────────────┘   └──────────────┘   │ vente/retrait autre│
                                                        └────────────────────┘
~~~

## 9. Règles métier

Numérotation à la suite de RG-66.

### RG-67 — Seul un membre connecté signale

Un signalement est toujours rattaché à un compte membre. Un visiteur anonyme est
invité à se connecter.

### RG-68 — On ne signale que ce qui est censé être en rayon

Le signalement n'est possible que pour une fiche de Ma sélection dont la
disponibilité est *Disponible* au moment de l'envoi. Le serveur revérifie cette
condition ; un signalement sur une fiche épuisée est refusé avec un message clair.

### RG-69 — Un signalement ouvert par membre et par fiche

Un membre ne peut avoir qu'un signalement ouvert sur une même fiche. Un second
appui ne crée pas de doublon. Après clôture *Retrouvé* ou *Sans suite*, le membre
peut signaler à nouveau.

### RG-70 — Plafond anti-abus

Un membre ne peut pas ouvrir plus de **10 signalements par jour** (valeur à régler
dans Paramètres). Au-delà, le bouton affiche « Vous avez déjà beaucoup signalé
aujourd'hui, merci ! » et l'envoi est refusé par le serveur.

### RG-71 — Le signalement ne modifie jamais le stock

Ni le signalement, ni leur nombre ne retirent automatiquement un livre du
catalogue. Seule la clôture *Retiré du stock*, faite par un bénévole habilité,
produit un mouvement RETRAIT. (Voir idée I-3 pour un éventuel masquage temporaire.)

### RG-72 — Retrait attribué et motivé

Le retrait issu d'un signalement réutilise le mouvement RETRAIT append-only :
compte du bénévole, date, quantité, note obligatoire, lien vers les signalements
clôturés.

### RG-73 — Caducité automatique

Si la quantité disponible d'une fiche tombe à 0 par un autre chemin (vente en caisse,
retrait via Désengorgement, correction), ses signalements ouverts passent à *CADUC*
et sortent de la file.

### RG-74 — Anonymat côté administration

La file n'affiche ni nom, ni e-mail, ni identifiant du membre : seulement le nombre
de membres distincts, les dates et les commentaires. Un administrateur n'a pas
besoin de savoir *qui* a signalé pour aller vérifier.

### RG-75 — Suppression du compte

À la suppression d'un compte, ses signalements ouverts sont conservés mais détachés
du membre (anonymes), pour ne pas perdre l'information de stock. Les commentaires
libres sont supprimés (ils peuvent contenir des données personnelles).

### RG-76 — Fin des états personnels

Les états À PRENDRE, PAS TROUVÉ et À REVOIR disparaissent de l'interface et de
l'API. ACHETÉ reste un état **posé uniquement par la caisse** (RG-65) et n'est
plus modifiable par le membre.

## 10. Reprise des données existantes

| Donnée actuelle | Devenir proposé |
|---|---|
| Lignes À PRENDRE / À REVOIR | Deviennent des lignes « simples » (aucun état) |
| Lignes ACHETÉ | Conservées telles quelles |
| Lignes ACHETÉ posées **à la main** par le membre (sans vente associée) | Voir Q-SIG-3 |
| Lignes PAS TROUVÉ | Voir Q-SIG-2 : soit converties en signalements ouverts si posées depuis moins de 30 jours sur une fiche encore disponible, soit simplement effacées |

## 11. Parcours de bout en bout

### Parcours A — Le livre a vraiment disparu

1. Camille ajoute *Le Horla* à Ma sélection avant la bourse (disponible, 1 ex.).
2. Le jour de la bourse, elle ne le trouve pas en rayon.
3. Depuis son téléphone, Mon compte › Ma sélection › « Je ne l'ai pas trouvé » ›
   « Envoyer le signalement ».
4. La ligne affiche « Signalé introuvable le 24 septembre — un bénévole va vérifier ».
5. Dans l'admin, le compteur *Livres introuvables* passe à 1 ; la tuile du tableau de
   bord l'annonce.
6. En fin de journée, un administrateur imprime la liste (CSV), fait le tour des
   rayons, ne trouve pas le livre.
7. Il ouvre la ligne › « Retirer du stock… » › « exemplaires trouvés : 0 » › confirme.
8. Un mouvement RETRAIT de 1 est créé ; la fiche passe *Épuisée* au catalogue public.
9. Chez Camille, la ligne affiche *Épuisé* et « Retiré après votre signalement, merci ! ».
10. Si Camille avait aussi *Le Horla* dans **Mes recherches**, l'alerte se redéclenchera
    normalement au prochain scan d'entrée de ce livre.

### Parcours B — Le livre était mal rangé

1–5 identiques.
6. Le bénévole trouve le livre dans le rayon Poésie, le remet en place.
7. « Retrouvé », note « était en Poésie ». Stock inchangé.
8. Chez Camille : « Un bénévole l'a retrouvé le 24 septembre. » Elle peut revenir.

### Parcours C — Plusieurs membres, plusieurs exemplaires

1. Trois membres signalent le même ISBN (stock : 3).
2. La file montre **une** ligne : « 3 signalements · 3 membres ».
3. Le bénévole trouve 1 exemplaire → saisit « trouvés : 1 » → 2 retirés.
4. Les trois signalements sont clôturés *Retiré du stock* ; le catalogue affiche 1 dispo.

### Parcours D — Le livre est vendu avant la vérification

1. Un membre signale un livre (stock : 1).
2. Une heure plus tard, un autre visiteur l'achète en caisse → stock 0.
3. Le signalement passe *CADUC* et disparaît de la file (RG-73).

## 12. Critères d'acceptation

| # | Critère |
|---|---|
| CA-1 | Ma sélection n'affiche plus de sélecteur d'état ; aucune requête de changement d'état n'est émise par l'interface membre. |
| CA-2 | Le bouton « Je ne l'ai pas trouvé » n'apparaît que sur les lignes *Disponible* non achetées. |
| CA-3 | Un signalement envoyé apparaît dans la file admin en moins de 5 s (rafraîchissement de la page). |
| CA-4 | Deux appuis rapides du même membre ne créent qu'un signalement. |
| CA-5 | Le 11ᵉ signalement d'un membre dans la journée est refusé. |
| CA-6 | La file regroupe les signalements par fiche et n'affiche aucune identité de membre. |
| CA-7 | « Retirer du stock » avec *trouvés = 0* sur un stock de 2 crée un RETRAIT de 2 attribué au bénévole et rend la fiche *Épuisée*. |
| CA-8 | « Retrouvé » ne modifie pas le stock et rend le bouton de signalement de nouveau disponible chez le membre. |
| CA-9 | Une vente qui épuise le stock rend caducs les signalements ouverts de la fiche. |
| CA-10 | Un compte sans rôle ne voit ni l'entrée ni l'API de la file (403). |
| CA-11 | À 390 px, Ma sélection et la feuille de confirmation sont utilisables sans défilement horizontal ; la file admin est lisible sur mobile. |
| CA-12 | Le bouton, la feuille et les actions admin sont accessibles au clavier et annoncés correctement par un lecteur d'écran. |

## 13. Besoins, idées et trous identifiés

### 13.1 Questions à trancher avant implémentation

| # | Question | Proposition par défaut |
|---|---|---|
| **Q-SIG-1** | **Qui vérifie ?** L'administration n'est ouverte qu'aux rôles *Administration* et *LivresRares*. Les bénévoles de tri/caisse, qui sont en rayon, n'y ont pas accès. | v1 : administrateurs uniquement + export CSV à donner aux bénévoles. v2 : un mode « Vérification » dans l'application Scan (voir I-1). |
| **Q-SIG-2** | Que fait-on des lignes PAS TROUVÉ existantes ? | Les convertir en signalements si < 30 jours et fiche encore disponible ; sinon les effacer. |
| **Q-SIG-3** | Des membres ont pu cocher ACHETÉ **à la main** (achat anonyme). On perd cette possibilité. Garder un « Je l'ai acheté » manuel ? | Non : proposer plutôt « Retirer de ma sélection ». L'historique fiable reste Mes achats. |
| **Q-SIG-4** | Y a-t-il plusieurs lieux où chercher (bourse ponctuelle vs local/permanence) ? Si oui, le lieu aide beaucoup le bénévole. | Masquer le champ tant que la réponse est « un seul lieu ». |
| **Q-SIG-5** | Pour une fiche rare introuvable, que signifie « sortir du stock » ? Il n'existe aujourd'hui que *Brouillon* / *Publié* et *Vendu*. | Ajouter un état « Retiré » ou repasser en *Brouillon* avec une note. |
| **Q-SIG-6** | Le membre doit-il être **prévenu** (e-mail) de l'issue ? | Non en v1 : l'information apparaît dans Ma sélection. Pas de nouvel e-mail = pas de nouveau consentement. |
| **Q-SIG-7** | Un signalement peut-il venir d'ailleurs que Ma sélection (fiche publique, Mes recherches) ? | Non en v1 (voir I-2). |
| **Q-SIG-8** | Faut-il un délai de relance : que devient un signalement jamais traité ? | Tuile « signalés depuis plus de 7 jours » ; pas de clôture automatique. |

### 13.2 Trous repérés dans le plan initial

1. **Le stock est par ISBN, pas par exemplaire.** Un signalement ne dit pas *quel*
   exemplaire manque. D'où le choix de demander au bénévole combien il en retrouve
   (7.4) plutôt que de retirer tout le stock.
2. **Les bénévoles de terrain n'ont pas accès à l'admin** (Q-SIG-1). Sans réponse,
   la file risque de rester pleine.
3. **Les ventes rendent les signalements obsolètes** — sans la règle RG-73, la file
   contiendrait des livres déjà partis.
4. **Abus possible** : un membre malveillant pourrait signaler tout le catalogue.
   Couvert par RG-70 et RG-71 (jamais de retrait automatique).
5. **Données personnelles** : un commentaire libre peut contenir un nom ou un
   téléphone. RG-74/RG-75 + mention dans le document RGPD (09) à ajouter.
6. **Filtre « Prochaine visite »** reposait sur les états supprimés : il faut le
   retirer ou le redéfinir (6.6).
7. **API existante** `PATCH /catalog/me/selection/{id}` : à retirer ou à restreindre, sinon
   un client ancien (cache navigateur) pourrait encore poser des états. Prévoir un
   refus propre (410 / 400) plutôt qu'une erreur générique.
8. **Statistiques** : le document 10 prévoyait de compter les lignes PAS TROUVÉ. La
   mesure devient « nombre de signalements, taux de *retrouvé* vs *retiré* ».
9. **Livres retirés puis rescannés** : si le livre réapparaît (retrouvé plus tard dans
   un carton), le scan d'entrée normal le remet en stock ; rien de spécial à faire,
   mais la fiche devrait garder la trace du retrait précédent (déjà le cas via les
   mouvements).

### 13.3 Idées (hors v1, à arbitrer)

| # | Idée | Intérêt |
|---|---|---|
| I-1 | **Mode « Vérification » dans l'application Scan** : le bénévole scanne l'ISBN d'un livre retrouvé → clôture *Retrouvé* ; en fin de tournée, les non scannés sont proposés au retrait. | Met la vérification entre les mains de ceux qui sont en rayon, sans droits admin. |
| I-2 | Bouton « Je ne l'ai pas trouvé » aussi sur la **fiche publique** pour les membres connectés. | Plus de signalements, même sans avoir préparé une sélection. |
| I-3 | **Badge « vérification en cours »** sur la fiche publique à partir de N signalements (ex. 2 membres distincts). | Évite que d'autres membres se déplacent pour rien, sans retirer le livre. |
| I-4 | **Indicateur de fiabilité du stock** dans Statistiques : taux de signalements confirmés par genre / par rayon. | Repérer les rayons mal tenus ou les pertes. |
| I-5 | Garder un signalement « en attente » dans le navigateur d'un visiteur anonyme et l'envoyer après connexion. | Moins de friction, mais plus complexe ; à faire seulement si les chiffres montrent un abandon. |
| I-6 | Remercier le membre (compteur « vous avez aidé à tenir le catalogue à jour 3 fois »). | Valorise la contribution, dans l'esprit associatif. |
| I-7 | Déclencher un **inventaire partiel** (maquette `AdminInventaire`) à partir des genres les plus signalés. | Relie la file au travail d'inventaire déjà prévu. |

## 14. Hors périmètre

- Retrait automatique du stock sans vérification humaine.
- Notification e-mail au membre.
- Signalement d'un livre **abîmé** ou d'une **erreur de fiche** (titre, couverture) :
  besoin voisin mais distinct, à traiter séparément si demandé.
- Géolocalisation / numéro de rayon (le stock n'a pas d'emplacement aujourd'hui).

## 15. Suite

1. Validation de ce document (et des réponses Q-SIG-1 à Q-SIG-8).
2. ✅ Maquettes des écrans E1 à E8, desktop et mobile 390 px :
   [`maquettes/signalement-introuvable/`](maquettes/signalement-introuvable/README.md).
3. ✅ Plan d'implémentation : [`plan/07-signalement-livre-introuvable.md`](plan/07-signalement-livre-introuvable.md)
   (la mise à jour de `06-regles-metier.md`, `10-…` et `09-rgpd-…` y est la tâche SIG-16).
