# Lot 2 — Palier 1, le socle interne

**Aucune exposition publique.** Fiches, quantités disponible et annoncée, tri avec ses deux
modes, bascule automatique, scan de vente, statistiques minimales.

**Critère de confiance.** La répétition générale de `P1-11` — une centaine de livres triés,
vendus, comptés, seul — se termine avec un écart **nul** entre le stock théorique et le
comptage physique. Nul, et pas « dans une marge acceptable » : l'exercice se fait par une
seule personne attentive, donc tout écart y est un défaut du logiciel.

La marge acceptable, elle, appartient à l'usage réel : le jour où de vrais caissiers
s'en servent une vraie journée de bourse, l'écart mesurera la **discipline de scan** — le
principal risque humain du projet — et ce n'est pas quelque chose qu'on peut éprouver
d'avance, seul.

**Prérequis bloquant.** `QT-02` doit être mesurée avant de construire le worker. Un échec
y est silencieux : les alertes ne partent jamais.

---

## `P1-1` — Mesurer `QT-02`, avant tout le reste

🧪 Déployer une fonction planifiée triviale avec `minReplicas: 0`, **ne pas y toucher
pendant deux heures**, vérifier dans les journaux qu'elle s'est exécutée aux échéances
attendues.

| Résultat | Suite |
|---|---|
| Le réveil fonctionne | `minReplicas: 0`, coût négligeable. On continue comme prévu |
| Le réveil ne fonctionne pas | `minReplicas: 1` — un conteneur permanent de plus, ~10 €/mois — ou temporisation par file Azure Queue Storage |
| Le worker devient un sixième conteneur permanent | **`DT-09` se rouvre légitimement** : dissoudre le worker dans l'API redevient le bon choix |

Vérifier au passage `revue.md` `R-27` : que l'intégration Azure Functions d'Aspire existe à
la version retenue et supporte un worker isolé `.NET 10`, et qu'une image de base
correspondante est disponible. `DT-04` s'en prévaut sans que le paquet soit référencé
aujourd'hui.

📌 Le résultat, et la variante retenue. Il commande la suite du lot.

**Trente minutes de mesure contre un mode de panne silencieux.** C'est le meilleur rapport
du projet.

## `P1-2` — Trancher les points de conception restants

🔧 Quatre constats de `revue.md` doivent être écrits **avant** le code qu'ils concernent.
Ce sont des tables et des règles, pas des ajustements.

| Constat | À trancher |
|---|---|
| `R-10` | **Le fuseau horaire.** Une phrase : tout en UTC, comparaisons de calendrier en `Europe/Paris`, et où la conversion a lieu. Sans elle, `RG-23` bascule à la mauvaise heure en été |
| `R-11` | **`RG-19` contre la file de sortie.** Ce qui est écrit au scan, ce qui l'est à la décision, comment une annulation retire une entrée non transmise |
| `R-12` | **La fusion de fiches** (`RG-07`) contre le mouvement en ajout seul : clé de substitution, ou fiche redirigée |
| `R-16` | **Ce qu'est une « bourse ouverte »** (`RG-33`) : quels champs d'`AssoEvents` font foi, et le cas du chevauchement |

🔧 **Et un cinquième point, que la revue classait comme un manque du dossier entier plutôt
que comme un constat : la stratégie de test des fronts.** Il se tranche ici parce qu'il
conditionne `P1-5`, pas après lui. `T-03` §6 traite le backend et son ordre de valeur ;
rien n'existe sur :

| Sujet | Pourquoi il ne peut pas rester à la main |
|---|---|
| **Le mode hors ligne** | C'est le chemin le plus critique et le plus coûteux à éprouver à la main. Le rejouer à chaque livraison en coupant vraiment le réseau n'est pas tenable |
| **La survie de la file de sortie** | Une régression y est silencieuse et se paie en heures de tri bénévole perdues |
| **Un jeu de données de démonstration** | Sans lui, on ne rejoue pas une session de tri ; on la refait |

À décider : quel outil, et surtout **quel niveau** — un test de bout en bout par navigateur
piloté, ou des tests de la couche de synchronisation avec un réseau simulé. Le second est
beaucoup moins cher et couvre l'essentiel du risque.

📌 Les décisions, en `DT-nn` dans `technique/01-decisions.md`.

## `P1-3` — Le domaine et la persistance

🔧 Les quatre agrégats de `T-02` §1, leurs configurations EF Core, la migration 1 —
`Books`, `BookMovements`, `ScanSessions`, `BookAnnouncements`, `AssociationSettings`.
C'est la migration 1 de `T-02` §6 ; la migration 0 est celle du préalable d'identité,
faite en `L0-11`.

Points où l'implémentation dérape :

