# Coups de cœur et suggestions personnalisées — spécification fonctionnelle

> Statut : **en relecture**, 2026-11-13. Étend le module
> [`docs/bourse-aux-livres/`](../../bourse-aux-livres/README.md) et la spécification des
> recommandations [`docs/recommandations-livres/07-specification.md`](../../recommandations-livres/07-specification.md).
> Règles nouvelles : `RG-130` à `RG-159`, à la suite de `RG-129`
> ([`isbndb-migration`](../isbndb-migration/01-fonctionnel.md)).

## 1. Contexte

Le site sait déjà recommander :

- **« Dans le même esprit »**, sous la fiche d'un livre : les voisins de ce livre dans le
  fonds de la bourse (anonyme) ;
- **« Pour vous »**, sur l'accueil et en tête de *Mes achats* : des livres disponibles
  qui ressemblent aux **achats associés** du membre (`07` §3.2).

Deux limites :

1. Les suggestions personnelles ne partent que des **achats**. Un livre acheté n'est pas
   forcément un livre aimé, et un visiteur sans compte ou sans achat n'a rien.
2. On ne peut partir que d'un livre **déjà passé à la bourse**. Or ce qui décrit le mieux
   les goûts d'un lecteur, ce sont les livres qu'il a aimés, où qu'il les ait lus.

Cette évolution permet à chacun de déclarer ses **coups de cœur**, y compris des livres
jamais passés à la bourse, et en tire des suggestions de livres **disponibles à la
bourse**. Elle les prolonge par un **e-mail avant chaque bourse** et par un signal au tri
sur la scanette.

## 2. Objectifs

