# Revue de la documentation technique

*Relecture du 2 septembre 2026, croisée avec l'état réel du dépôt.*

Ce document n'est pas un chapitre de l'architecture : c'est une liste de défauts à
corriger dans les documents `00` à `10`. Chaque constat porte un identifiant `R-nn`
stable, pour que le plan d'implémentation puisse s'y référer. Une fois un constat
traité dans le document concerné, il se marque `Traité` ici plutôt que de disparaître.

**Ce que la revue ne remet pas en cause.** Les dix décisions `DT-nn` tiennent. Le
raisonnement de `DT-02` (tout en SQL), de `DT-03` (outbox en table) et le réexamen de
`DT-04` sont solides et chiffrés. Le chapitre `04-app-scan.md` §3 — le parallélisme du
scan, la réponse tardive à jeter, le geste qui passe toujours par la file — décrit
correctement les pièges réels. Les constats ci-dessous ne portent pas sur les choix mais
sur ce qui manque autour d'eux.

## Vue d'ensemble

| Gravité | Constats |
|---|---|
| 🔴 **Bloquant** — la conception est fausse ou absente, et ça se paie en refonte | ~~`R-01`~~ ~~`R-02`~~ ~~`R-03`~~ ~~`R-04`~~ `R-06` ~~`R-08`~~ `R-11` |
| 🟠 **Sérieux** — la conception manque, mais s'ajoute sans casser | ~~`R-05`~~ ~~`R-07`~~ `R-09` `R-10` `R-12` `R-13` `R-14` `R-15` `R-16` `R-17` `R-18` |
| 🟡 **Factuel** — le document décrit un dépôt qui n'existe plus | `R-19` `R-20` `R-21` `R-22` |
| ⚪ **Mineur** — à corriger au passage | `R-23` à `R-30` |

**Où en est chaque constat ouvert dans le plan.** Un constat traité dans un document est
barré ci-dessus ; un constat ouvert est soit porté par une étape, soit non. La distinction
compte, parce que « ouvert » sans étape veut dire « oublié ».

| Constat | Porté par |
|---|---|
| `R-06` | `L0-11`, étape 8 — **arbitré en faveur de la recommandation de cette revue** : la suppression Graph se fait au préalable d'identité, pendant qu'il n'y a personne à supprimer |
| `R-09` | `plan/03`, palier 2 — le sort des fiches épuisées reste à trancher avant la première indexation |
| `R-10` `R-11` `R-12` `R-16` | `P1-2` |
| `R-13` `R-14` | ❌ **Aucune étape.** Le filigrane de synchronisation et le décompte des demandeurs dans la projection embarquée relèvent de `P1-5` et n'y sont pas nommés |
| `R-15` | `P1-4` |
| `R-17` | `plan/03`, palier 2 |
| `R-18` `R-29` | `L0-11` |
| `R-19` | `L0-5` |
| `R-20` | Correction documentaire seule — les fronts sont en Angular 21, pas 18 |
| `R-21` `R-22` `R-24` `R-25` `R-30` | `P1-8` |
| `R-23` | `L0-6` |
| `R-26` | Traité depuis par `T-08` §7, qui chiffre la base, l'ingestion et l'envoi. Reste « à chiffrer sur le calculateur Azure » les cinq réplicas permanents — sans étape |
| `R-27` | `P1-1` |
| `R-28` | `L0-11`, étape 4 — c'est la migration 0 |

### Traités

| # | Traité par | Le 2 septembre 2026 |
|---|---|---|
| `R-01` | **`DT-11`** | Sortie du serverless pour le palier fixe `S1`. `QT-09` mesure sa tenue au palier 1. `08` §2, §7 et §9 mis à jour |
| `R-05` | **`DT-12`** | Azure Communication Services Email sur `mail.volepapillondamour.fr`. Remonté au préalable pour la réputation d'envoi. `07` §7 et `08` §9 mis à jour |
| `R-07` | **`DT-13`** | `livres.volepapillondamour.fr`, URL en slug + ISBN, page d'œuvre canonique. `05` §1 mis à jour |
| `R-09` | *partiellement* | `05` §1 liste désormais ce que le SSR ne couvre pas. **Reste ouvert** : le traitement des fiches épuisées de `RG-26` — canonisation vers l'œuvre ou `noindex` sous un seuil — n'est pas tranché |
| `R-02` | `02` §2 | Table `AssociationSettings` à ligne unique et colonnes typées, migration 1, et transport dans le delta (`04` §2) |
| `R-03` | `02` §2 | Table `UserAlertHistory`, écrite **à l'envoi** dans la même transaction que le passage à `Sent`, avec la double vérification de `RG-30` |
| `R-08` | `02` §2 | Colonne `ClientGestureId` sur `BookMovements`, index unique filtré, recopiée sur l'annonce engendrée |
| `R-04` | **`DT-14`** | Une seule table de personnes, clé `oid`. `Members` abandonnée, facette « membre » sur `Watchlist`, suppression et anonymisation distinguées pour `ENF-12` |

