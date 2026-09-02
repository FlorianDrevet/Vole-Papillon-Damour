# Lot 0 — Socle technique, puis préalable d'identité

Deux blocs, dans cet ordre. Le premier ne produit aucune fonctionnalité, ce qui est
exactement pourquoi il ne se fera jamais si on ne l'ordonnance pas. Le second est le
premier élément livré du projet (`DT-10`), et il a des **délais externes** qu'on ne
compresse pas.

**Critère de sortie du lot.** Un administrateur se connecte au `BackOffice` par Entra,
**plus aucun mot de passe n'existe en base**, la clé de signature JWT ne sert plus à rien,
et les enregistrements DNS du catalogue et de l'envoi d'e-mails sont posés et vérifiés.

---

## Bloc A — Socle technique

`DT-15`. À faire avant tout le reste : tout ce qui suit s'écrit dessus.

> **Précision d'ordonnancement.** `DT-15` place ces points « en premier lot du palier 1 ».
> En écrivant le plan, deux d'entre eux se sont révélés devoir bouger : le socle de
> versions vient **avant** le préalable, parce que le préalable écrit du code ; et la
> montée de `MauiCashApp` en `net10.0` va **avec** le préalable, parce que `DT-15`
> lui-même argumente qu'elle doit voyager dans la même redistribution que la migration
> MSAL (étape `L0-9`). La décision est inchangée, son découpage est affiné.

### `L0-1` — Épingler le SDK

🔧 Créer un `global.json` à la racine, épinglant la version du SDK .NET 10 utilisée, avec
`rollForward` en `latestFeature`.

✅ `dotnet --version` renvoie la version attendue sur chaque machine.

📌 Consigner dans `NEXT.md` la version épinglée — c'est ce qu'il faudra installer sur la
machine suivante.

**Pourquoi en premier.** Le développement se fait sur plusieurs machines et la
construction sur un runner. Sans épinglage, ces trois environnements peuvent compiler
différemment, et le jour où l'un échoue seul, on cherche au mauvais endroit.

### `L0-2` — Centraliser les versions de paquets

🔧 Créer `src/Backend/Directory.Packages.props`, activer
`ManagePackageVersionsCentrally`, y remonter toutes les versions et les retirer des
`.csproj`.

**Résoudre au passage les écarts relevés** : `Microsoft.Extensions.*` cohabite aujourd'hui
en `10.0.7`, `9.0.8` et `9.0.5` dans la même solution.

✅ `dotnet restore` puis `dotnet build` sans avertissement de rétrogradation
(`NU1605`) ni de version.

**Pourquoi maintenant.** L'API et le worker **doivent être construits depuis le même
commit** (`06` §2). Un écart de version entre les deux hôtes sur une bibliothèque partagée
est précisément le genre de panne qu'on diagnostique mal.

### `L0-3` — Monter Aspire à la dernière version

🔧 `Aspire.AppHost.Sdk`, `Aspire.Hosting.AppHost`, `.Azure.Storage`, `.SqlServer` en
13.4.6. **Aligner `Aspire.Hosting.NodeJs`**, resté en `9.5.2` dans le même AppHost.
Installer la CLI Aspire à la même version.

✅ `dotnet build` de la solution.

🧪 Lancer l'AppHost. **Attendu** : les quatre ressources — SQL, stockage, API, les deux
applications Angular — démarrent, et le tableau de bord montre traces, journaux et
mesures. C'est le socle de débogage local de `11` §7 ; s'il ne fonctionne pas, tout le
reste se déboguera en production.

📌 Version d'Aspire retenue, et version de la CLI.

### `L0-4` — Compilation et tests au push

🔧 Un workflow `ci.yml` déclenché sur `push` et `pull_request` : restauration,
compilation de la solution, `dotnet test` des trois projets de test, puis `npm ci` et
`npm run build` des deux applications Angular.

**C'est le seul vrai manque de l'intégration continue** — contrairement à ce
qu'affirmait `08` §5, sept workflows existent déjà, mais **tous en déclenchement manuel**
(`revue.md` `R-19`).

✅ Le workflow passe au vert sur une poussée de branche.

📌 Rien — c'est dans le dépôt.

> **Réserve connue.** `npm test` échoue côté `BackOffice`, faute de fichiers de test. Ne
> pas mettre le test front dans la porte tant que ce n'est pas réglé : une CI rouge en
> permanence est une CI qu'on cesse de lire. Compiler, oui ; tester, quand il y aura quoi
> tester.

### `L0-5` — Points de santé et sondes

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

### `L0-6` — Base SQL en `S1`

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

`DT-10`, `DT-12`, `DT-13`. **Ce qui compte ici, c'est de démarrer tôt ce qui attend.**

### `L0-7` — Poser les enregistrements DNS

🔧 Sur `volepapillondamour.fr`, dont vous avez la main :

| Enregistrement | Pour |
|---|---|
| `CNAME` + `TXT asuid` sur `livres` | Le catalogue (`DT-13`) — même si l'application n'existe pas encore |
| `TXT` de propriété, SPF, DKIM sur `mail` | L'envoi d'e-mails (`DT-12`) |
| `DMARC` | Idem |
| `TXT` de vérification Search Console | Le référencement (`ENF-09`) |

**Le SPF du sous-domaine d'envoi doit être en `-all`**, et ne contenir que le mécanisme
d'ACS : le service exige une correspondance exacte et refuse `~all` comme les
enregistrements composés. C'est la raison d'être du sous-domaine.