- **Identifiants fortement typés**, en reprenant les conversions déjà en place.
- **`RowVersion` sur `Books`** : deux scanettes incrémentent la même fiche.
- **Collation insensible aux accents** sur `Title` et `Authors`, sans quoi la recherche est
  jugée inutilisable au premier essai. **Dès la migration 1**, contrairement à ce
  qu'annonçait `T-02` §6, qui la reportait avec l'index plein texte au palier 2 : l'index
  peut attendre, la collation non — la changer plus tard, sur des colonnes remplies et
  indexées, est un tout autre chantier. `T-02` §6 est corrigé en ce sens.
- **Index unique filtré sur `ClientGestureId`** — c'est lui qui rend la retransmission sûre.
- **Unité de travail explicite** (`DT-06`) : ne pas passer par le `BaseRepository`, qui
  enregistre à chaque opération.

✅ Tests de domaine, dans l'ordre de valeur de `T-03` §6 : `RG-15` (la table de priorité
des verdicts — peu de code, beaucoup de cas, une erreur invisible en production), puis
`RG-10` (le comptage doit inclure les annonces), puis `RG-01`.

## `P1-4` — Les handlers, et les trois qui comptent

🔧 Les tranches de `T-03` §2, **plus les sept oubliées** relevées en `revue.md` `R-15` :
correction manuelle des métadonnées (`RG-05`), suppression de fiche (`RG-06`), rattachement
d'une annonce sans date (`RG-24`), annulation et envoi forcé des alertes (`RG-45`), rappel
de rebond (`RG-31`), lecture et écriture des paramètres (`ENF-25`), correction unitaire de
quantité (`RG-34`).

Trois concentrent la difficulté :

- **`ScanBook`** — le chemin le plus chaud. La résolution des métadonnées se déclenche
  **hors transaction et hors réponse** : un appel externe ne doit jamais retarder la
  réponse ni faire échouer l'écriture.
- **`CloseScanSession`** — **idempotent**, appelé depuis quatre origines. Une session déjà
  `Terminee` ne produit rien.
- **`ReassignSessionMode`** — inversion et rejeu, avec et sans alertes déjà parties.

✅ Tests sur l'idempotence des quatre causes de clôture, et sur la reprise dans les deux
cas d'échéance.

## `P1-5` — L'application de scan

🔧 La PWA de `T-04`, cette fois complète : IndexedDB à quatre magasins (`catalog`,
`outbox`, `sales`, `session`), file de sortie durable, verdict calculé localement,
synchronisation delta, service worker.

Les points qui font ou défont l'outil :

- **Demander explicitement le stockage persistant, et vérifier que c'est accordé.** Un
  navigateur peut supprimer IndexedDB. Perdre `catalog` est indolore ; perdre `outbox`,
  c'est perdre des heures de tri bénévole.
- **Ne jamais confondre `catalog` et `outbox`.** Une purge « pour repartir propre » qui
  viderait les deux serait un incident majeur.
- **Jeter les réponses tardives** : chaque réponse porte l'ISBN demandé et n'est appliquée
  que si l'écran affiche encore ce livre. Sans ce garde-fou, on obtient le titre du livre
  précédent sur le livre courant.
- **Le bandeau de mode ne disparaît jamais** — ni au défilement, ni pendant un chargement.
  C'est la seule protection contre une session entière dans le mauvais mode.
- **Réserver la place du titre** par un gabarit de hauteur fixe, pour que l'arrivée des
  métadonnées ne pousse pas le verdict vers le bas.
- **Les deux constats de la synchronisation delta**, que la revue avait relevés et que le
  plan n'attribuait à aucune étape : le filigrane ne couvre pas la projection embarquée
  (`revue.md` `R-13`) — une fiche modifiée côté serveur sans mouvement ne redescend jamais
  —, et cette projection ne transporte pas le décompte des demandeurs (`R-14`), sans quoi
  le signal « recherché » du palier 3 ne peut pas s'afficher hors ligne. Les deux se règlent
  en écrivant le delta ; découverts après, ils se paient en migration de schéma embarqué.

🧪 Le test qui compte, et il ne se fait pas au bureau : **une session de tri complète en
mode avion**, puis retour du réseau. Attendu : les gestes partent, rien n'est perdu, rien
n'est doublé. **Puis recommencer en coupant le réseau en pleine transmission** — c'est le
cas que `ClientGestureId` existe pour couvrir.

🧪 Fermer l'application en pleine session, batterie retirée si possible. Attendu : la file
survit.