Le blocage commun à `R-05` et `R-07` — l'accès à la zone DNS — **est levé** :
l'association détient `volepapillondamour.fr` et en a la main pleine et entière. Les
enregistrements se posent en une seule fois.

---

## 🔴 Bloquants

### `R-01` — La base SQL est *serverless* avec pause automatique, et le dossier technique l'ignore

> ✅ **Traité par [`DT-11`](01-decisions.md).** Palier fixe `S1`, ~30 $/mois, sans pause ni
> démarrage à froid — et vraisemblablement en baisse par rapport à la facture actuelle.
> PostgreSQL instruit et écarté. `QT-09` mesure la tenue de `S1` sur son stockage à disque
> dur au palier 1. Le texte ci-dessous n'est pas retouché : c'est le constat d'origine.

Le dossier ne mentionne nulle part que `vole-papillon-damour-db` est un
`GP_S_Gen5_1` **serverless, avec `autoPauseDelayMinutes: 60`**
(`infra/parameters/main.dev.bicepparam`, documenté dans `infra/README.md`). C'est
l'omission la plus lourde du dossier, parce qu'elle joue dans les deux sens et qu'aucune
des deux directions n'est neutre.

**Si la base reste éveillée.** Le balayage `sweep` toutes les cinq minutes
(`06` §3) interdit toute pause : chaque exécution ouvre une session SQL et remet le
compteur à zéro. La base passe de « quelques heures actives par jour » à **24 h/24**.
C'est un poste de coût récurrent que `08` §7 ne chiffre pas, et il est vraisemblablement
supérieur à celui du worker lui-même — dont le §7 fait pourtant grand cas.

**Si la base s'endort.** La reprise d'une base en pause prend des dizaines de secondes.
Or l'application de scan est utilisée **par salves, après plusieurs jours d'inactivité**
— c'est exactement le profil décrit en `DT-07` (« en dents de scie, une semaine par
mois »). Le premier scan d'une session de tri paierait ce réveil, et `ENF-01` (verdict en
moins d'une seconde) tomberait au moment précis où le bénévole juge l'outil. Même chose
pour la première visite du catalogue public, et `ENF-08`.

**Ce qu'il faut.** Une décision technique explicite — candidate `DT-11` — qui tranche
entre : désactiver la pause automatique et assumer le coût ; espacer le `sweep` au point
de laisser la base dormir, en acceptant que `RG-44` glisse ; ou dissocier la cadence des
traitements de fond de la disponibilité de la base. Et une mesure préalable : combien de
temps prend réellement la reprise, sur cette base, à ce gabarit.

Note connexe : ce paramètre est aussi ce qui rend l'argument de `DT-09` (« le coût
marginal serait nul ») moins net qu'il n'y paraît — l'API à `minReplicas: 1` qui
balaierait l'outbox toutes les cinq minutes tiendrait la base éveillée de la même façon.

### `R-02` — Aucun modèle pour les paramètres de `ENF-25`

> ✅ **Traité.** Table `AssociationSettings` en [`02`](02-modele-de-donnees.md) §2, ligne
> unique à colonnes typées, en migration 1. Transportée dans la réponse du delta
> ([`04`](04-app-scan.md) §2), sans quoi le verdict hors ligne n'appliquerait pas les
> seuils réels.

`ENF-25` exige que les huit seuils de `05` §9 soient modifiables sans redéploiement.
Le dossier technique n'en dit **rien** : ni table dans `02`, ni agrégat, ni commande dans
les tranches de `03` §2, ni endpoint. Seule la ligne « paramètres (`ENF-25`) » de
`03` §4 y fait allusion, sans contrepartie.

S'y ajoute une dépendance oubliée : `04` §5 pose que « les seuils viennent du serveur et
sont mis en cache avec le catalogue, pour rester applicables hors ligne ». Le contrat de
`GET /scan/catalog/delta` doit donc les transporter — la projection décrite en `04` §2
ne les contient pas.

Les huit paramètres pilotent `RG-10`, `RG-12`, `RG-14`, `RG-27`, `RG-30`, `RG-43`,
`RG-44` et l'écran de désengorgement. Ce n'est pas un détail d'administration : c'est le
calcul du verdict.

### `R-03` — Aucune table d'historique d'alertes, alors que deux règles l'exigent

> ✅ **Traité.** Table `UserAlertHistory` en [`02`](02-modele-de-donnees.md) §2, écrite
> **à l'envoi** et non à la mise en file, dans la même transaction que le passage à `Sent`.
> `RG-30` se vérifie deux fois : indicative à la clôture, faisant foi à l'envoi.

