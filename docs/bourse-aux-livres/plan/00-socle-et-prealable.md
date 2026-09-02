# Lot 0 — Socle technique, puis préalable d'identité

Deux blocs, dans cet ordre. Le premier ne produit aucune fonctionnalité, ce qui est
exactement pourquoi il ne se fera jamais si on ne l'ordonnance pas. Le second est le
premier élément livré du projet (`DT-10`), et il a des **délais externes** qu'on ne
compresse pas.

**Critère de sortie du lot.** Un administrateur se connecte au `BackOffice` par Entra, un
caissier encaisse depuis un appareil réel mis à jour, **plus aucun mot de passe n'existe en
base**, la clé de signature JWT ne sert plus à rien, et le sous-domaine d'envoi d'e-mails
est vérifié et chauffe.

> **Ce que le lot 0 ne contient pas, contrairement à une version antérieure de ce plan.**
> Les enregistrements DNS du **catalogue** (`livres.`) ne sont pas posés ici : un `CNAME`
> Container Apps pointe vers le FQDN d'une application qui n'existe qu'au palier 2, et sa
> vérification se fait à la liaison du domaine. Seul l'envoi d'e-mails a une raison d'être
> posé tôt — la réputation. Le DNS du catalogue est dans [`lot 3`](03-paliers-2-et-3.md).

> **Le lot 0 vaut indépendamment du verdict du palier 0.** Le palier suivant a le droit de
> dire non ; ce lot-ci n'est pas perdu pour autant, parce qu'il supprime une
> authentification maison d'un `BackOffice` **déjà en service** et remet le socle
> d'exécution d'aplomb. Ce n'est pas une dépense engagée sur un pari.

---

## Bloc A — Socle technique

`DT-15`. À faire avant tout le reste : tout ce qui suit s'écrit dessus.

> **Précision d'ordonnancement.** `DT-15` place ces points « en premier lot du palier 1 ».
> En écrivant le plan, deux d'entre eux se sont révélés devoir bouger : le socle de
> versions vient **avant** le préalable, parce que le préalable écrit du code ; et la
> montée de `MauiCashApp` en `net10.0` va **avec** le préalable, parce que `DT-15`
> lui-même argumente qu'elle doit voyager dans la même redistribution que la migration
> MSAL (étape `L0-11`). La décision est inchangée, son découpage est affiné.

### `L0-1` — Épingler les runtimes, .NET **et** Node

🔧 Deux épinglages, pour la même raison :

1. Un `global.json` à la racine, épinglant la version du SDK .NET 10 utilisée, avec
   `rollForward` en `latestFeature`.
2. Un `.nvmrc` à la racine **et** un champ `engines` dans les deux `package.json`,
   épinglant la version de Node. Le workflow de `L0-5` utilise la même par
   `actions/setup-node`, aujourd'hui absent de tous les workflows.

✅ `dotnet --version` et `node --version` renvoient les versions attendues sur chaque
machine.

📌 Consigner dans `NEXT.md` les deux versions épinglées — c'est ce qu'il faudra installer
sur la machine suivante.

**Pourquoi en premier.** Le développement se fait sur plusieurs machines et la
construction sur un runner. Sans épinglage, ces trois environnements peuvent compiler
différemment, et le jour où l'un échoue seul, on cherche au mauvais endroit. L'argument
vaut mot pour mot pour Node, que `DT-15` avait laissé de côté : les deux applications
Angular se construisent aujourd'hui sur la version de Node qui traîne sur la machine.

### `L0-2` — Centraliser les versions de paquets

🔧 Créer `src/Backend/Directory.Packages.props` — c'est la racine de la solution —,
activer `ManagePackageVersionsCentrally`, y remonter toutes les versions et les retirer
des `.csproj`.

🔧 **Traiter `MauiCashApp` séparément, et le savoir.** `DT-15` annonce trois versions de
`Microsoft.Extensions.*` « dans la même solution ». C'est inexact : `10.0.7` est dans les
projets du backend, `9.0.8` et `9.0.5` sont dans `src/MauiCashApp/ShopAppVpd.csproj`, qui
**n'est référencé par aucune solution** (`Vole_Papillon_Damour.sln` contient neuf projets,
aucun MAUI). Un `Directory.Packages.props` sous `src/Backend/` ne le verra jamais. Deux
options, à trancher ici :

