# 11 — Observabilité, journalisation et débogage

`DT-16` : l'instrumentation s'écrit avec la fonctionnalité, jamais après. Ce chapitre dit
quoi instrumenter, comment, et ce qu'on ne journalise jamais.

## 1. Le problème propre à ce système

**Ses pannes ne lèvent pas d'exception.** C'est la seule chose à retenir avant de lire le
reste.

| Panne | Ce que voit le code | Ce que voit l'utilisateur |
|---|---|---|
| Le worker ne s'exécute plus (`QT-02`) | Rien | Rien — jusqu'à ce qu'un membre ne soit pas prévenu |
| Une annonce ne bascule pas (`RG-23`) | Rien | Un livre annoncé qui n'arrive jamais en rayon |
| La file d'un appareil ne se vide pas | Rien côté serveur | Un bénévole qui range son téléphone, travail perdu |
| Une session reste ouverte indéfiniment | Rien | Des alertes qui ne partent jamais |
| Le `WorkId` manque (`RG-46`) | Rien | Une alerte manquée, en silence — `07` §4 |

Aucune ne produit de 500, aucune ne remonte dans un journal d'erreurs, et personne ne se
plaint : les gens ne réclament pas un e-mail qu'ils ignorent devoir recevoir. On l'apprend
une bourse plus tard, ou jamais.

**Conséquence directe** : dans ce système, l'observabilité ne sert pas d'abord à diagnostiquer
ce qui a échoué. Elle sert à **détecter ce qui ne s'est pas produit**. C'est ce qui explique
que les mesures les plus importantes du §4 soient des mesures d'absence — un âge, un
retard, une profondeur de file.

## 2. Le socle, et ce qu'on n'ajoute pas

**OpenTelemetry, exporté vers les Application Insights déjà en place.** `Azure.Monitor.OpenTelemetry.AspNetCore`
est déjà référencé par l'API et branché quand la chaîne de connexion existe. Rien à
choisir, rien à installer.

**Aucun second système.** Pas de Grafana, pas de Loki, pas de Sentry, pas d'agent tiers.
`ENF-24` : chaque brique ajoutée est une brique à maintenir, à mettre à jour et à
diagnostiquer quand elle-même tombe. Un système d'observabilité en panne est le pire des
deux mondes — il coûte et il ment.

**Une ressource Application Insights par application**, comme aujourd'hui, mais **un seul
Log Analytics**, comme aujourd'hui aussi. Les six applications à terme partagent donc
l'espace de travail : les requêtes se font au niveau de l'espace, jamais au niveau d'une
ressource. C'est ce qui permet de suivre une trace qui part du scan, traverse l'API et
finit dans le worker.

Chaque hôte pose son `service.name` — `vpd-api`, `vpd-scan`, `vpd-catalog`, `vpd-worker` —
sans quoi tout se mélange dans l'espace commun.

## 3. La corrélation, et la frontière hors ligne

C'est le point difficile, et il est propre à ce projet.

Un geste est produit sur un téléphone **à 14 h 07, sans réseau**. Il dort dans IndexedDB.
Il est transmis **à 17 h 32**, dans un lot de quatre-vingts. La trace serveur de
`POST /scan/sessions/{id}/scans` décrit donc **la transmission**, pas le geste. Elle dure
deux cents millisecondes et couvre trois heures de travail bénévole.

Un contexte de trace propagé par en-tête ne résout rien : il relierait quatre-vingts gestes
à un seul appel, et le lien entre un geste et son moment réel serait perdu.

**La clé de corrélation est le `ClientGestureId`** ([`02`](02-modele-de-donnees.md) §2),
déjà retenu pour l'idempotence. Trois règles :

1. **Toute trace, toute mesure et tout journal qui touche un geste porte son
   `ClientGestureId`** en attribut. C'est ce qui rend « pourquoi ce livre affiche trois
   exemplaires » répondable par une seule recherche.
2. **Le span de traitement d'un lot porte le nombre de gestes, pas leur détail** ; chaque
   geste ouvre son propre span enfant. Un lot de quatre-vingts produit quatre-vingt-un
   spans, ce qui est parfaitement supportable à ce volume (§5).
3. **On ne trace jamais sur `ReceivedAt` quand on veut dire `OccurredAt`.** Les deux
   horodatages du modèle sont exactement cette distinction, et un graphique construit sur
   le mauvais des deux montre trois heures de tri concentrées sur une seconde.

**Le corollaire, souvent oublié** : `ClockSuspect` n'est pas qu'une colonne d'audit, c'est
une **mesure**. Un appareil dont l'horloge dérive produit des mouvements aberrants qui
polluent les statistiques par bourse. Le taux de gestes marqués `ClockSuspect`, par
appareil, dit lequel.

## 4. Ce qu'on mesure