`RG-30` interdit plus d'une alerte par couple membre/ISBN sur une fenêtre glissante de
30 jours. Cela suppose d'interroger `(membre, ISBN, date d'envoi)`. La table
`OutboxMessage` de `06` §4 ne le permet pas : elle est **par membre et par session**, son
contenu est un `PayloadJson` opaque, et ses lignes `Sent` ne constituent pas un
historique requêtable par ISBN.

`ENF-12` cite par ailleurs explicitement « l'historique d'alertes » parmi ce qu'une
suppression de compte doit effacer. Cet historique n'existe dans aucun des documents.

Il manque une table — `UserAlertHistory(MemberId, Isbn13, SentAt)` ou équivalent —
écrite au moment de l'envoi, indexée pour `RG-30`, et incluse dans la cascade de
`ENF-12`. Sans elle, `RG-30` est inapplicable et `CloseScanSession` ne peut pas faire ce
que `03` §3 lui demande (« en respectant `RG-30` »).

### `R-04` — Deux tables de personnes, deux clés d'identité incompatibles

> ✅ **Traité par [`DT-14`](01-decisions.md).** Une seule table de personnes — la table
> `Users` existante —, rapprochée par **`oid`** et par lui seul. `Members` est abandonnée.
> La facette « membre » (statut d'alerte, compteur de rebonds) vit sur l'agrégat
> `Watchlist`, dont la ligne n'existe que pour qui se sert de la fonction. `ENF-12`
> distingue suppression et anonymisation selon que des mouvements pointent vers la
> personne. `02` §1 et §2, `10` §5 et `03` §2 mis à jour.

`10` §5 pose que la ligne `User` existante gagne un `ExternalId` = **`oid`** du jeton.
`02` §2 crée une table `Members` avec `ExternalSubjectId` = **`sub`** (« claim sub »).

Deux problèmes distincts, tous deux structurants :

**La clé n'est pas la même.** Dans un locataire externe, `sub` est **appairé par
application** : le même compte présente un `sub` différent au catalogue et au scan. `oid`
est stable à l'échelle du locataire. Rapprocher un `Member` d'un `User` par ces deux
colonnes est impossible, et un `Member` créé depuis le catalogue ne serait même pas
reconnu si l'on interrogeait `sub` depuis une autre application.

**La même personne aura deux lignes.** `01` §3 pose que « une bénévole trieuse est aussi
une membre inscrite », et `DT-10` fait de l'annuaire unique son argument central. Or le
modèle en crée deux : deux e-mails à tenir à jour, deux cycles de vie, et une suppression
`ENF-12` qui n'en efface qu'une — la ligne `User` survivrait avec son e-mail, ce qui n'est
pas une suppression.

Il faut trancher : une seule table de personnes portant les deux rôles (bénévole via
`RG-41`, membre via la liste de recherche), ou deux tables avec une clé commune explicite
(`oid` partout) et une règle de rapprochement écrite.

### `R-06` — La suppression du compte chez le fournisseur d'identité n'est conçue nulle part

`ENF-12` (effacement en deux clics) et `ENF-13` (purge après trois ans) exigent que le
compte disparaisse **aussi d'Entra**. `QT-04` le signale comme « le point le plus facile
à rater » — et le dossier s'arrête là. Aucune conception ne le couvre.

Ce que cela implique concrètement, et qui n'est écrit nulle part :

- un appel **Microsoft Graph applicatif** (`User.ReadWrite.All` ou équivalent), donc un
  enregistrement d'application supplémentaire, avec un secret ou un certificat, et son
  renouvellement ;
- ce droit doit être porté par **l'API** (`DELETE /catalog/me`, immédiat) **et** par le
  **worker** (purge `ENF-13`) ;
- cela contredit directement `QT-04`, qui affirme que l'exposition à l'authentification
  M2M facturée est « nulle aujourd'hui : le worker attaque SQL en direct ». Dès qu'un
  composant s'authentifie sans utilisateur pour appeler Graph, l'affirmation est à
  réexaminer — au minimum à vérifier, ce que `QT-04` invitait justement à faire ;
- l'ordre des opérations compte : supprimer d'abord chez nous puis échouer chez eux
  laisse une identité orpheline qui peut se reconnecter ; l'inverse laisse des données
  sans propriétaire. Il faut une règle et une reprise sur échec.

### `R-08` — La clé d'idempotence du scan n'a pas de place en base

> ✅ **Traité.** Colonne `ClientGestureId` sur `BookMovements` en
> [`02`](02-modele-de-donnees.md) §2, index unique filtré, recopiée sur la ligne d'annonce
> que le geste engendre.

`03` §4 et `04` §4 exigent tous les deux un identifiant produit par le client et un
endpoint de lot idempotent — c'est ce qui permet de retransmettre la file de sortie sans
raisonner. Mais `02` ne stocke cet identifiant **nulle part**.

`BookMovements.Id` est un `uniqueidentifier` dont rien ne dit qu'il vaut l'identifiant
client. Et il ne peut pas le valoir : un geste « gardé en mode `PROCHAINE BOURSE` »
produit **deux lignes** — un mouvement et une annonce — plus une mise à jour de la fiche
et des compteurs de session. Un seul identifiant client ne peut pas être la clé primaire
de deux tables.

Il faut soit une colonne dédiée (`ClientGestureId`) avec index unique sur `BookMovements`
et report sur `BookAnnouncements`, soit une table `ReceivedGestures` qui porte la
déduplication. À défaut, la première retransmission après coupure double les mouvements —
c'est-à-dire fausse les quantités, silencieusement, le jour d'une bourse.

### `R-11` — `RG-19` et la file de sortie se contredisent

`04` §3 est explicite : le geste est écrit dans l'`outbox` **au moment du scan**, avant
toute tentative d'envoi, et « il n'existe pas de branche en ligne ». `RG-19` est tout
aussi explicite : en mode tri, c'est **le scan suivant** qui vaut « garder » pour le
précédent. Et `RG-17`/`RG-18` permettent d'annuler des gestes de la session en cours.

Les deux ne peuvent pas être vrais en même temps. Ou bien la file contient des gestes
dont la décision n'est pas encore prise — et il faut dire ce qu'on y écrit, à quel moment
on la complète, et ce que voit le compteur de `ENF-07` ; ou bien l'écriture attend la
décision, et `04` §3 est faux sur le point qu'il présente comme central.

Il manque aussi la mécanique de l'annulation locale : retirer une entrée déjà dans la
file mais pas encore transmise n'est pas la même opération que produire un mouvement
inverse côté serveur. Le document ne distingue pas les deux.

---

## 🟠 Sérieux

### `R-05` — Aucun fournisseur d'e-mail n'est choisi, et c'est un préalable à délai externe

> ✅ **Traité par [`DT-12`](01-decisions.md).** Azure Communication Services Email, sur le
> sous-domaine d'envoi dédié `mail.volepapillondamour.fr` — imposé par la contrainte SPF
> exacte d'ACS, et bonne pratique par ailleurs. Remonté au **préalable**, avec le locataire
> d'identité, pour laisser chauffer la réputation d'envoi.

`07` §7 décrit le contenu du message, le regroupement par membre, le traitement des
rebonds — jamais **qui envoie**. Or ce choix commande, en cascade :

- une ressource Azure de plus (Azure Communication Services Email) ou un service tiers,
  donc du Bicep, un secret, un coût récurrent ;
- **un domaine d'envoi à vérifier** : SPF, DKIM, DMARC, donc des enregistrements DNS sur
  `volepapillondamour.fr` et un délai de propagation et de validation qui ne se compresse
  pas ;
- le format exact du rappel de rebond de `RG-31`, qui n'a rien de standard et varie d'un
  fournisseur à l'autre — c'est lui qui détermine la forme de l'endpoint décrit en
  `03` §5 ;
- la réputation d'envoi, qui se construit sur des semaines et qu'un premier envoi en
  masse depuis un domaine neuf abîme durablement.

C'est un préalable de la même nature que le locataire d'identité : ça ne se décide pas la
veille du palier 3. À traiter comme tel dans l'ordre de déploiement de `08` §9.

### `R-07` — Aucun nom de domaine, aucun certificat, aucune stratégie d'URL

> ✅ **Traité par [`DT-13`](01-decisions.md).** `livres.volepapillondamour.fr` avec
> certificat managé gratuit, fiche en `/livres/{slug}-{isbn13}`, page d'œuvre canonique.
> Le chemin sur le domaine principal est écarté pour 35 $/mois d'Azure Front Door, et se
> rouvre le jour où un CDN se justifie par ailleurs. **L'accès à la zone DNS n'est pas un
> obstacle** : l'association détient le domaine.

L'association est sur `volepapillondamour.fr` (`src/Website/public/robots.txt` et
`sitemap.xml`). Le catalogue arrive sur une nouvelle Container App, donc par défaut sur
un `*.azurecontainerapps.io`. `ENF-09` fait pourtant du référencement « le principal
canal d'acquisition gratuit de l'association ».

Rien dans `05` ni dans `08` ne traite : le choix entre un sous-domaine
(`livres.volepapillondamour.fr`) et un chemin sur le site existant, la déclaration du
domaine personnalisé et du certificat managé sur la Container App, ni le fait que ce
choix doit être fait **avant** l'indexation — une URL qui change après coup coûte des
mois d'autorité.

### `R-09` — Le référencement s'arrête au rendu serveur

> 🟡 **Partiellement traité.** `05` §1 liste désormais ce qui manque au-delà du SSR, et
> `DT-13` fixe la forme des URL et la page d'œuvre canonique. **Reste ouvert** : le
> traitement des fiches épuisées que `RG-26` maintient au catalogue — canonisation vers
> l'œuvre ou `noindex` sous un seuil de contenu.

`05` §1 défend correctement le SSR, puis s'arrête. Pour l'exigence présentée comme la
plus rentable du projet, il manque tout le reste : sitemap dynamique pour ~15 000 fiches
(le sitemap actuel est un fichier statique de routes en dur), URL canoniques, `robots.txt`
de la nouvelle application, données structurées `schema.org/Book`, titres et
métadescriptions dérivés de la fiche.

Et surtout une règle sur les fiches épuisées : `RG-26` les garde au catalogue, ce qui
produit des milliers de pages à contenu très mince — le profil exact que les moteurs
déclassent, et qui peut entraîner le reste du site avec lui. À trancher : `noindex` en
dessous d'un seuil de contenu, ou regroupement par œuvre.

### `R-10` — Le fuseau horaire n'apparaît nulle part

`RG-23` bascule les annonces « à la date d'ouverture de la bourse ». `AssoEvents` porte
des `DateTimeOffset` ; les nouvelles tables de `02` §2 des `datetime2` ; la relève
d'outbox de `06` §4 du `SYSUTCDATETIME()`.

Sans règle explicite, une bourse qui ouvre à 9 h bascule à 10 h en heure d'été — ou la
veille au soir, selon le sens de l'erreur. Le même flou touche `RG-43` (deux heures
d'inactivité), `RG-30` (trente jours glissants), le regroupement des statistiques « par
jour » de `05`, et l'horloge cliente de `04` §4 point 3.

Il faut une phrase, et une seule : tout est stocké en UTC, les comparaisons de calendrier
se font en heure locale `Europe/Paris`, et voici où la conversion a lieu.

### `R-12` — La fusion de fiches contredit le mouvement en ajout seul

`RG-07` permet de fusionner deux fiches désignant la même édition, en additionnant
mouvements et quantités. Or `Books` a pour clé primaire **l'ISBN-13** et
`BookMovements.Isbn13` en est une clé étrangère. Fusionner impose donc de réécrire les
mouvements de la fiche absorbée — dans une table dont `02` §2 affirme : « on n'y met
jamais à jour, on n'y supprime jamais ».

Deux issues, et c'est un choix de modèle, pas un détail d'implémentation : une clé de
substitution sur `Books` avec l'ISBN en clé alternative (les mouvements pointent alors
vers un identifiant stable qui survit à la fusion), ou une fiche marquée comme redirigée
vers sa fiche canonique, les mouvements restant en place. La même question vaut pour les
entrées de liste de recherche qui pointent l'ISBN absorbé.

### `R-13` — Le filigrane de synchronisation ne couvre pas la projection embarquée

`04` §2 fonde le delta sur `Books.UpdatedAt`. Mais deux des neuf champs de la projection
`catalog` n'appartiennent pas à `Books` :

- `qtyAnnounced` est un agrégat de `BookAnnouncements` — `02` §2 le dit explicitement,
  « `QuantityAnnounced` n'est pas ici » ;
- `isWanted` dérive de `MemberWatchlistItems`, table qui n'a délibérément aucune relation
  avec `Books`.

Un membre qui ajoute un livre à sa liste, ou une bascule qui ne touche que la ligne
d'annonce, ne remonteraient donc **jamais** aux appareils. Le signal « recherché » —
l'objectif `O3` — n'arriverait pas au bénévole, silencieusement.

Il faut soit poser comme invariant que toute écriture modifiant la projection touche
`Books.UpdatedAt`, et l'écrire comme une contrainte de conception au même titre que les
trois de `06` §2 ; soit un filigrane propre à la projection.

### `R-14` — La projection embarquée ne transporte pas le décompte des demandeurs

`04` §2 embarque `isWanted`, un booléen. `RG-13` exige que « le nombre de demandeurs
[soit] affiché ». `RG-42` interdit l'identité, pas le décompte — et `04` §2 le dit
lui-même en passant (« jamais l'identité des demandeurs »).

### `R-15` — Tranches manquantes dans le découpage backend

`03` §2 se présente comme le découpage à créer. Sept fonctions du fonctionnel n'y ont
aucune commande, alors qu'elles sont décrites ailleurs dans le même dossier :

| Manque | Exigé par |
|---|---|
| Correction manuelle des métadonnées | `RG-05` — alors que `Books.ManuallyEditedFields` est prévue pour elle |
| Suppression d'une fiche | `RG-06` |
| Rattachement manuel d'une annonce sans date | `RG-24`, écran `05` §4 |
| Annulation / envoi forcé des alertes en attente | `RG-45`, écran `05` §4 bis — c'est l'argument central de `DT-03` |
| Rappel de rebond e-mail | `RG-31`, mentionné en `03` §5 mais sans tranche |
| Lecture et écriture des paramètres | `ENF-25`, voir `R-02` |
| Correction unitaire de quantité tracée | `RG-34`, exigible dès le palier 1 même si l'écran de remise à plat est reporté |

### `R-16` — `RG-33` n'a pas de critère : qu'est-ce qu'une « bourse ouverte » ?

`RG-33` rattache toute vente « à la session de bourse ouverte au moment du scan ».
`AssoEvents` porte `DateStart` (obligatoire), `DateEnd`, `HourOpenDoors` et
`HourCloseDoors`, tous trois nullables. Aucun document ne dit lesquels font foi, ni ce
qui se passe si deux bourses se chevauchent, ni si `DateEnd` est nul.

C'est le rattachement de toutes les statistiques par bourse (`05` §2) et de la recette de
`RG-51`. Une règle floue ici produit des chiffres faux dont personne ne saura d'où ils
viennent.

### `R-17` — `ENF-14` entre en conflit avec ce que fait déjà le `Website`

`ENF-14` interdit tout traceur et impose une mesure d'audience fonctionnant sans
consentement — donc sans bandeau. Or le `Website` existant embarque **GA4**, injecté au
build par `website-deploy.yml` depuis la variable `GOOGLE_ANALYTICS_MEASUREMENT_ID`.

Le dossier technique ne le dit pas. Le réflexe naturel, en créant une troisième
application Angular à partir des deux existantes, sera de reprendre la même configuration
— et de mettre l'association en défaut sur sa propre exigence. À écrire noir sur blanc
dans `05` §6, avec le nom de l'alternative retenue.

### `R-18` — La migration de l'authentification casse l'application de caisse installée

`10` §6 liste correctement ce qui disparaît, `MauiCashApp` compris. Deux conséquences ne
sont traitées nulle part :

**La distribution.** Le jour où `/auth/login` disparaît de l'API, l'application installée
sur les appareils de caisse **cesse de fonctionner**, jusqu'à ce qu'une nouvelle version
soit construite et distribuée sur chaque appareil. Le critère de passage du préalable
d'identité (`01` §7 : « un administrateur se connecte au `BackOffice` par Entra et plus
aucun mot de passe en base ») ne mentionne ni la caisse ni sa redistribution. C'est
pourtant le seul composant du système qui ne se met pas à jour par un déploiement.

**Les URI de redirection par plateforme.** `ShopAppVpd.csproj` cible Android, iOS,
MacCatalyst et Windows. MSAL.NET y exige des redirections propres à chaque plateforme
(`msal<clientId>://auth`, filtre d'intention Android, droits de trousseau iOS). Or
`Configure-EntraApps.ps1` n'enregistre que `http://localhost` pour `vpd-caisse` — ce qui
ne fonctionne que sur bureau.