| Option | Effet |
|---|---|
| Remonter le `Directory.Packages.props` à `src/` | Une seule liste de versions, mais elle couvre des cibles très différentes |
| Un second fichier propre à `MauiCashApp` | Deux listes, aucun couplage entre la caisse et le backend |

La seconde est la plus sûre tant que la caisse reste hors solution ; c'est aussi celle
qui rend visible que la caisse n'a **aucune barrière de compilation** avant `L0-5`.

✅ `dotnet restore` puis `dotnet build` sans avertissement de rétrogradation
(`NU1605`) ni de version.

**Pourquoi maintenant.** L'API et le worker **doivent être construits depuis le même
commit** (`T-06` §2). Un écart de version entre les deux hôtes sur une bibliothèque
partagée est précisément le genre de panne qu'on diagnostique mal.

### `L0-3` — Monter Aspire à la dernière version

🔧 `Aspire.AppHost.Sdk`, `Aspire.Hosting.AppHost`, `.Azure.Storage`, `.SqlServer` et
`.JavaScript` en `13.5.3`. `Aspire.Hosting.NodeJs` est un paquet legacy déprécié en
`9.5.2` ; le remplacer par `Aspire.Hosting.JavaScript`, puis remplacer `AddNpmApp` par
`AddJavaScriptApp`. Conserver les scripts `start` et les arguments des deux applications
Angular avec `.WithRunScript("start").WithArgs(...)`. Le choix de `AddJavaScriptApp` est
documenté dans `DT-15` ; `AddViteApp` n'est pas retenu ici, car le plan ne décide pas de
changer le montage SSR du `Website`.

Installer la CLI Aspire à la même version et activer explicitement le bundle CLI avec
`AspireUseCliBundle=true` dans l'AppHost. Cette option est recommandée pour les AppHosts
existants avec Aspire 13.5 ; elle fait utiliser au tableau de bord et au plan de contrôle
les composants fournis par la CLI correspondante.

✅ `dotnet restore` puis `dotnet build` de la solution, sans `NU1605` ni avertissement de
version.

🧪 Lancer l'AppHost. **Attendu** : les quatre ressources — SQL, stockage, API, les deux
applications Angular — démarrent, et le tableau de bord montre traces, journaux et
mesures. C'est le socle de débogage local de `T-11` §7 ; s'il ne fonctionne pas, tout le
reste se déboguera en production.

📌 Version d'Aspire retenue (`13.5.3`), remplacement de l'intégration JavaScript legacy,
bundle CLI activé dans l'AppHost (`AspireUseCliBundle=true`), et version de la CLI.

### `L0-4` — Rendre le socle front reproductible

🔧 **L'étape que le plan avait oubliée, et c'est précisément le genre d'état hors dépôt
qu'il prétend porter.**

`SharedUi` n'est pas un paquet : c'est un dossier de sources, exposé aux applications par
un alias `@vpd/ui` dans leurs `tsconfig.json`. Comme il vit hors du dossier de
l'application, Node ne sait pas y résoudre `@angular/*` — d'où
`src/Website/scripts/link-shared-ui.mjs`, qui lie `src/SharedUi/node_modules` à
`src/Website/node_modules`, appelé en `prebuild` et en `prestart`.

Trois conséquences, aucune écrite nulle part :

- **`BackOffice` n'a pas ce script.** Sa compilation dépend donc de l'installation du
  `Website`, sans que rien ne le dise ni ne l'ordonne.
- **Un clone neuf ne compile pas** tant que `npm ci` n'a pas tourné dans `Website`. C'est
  exactement le scénario « machine suivante » du plan.
- **Deux applications Angular s'ajoutent** — `scan` au palier 0 ou 1, `catalogue` au palier
  2 —, et chacune reposera la question.