Distinguer les deux natures, parce qu'elles ne servent pas les mêmes personnes.

### Les mesures qui disent que le système va bien

Ce sont des mesures d'absence, et elles priment sur toutes les autres.

| Mesure | Ce que sa dérive signale | Alerte |
|---|---|---|
| **Âge du plus vieux `OutboxMessage` échu non traité** | Le worker ne tourne plus. **La mesure la plus importante du système** | 🔴 Oui |
| Annonces dont la bourse a commencé et qui n'ont pas basculé | Worker arrêté, ou agenda incohérent (`RG-23`, `RG-38`) | 🔴 Oui |
| Sessions `EnCours` dont `LastSyncAt` dépasse un seuil | Un appareil hors ligne avec du travail non transmis (`ENF-07`) | 🟠 Oui |
| Sessions `EnCours` dont `LastScanAt` dépasse largement le seuil de `RG-43` | La clôture par inactivité ne s'exécute pas | 🟠 Oui |
| Messages d'outbox en `Failed` | Envoi cassé : domaine, quota, informations d'authentification | 🔴 Oui |
| Dernière exécution réussie de `sweep` et de `enrich` | Le battement de cœur du worker | 🔴 Oui |

**La première ligne mérite d'être comprise.** Elle ne mesure pas une erreur, elle mesure un
silence. Si `sweep` cesse de s'exécuter — `QT-02` en décrit le scénario exact —, aucune
exception n'est levée nulle part, et cette mesure est le **seul** signal disponible. Elle
se calcule par une requête, pas par un compteur incrémenté dans le code : un compteur
partage le sort du processus qui ne tourne plus.

### Les mesures qui disent que le métier va bien

| Mesure | Pourquoi | Origine |
|---|---|---|
| **Désaccord de verdict client / serveur** | Voir ci-dessous | `ScanBook` |
| Taux de métadonnées `NotFound` par source | Le pari de `DT-01`, mesuré en continu et non une seule fois par `QT-01` | `enrich` |
| Proportion de fiches sans `WorkId` | Conditionne `RG-46` ; sous un seuil, le repli titre + auteur devient nécessaire (`07` §4) | `enrich` |
| Appels sortants par source et par jour | Le jour où un rattrapage martèle la BnF, dont les conditions prévoient un blocage sans préavis | `enrich` |
| Écart constaté à la remise à plat (`RG-34`) | L'indicateur de la discipline de scan en caisse, donc **le principal indicateur de santé du projet** (`05` §6) | Administration |
| Rebonds e-mail, comptes suspendus | `RG-31` | `sweep` |
| Gestes marqués `ClockSuspect`, par appareil | §3 | Réception de lot |

**Le désaccord de verdict est la mesure que personne ne pense à poser, et c'est la plus
utile.** [`04`](04-app-scan.md) §5 pose que « le client affiche, le serveur fait foi » : le
verdict est donc calculé **deux fois**, une fois sur la copie locale et une fois en base.
Comparer les deux à la réception coûte une soustraction et donne, gratuitement :

- le **taux de péremption réel** de la copie embarquée — c'est-à-dire si le bandeau de
  fraîcheur de `ENF-05` est bien réglé ;
- une détection immédiate de toute divergence d'implémentation entre les deux calculs de
  `RG-10` à `RG-15`, qui sont écrits deux fois, dans deux langages ;
- une mesure directe de la qualité de `O1` : un bénévole à qui l'on a affiché « premier
  exemplaire » sur un livre présent en cinq exemplaires a reçu une mauvaise information.

Un désaccord n'est pas une erreur — il est attendu et sans gravité (`04` §4). C'est **sa
courbe** qui informe.

### Ce qu'on ne mesure pas

Processeur, mémoire et débit des conteneurs. À 2,5 requêtes par seconde de pointe, ils
diront toujours que tout va bien, y compris pendant une panne totale des alertes. Les
tableaux de bord d'infrastructure fournis par défaut sont, ici, une distraction.

## 5. L'échantillonnage

**Aucun échantillonnage en v1.** Et le réglage est explicite, pas subi.

Les volumes le permettent largement : quelques milliers de scans par bourse, quelques
dizaines d'e-mails par semaine, 2,5 requêtes par seconde en pointe. L'échantillonnage est
un outil pour les services à fort trafic, où l'on peut jeter 90 % des traces sans rien
perdre parce que le millier restant contient déjà tous les cas.

**Ici, il jetterait exactement ce dont on a besoin.** Le bogue qu'on cherchera arrive une
fois par mois, sur un appareil, dans une session. Une trace échantillonnée à 10 % a neuf
chances sur dix de l'avoir perdue — et c'est la seule qui comptait.

Deux précautions concrètes :

- **Fixer le taux à 1.0 explicitement** dans la configuration du distributeur Azure
  Monitor, et écrire pourquoi à côté. Un réglage laissé par défaut est un réglage que
  quelqu'un « optimisera » un jour sans savoir ce qu'il jette.