🧪 **La moitié de `QT-08` qui n'était pas mesurable en `L0-12`** : après quarante-huit
heures sans y toucher, rouvrir l'application **en mode avion** et vérifier que le geste de
scan reste possible et que l'identité du bénévole est toujours connue de l'appareil. La
durée de vie du jeton, elle, a déjà été mesurée au lot 0 ; ce qui se vérifie ici, c'est que
l'application s'en accommode.

## `P1-6` — Le worker

🔧 Second hôte au-dessus des mêmes bibliothèques. Deux déclencheurs seulement — `sweep`
toutes les cinq minutes, `enrich` horaire — et non neuf.

Les trois contraintes de `T-06` §2 sont des pièges réels : aucun handler ne dépend d'un
utilisateur ambiant, une portée d'injection par exécution, et **le worker n'écrit jamais un
`UPDATE` direct sur les quantités** — il court-circuiterait `RG-35`.

🧪 **Avancer l'horloge, pas attendre.** Créer une annonce sur une bourse dont la date est
dans deux minutes, et vérifier que la bascule se fait sans geste humain. Puis créer une
annonce sur une bourse **déjà passée**, et vérifier le rattrapage de `RG-38`.

## `P1-7` — L'observabilité, en même temps

🔧 `DT-16`. Les mesures de `T-11` §4 et leurs règles d'alerte, en Bicep. **Plafond journalier
sur chaque Application Insights, dès la création.**

Au minimum, avant la première bourse : l'âge du plus vieux message échu, le battement de
cœur du worker, les annonces en retard de bascule.

🧪 **Arrêter le worker volontairement pendant une heure**, et vérifier que l'alerte part et
arrive dans la bonne boîte. Une alerte non testée est une supposition.

📌 Les règles d'alerte actives et l'adresse de destination.

## `P1-8` — Infrastructure et pipelines

🔧 Trois Container Apps de plus — `scan`, `worker`, et `catalog` au lot suivant —, chacune
avec son identité managée, son Application Insights et ses attributions de rôle. **Le
worker a besoin de `Key Vault Secrets User`**, que `main.bicep` n'accorde aujourd'hui qu'à
l'API.

Deux obstacles connus (`revue.md` `R-21`, `R-22`) :

- Le module `ContainerApp` **ne sait pas déployer le worker en l'état** : pas de paramètre
  `kind`, et sa sortie `containerAppFqdn` échoue si l'ingress est désactivé. À corriger
  avant, pas pendant.
- `main.bicep` n'a **aucune boucle**. C'est le moment de peser un passage en tableau typé,
  plutôt qu'à la sixième application.

Ajouter le conteneur blob des couvertures, absent des quatre déclarés (`revue.md` `R-24`)
— et **trancher au passage comment il est servi** : accès public `Blob` comme les quatre
autres, ou passage par l'API. Les quatre existants sont publics ; reprendre ce choix par
défaut est défendable pour des couvertures de livres, mais c'est un choix, pas une
reconduction automatique. Le décider ici évite d'avoir à le changer une fois les URL
publiées dans le catalogue.

**Resserrer le CORS** (`revue.md` `R-30`). L'API est en `AllowAnyOrigin` dans
`Program.cs`. C'est acceptable avec des jetons porteurs, et c'est exactement le moment que
la revue désigne : on passe de trois à cinq applications, donc à une liste blanche
d'origines paramétrée par environnement. Après, on ne le fera plus.

🔧 Pipelines : `scan-deploy.yml` et `worker-deploy.yml` sur le modèle existant. **L'API et
le worker se construisent et se déploient depuis le même commit** (`T-06` §2) — deux images,
une seule étiquette de version. C'est une contrainte à écrire dans le workflow, pas à
retenir.

🧪 Déployer, puis vérifier que la révision précédente est bien remplacée et que l'ancienne
ne reçoit plus de trafic.

## État d'exécution — 2026-09-05

Le code des étapes `P1-6` à `P1-8` est implémenté et déployé : le Worker expose `Sweep` et
`Enrich`, l'instrumentation et les alertes App Insights sont déclarées, et les pipelines
construisent API et Worker depuis un tag partagé. La reprise de l'enrichissement ajoute un
cooldown d'une heure sur les pannes transitoires et un ordre équitable des candidats ; elle
est couverte par les tests backend. La PR #61 ajoute le lookup provider-neutral de la file de
suppression et le nettoyage transactionnel des projections membre avant suppression ou
anonymisation. L'ensemble est déployé par `Books runtime - deploy` `33929828651`, avec le tag
partagé `dcc0c23` et sans nouvelle migration.
Les étapes `P1-9`, `P1-10` et `P1-11` restent des campagnes manuelles, non remplaçables par un
smoke HTTP.

## `P1-9` — Mesurer `QT-09`