Ce qu'il faut faire ici : déplacer le script dans un emplacement partagé, l'appeler depuis
les `prebuild`/`prestart` de **chaque** application Angular, présente et à venir, et faire
qu'il lie vers l'installation de l'application appelante plutôt que vers celle du
`Website`.

✅ Depuis un clone neuf : `npm ci` puis `npm run build` dans `BackOffice` **seul**
réussit, sans avoir rien installé ailleurs.

📌 Rien — et c'est le but. Cette étape retire une ligne de `NEXT.md` au lieu d'en ajouter
une.

### `L0-5` — Compilation et tests au push

🔧 Un workflow `ci.yml` déclenché sur `push` et `pull_request` : restauration,
compilation de la solution, `dotnet test` des trois projets de test, puis `npm ci` et
`npm run build` des deux applications Angular, sur la version de Node épinglée en `L0-1`.

🔧 **Y compter `MauiCashApp`.** Elle n'est dans aucune solution : « compiler la solution »
ne la compile pas. Sans une étape `dotnet build` explicite sur son `.csproj`, tout le
chantier de `L0-11` — MSAL.NET et montée en `net10.0` — se ferait sans aucune barrière
automatique, sur le seul composant qu'on ne peut pas corriger par un redéploiement.
Compiler au moins la cible retenue en `L0-10` suffit à attraper l'essentiel.

**C'est le seul vrai manque de l'intégration continue** — contrairement à ce
qu'affirmait `T-08` §5, sept workflows existent déjà, mais **tous en déclenchement
manuel** (`revue.md` `R-19`).

✅ Le workflow passe au vert sur une poussée de branche.

📌 Rien — c'est dans le dépôt.

> **Réserve connue.** `npm test` échoue côté `BackOffice`, faute de fichiers de test — il
> n'en contient aucun, quand le `Website` en compte dix-neuf. Ne pas mettre le test front
> dans la porte tant que ce n'est pas réglé : une CI rouge en permanence est une CI qu'on
> cesse de lire. Compiler, oui ; tester, quand il y aura quoi tester.

### `L0-6` — Points de santé et sondes

🔧 Exposer `/health` sur l'API (vivacité, et disponibilité incluant la base). Renseigner
les sondes dans `main.dev.bicepparam` — elles sont aujourd'hui désactivées, chemins vides
et ports à zéro.

✅ `GET /health` en local répond 200.

🚀 `infra-deploy` puis `api-deploy`.

🧪 **Attendu** : une révision qui démarre mal ne prend pas de trafic. Le vérifier une fois,
en déployant volontairement une image cassée sur une révision — c'est la seule façon de
savoir que la sonde sert à quelque chose.

**Pourquoi avant d'ajouter trois applications** (`revue.md` `R-23`) : les poser
correctement une fois vaut mieux que les rattraper six fois.

### `L0-7` — Base SQL en `S1`

🔧 `DT-11`. Passer `sqlDatabaseSku` de `GP_S_Gen5_1` serverless à `S1`, dans
`main.dev.bicepparam` et dans le type du module `SqlServer`.

🚀 `infra-deploy`. La montée en gamme est en ligne.

✅ Le portail montre le palier `S1` et **aucune pause automatique**.

🧪 **Attendu, et c'est le point** : ouvrir le site public après plusieurs heures
d'inactivité. La première page doit s'afficher **sans le délai de réveil** qu'on observe
aujourd'hui. C'est `ENF-01` et `ENF-08` qui se jouent là.

📌 Date du basculement — utile pour lire la facture du mois et confirmer la baisse
attendue.

> `QT-09` mesure au palier 1 si `S1` tient sur son stockage à disque dur. Ici, on ne fait
> que basculer.

---

## Bloc B — Préalable d'identité et délais externes

`DT-10`, `DT-12`. **Ce qui compte ici, c'est de démarrer tôt ce qui attend.**

### `L0-8` — Poser les enregistrements DNS de l'envoi d'e-mails

🔧 Sur `volepapillondamour.fr`, dont vous avez la main :