- **Vérifier qu'aucun échantillonnage adaptatif n'est actif.** C'est le piège : il se
  déclenche seul sous charge, et sa première victime est la rafale de scans d'une session
  de tri — le moment précis où l'on veut tout voir.

**Le levier de coût n'est pas l'échantillonnage, c'est la rétention et le plafond
journalier** (§8). Ils se règlent sans rien perdre de la fenêtre de diagnostic utile.

## 6. La journalisation

### Les niveaux, et ce qui va dedans

| Niveau | Usage | Exemples |
|---|---|---|
| `Error` | Une intervention humaine est requise | Envoi en `Failed`, migration refusée, source externe en erreur persistante |
| `Warning` | Anormal mais absorbé | `ClockSuspect` levé, métadonnées `NotFound`, arrivée tardive dans une session close |
| `Information` | Les faits métier qu'on voudra reconstituer | Session ouverte et close avec sa cause, lot reçu, alertes mises en file, bascule exécutée, reprise de session |
| `Debug` | Le détail du raisonnement | Verdict calculé et ses entrées, pipeline de résolution étape par étape |

**`Information` porte les faits, pas le déroulé.** « Session `{SessionId}` close, cause
`{CloseReason}`, `{AlertCount}` alertes en file, échéance `{DueAt}` » vaut trente lignes de
progression. Le critère est simple : ce niveau doit permettre de **reconstituer ce qui
s'est passé** un mois plus tard, sans rien de plus.

**Journalisation structurée, jamais d'interpolation.** `LogInformation("Session {SessionId} close", id)`
et non `LogInformation($"Session {id} close")` : le second produit une chaîne unique
qu'aucune requête ne saura regrouper — et c'est irréparable après coup, les données sont
déjà ingérées.

**Ne pas journaliser les instructions SQL en production.** EF Core sait être très bavard ;
c'est le premier poste d'ingestion inutile, et il contient des valeurs de paramètres.

### Ce qui ne va jamais dans un journal

C'est le point que ce chapitre existe pour poser.

**Un journal est une donnée personnelle.** Il est ingéré dans Log Analytics, conservé, et
requêtable par quiconque a accès à l'espace de travail. Les exigences du dossier
s'appliquent à ce qu'on y écrit **exactement comme** à ce qu'on renvoie dans une réponse
HTTP.

| Interdit | Règle | Ce qui arriverait |
|---|---|---|
| L'identité d'un membre qui recherche un livre | `RG-42` | Une ligne « alerte constituée pour `{Email}` sur `{Isbn}` » fait fuiter dans les journaux précisément ce que l'API a interdiction de renvoyer |
| Une adresse e-mail, où que ce soit | `ENF-10`, `ENF-12` | Un e-mail écrit dans un journal **survit à la suppression du compte**. La cascade de `ENF-12` ne touche pas Log Analytics |
| Le contenu d'un e-mail d'alerte | `RG-42`, `ENF-10` | Il contient l'adresse et les titres suivis |
| Un jeton, un en-tête `Authorization`, une chaîne de connexion | — | Évident, et pourtant le mode le plus courant de fuite d'identifiants |

**La règle de remplacement** : on journalise des **identifiants**, jamais des personnes.
`{UserId}` et non `{Email}`, `{RecipientCount}` et non la liste. L'identifiant permet de
retrouver la ligne en base tant que la personne existe — et cesse d'être exploitable une
fois le compte supprimé ou anonymisé (`DT-14`), ce qui est exactement le comportement
voulu.

Ce n'est pas une précaution théorique : `ENF-12` promet un effacement effectif, et une
adresse restée dans les journaux le contredit.

## 7. Le débogage

### Ce que les données répondent déjà

**`BookMovements` est en ajout seul, daté deux fois et attribué** (`RG-41`). Cela signifie
que la question la plus fréquente — « pourquoi ce livre affiche trois exemplaires ? » —
**se répond en base, pas dans les journaux** : la suite des mouvements *est* la
démonstration. C'est une propriété rare, et elle vient du modèle, pas de l'outillage.

L'observabilité sert le complément, qui est plus dur : **pourquoi un mouvement n'est jamais
arrivé.** D'où le §4.

### En local

Le tableau de bord Aspire montre traces, journaux et mesures des ressources orchestrées,
sans configuration. `DT-15` met Aspire à jour, notamment pour la sortie structurée et la
diffusion de télémétrie de la ligne de commande apportées en 13.2.

Deux exigences pour que le local reste utile :

- **Le worker tourne dans l'AppHost**, au même titre que l'API. Un traitement de fond qu'on
  ne sait pas exécuter en local se déboguera en production.