🧪 **Attendu** : `nslookup -type=TXT mail.volepapillondamour.fr` renvoie les
enregistrements attendus depuis une machine extérieure au réseau qui les héberge.

📌 **La liste exacte des enregistrements posés, et la date.** C'est le premier candidat à
l'oubli entre deux machines, et le premier suspect quand un envoi échoue six mois plus
tard.

### `L0-8` — Créer le locataire et la messagerie

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

### `L0-9` — Migrer l'authentification

🔧 Le chantier de `10` §6, et il se fait **entièrement ou pas du tout** : une migration à
moitié faite laisse un chemin d'authentification parallèle ouvert, ce qui est pire que de
ne rien changer.

1. Exécuter `infra/entra/Configure-EntraApps.ps1` — cinq enregistrements, portée, rôles.
   **Avec `-WhatIf` au premier passage.**
2. Recréer à la main les comptes administrateurs existants, et leur attribuer
   `Administration` par `Set-VpdUserRole.ps1`. Ils ne se migrent pas : on ne transfère pas
   une empreinte de mot de passe.
3. API : `Microsoft.Identity.Web`, audience et autorité vérifiées, politiques `Tri`,
   `Caisse`, `Administration`.
4. Migration de base : supprimer `Password`, `Salt`, `Role` ; ajouter `ExternalId`,
   `CreatedAt`, `LastSeenAt`, `AnonymizedAt` (`DT-14`).
5. Supprimer tout l'inventaire de `10` §6 — jusqu'au **secret de signature JWT en Key
   Vault**, et jusqu'aux paramètres `jwt*` de `main.bicep` et du `bicepparam`.
6. `BackOffice` : MSAL Angular, suppression du service d'authentification maison et des
   dépendances `@auth0/angular-jwt` et `ngx-cookie-service`.
7. `MauiCashApp` : MSAL.NET, **et montée en `net10.0`** dans la même livraison.

**Le point à ne pas rater sur la caisse.** L'application MAUI est le seul composant qui ne
se met pas à jour par un déploiement. Le jour où `/auth/login` disparaît de l'API,
**l'application installée sur les appareils cesse de fonctionner** jusqu'à ce qu'une
nouvelle version soit posée sur chacun. Et `Configure-EntraApps.ps1` n'enregistre
aujourd'hui que `http://localhost` pour `vpd-caisse` : MSAL.NET exige des redirections par
plateforme — `msal<clientId>://auth`, filtre d'intention Android, droits de trousseau iOS
(`revue.md` `R-18`).

✅ `dotnet build`, tests au vert, aucune occurrence résiduelle de `IJwtGenerator`,
`HashPassword`, `JwtSettings`.

🧪 Quatre tests manuels, et le troisième est celui qu'on oublie :

1. Se connecter au `BackOffice` avec un compte Entra portant `Administration`. **Attendu** :
   accès complet.
2. Se connecter avec un compte sans rôle. **Attendu** : refus propre, pas une page blanche.
3. **Ouvrir la caisse MAUI sur un appareil réel**, se connecter, encaisser. **Attendu** :
   le parcours MSAL aboutit sur la plateforme visée. À faire **avant** de retirer
   `/auth/login`, pas après.
4. Vérifier en base que les colonnes `Password`, `Salt` et `Role` n'existent plus.

🚀 `infra-deploy`, `api-deploy` avec `run_migrations`, `backoffice-deploy`, puis
redistribution de la caisse sur chaque appareil.

📌 Les identifiants d'application produits par le script, la liste des comptes recréés et
leurs rôles, **et la liste des appareils de caisse mis à jour** — celle-là ne se déduit de
rien.

### `L0-10` — Les mesures d'identité

🔧 Trois questions ouvertes se règlent ici, et deux sont bloquantes.

| Mesure | Ce qu'elle décide | Durée |
|---|---|---|
| `QT-07` | Qu'une application peut être **en connexion seule**, sans offrir l'inscription | Une heure |
| `QT-08` | La forme du démarrage de session dans la PWA face au hors ligne | **Deux jours d'attente** |
| `QT-04` | Le parcours d'inscription, et que `ENF-12` supprime bien des deux côtés | Une heure |

🧪 `QT-07` : ouvrir l'écran de connexion de `vpd-scan` en navigation privée et **chercher
un lien d'inscription**. Il ne doit pas y en avoir. Sinon, l'annuaire se remplira de
comptes créés par n'importe qui.

🧪 `QT-08` : se connecter avec le maintien de session, **ne pas y toucher pendant
quarante-huit heures**, puis rouvrir l'application **en mode avion**. Trois observations :
la session se rétablit-elle silencieusement au retour du réseau ; le geste de scan
reste-t-il possible sans réseau ; l'identité du bénévole est-elle toujours connue de
l'appareil.

**Lancer `QT-08` dès l'ouverture du bloc B**, pas à la fin : elle attend deux jours pendant
qu'on fait autre chose. C'est typiquement ce que `NEXT.md` doit porter — une mesure en
cours, avec sa date de début.

📌 Les résultats, datés. `QT-08` peut rouvrir `DT-08` pour l'application de scan ; ce n'est
pas un détail qu'on retrouve de mémoire trois semaines plus tard.

### `L0-11` — Reconnaître la fin du préalable

🧪 La vérification qui vaut pour toutes les autres : **la clé de signature JWT ne sert plus
à rien.** Tant qu'elle existe en Key Vault et qu'une application sait s'en servir, la
migration n'est pas finie. La supprimer, et vérifier que tout continue de fonctionner, est
le test de fin de lot.

📌 Marquer le lot clos dans `NEXT.md`, avec la date.