---

## 🟡 Faits erronés ou périmés

### `R-19` — « Aucun fichier de CI n'existe » est faux

`08` §5 fonde tout son chapitre sur cette phrase, reprise de
`.github/memory/09-auth-and-build.md:51` (« No CI pipeline file was detected »), qui est
**périmée**. Le dépôt contient **sept workflows** :

```
api-deploy.yml   backoffice-deploy.yml   website-deploy.yml   infra-deploy.yml
db-import.yml    storage-migrate.yml     local-snapshot.yml
```

Conséquences sur le chapitre :

- ses points 2 et 3 (construire et publier les images, mettre à jour les révisions) sont
  **déjà faits** pour les trois applications existantes ;
- son point 4 (« appliquer les migrations en étape explicite, pas au démarrage ») est
  **déjà fait** : `api-deploy.yml` porte une entrée `run_migrations`, ouvre le pare-feu
  SQL pour le runner et le referme dans un `if: always()` ;
- il reste donc **un seul vrai manque**, et le chapitre ne le nomme pas : tous les
  workflows sont en `workflow_dispatch` **manuel uniquement** (« Manual only: the pipeline
  never fires on a push »). Il n'existe **aucune compilation ni exécution de tests au
  push**. C'est le point 1, et c'est le seul à écrire.