| Enregistrement | Pour |
|---|---|
| `TXT` de propriété, SPF, DKIM sur `mail` | L'envoi d'e-mails (`DT-12`) |
| `DMARC` | Idem |
| `TXT` de vérification Search Console | Le référencement (`ENF-09`) — gratuit à poser maintenant, et la propriété du domaine sert dès le palier 2 |

**Le SPF du sous-domaine d'envoi doit être en `-all`**, et ne contenir que le mécanisme
d'ACS : le service exige une correspondance exacte et refuse `~all` comme les
enregistrements composés. C'est la raison d'être du sous-domaine.

**Le DNS du catalogue n'est pas ici.** `livres.volepapillondamour.fr` demande un `CNAME`
vers le FQDN de la Container App du catalogue, qui n'existe pas avant le palier 2, et sa
vérification se fait à la liaison du domaine. Le poser à vide n'avance rien et le critère
de sortie du lot ne peut pas l'exiger. Il est au [`lot 3`](03-paliers-2-et-3.md).

🧪 **Attendu** : `nslookup -type=TXT mail.volepapillondamour.fr` renvoie les
enregistrements attendus depuis une machine extérieure au réseau qui les héberge.

📌 **La liste exacte des enregistrements posés, et la date.** C'est le premier candidat à
l'oubli entre deux machines, et le premier suspect quand un envoi échoue six mois plus
tard.

### `L0-9` — Créer le locataire et la messagerie

🔧 Deux créations, qui se font en parallèle et qui attendent toutes deux :

1. **Locataire Entra External ID**, rattaché à un abonnement Azure — un locataire externe
   n'a pas de capacité de gestion d'abonnement, le rattachement se fait à un abonnement
   détenu par le locataire de travail. C'est l'une des deux exceptions assumées à la
   configuration scriptée (`ENF-27`).
2. **Ressource Azure Communication Services Email**, en Bicep, et vérification du
   sous-domaine d'envoi.

**On crée la messagerie maintenant alors qu'aucun e-mail ne partira avant le palier 3.**
C'est délibéré : la **réputation d'un domaine d'envoi se construit sur des semaines**, et
un domaine neuf qui émettrait d'un coup son premier lot d'alertes groupées partirait en
indésirables. `RG-28` et l'objectif `O5` échoueraient en silence.

🧪 **Attendu** : le portail affiche le sous-domaine d'envoi **vérifié**, SPF et DKIM au
vert. Envoyer un message d'essai vers une adresse personnelle, et **vérifier qu'il arrive
en boîte de réception et non en indésirables**.

📌 Identifiant du locataire, nom de la ressource ACS, adresse d'expédition retenue, date de
vérification du domaine.

### `L0-10` — Réduire la caisse à Android, et savoir la redistribuer

🔧 **Étape à part entière, parce qu'elle conditionne `L0-11` et qu'aucun document du
dossier ne la décrivait.** `MauiCashApp` est le seul composant qui ne se met pas à jour par
un déploiement : il faut donc, avant de toucher à l'authentification, savoir **par quel
canal une nouvelle version arrive sur les appareils**, et l'avoir prouvé une fois.

🔧 **Retirer les trois cibles inutiles.** La caisse tourne uniquement sur des téléphones et
des tablettes **Android**. Le `.csproj` en cible pourtant quatre : `net9.0-android`,
`net9.0-ios`, `net9.0-maccatalyst` et, sous Windows, `net9.0-windows10.0.19041.0`. Ne
garder qu'Android. Ce n'est pas du ménage : chaque cible conservée est une chaîne de
signature, un jeu de redirections MSAL et une compilation à maintenir — et `L0-11` doit
enregistrer une redirection **par plateforme**. Trois plateformes en moins, c'est trois
sources d'erreur en moins sur le seul composant qu'un redéploiement ne corrige pas.

*Effet de bord bienvenu :* la compilation de `L0-5` n'a plus qu'une cible à construire, et
elle n'exige plus de machine macOS.

🔧 **Le canal.** À notre échelle — quelques appareils, tous détenus par l'association —
c'est un **APK signé, installé à la main sur chaque appareil**. Ce que cela suppose et
qu'il faut poser une fois : un magasin de clés de signature (`.keystore`), conservé
**hors du dépôt** et sauvegardé — le perdre interdit toute mise à jour des installations
existantes —, et l'autorisation des sources inconnues sur chaque appareil.