🧪 Sur un catalogue de quelques milliers de fiches, relever trois temps : la recherche
plein texte, la requête de désengorgement de `F-05` §5, l'écriture d'un lot de scans en
transaction. Refaire à quinze mille fiches.

📌 Les temps. `S2` est à un paramètre de distance si `S1` ne tient pas sur son disque dur.

## `P1-10` — Le hors-ligne de la caisse, faute de repli

✅ **La persistance locale et la reprise réseau sont implémentées.** Le bouton `VALIDER`
crée une entrée durable dans le magasin IndexedDB `sales`, décrémente immédiatement la
projection locale, puis la synchronisation appelle `POST /scan/sales` avec un
`ClientGestureId` idempotent. La réponse serveur réconcilie le stock et le compteur de
ventes. Il n'y a toujours **aucun repli d'exploitation** (`ENF-21`, réécrit) : si
l'application ne fonctionne pas, on vend quand même et on n'enregistre rien — pas de feuille
de papier, pas d'écran de ressaisie, pas de rattrapage.

**Ce que cette décision déplace.** Elle ne supprime pas le risque, elle le concentre sur un
seul point : `ENF-05`, le fonctionnement hors ligne de la caisse, devient la **seule**
protection qui reste. Une coupure réseau dans le local ne doit pas être une panne — sinon
la décision « tant pis » se déclenche pour un motif banal, plusieurs fois par bourse.

C'est donc ici qu'on éprouve le mode dégradé de la caisse, sérieusement, plutôt que d'y
revenir après.

🧪 Trois pannes provoquées, à froid, seul :

1. **Réseau coupé** en pleine vente. Attendu : la vente se scanne, le stock se décrémente
   localement, rien ne bloque, et tout remonte au retour du réseau — sans doublon.
2. **Application fermée** en pleine session, réseau toujours coupé. Attendu : les ventes
   non transmises survivent à la réouverture. C'est le même mécanisme qu'en tri, et il se
   teste de la même façon.
3. **Batterie à plat** (`ENF-04`). Attendu : même chose. À défaut d'attendre la décharge,
   couper l'alimentation d'un coup plutôt que fermer proprement.

Si l'une des trois échoue, le corriger avant `P1-11` : c'est le seul filet restant.

📌 Ce que chaque panne a donné, et combien de ventes ont été perdues le cas échéant.

## `P1-11` — La répétition générale

🧪 **Le test le plus utile du palier, et il se fait seul, sans bourse et sans personne.**
Il ne remplace pas l'usage réel — la discipline de scan en caisse ne se mesure vraiment
qu'avec de vrais caissiers un vrai jour de bourse —, mais il attrape tout ce qui peut être
attrapé avant.

Le déroulé, sur deux ou trois heures :

| Étape | Ce qu'on fait | Ce qu'on regarde |
|---|---|---|
| 1 | Une session de tri en `DISPONIBLE MAINTENANT` sur **une centaine de livres réels**, dont des doublons volontaires | Le verdict change bien au deuxième exemplaire, la cadence tient, rien ne se perd |
| 2 | Une session en `PROCHAINE BOURSE` sur cinquante autres, sur une bourse fictive datée dans deux minutes | La bascule automatique se fait seule (`RG-23`), et l'affichage passe d'annoncé à disponible |
| 3 | Une session **en mode avion**, puis retour du réseau | Rien de perdu, rien de doublé |
| 4 | Une session de vente en caisse sur trente de ces livres, dont cinq hors ligne | Les quantités descendent, la file remonte |
| 5 | **Compter physiquement** les livres restants d'un échantillon, et comparer | L'écart doit être **nul**, puisque tout a été scanné par une seule personne attentive |

**Un écart non nul ici est un défaut du logiciel**, pas de la discipline : c'est tout
l'intérêt de le faire seul. Le jour où de vrais caissiers s'en servent, un écart apparaîtra
et il faudra pouvoir dire lequel des deux on regarde.

🧪 **Et le même exercice sur un catalogue déjà peuplé.** Rejouer l'étape 1 après avoir
chargé quinze mille fiches (`P1-9`), pour voir si le verdict reste sous la seconde
(`ENF-01`) quand la base n'est plus vide.

📌 L'écart constaté, la taille de l'échantillon, et ce qui a coincé. C'est la première
valeur d'une série qu'on suivra dans le temps.

**Ce qui ne se teste toujours pas seul, et qu'il faut savoir en attendant.** Le catalogue
démarre vide (`RG-48`) : pendant des mois, « inutile d'en garder » ne se déclenchera presque
jamais et « premier exemplaire » se déclenchera à tort sur des titres déjà en rayon. Un
bénévole non prévenu en conclut que l'outil ne marche pas. Ce n'est pas une étape du plan,
c'est une phrase à dire le jour de la première utilisation réelle.