À corriger aussi dans la mémoire projet, sans quoi l'erreur se propagera encore.

### `R-20` — Les applications Angular sont en version 21, pas 18

`00` §2 et `DT-08` parlent d'« Angular 18 ». `Website` et `BackOffice` sont en
`@angular/core: ^21.2.12`, TypeScript 5.9 — ce que dit d'ailleurs correctement
`.github/memory/04-frontend.md`. Sans conséquence de conception, mais un lecteur qui
dimensionne la réutilisation de `SharedUi` doit lire le bon chiffre.

### `R-21` — « Le module `ContainerApp` se réutilise tel quel » est faux pour le worker

`08` §2 affirme : « Le module `ContainerApp` existant se réutilise tel quel. Pour le
worker, le delta est `kind: functionapp` ». Vérification faite dans
`infra/modules/ContainerApp/containerApp.module.bicep`, trois obstacles :

1. le module n'expose **aucun paramètre `kind`** — c'est une propriété de premier niveau
   de la ressource, pas un champ de `properties` ;
2. sa sortie est
   `output containerAppFqdn = containerApp.properties.configuration.ingress.fqdn`, qui
   **échoue au déploiement si l'ingress est désactivé**. Le module ne sait pas déployer
   une application sans ingress ;
3. `ScalingConfig` ne porte que `minReplicas` et `maxReplicas` : ni règle d'échelle KEDA,
   ni `AzureWebJobsStorage`, ni configuration de runtime Functions.