| # | Objectif |
|---|---|
| O1 | Tout visiteur, connecté ou non, peut constituer une liste de **coups de cœur** à partir de n'importe quel livre, présent ou non à la bourse. |
| O2 | Il obtient des **suggestions de livres disponibles ou annoncés** qui ressemblent à ses coups de cœur, classées et notées par un **indice de confiance en cœurs**, avec un réglage d'exigence. |
| O3 | Les suggestions personnelles partent **des coups de cœur** et non plus automatiquement des achats. Un achat devient coup de cœur par un geste explicite (« J'ai aimé »). |
| O4 | Le visiteur sans compte est prévenu que sa liste vit dans son navigateur, et invité à créer un compte qui **reprend automatiquement** sa liste. |
| O5 | Le membre voit ses suggestions **en premier sur l'accueil**, juste sous la barre de recherche. |
| O6 | Le membre qui l'accepte reçoit, **quelques jours avant chaque bourse**, un e-mail avec les nouveautés qui pourraient lui plaire et les livres très demandés de nouveau disponibles. |
| O7 | Au tri, la scanette signale qu'un livre a **déjà plu** à des lecteurs de la bourse. |
| O8 | Ajouter un coup de cœur crée la fiche du livre une fois pour toutes : si le livre arrive un jour au tri, **aucune nouvelle requête ISBNdb** n'est nécessaire. |

## 3. Hors périmètre

- Une note négative (« je n'ai pas aimé ») ou une note de 1 à 5 donnée par le lecteur.
- Le partage d'une liste de coups de cœur, ou des listes publiques.
- L'import depuis Babelio, Goodreads ou un autre site.
- Suggérer des livres **absents** de la bourse : on ne suggère que des livres disponibles
  ou annoncés (`07` §3.2).
- Les notifications push ou SMS.
- Toute modification du verdict de tri (`RG-15`) : le signal coup de cœur est informatif
  (`RG-155`).
- La refonte de « Dans le même esprit » sous la fiche : inchangé.

## 4. Vocabulaire et acteurs

| Terme | Définition |
|---|---|
| **Coup de cœur** | Livre qu'une personne déclare avoir aimé. Il sert de point de départ aux suggestions. Il ne réserve rien, ne déclenche aucune alerte et ne signifie pas qu'on le recherche. |
| **Mes coups de cœur** | Liste des coups de cœur d'une personne : dans son navigateur si elle n'est pas connectée, dans son compte sinon. |
| **Suggestion** | Livre disponible ou annoncé à la bourse, proposé parce qu'il ressemble à un ou plusieurs coups de cœur. |
| **Indice de confiance** | Note de 1 à 5 cœurs qui dit à quel point le site est sûr que la suggestion plaira. |
| **Exigence** | Réglage de la personne : nombre minimum de cœurs pour qu'une suggestion s'affiche (« Plus de découvertes ↔ Plus sûr »). |
| **Fiche de référence** | Fiche livre créée pour un coup de cœur, sans aucun mouvement de stock. Elle n'apparaît pas dans le catalogue public tant que le livre n'est pas arrivé à la bourse. |
| **E-mail avant la bourse** | Message envoyé quelques jours avant l'ouverture d'une bourse aux membres qui l'ont accepté. |
| **Très demandé** | Œuvre tombée à 0 exemplaire après au moins 3 ventes, ou classée dans les 20 plus vendues de la bourse précédente (`RG-149`). |

Les termes de `10` §3 restent valables. *Coup de cœur* ne remplace ni *liste de
recherche* (je veux être alerté quand il arrive) ni *Ma sélection* (je le repère pour ma
visite).

| Acteur | Ce qu'il fait |
|---|---|
| **Visiteur anonyme** | Constitue ses coups de cœur dans son navigateur, voit ses suggestions, peut créer un compte. |
| **Membre connecté** | Coups de cœur synchronisés, suggestions sur l'accueil, « J'ai aimé » dans *Mes achats*, e-mail avant la bourse s'il l'accepte. |
| **Bénévole de tri** | Voit le bandeau « Ce livre a déjà plu » sur la scanette. |
| **Administrateur** | Prévisualise l'e-mail avant la bourse, voit sa date d'envoi et son compte rendu, peut le suspendre. |
| **Système** | Crée les fiches de référence, calcule les suggestions, envoie l'e-mail. |

## 5. Parcours

### P1 — Un visiteur sans compte découvre la fonctionnalité

1. Sur l'accueil, il voit l'encart « Trouver un livre qui pourrait vous plaire » et clique.
2. La page **Trouver mon prochain livre** (E2) s'ouvre, vide. Elle l'invite à ajouter un
   premier coup de cœur.
3. Il clique « Ajouter un coup de cœur » (E3) et tape « Le Seigneur des anneaux ». Les
   résultats viennent du catalogue et des sources gratuites. ISBNdb n'est pas interrogé
   (`RG-133`).
4. Il choisit une édition. Le livre rejoint *Mes coups de cœur* (`RG-134`). Un bandeau
   lui explique que sa liste est enregistrée dans ce navigateur (`RG-140`).
5. En quelques secondes, la partie « Ils pourraient vous plaire » se remplit de livres
   disponibles à la bourse, chacun avec ses cœurs et sa raison (`RG-136`, `RG-137`).
   Pendant le calcul, la partie affiche « Calcul en cours » (`RG-139`).
6. Il ajoute deux autres livres. Les suggestions se mettent à jour.
7. Il règle le curseur d'exigence sur « Plus sûr » : seules les suggestions à 4 ou 5
   cœurs restent (`RG-138`).

### P2 — Il crée un compte pour garder sa liste

1. Dans le bandeau navigateur de E2, il clique « Créer un compte pour garder ma liste ».
2. Le formulaire de création de compte propose la case « Recevoir avant chaque bourse
   une sélection de livres qui pourraient me plaire » (non cochée, `RG-146`).
3. Après la création du compte (ou une connexion à un compte existant), ses coups de cœur
   locaux sont **ajoutés automatiquement** au compte (`RG-141`). Un message confirme :
   « 3 coups de cœur ajoutés à votre compte ».
4. Le bandeau navigateur disparaît. Ses suggestions apparaissent sur l'accueil (`RG-143`).

### P3 — Un membre transforme ses achats en coups de cœur

1. Dans *Mes achats*, chaque livre porte un bouton « J'ai aimé » (E6).
2. Il clique sur deux livres. Ils rejoignent ses coups de cœur (`RG-142`).
3. Les autres achats ne servent plus de base aux suggestions. Ils restent exclus des
   suggestions (`RG-137`).

### P4 — Un membre ouvre l'accueil

1. Il a des coups de cœur et au moins 3 suggestions au niveau d'exigence choisi.
2. La rangée « Pour vous » est la **première section sous la barre de recherche**
   (`RG-143`). Chaque carte montre ses cœurs et sa raison.
3. « Voir toutes mes suggestions » ouvre E2.

### P5 — L'e-mail avant la bourse

1. Une bourse est programmée le samedi 14 mars 2027.
2. Le mercredi 11 mars à 10 h (heure de Paris), le système prépare l'e-mail de chaque
   membre qui l'a accepté et a au moins un coup de cœur (`RG-145`, `RG-146`).
3. Le membre reçoit « Avant la bourse du 14 mars : 6 livres qui pourraient vous plaire ».
   Il y voit l'encart de la bourse, la section **« Nouveautés pour vous »** (livres arrivés
   depuis la bourse précédente), puis **« De retour à la bourse »** (livres très demandés de
   nouveau disponibles) (`RG-147` à `RG-150`).
4. Il clique sur une carte et arrive sur la fiche du livre. Il peut se désabonner en un
   clic (`RG-152`).

### P6 — Au tri, un livre qui a déjà plu

1. Un bénévole scanne *L'Écume des jours*. Trois membres l'ont en coup de cœur (cette
   édition ou une autre édition de la même œuvre).
2. Sous le verdict, la scanette affiche un bandeau rouge avec un cœur : « Ce livre a déjà
   plu à 3 lecteurs de la bourse » (`RG-155`).
3. Le verdict ne change pas. Le bénévole décide (`RG-16`).
4. Si ce livre était une fiche de référence créée pour un coup de cœur, sa fiche est déjà
   complète : aucune requête ISBNdb n'est faite (`RG-132`).

## 6. Écrans

Sauf mention contraire, chaque écran est maquetté en desktop 1440 px et en mobile 390 px.

| # | Écran | Nature | Écran d'origine |
|---|---|---|---|
| **E1** | **Accueil** : rangée « Pour vous » remontée juste sous la barre de recherche, avec cœurs et lien « Voir toutes mes suggestions » ; sans coup de cœur, encart d'entrée « Trouver un livre qui pourrait vous plaire » au même endroit | *modifié* | accueil du catalogue, section « Pour vous » (`07` R4, R5) |
| **E2** | **Trouver mon prochain livre** : en-tête, partie « Mes coups de cœur » (liste, ajout, retrait, pastille de disponibilité), partie « Ils pourraient vous plaire » (cartes avec cœurs et raison, curseur d'exigence), bandeau navigateur et bouton de création de compte pour l'anonyme, préférence e-mail pour le membre | *nouveau* | même grammaire que l'accueil et *Ma sélection* |
| **E3** | **Ajouter un coup de cœur** : recherche (titre, auteur, ISBN), résultats du catalogue et des sources externes, choix de l'édition, ajout ; panneau latéral sur desktop, plein écran sur mobile | *nouveau* | même grammaire que la recherche du catalogue (`catalog-search-page`) |
| **E4** | **Création de compte et connexion** : case de consentement à l'e-mail avant la bourse (si l'on vient des coups de cœur), message de reprise « N coups de cœur ajoutés à votre compte » | *modifié* | pages de création de compte et de connexion du catalogue |
| **E5** | **Fiche livre** : bouton « Coup de cœur » (ajout et retrait) près des actions existantes | *modifié* | fiche publique du livre (`04` §5) |
| **E6** | **Mes achats** : bouton « J'ai aimé » sur chaque ligne ; retrait du bandeau « Pour prolonger vos lectures » | *modifié* | *Mes achats* (`07` R6) |
| **E7** | **Compte et données** : préférence « Suggestions personnalisées » au texte réécrit, nouvelle préférence « E-mail avant chaque bourse », réglage d'exigence | *modifié* | Compte et données (`07` R7) |
| **E8** | **E-mail avant la bourse** : en-tête, accroche, encart bourse, « Nouveautés pour vous », « De retour à la bourse », mention de non-réservation, bouton « Voir toutes mes suggestions », pied avec désabonnement. Rendus 600 px et 390 px | *nouveau* | gabarit des alertes livres (`RG-90` à `RG-99`) |
| **E9** | **Administration, e-mail avant la bourse** : prochaine bourse, date d'envoi prévue, nombre de destinataires estimé, aperçu, « M'envoyer un test », « Suspendre l'envoi pour cette bourse », compte rendu du dernier envoi | *nouveau* | même grammaire que les tuiles du tableau de bord d'administration |
| **E10** | **Scanette, écran de résultat** : bandeau rouge avec un cœur « Ce livre a déjà plu à N lecteurs de la bourse », sous le verdict | *modifié* | `03` §3.3 Écran de résultat (mobile seulement) |
| **E11** | **États** : E2 vide, E2 en calcul, E2 sans suggestion au niveau d'exigence choisi, suggestions désactivées, plafond de coups de cœur atteint, plafond de création atteint, erreur de recherche ; E8 sans « Nouveautés », images bloquées, mode sombre, texte brut ; E10 hors ligne | *nouveau* | — |

## 7. Règles métier

### Coups de cœur

#### `RG-130` — Un coup de cœur porte sur une édition et compte pour son œuvre
On ajoute une **édition** précise (un ISBN), celle qu'on choisit dans la recherche.
Pour les suggestions, les exclusions et le signal de tri, elle compte pour **toute son
œuvre** quand le lien entre éditions est fiable (identifiant d'œuvre BnF / Open Library ou
`other_isbns`, `RG-119`, décision `Q-01`). Ajouter deux éditions de la même œuvre n'en
fait qu'un seul coup de cœur affiché, avec la mention « 2 éditions ».

#### `RG-131` — Plafonds
- *Mes coups de cœur* contient au plus **50** livres dans un navigateur et **200** dans un
  compte. Au-delà, l'ajout est refusé avec « Vous avez atteint 50 coups de cœur. Retirez-en
  un pour en ajouter un autre. » (et l'invitation à créer un compte pour l'anonyme).
- Les valeurs sont réglables sans modifier l'application.

#### `RG-132` — Fiche de référence : créée une fois, réutilisée au tri
Quand on ajoute un livre qui n'a pas encore de fiche, le système crée sa fiche de la même
façon qu'au scan : ISBNdb d'abord, dans le respect de `RG-104` à `RG-107`, puis le secours
si nécessaire. Cette **fiche de référence** :
- n'a aucun mouvement de stock (ni disponible, ni annoncé, ni vendu) ;
- n'apparaît **nulle part dans le catalogue public** (listes, recherche, pages œuvre) tant
  qu'elle n'a pas de mouvement ;
- est conservée même si plus personne ne l'a en coup de cœur ;
- est **réutilisée telle quelle** si le livre arrive au tri : aucune requête ISBNdb
  supplémentaire (`RG-104`). Elle devient alors une fiche ordinaire du catalogue.

Une fiche de secours créée pour un coup de cœur entre dans le rattrapage nocturne au même
rang que les fiches suivies par un membre (`RG-108`, rang 2).

#### `RG-133` — Pas d'ISBNdb pendant la recherche
La recherche de E3 s'appuie sur le catalogue et les sources gratuites (BnF, Open Library),
comme la recherche publique (`RG-116`). Seul l'**ajout** d'un livre sans fiche coûte une
requête ISBNdb. Le bouton « Voir d'autres éditions » de `RG-117` est aussi proposé dans
E3, avec ses règles et ses plafonds (`RG-118`).

#### `RG-134` — Ajouter, retirer
- On ajoute depuis E3, depuis la fiche d'un livre (E5) ou depuis *Mes achats* (E6).
- On retire depuis E2, E5 ou E6 (le bouton fonctionne dans les deux sens). Le retrait est
  immédiat, avec « Annuler » pendant quelques secondes.
- La liste est triée de l'ajout le plus récent au plus ancien.
- Une carte de coup de cœur montre la couverture, le titre et l'auteur. Si le livre est
  disponible ou annoncé à la bourse, elle porte la pastille de disponibilité et mène à sa
  fiche. Sinon elle n'est pas cliquable.

#### `RG-135` — Plafond de création de fiches
La création de fiches de référence est limitée **par visiteur** (*valeur de départ :
20 fiches par jour*) et **au total** (*valeur de départ : 300 crédits ISBNdb par jour*,
décomptés à part dans la carte ISBNdb sous l'usage « Coups de cœur », `RG-125`). Au-delà,
ou si le quota est épuisé, la fiche est créée par le secours (`RG-106`) : l'ajout réussit
toujours et le visiteur ne voit aucune différence. Si même le secours ne trouve rien,
l'ajout est refusé : « Nous ne trouvons pas assez d'informations sur ce livre pour vous
proposer des suggestions. »

### Suggestions

#### `RG-136` — Les suggestions partent des coups de cœur
Les suggestions sont calculées avec le moteur existant (`07` §2) : voisins de chaque coup de
cœur dans le fonds de la bourse, score, raison. Tous les coups de cœur ont le même poids.
La raison affichée est celle du coup de cœur qui contribue le plus : « La suite de *X* »
si c'est le tome suivant, sinon « Parce que vous avez aimé *X* ». Si plusieurs coups de
cœur contribuent : « Parce que vous avez aimé *X* et 2 autres ».

**Cette règle remplace la base de calcul de `07` §3.2 :** les achats associés ne sont plus
des points de départ.

#### `RG-137` — Ce qu'on ne suggère jamais
Une suggestion est toujours une fiche visible, **disponible ou annoncée**, dont le score
atteint le seuil (`Recommendations:SimilarMinScore`). Sont exclus :
- toute édition d'une œuvre **déjà achetée** (achats associés, pour un membre) ;
- toute édition d'une œuvre **déjà en coup de cœur** ;
- les livres de *Ma sélection* ;
- les fiches de référence (elles n'ont pas de stock).

#### `RG-138` — Indice de confiance et exigence
- Chaque suggestion reçoit **1 à 5 cœurs**. Ils traduisent son score par paliers. Une
  suggestion qui ressemble à plusieurs coups de cœur gagne un palier (sans dépasser 5).
  Les paliers sont réglables sans modifier l'application et calibrés sur le vrai
  catalogue avant l'ouverture.
- Les suggestions sont triées par nombre de cœurs, puis par score.
- Le curseur **« Plus de découvertes ↔ Plus sûr »** a quatre positions : 1, 2, 3 ou 4 cœurs
  minimum. **Défaut : 3.** Il s'applique partout : E2, accueil, e-mail.
- Le réglage est gardé dans le navigateur pour l'anonyme et dans le compte pour le membre.
  La reprise de compte (`RG-141`) le conserve.
- Les cœurs se lisent aussi sans couleur : « 4 cœurs sur 5 » pour les lecteurs d'écran.

#### `RG-139` — Suggestions disponibles dans la minute
Un coup de cœur ajouté produit ses suggestions **sans attendre le calcul de nuit** :
profil, vecteur et voisins du livre sont calculés à l'ajout. Tant que ce calcul n'est pas
fini, E2 affiche « Calcul en cours… » à la place des cartes liées à ce livre, et le calcul
se complète sans recharger la page. Pour une fiche de secours, le calcul se fait tout de
suite sur les données de secours, puis est refait quand le rattrapage l'a remplie
(exception assumée à `RG-112`).

Le calcul de nuit continue de couvrir les fiches de référence, **comme points de départ
seulement**.

#### Visiteur anonyme
Pour l'anonyme, le navigateur n'envoie que la liste des ISBN de ses coups de cœur et son
réglage d'exigence. Aucun identifiant n'est conservé côté serveur avec cette liste.

### Sans compte, puis avec compte

#### `RG-140` — Bandeau navigateur
Tant qu'une personne non connectée a au moins un coup de cœur, E2 affiche le bandeau :
« Vos coups de cœur sont enregistrés dans ce navigateur. Si vous videz son cache ou
changez d'appareil, ils seront perdus. Créez un compte pour les garder, les retrouver
partout, voir vos suggestions sur l'accueil et recevoir une sélection avant chaque
bourse. » avec le bouton « Créer un compte pour garder ma liste » et le lien « J'ai déjà
un compte ». Le bandeau ne peut pas être fermé définitivement. On peut le replier.

#### `RG-141` — Reprise automatique à la connexion
À la création d'un compte, ou à la connexion depuis un navigateur qui contient des coups
de cœur locaux, ceux-ci sont **ajoutés au compte sans confirmation** : c'est une union, rien
n'est supprimé (`RG-56`). Les doublons (même ISBN) sont fusionnés. Le plafond du compte
s'applique : au-delà de 200, les plus anciens coups de cœur locaux ne sont pas repris et
le message le dit. Message : « 3 coups de cœur ajoutés à votre compte ». La liste locale
est ensuite vidée. C'est le compte qui fait foi.

#### `RG-142` — « J'ai aimé » dans Mes achats
Chaque ligne de *Mes achats* porte « J'ai aimé ». Le bouton ajoute ce livre aux coups de
cœur (ou le retire). Le bandeau « Pour prolonger vos lectures » et l'invitation « sans
achat » (`07` R6, R8-A) sont retirés : les suggestions vivent dans E2 et sur l'accueil.

### Accueil

#### `RG-143` — « Pour vous » en premier
Si la personne (membre, ou anonyme avec des coups de cœur locaux) obtient au moins
**3 suggestions** au niveau d'exigence choisi, la rangée « Pour vous » est la **première
section sous la barre de recherche**. Elle montre 4 cartes sur desktop, en défilement sur
mobile, chacune avec ses cœurs et sa raison, et le lien « Voir toutes mes suggestions »
vers E2.

#### `RG-144` — Encart d'entrée
Sans coup de cœur, ou avec moins de 3 suggestions, le même emplacement affiche l'encart
« Trouver un livre qui pourrait vous plaire », qui mène à E2. La personne peut le masquer
(« Ne plus afficher »). Ce choix est mémorisé dans le navigateur. E2 reste accessible
depuis le menu.

### E-mail avant la bourse

#### `RG-145` — Quand
L'e-mail part **3 jours avant le premier jour d'une bourse programmée, à 10 h (heure de
Paris)**. Ces deux valeurs sont réglables. Si la bourse est programmée moins de 3 jours
avant son ouverture, l'e-mail part dans l'heure qui suit, à condition qu'il reste au
moins 24 h avant l'ouverture. Sinon, il ne part pas. Un seul envoi par bourse et par
membre.

#### `RG-146` — À qui : consentement explicite
L'e-mail ne part qu'aux membres qui ont **coché** « Recevoir avant chaque bourse une
sélection de livres qui pourraient me plaire » et qui ont au moins un coup de cœur.
- La case n'est jamais cochée par défaut. Elle est proposée à la création de compte depuis
  E2 (E4), dans E2 pour un membre, et dans Compte et données (E7).
- Le consentement est tracé (date). On le retire en un clic, depuis l'e-mail ou E7.
- Les membres dont les alertes sont suspendues (`RG-44`) ne le reçoivent pas.

#### `RG-147` — « Nouveautés pour vous »
Cette section contient les suggestions (`RG-136` à `RG-138`, avec l'exigence du membre)
limitées aux livres **arrivés depuis la bourse précédente** : ceux rendus disponibles ou
annoncés après le dernier jour de la bourse précédente, et encore disponibles ou annoncés
pour celle-ci. Au plus **8** cartes, les plus sûres d'abord, chacune avec ses cœurs et sa
raison. Si la préférence « Suggestions personnalisées » est désactivée, la section est
absente.

#### `RG-148` — « De retour à la bourse »
Cette section contient les livres **très demandés** (`RG-149`) arrivés depuis la bourse
précédente et disponibles ou annoncés pour celle-ci. Au plus **6** cartes, les plus
vendues d'abord, avec l'étiquette « Très demandé » ou « De retour ». La liste est la même
pour tous, moins les exclusions personnelles (`RG-150`).

#### `RG-149` — Très demandé
Une œuvre est très demandée si l'une des conditions est vraie :
- elle est tombée à **0 exemplaire** disponible et annoncé après au moins **3 ventes**
  cumulées ;
- elle est parmi les **20 œuvres les plus vendues** de la bourse précédente.

Les valeurs 3 et 20 sont réglables.

#### `RG-150` — Exclusions de l'e-mail
Ne figurent jamais dans l'e-mail d'un membre : ses œuvres déjà achetées, ses coups de
cœur, et les livres de sa **liste de recherche** (déjà annoncés par une alerte, `RG-28`).
Un même livre n'apparaît qu'une fois : s'il relève des deux sections, il reste dans
« Nouveautés pour vous ».

#### `RG-151` — Rien à dire, rien d'envoyé
L'e-mail part si « Nouveautés pour vous » contient au moins 1 livre, **ou** si « De retour
à la bourse » en contient au moins 3. Sinon il ne part pas pour ce membre.

#### `RG-152` — Gabarit et contenu
L'e-mail reprend le gabarit des alertes (`RG-90`) : en-tête, accroche, encart bourse
(`RG-91`, mode `PROCHAINE BOURSE`), cartes (`RG-92`, avec les cœurs en plus), mention de
non-réservation (`RG-94`), lisibilité (`RG-98`), version texte (`RG-99`).
- **Objet** : « Avant la bourse du samedi 14 mars : 6 livres qui pourraient vous plaire ».
- **Texte d'aperçu** : « 4 nouveautés pour vous · 2 livres de retour ».
- **Accroche** : « Bonjour Marie, voici ce qui vous attend samedi. »
- **Bouton principal** : « Voir toutes mes suggestions », vers E2.
- **Pied** : « Vous recevez cet e-mail parce que vous avez demandé une sélection avant
  chaque bourse. » avec « Se désabonner » (retrait du consentement en un clic, avec
  l'en-tête de désabonnement en un clic).

#### `RG-153` — Pilotage par l'administration
E9 montre, pour la prochaine bourse : la date d'envoi prévue, le nombre de destinataires
estimé, un aperçu construit avec les coups de cœur de l'administrateur connecté (ou un
jeu d'exemple s'il n'en a pas), « M'envoyer un test » et « Suspendre l'envoi pour cette
bourse » (réversible jusqu'à l'envoi). Après l'envoi, E9 montre le compte rendu :
envoyés, non envoyés faute de contenu, erreurs.

### Préférences

#### `RG-154` — Préférences du membre
Dans Compte et données (E7) :
- **Suggestions personnalisées** (`07` §3.3) : activée par défaut. Texte réécrit : « Nous
  vous proposons des livres de la bourse qui ressemblent à vos coups de cœur. Le calcul se
  fait sur nos serveurs, à partir des notices des livres ; aucune donnée personnelle n'est
  envoyée à un service extérieur. » Désactivée, elle fait disparaître les suggestions de
  l'accueil, de E2 (les coups de cœur restent gérables) et de l'e-mail (`RG-147`).
- **E-mail avant chaque bourse** (`RG-146`) : désactivé par défaut.
- **Exigence** (`RG-138`).

### Scanette

#### `RG-155` — Bandeau « Ce livre a déjà plu »
Au tri, si le livre scanné (cet ISBN, ou une édition de la même œuvre par lien fiable,
`RG-130`) figure dans les coups de cœur d'au moins une personne, l'écran de résultat
affiche, **sous le verdict**, un bandeau **rouge avec un cœur** :
- « Ce livre a déjà plu à un lecteur de la bourse » ;
- « Ce livre a déjà plu à 3 lecteurs de la bourse ».

Le nombre est affiché, **jamais l'identité** (`RG-13`). Le bandeau est un complément : il
ne remplace pas le verdict et ne change pas sa priorité (`RG-15`, `RG-16`). Il apparaît
aussi en mode Consultation, pas en Caisse.

#### `RG-156` — Qui compte
Le nombre compte :
- les membres qui ont ce livre (ou son œuvre) en coup de cœur ;
- les ajouts faits sans compte. Le navigateur signale chaque ajout et chaque retrait au
  serveur avec le seul ISBN, sans aucun identifiant. Le serveur tient un compteur par
  livre. Lors de la reprise de compte (`RG-141`), l'ajout anonyme déjà compté n'est pas
  compté une deuxième fois.

#### `RG-157` — Hors ligne
Le nombre de coups de cœur par livre fait partie des données synchronisées sur la
scanette, comme le nombre de demandeurs (`RG-13`, `RG-122`). Hors ligne, le bandeau
s'appuie sur ces données. Sa fraîcheur est celle des autres données locales (`ENF-05`). Le
délai d'affichage du verdict (`ENF-01`) ne doit pas s'allonger.

### Données personnelles

#### `RG-158` — RGPD
- Les coups de cœur d'un membre sont des données personnelles. Ils sont supprimés avec
  le compte et inclus dans l'export de ses données (`09` §4).
- Les coups de cœur d'un anonyme restent dans son navigateur. Le serveur ne reçoit que
  des ISBN, sans identifiant (`RG-139`, `RG-156`).
- Le compteur de la scanette ne contient aucune donnée personnelle.
- L'e-mail avant la bourse repose sur le **consentement** (`RG-146`). Les suggestions
  personnalisées restent fondées sur l'intérêt légitime, avec opposition par la
  préférence (`07` §4).
- À faire avant l'ouverture : ajouter « Coups de cœur » et « E-mail avant la bourse » au
  tableau des traitements de `09` §2, et mettre à jour la page publique « Vos données et
  le RGPD ».

#### `RG-159` — Interrupteur général
Toute la fonctionnalité (E1 à E11) dépend de l'interrupteur des recommandations
(`Recommendations:Enabled`). L'e-mail avant la bourse a son propre interrupteur, désactivé
par défaut, pour pouvoir ouvrir les coups de cœur avant l'e-mail.

## 8. Critères d'acceptation

| # | Critère |
|---|---|
| CA-01 | Un visiteur sans compte ajoute *Le Seigneur des anneaux* (jamais passé à la bourse) : la recherche n'a fait aucune requête ISBNdb, l'ajout en a fait une, et la fiche créée n'apparaît ni dans la recherche publique ni dans les listes du catalogue. |
| CA-02 | Dans la minute qui suit l'ajout, E2 propose des livres disponibles ou annoncés qui lui ressemblent, chacun avec 1 à 5 cœurs et une raison. |
| CA-03 | Aucune suggestion n'est un coup de cœur, une autre édition d'un coup de cœur, un livre déjà acheté (toute édition de l'œuvre), un livre de *Ma sélection* ou un livre ni disponible ni annoncé. |
| CA-04 | Avec l'exigence sur « 4 cœurs minimum », aucune suggestion à 3 cœurs ou moins ne s'affiche, ni dans E2, ni sur l'accueil, ni dans l'e-mail. |
| CA-05 | Sans compte, le bandeau navigateur est visible dès le premier coup de cœur. Après la création d'un compte depuis ce bandeau, tous les coups de cœur locaux sont dans le compte sans confirmation, et le message indique leur nombre. |
| CA-06 | Un livre ajouté en coup de cœur puis scanné au tri ne déclenche aucune requête ISBNdb. Sa fiche devient visible au catalogue dès qu'il est disponible ou annoncé. |
| CA-07 | Un membre avec des achats mais sans coup de cœur ne voit pas « Pour vous » sur l'accueil : il voit l'encart d'entrée. Après « J'ai aimé » sur des achats, les suggestions apparaissent (s'il y en a au moins 3). |
| CA-08 | Avec au moins 3 suggestions, « Pour vous » est la première section sous la barre de recherche de l'accueil, en desktop comme en mobile. |
| CA-09 | Quota ISBNdb épuisé : l'ajout d'un coup de cœur réussit avec une fiche de secours, ses suggestions apparaissent tout de suite et sont recalculées après le rattrapage. |
| CA-10 | Le 21e ajout d'un même visiteur dans la journée (sans compte) crée une fiche de secours, sans requête ISBNdb ; le 51e coup de cœur local est refusé avec le message de `RG-131`. |
| CA-11 | Pour une bourse programmée le samedi 14 mars, l'e-mail part le mercredi 11 mars vers 10 h, uniquement aux membres qui ont coché le consentement et ont au moins un coup de cœur, une seule fois chacun. |
| CA-12 | L'e-mail ne contient que des livres arrivés depuis la bourse précédente. Il ne contient aucun livre déjà acheté, en coup de cœur ou dans la liste de recherche du membre. |
| CA-13 | Un membre sans nouveauté personnalisée et avec moins de 3 livres « De retour » ne reçoit rien. |
| CA-14 | « Se désabonner » dans l'e-mail retire le consentement en un clic ; aucun e-mail avant la bourse ne part ensuite. |
| CA-15 | Au tri, un livre en coup de cœur chez 3 membres affiche, sous le verdict, le bandeau rouge « Ce livre a déjà plu à 3 lecteurs de la bourse » ; le verdict est le même que sans coup de cœur ; le bandeau s'affiche hors ligne pour des données synchronisées. |
| CA-16 | La suppression d'un compte supprime ses coups de cœur et fait baisser les compteurs de la scanette. |
| CA-17 | Mobile 390 px : E2, E3 et l'accueil ne défilent pas horizontalement ; seules les rangées de cartes défilent. |

## 9. Contraintes

- **ISBNdb** : toutes les règles de `isbndb-migration` s'appliquent (une requête par ISBN,
  débit, quota partagé, secret jamais côté navigateur). La recherche de E3 n'interroge
  jamais ISBNdb.
- **Moteur de recommandation** : on réutilise les profils, les vecteurs, les voisins et les
  raisons de `07` §2. Seule la base de calcul personnelle change, et une fiche ajoutée
  est calculée tout de suite (`RG-139`). Les textes envoyés au service de vectorisation ne
  contiennent jamais de donnée personnelle (`07` CA-8).
- **Stockage local** : même principe que *Ma sélection* anonyme (`RG-55`). La lecture et
  l'écriture du navigateur peuvent échouer (navigation privée) : E2 reste utilisable pour
  la session, avec un message.
- **E-mail** : gabarit et contraintes de `book-alert-email-template` (tableaux, styles en
  ligne, 600 px, pas de JavaScript) ; expéditeur et image de marque de
  `docs/email-branding.md`.
- **Scanette** : le compteur de coups de cœur est une donnée de plus dans la
  synchronisation existante. Aucun appel réseau supplémentaire au moment du scan.
- **Couleur du bandeau** : le rouge est déjà la couleur du verdict « Inutile d'en
  garder » (`03` §3.3). Le bandeau coup de cœur doit rester distinct : bandeau fin sous le
  bloc de verdict, icône cœur, libellé explicite. La maquette vérifiera qu'on ne les
  confond pas.

## 10. Décisions

Arbitrages du 2026-11-13, validés sur la reformulation :

| # | Question | Décision |
|---|---|---|
| D-01 | Nom de l'écran | « Trouver mon prochain livre », qui contient « Mes coups de cœur » et « Ils pourraient vous plaire ». |
| D-02 | L'anonyme voit-il « Pour vous » sur l'accueil ? | Oui, à partir de ses coups de cœur locaux. |
| D-03 | Quota et abus | Plafonds `RG-131`, `RG-135` ; secours immédiat calculé tout de suite, recalculé après rattrapage. |
| D-04 | Consentement à l'e-mail | Case à cocher explicite, jamais par défaut. |
| D-05 | E-mail sans nouveauté personnalisée | Envoyé s'il y a au moins 3 livres « De retour », sinon rien. |
| D-06 | Définition de « très demandé » | `RG-149`. |
| D-07 | Mes achats | Bouton « J'ai aimé » ; bandeau « Pour prolonger vos lectures » retiré. |
| D-08 | Administration | Écran minimal E9. |
| D-09 | Signal au tri | Bandeau rouge avec un cœur sous le verdict, nombre de lecteurs, sans effet sur le verdict. |

## 11. Questions ouvertes

Aucune. Valeurs à calibrer avant l'ouverture (suivi, pas test) : paliers des cœurs
(`RG-138`), plafonds (`RG-131`, `RG-135`), seuils « très demandé » (`RG-149`).