- **Un jeu de données de démonstration** permettant de rejouer une session de tri complète —
  scanner, clôturer, voir les alertes se mettre en file. Sans lui, chaque essai demande de
  reconstituer un état à la main, et on cesse d'essayer.

### En production

Le point d'entrée du diagnostic est une **requête**, pas un tableau de bord. Trois qu'il
faut avoir écrites d'avance, parce qu'on les veut au moment où l'on est pressé :

1. Tout ce qui touche un `ClientGestureId` — le geste, sa transmission, ses mouvements.
2. Le cycle de vie complet d'une session — ouverture, scans, clôture, mise en file, envoi.
3. L'état de la file d'outbox — ce qui est dû, réclamé, envoyé, en échec, et depuis quand.

### La télémétrie côté navigateur

L'application de scan tourne sur le téléphone personnel d'un bénévole, dans un local mal
éclairé, souvent sans réseau. Quand une bénévole dit « ça a planté », il n'y a
aujourd'hui rien à regarder.

| Application | Télémétrie navigateur | Motif |
|---|---|---|
| `scan` | **Oui** — erreurs et faits de session | Outil interne, utilisé par des bénévoles identifiés |
| `catalog`, zone d'administration | **Oui** | Idem |
| `catalog`, pages publiques | **Non, jamais** | `ENF-14` : aucun traceur, et une mesure d'audience doit fonctionner sans consentement |

La distinction n'est pas de commodité : `ENF-14` porte sur les visiteurs du site public,
pas sur les outils de travail de l'association. Elle doit néanmoins être **appliquée par la
construction** — deux applications distinctes, donc deux configurations —, et non par une
condition dans le code, qui finira par être fausse.

**La télémétrie du client ne se met pas en file durable.** Il serait tentant de réutiliser
le mécanisme de l'`outbox` ; c'est une erreur. La file de sortie est précieuse
([`04`](04-app-scan.md) §2) et rien ne doit la concurrencer pour la place de stockage. La
télémétrie reste au mieux : un tampon circulaire des derniers événements, envoyé si le
réseau le permet, jeté sinon. Ce tampon a une seconde vertu — il est **affichable dans
l'application**, ce qui donne à une bénévole de quoi décrire ce qu'elle a vu.

## 8. Les alertes

**Une mesure que personne ne regarde n'est pas une alerte.** Les mesures du §4 marquées
🔴 et 🟠 portent une règle Azure Monitor, déclarée en Bicep comme le reste, avec un groupe
d'actions qui envoie un e-mail à la personne qui maintient le système.

Trois principes, faute de quoi les alertes seront désactivées au bout d'un mois :

- **Peu d'alertes, toutes actionnables.** Six règles qu'on lit valent mieux que trente
  qu'on filtre. Chacune doit répondre à « et je fais quoi ? ».
- **Des seuils tenant compte du rythme réel.** L'association trie une semaine par mois.
  Une alerte « aucun scan depuis 48 h » se déclencherait trois semaines sur quatre.
- **Une alerte de battement de cœur**, qui se déclenche sur l'**absence** de signal du
  worker. C'est la seule qui détecte le cas de `QT-02`, et c'est le mode de panne le plus
  probable du système.

### Coût et garde-fous

L'ingestion Log Analytics se facture au gigaoctet. À ce volume, sans échantillonnage mais
sans journalisation SQL, on reste très en dessous de ce qui compte — mais un rattrapage
mal réglé ou une boucle bavarde peut le faire monter vite, et silencieusement.

| Garde-fou | Réglage |
|---|---|
| **Plafond journalier** sur chaque Application Insights | Le filet de sécurité contre la boucle bavarde. À poser dès la création, pas après la facture |
| **Rétention** | La durée incluse suffit : au-delà, le diagnostic passe par les données métier, qui sont conservées (`ENF-22`) |
| Journalisation SQL en production | Désactivée |

## 9. Ce que cela impose au fil du développement

`DT-16` fait de l'instrumentation une condition de livraison. En pratique, une tranche
n'est terminée que si l'on peut répondre à ces quatre questions :

1. **Que voit-on quand elle fonctionne ?** Un fait au niveau `Information`, avec ses
   identifiants — jamais ses personnes.
2. **Que voit-on quand elle échoue bruyamment ?** Une erreur avec de quoi agir.
3. **Peut-elle échouer en silence ?** Si oui, quelle mesure le révèle, et quelle alerte la
   porte. Si l'on ne sait pas répondre, la tranche n'est pas comprise.
4. **Comment la déboguer sans y ajouter de code ?** Si la réponse est « en ajoutant un
   journal et en redéployant », l'instrumentation est insuffisante.

La quatrième est le vrai test. C'est aussi celle qui se paie le plus cher quand on
l'ignore : ajouter un journal à un traitement différé qui ne s'exécute qu'une fois par
mois, c'est attendre un mois pour savoir.