À quoi s'ajoute une vérification à faire, pas à supposer : que
`Microsoft.App/containerApps@2024-03-01` — la version figée dans le module — accepte bien
`kind`.

### `R-22` — L'ajout de trois applications dans `main.bicep` n'est pas un ajout de trois lignes

`main.bicep` ne comporte **aucune boucle** : chaque application y est déclarée en clair.
Pour une application, cela représente quatre paramètres typés, une identité managée, un
Application Insights, deux attributions de rôle, le module lui-même et deux sorties. Trois
applications de plus, c'est de l'ordre de **douze nouveaux paramètres et deux cents lignes
de Bicep** — sans compter le jeu de paramètres.

Ce n'est pas un problème en soi, mais `08` §2 laisse entendre l'inverse, et le palier
d'infrastructure en est sous-estimé d'autant. Une alternative existe — passer les
applications en boucle sur un tableau typé — qui vaut d'être pesée maintenant plutôt
qu'à la sixième application.

---

## ⚪ Mineurs

| # | Constat |
|---|---|
| `R-23` | Aucune sonde de santé n'est configurée (`main.dev.bicepparam` : chemins vides, ports à 0) et l'API n'expose pas de point `/health`. Une révision qui démarre mal prend quand même du trafic. À régler **avant** d'ajouter trois applications, pas après. |
| `R-24` | Le conteneur blob des couvertures n'existe pas : `main.bicep` en déclare quatre, tous en accès public `Blob`. Petit delta, mais réel — paramètre, conteneur, variable d'environnement. Et à décider : accès public comme les autres, ou servi par l'API. |
| `R-25` | Le worker aura besoin de `Key Vault Secrets User` (chaîne SQL) et de sa propre identité managée. `main.bicep` ne l'accorde qu'à l'API — `08` §3 dit « rien de nouveau », ce qui est vrai en nature, faux en travail. |
| `R-26` | `08` §7 chiffre les réplicas permanents mais oublie trois postes : la base (`R-01`), l'ingestion Log Analytics qui croît avec trois applications de plus, et l'envoi d'e-mails (`R-05`). |
| `R-27` | `DT-04` s'appuie sur « .NET Aspire a une intégration Azure Functions » pour préserver le montage local. `AppHost` est désormais en Aspire 13.5.3, mais ne référence toujours pas `Aspire.Hosting.Azure.Functions`. À vérifier avant de s'en prévaloir : que le paquet existe à cette version, et qu'il supporte le worker isolé **.NET 10** — de même que l'image de base Functions correspondante. **`DT-15` ne referme pas ce point**, il en fait le premier essai à tenter, sur un socle à jour. |
| `R-28` | `02` §6 propose une migration par palier, mais oublie celle du **préalable d'identité** : suppression de `Password`, `Salt`, `Role`, ajout de `ExternalId`. Elle vient avant les trois listées. |
| `R-29` | `infra/entra/README.md` donne en exemple `-CatalogRedirectUri 'https://vpd-web-ca-dev...'` — c'est la Container App du `Website` existant, pas celle du futur catalogue. Et `Configure-EntraApps.ps1` **remplace** la liste des URI (`RedirectUris = @($client.Uri)`) : impossible d'avoir `localhost` et la production sur le même enregistrement. Cela se verra au premier développement local après un passage en production. |
| `R-30` | Le CORS de l'API est `AllowAnyOrigin` (`Program.cs`). Acceptable avec des jetons porteurs, mais deux applications de plus en font le bon moment pour passer à une liste blanche. |