✅ `dotnet publish -f net10.0-android -c Release` produit un APK signé.

🧪 Poser sur un appareil de caisse une version identique à celle en service, par ce canal.
**Attendu** : l'installation aboutit en écrasant la précédente sans la désinstaller — ce
qui n'arrive que si la signature est la même —, et l'application démarre. Si cette étape
achoppe, `L0-11` est bloquée, et il vaut mille fois mieux le découvrir ici que le jour où
`/auth/login` a disparu.

📌 Où est le magasin de clés et qui en a une copie *(l'emplacement, jamais le mot de
passe)*, et **la liste des appareils de caisse avec leur version installée**. Cette liste ne
se déduit de rien, et c'est elle qu'on cherchera à chaque livraison.

### `L0-11` — Migrer l'authentification

🔧 Le chantier de `T-10` §6. Il se fait **en une seule fois du point de vue de la cible** —
une migration à moitié faite laisse un chemin d'authentification parallèle ouvert, ce qui
est pire que de ne rien changer — mais **en trois déploiements**, parce que la caisse ne se
met pas à jour par un déploiement et doit être validée avant qu'on retire l'ancien chemin.
Voir la séquence plus bas : c'est le point où ce plan se contredisait.

1. Exécuter `infra/entra/Configure-EntraApps.ps1` — cinq enregistrements (`vpd-api`,
   `vpd-catalog`, `vpd-scan`, `vpd-backoffice`, `vpd-caisse`, tous suffixés par
   l'environnement), portée, rôles. **Avec `-WhatIf` au premier passage.**
2. Recréer à la main les comptes administrateurs existants, et leur attribuer
   `Administration` par `Set-VpdUserRole.ps1`. Ils ne se migrent pas : on ne transfère pas
   une empreinte de mot de passe.
3. API : `Microsoft.Identity.Web`, audience et autorité vérifiées, politiques `Tri`,
   `Caisse`, `Administration`.
4. Migration de base — c'est la **migration 0** de `T-02` §6 : supprimer `Password`,
   `Salt`, `Role` ; ajouter `ExternalId`, `CreatedAt`, `LastSeenAt`, `AnonymizedAt`
   (`DT-14`).
5. Supprimer tout l'inventaire de `T-10` §6 — jusqu'au **secret de signature JWT en Key
   Vault**, et jusqu'aux paramètres `jwt*` de `main.bicep` et du `bicepparam`.
6. `BackOffice` : MSAL Angular, suppression du service d'authentification maison et des
   dépendances `@auth0/angular-jwt` et `ngx-cookie-service`.
7. `MauiCashApp` : MSAL.NET, **et montée en `net10.0-android`** dans la même livraison.
8. **La suppression du compte dans le locataire** (`revue.md` `R-06`) : un enregistrement
   d'application avec la permission applicative `User.ReadWrite.All`, son secret en Key
   Vault, et l'appel Microsoft Graph qui supprime l'identité en même temps que la ligne en
   base. Voir plus bas — c'est ce qui rend `ENF-12` tenable.

**Deux pièges dans le script d'enregistrement, et ils ne se voient pas à l'exécution.**

- *Les redirections par plateforme* (`revue.md` `R-18`). `Configure-EntraApps.ps1`
  n'enregistre aujourd'hui que `http://localhost` pour `vpd-caisse`, alors que MSAL.NET
  exige une redirection par plateforme. Une seule à poser, la caisse étant Android seule
  (`L0-10`) : `msal<clientId>://auth`, avec le filtre d'intention correspondant dans le
  manifeste Android.
- *Le script **remplace** la liste des URI* (`revue.md` `R-29`) : `RedirectUris =
  @($client.Uri)`, une seule valeur. Impossible d'avoir `localhost` **et** la production
  sur le même enregistrement — le second passage efface le premier. Cela ne casse rien le
  jour de la migration : cela casse le premier développement local qui suit un passage en
  production, quand personne ne fait plus le lien. À corriger dans le script ici, en
  fusionnant les URI au lieu de les écraser. Le `README` d'`infra/entra/` donne par
  ailleurs en exemple la Container App du `Website` existant comme origine du catalogue —
  à corriger au passage.

**La séquence de déploiement, et pourquoi elle n'est pas en un seul geste.**

| # | Ce qu'on déploie | Ce qui doit être vrai après |
|---|---|---|
| 0 | Rien. **Une sauvegarde de la base**, et sa restauration vérifiée (`T-08` §8) | L'étape 4 supprime trois colonnes ; elle n'est pas réversible par un redéploiement |
| 1 | API acceptant **les deux** chemins d'authentification — Entra et `/auth/login` | Rien n'est cassé ; le `BackOffice` en service continue de fonctionner |
| 2 | Migration de base, `BackOffice` MSAL, caisse MSAL **redistribuée sur chaque appareil** | Tout le monde passe par Entra ; l'ancien chemin existe encore mais ne sert plus |
| 3 | Retrait de `/auth/login`, du générateur de jeton, du secret en Key Vault | Le préalable est fini |

C'est ce que le 🧪 n°3 demandait — « encaisser sur un appareil réel **avant** de retirer
`/auth/login` » — et que le « entièrement ou pas du tout » rendait impossible. Les deux
sont vrais : la cible est indivisible, le chemin qui y mène a une marche intermédiaire.

**La suppression du compte dans le locataire, et pourquoi elle se fait ici** (`R-06`).
`ENF-12` promet un effacement effectif ; effacer nos données en laissant l'identité vivante
dans l'annuaire n'est pas une suppression. Cela suppose un appel Microsoft Graph
applicatif — donc un enregistrement d'application de plus, un secret, et la première
authentification de machine à machine du système, que `QT-04` déclarait nulle.

Ce chantier se fait **maintenant** et non au palier 3, pour trois raisons : on est déjà
dans le locataire et dans le modèle de personnes que `DT-14` remanie, l'enregistrement
d'application se crée avec les cinq autres au lieu d'être un chantier à part, et surtout
**il n'y a encore personne à supprimer**. Une erreur ici ne coûte rien ; la même erreur au
palier 3 se découvre sur une demande d'effacement réelle.

Ce qu'il faut poser : la permission applicative `User.ReadWrite.All` avec consentement
administrateur, un secret à rotation en Key Vault, et un chemin de suppression **unique** —
une seule opération qui supprime la ligne en base et l'identité dans l'annuaire, jamais
deux appels que rien ne relie. Ce qui échoue à moitié doit se rejouer, pas se rattraper à
la main.

🧪 Créer un compte d'essai dans le locataire, l'appeler par ce chemin, et **vérifier des
deux côtés** : plus de ligne en base, plus d'utilisateur dans l'annuaire. Le test complet
de `ENF-12` — avec liste de recherche et historique d'alertes — attendra le palier 3, mais
le mécanisme, lui, est éprouvé ici.

**Le point à ne pas rater sur la caisse.** Le jour où `/auth/login` disparaît de l'API,
**toute application installée qui ne serait pas passée au déploiement 2 cesse de
fonctionner** jusqu'à ce qu'une nouvelle version soit posée dessus. D'où la liste
d'appareils de `L0-10`, et d'où le fait que le déploiement 3 ne se fait pas le même jour
que le 2.

✅ `dotnet build` (solution **et** `MauiCashApp`), tests au vert, aucune occurrence
résiduelle de `IJwtGenerator`, `HashPassword`, `JwtSettings`.

🧪 Quatre tests manuels, et le troisième est celui qu'on oublie :

1. Se connecter au `BackOffice` avec un compte Entra portant `Administration`. **Attendu** :
   accès complet.
2. Se connecter avec un compte sans rôle. **Attendu** : refus propre, pas une page blanche.
3. **Ouvrir la caisse MAUI sur un appareil réel**, se connecter, encaisser. **Attendu** :
   le parcours MSAL aboutit sur la plateforme visée. À faire **après le déploiement 2 et
   avant le déploiement 3**.
4. Vérifier en base que les colonnes `Password`, `Salt` et `Role` n'existent plus.

🚀 Trois passages, selon le tableau ci-dessus : `infra-deploy`, `api-deploy` avec
`run_migrations`, `backoffice-deploy`, et la redistribution de la caisse entre le
deuxième et le troisième.

📌 Les identifiants d'application produits par le script, la liste des comptes recréés et
leurs rôles, **la liste des appareils de caisse mis à jour**, et **le déploiement auquel on
en est** — cette séquence s'étale sur plusieurs jours et se reprend sur une autre machine.

### `L0-12` — Les mesures d'identité

🔧 Trois questions ouvertes se règlent ici, et deux sont bloquantes. **Elles se mesurent
sans l'application de scan, qui n'existe pas encore** — le lot 1 la construit en
consultation seule et le palier 1 la construit vraiment. Ce qui suit dit avec quoi.

| Mesure | Ce qu'elle décide | Durée | Support |
|---|---|---|---|
| `QT-07` | Qu'une application peut être **en connexion seule**, sans offrir l'inscription | Une heure | L'enregistrement `vpd-scan` de `L0-11` |
| `QT-08` | La forme du démarrage de session dans la PWA face au hors ligne | **Deux jours d'attente** | Une page monopage jetable |
| `QT-04` | Le parcours d'inscription, et que `ENF-12` supprime bien des deux côtés | Une heure | L'enregistrement `vpd-catalog` |

🧪 `QT-07` : l'enregistrement `vpd-scan` existe depuis `L0-11`, l'application non. Ouvrir
son écran de connexion en **construisant l'URL d'autorisation à la main** dans une fenêtre
de navigation privée, et **chercher un lien d'inscription**. Il ne doit pas y en avoir.
Sinon, l'annuaire se remplira de comptes créés par n'importe qui. Refaire le test dans les
deux configurations décrites en `T-09` `QT-07` : sans flux rattaché, et avec un flux dont
l'inscription est désactivée.

🧪 `QT-08` : **une page monopage jetable suffit, et il faut le dire** — quelques dizaines
de lignes avec MSAL Angular, un bouton « se connecter » avec maintien de session, et
l'affichage de l'identité lue depuis le stockage local. Ce que cette page mesure, et ce
qu'elle ne mesure pas :

| Question de `QT-08` | Mesurable ici | Pourquoi |
|---|---|---|
| Le jeton de rafraîchissement survit-il à quarante-huit heures ? | **Oui** | C'est le plafond de vingt-quatre heures des applications monopages qui est en cause, pas l'application |
| La session se rétablit-elle silencieusement au retour du réseau ? | **Oui** | Idem |
| L'identité du bénévole est-elle encore connue de l'appareil hors ligne ? | **Oui**, si la page lit son identité depuis le stockage local comme le prévoit `T-10` §9 | C'est le mécanisme qui est testé, pas l'écran |
| Le geste de scan reste-t-il possible sans réseau ? | **Non** | Il n'y a ni file de sortie ni IndexedDB avant `P1-5`. Cette observation-là se fait à `P1-5`, où elle est déjà prévue |

**Lancer `QT-08` dès l'ouverture du bloc B**, pas à la fin : elle attend deux jours pendant
qu'on fait autre chose. C'est typiquement ce que `NEXT.md` doit porter — une mesure en
cours, avec sa date de début.

📌 Les résultats, datés. `QT-08` peut rouvrir `DT-08` pour l'application de scan — c'est
`S0-2` et `P1-5` qui changeraient de nature —, ce n'est pas un détail qu'on retrouve de
mémoire trois semaines plus tard.

### `L0-13` — Reconnaître la fin du préalable

🧪 La vérification qui vaut pour toutes les autres : **la clé de signature JWT ne sert plus
à rien.** Tant qu'elle existe en Key Vault et qu'une application sait s'en servir, la
migration n'est pas finie. La supprimer, et vérifier que tout continue de fonctionner, est
le test de fin de lot.

📌 Marquer le lot clos dans `NEXT.md`, avec la date.