---

## Ce qui manque au dossier en tant que dossier

Trois sujets n'ont pas de chapitre, et ne relèvent d'aucun constat ci-dessus parce
qu'ils sont absents en entier.

**Le repli d'exploitation.** ✅ **Tranché — il n'y en aura pas.** `ENF-21` est réécrit :
en cas de panne, on vend sans enregistrer, et rien n'est rattrapé. Le constat ci-dessous
reste pour mémoire, parce qu'il explique ce que cette décision accepte. Ce qu'elle déplace
est traité en `P1-10` du [`lot 2`](../plan/02-palier-1-socle-interne.md) : le hors-ligne de
la caisse devient la seule protection restante, et se teste comme telle. Le constat
d'origine : `ENF-21` — « une indisponibilité ne doit jamais empêcher de vendre » —
est présenté en `07` §8 comme « le critère qui prime sur tout le reste de ce document », et
aucune procédure n'est décrite. Que fait le caissier si son appareil tombe en panne un jour
de bourse ? Une feuille de papier, puis quelle saisie, par quel écran, rattachée à quoi ?
`ENF-04` (autonomie de l'appareil) est dans le même cas. **C'est une décision
d'organisation, pas de conception** : elle appartient à l'association, et l'étape ne fait
que lui donner une date limite.

**L'alerte sur les mesures d'observabilité.** ✅ **Traité** par
[`11-observabilite.md`](11-observabilite.md) et `DT-16`. Le chapitre pose les mesures et
leurs alertes (§4, §8), la corrélation à travers la frontière hors ligne (§3),
l'échantillonnage (§5), ce qu'on ne journalise jamais (§6) et les garde-fous de coût. Deux
apports que la revue n'avait pas vus : un **journal est une donnée personnelle**, donc
`RG-42` et `ENF-12` s'y appliquent — une adresse écrite dans Log Analytics survit à la
suppression du compte ; et le **désaccord de verdict entre client et serveur** est une
mesure gratuite qui donne la péremption réelle de la copie embarquée.

**La stratégie de test au-delà du backend.** ✅ **A trouvé une place** — étape `P1-2` du
[`lot 2`](../plan/02-palier-1-socle-interne.md), avant l'application de scan et non après.
Le manque, lui, est intact : `03` §6 traite bien les tests backend et leur ordre de valeur,
mais rien sur les fronts — ni test du mode hors ligne (le chemin le plus critique et le
plus difficile à éprouver à la main), ni test de la file de sortie et de sa survie à une
fermeture, ni jeu de données de démonstration permettant de rejouer une session de tri.
`npm test` échoue par ailleurs côté `BackOffice`, qui ne contient **aucun** fichier de test
quand le `Website` en compte dix-neuf.

---

## Suite

Les trois constats qui changeaient l'ordonnancement — `R-01`, `R-05`, `R-07` — **sont
tranchés**, par `DT-11`, `DT-12` et `DT-13`. Le plan d'implémentation peut donc s'écrire.

Ce qu'ils changent dans les paliers :

- le **préalable** accueille désormais deux chantiers à délai externe au lieu d'un : le
  locataire d'identité **et** l'envoi d'e-mails, plus les enregistrements DNS des deux
  décisions ;
- le **palier 1** commence par le passage de la base en `S1`, avant le worker ;
- le **palier 2** porte le domaine du catalogue et son certificat.

**Les quatre constats qui touchaient le modèle de données sont écrits** — `R-02`, `R-03`,
`R-08` dans `02` §2, et `R-04` par `DT-14`. Le modèle est complet ; le plan
d'implémentation peut s'appuyer dessus sans réserve.

Deux des trois manques structurels sont comblés depuis :
[`11-observabilite.md`](11-observabilite.md) traite l'alerte et le débogage, et `DT-15`
unifie le socle d'exécution. Les deux autres sont réglés depuis : le repli
d'exploitation **n'existera pas** — `ENF-21` est réécrit, une panne fait vendre sans
enregistrer et rien n'est rattrapé, `P1-10` en tire la conséquence sur le hors-ligne — et la
stratégie de test des fronts a une étape, `P1-2`, où elle reste à écrire.

**Combien de constats restent ouverts, et lesquels.** Une version antérieure de cette
section annonçait « onze constats ouverts ». Le chiffre ne comptait que les bloquants et les
sérieux : les quatre factuels et les huit mineurs sont ouverts eux aussi. Le compte exact
est **vingt-trois**, dont vingt-et-un sont portés par une étape du plan — voir le tableau
« Où en est chaque constat ouvert » plus haut. **Aucun ne conditionnait l'écriture du plan.**

Deux échappent encore à toute étape et ce sont les seuls à surveiller :

- **`R-13` et `R-14`** — le filigrane de synchronisation qui ne couvre pas la projection
  embarquée, et cette projection qui ne transporte pas le décompte des demandeurs. Les deux
  relèvent de `P1-5` sans y être nommés ; ils se règlent en écrivant le delta, à condition
  d'y penser à ce moment-là.

Trois méritent par ailleurs d'être traités tôt, parce qu'ils sont bon marché maintenant et
coûteux plus tard :

- `R-06`, la suppression du compte dans le locataire — même chantier que `DT-14`, et
  `ENF-12` n'est pas tenue sans lui. ✅ **Suivi** : le plan l'a ramené en `L0-11` ;
- `R-10`, le fuseau horaire — une phrase à écrire, un défaut sournois à ne pas laisser
  s'installer dans le code ;
- `R-23`, les sondes de santé, **avant** d'ajouter trois applications plutôt qu'après.
