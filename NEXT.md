# NEXT — où en est la bourse aux livres

> **À lire en premier en arrivant sur une machine. À mettre à jour en dernier avant de la
> quitter**, même en pleine étape.
>
> Ce fichier porte **ce que git ne sait pas** : l'état d'Azure, du locataire, du DNS, les
> mesures en cours, les tests manuels passés. Les étapes, elles, sont dans
> [`docs/bourse-aux-livres/plan/`](docs/bourse-aux-livres/plan/README.md).

---

## En un coup d'œil

| | |
|---|---|
| **Lot en cours** | `S0-2` → `S0-3` — sonde locale à valider, puis instrument de comparaison des sources ; les déploiements 2–3 de `L0-11` restent à faire hors dépôt |
| **Prochaine action** | Faire le smoke test S0-2 avec l'AppHost, implémenter l'instrument `S0-3`, puis mesurer `S0-4` sur 300 livres ([palier 0](docs/bourse-aux-livres/plan/01-palier-0-sonde.md)) |
| **Dernière machine** | Windows — `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour` |
| **Dernière mise à jour** | 2026-09-03 |
| **Branche** | `main` |

---

## Décisions prises

Les arbitrages que le plan ne pouvait pas prendre seul sont tranchés. Rien n'attend
plus de réponse ; ceci est un rappel, le détail est dans les documents cités.

| Sujet | Décision |
|---|---|
| Caisse | **Android seul**, téléphones et tablettes. iOS, Mac Catalyst et Windows retirés du `.csproj`. APK signé, posé à la main sur chaque appareil (`L0-10`) |
| Authentification BackOffice | **MSAL Angular `5.3.1` avec MSAL Browser `5.20.0`**, dernière ligne compatible avec Angular 21. Les routes utilisent `MsalGuard`, la connexion passe par le redirect Entra et Axios acquiert silencieusement la portée API via un adaptateur dédié ; `MsalInterceptor` n'est pas utilisé car le BackOffice utilise Axios (`L0-11`) |
| Authentification caisse | **MSAL.NET `4.88.0`**, sans broker pour cette première livraison. `MauiCashApp` acquiert silencieusement la portée API puis utilise le parcours interactif Android avec `msal<clientId>://auth` (`L0-11`) |
| Suppression du compte dans le locataire | **Au préalable d'identité** (`L0-11`, étape 8), pendant qu'il n'y a encore personne à supprimer |
| Genres et classement | **Depuis les sources bibliographiques**, et le site n'indique **jamais** où se trouve un livre dans le local (`Q-07`) |
| Repli d'exploitation | **Aucun.** Une panne fait vendre sans enregistrer, rien n'est rattrapé. Le hors-ligne de la caisse devient la seule protection (`ENF-21`, `P1-10`) |

Les **chiffres cibles du palier 0** (`S0-1`) sont fixés avant la campagne. Le **choix du
matériel de scan** (`Q-08`) reste à trancher après la campagne, s'il s'avère nécessaire.

---

## Le mode de travail

**Pas de date, pas d'échéance.** On construit au fil de l'eau, on teste en même temps, et
on ne montre l'ensemble que lorsqu'il tient debout. Les paliers restent un **ordre de
construction** : ils disent quoi écrire avant quoi, pas quand livrer.

Les 🧪 des lots sont des tests à faire soi-même, seul. Trois choses y échappent et
attendront l'usage réel : le ressenti d'un bénévole sur le geste de scan, la discipline de
scan en caisse un jour de bourse, et la réputation du domaine d'envoi — d'où `L0-9`, très
en avance sur son besoin.

---

## Reprendre le travail

```bash
git pull
# puis lire ce fichier en entier avant de toucher à quoi que ce soit
```

**Prérequis sur une machine neuve** — à compléter au fil du lot 0 :

| Outil | Version | Posé ? |
|---|---|---|
| SDK .NET | `10.0.203` | Oui |
| Node | `24.15.0` | Oui |
| CLI Aspire | `13.5.3` | Oui |
| Azure CLI, connecté au bon abonnement | — | — |
| PowerShell 7 + modules Microsoft.Graph | `Authentication`, `Applications`, `Identity.SignIns`, `Users` pour `infra/entra/` | Oui — PowerShell `7.6.5`, modules Graph `2.39.0` installés localement |
| Docker | pour les images | — |

## En cours

Le prérequis de l'étape 4 est vérifié par le run GitHub Actions
`Database - verify point-in-time restore #33690143650` : une copie isolée a été restaurée
depuis la chaîne de sauvegardes Azure SQL au niveau S1, puis l'outil `DbSnapshot` a lu les
10 tables applicatives, dont `dbo.Users` avec 2 lignes. Le firewall et la copie
`vpd-sql-restore-33690143650` ont ensuite été supprimés. La décision est prise de ne pas
conserver les utilisateurs legacy : la migration 0 supprime toutes les lignes `Users` et
les recréations se feront dans Entra. Entra ne réplique pas automatiquement ses comptes
dans cette table ; la projection applicative sera réconciliée par le code d'identité du
déploiement 2.

La migration 0 est préparée dans `20260902223842_MigrateUsersToEntraIdentity` : elle retire
`Password`, `Salt` et `Role`, ajoute `ExternalId`, `CreatedAt`, `LastSeenAt` et
`AnonymizedAt`, rend le nom nullable et crée l'index unique filtré attendu. Elle est
explicitement non réversible car les identifiants legacy sont supprimés. La migration n'a
pas encore été appliquée à la base.

`L0-7` est terminé. `infra/parameters/main.dev.bicepparam` cible désormais Azure SQL `S1`
(`Standard`, 20 DTU, 250 Go) sans pause automatique, et le type
`infra/modules/SqlServer/types.bicep` documente explicitement les paramètres des paliers DTU.
Les deux fichiers Bicep compilent. Le run GitHub Actions `Infra - deploy #6` a déployé le
changement le 2026-09-02 ; le portail Azure confirme `Standard S1: 20 DTUs` pour
`vole-papillon-damour-db`. Le test manuel après plusieurs heures d'inactivité reste à faire.

`L0-8` est réalisé côté OVH le 2026-09-02. Les enregistrements de propriété, SPF, DKIM et
DMARC sont posés pour `mail.volepapillondamour.fr` ; le SPF racine existant et le TXT
Search Console n'ont pas été modifiés. OVH annonce une propagation pouvant durer jusqu'à
24 h.

`L0-9` est réalisé côté ressources : le locataire Entra External ID et la ressource ACS
Email sont créés. ACS est déployé par le run GitHub Actions `Infra - deploy #10` ; la
vérification du domaine d'envoi reste à relever après propagation DNS. Le choix retenu est
`vpd-acs-email-dev`, données en France, expéditeur `noreply@mail.volepapillondamour.fr` et
DMARC `p=none`. Aucun e-mail applicatif n'est envoyé avant `P3`.

`L0-10` est en cours avec l'option A : `MauiCashApp` cible désormais uniquement
`net10.0-android`, montée regroupée avec `L0-11` et MSAL comme prévu par `DT-15`.
Le mode actuel est le build direct de l'application. Aucun magasin de clés n'existe et aucun
keystore n'a été créé. La restauration Android passe, mais le build local est bloqué par
l'absence de SDK Android détectable (`XA5300`). La publication signée et la redistribution
durable sont reportées conformément au choix fait pour cette étape.

`L0-11` est en cours avec le déploiement 1 côté API : `Microsoft.Identity.Web` 4.14.2 est
enregistré, le schéma composite `Bearer` dirige les jetons Entra External ID vers le
schéma `Entra` et conserve le schéma `LegacyJwt` pour les sessions issues de
`/auth/login`. Les politiques `Tri`, `Caisse` et `Administration` sont posées ; l’alias
`IsAdmin` et l’inventaire JWT restent volontairement présents jusqu’aux déploiements 2 et
3. Les PR de préparation et d’API sont fusionnées dans `main`. PowerShell 7 et les
modules Graph sont maintenant installés localement ; `-UseDeviceCode` permet d’exécuter
les scripts Graph sans `az login`, en validant la connexion dans un navigateur séparé.
Les cinq enregistrements d’application et les principaux de service ont été créés par
`Configure-EntraApps.ps1` après simulation `-WhatIf` réussie ; les consentements vers
`access_as_user` ont été accordés. Le compte cible a reçu le rôle `Administration`,
attribution vérifiée par `Get-VpdUserRoles.ps1`. Le rapport est conservé hors dépôt dans
`%TEMP%\vpd-entra-dev.json`. Le déploiement 1 API est maintenant actif : le run GitHub
Actions `Infra - deploy #20` a injecté `AzureAd__ClientId` et `AzureAd__Audience`, puis
`API - deploy #7` a construit et déployé l’image `vpd-api:cfd43cb` sans migration de base ;
`GET /health` répond 200. Les déploiements Azure de cette branche passent par GitHub OIDC.
La migration de base 0 est fusionnée dans `main` mais n'est pas encore appliquée. Le
BackOffice est maintenant migré sur `main` après le merge de la PR #20 : les anciens
formulaires username/mot de passe, le cookie JWT, `@auth0/angular-jwt` et
`ngx-cookie-service` sont retirés ; les identifiants publics MSAL sont configurés dans les
environnements et le jeton d'API est ajouté aux requêtes Axios après acquisition silencieuse.
La caisse est en cours sur `feat/l0-11-maui-msal` : `Microsoft.Identity.Client` `4.88.0`,
acquisition silencieuse puis interactive de la portée `access_as_user`, handler Bearer sur
Refit et redirection Android `msal427c90de-bf59-4b01-af63-dc0799248496://auth`. Le test ciblé
du handler passe (1/1) et la restauration MAUI passe. La compilation Android locale reste
bloquée par l'absence du SDK Android (`XA5300`) ; aucun déploiement 2 n'a été effectué.
Pour un essai local, l'enregistrement SPA `vpd-backoffice-dev` doit contenir
`http://localhost:4200` en plus de l'URI de production ; cet état n'est pas confirmé par le
rapport Entra conservé ci-dessus.

L'étape 8 de `L0-11` est implémentée côté dépôt. `DELETE /catalog/me` crée une demande
d'effacement durable dans `OutboxMessages`, appelle Microsoft Graph avec l'application
`User.ReadWrite.All`, puis supprime ou anonymise la projection locale. Un `404` Graph est
traité comme « déjà supprimé » ; une panne avant la finalisation laisse la demande rejouable.
Le worker Functions partage `Application` et `Infrastructure`, réclame au plus 50 messages
avec un bail de cinq minutes et ouvre une portée DI par déclenchement. La migration
`20260903002636_AddAccountDeletionOutbox` est générée mais n'est pas encore appliquée à la
base. Le worker est intégré à l'AppHost pour le développement local ; sa ressource ACA et
son déploiement restent dans le périmètre ultérieur lié à `QT-02`/`P1-8`.

La configuration Bicep, le workflow d'infrastructure et `Configure-EntraApps.ps1` sont prêts
pour l'application `vpd-account-deletion-dev`, son secret hors dépôt et le secret Key Vault
`entra-graph-client-secret`. Aucun déploiement ni changement Entra n'a été effectué depuis
cette branche. Validation locale : solution `.slnx` compilée ; 78 tests backend passés ;
compilation Bicep et analyse syntaxique PowerShell passées.

La sonde `S0-2` est maintenant implémentée localement dans `src/Scan` : saisie ISBN,
scanette clavier, caméra via `BarcodeDetector`, conversion ISBN-10 → ISBN-13 et appel
consultation seule à `GET /books/{isbn13}/metadata`. L'API interroge la BnF SRU puis Open
Library en repli, sans session, IndexedDB, authentification ni écriture. L'AppHost expose
la sonde sur le port `4202` et l'API sur `5257` ; l'URL de développement suit l'hôte du
portable pour permettre l'essai depuis un téléphone sur le LAN. Les cibles `S0-1` sont
`≥ 90 %` de lecture au premier essai, `≥ 85 %` de notices trouvées et `≤ 3 s` par livre.
Validation locale : 92 tests backend ciblés, 9 tests ChromeHeadless, build de la solution,
build AppHost et builds Scan production/développement. Aucun déploiement ni test manuel de
campagne n'a encore été effectué.

`L0-6` est fusionné via la PR #6 : l'API expose `GET /health` avec un contrôle de connexion à
la base, et les sondes API readiness/liveness/startup ciblent `/health` sur le port `8080`.
La validation locale et la validation GitHub sont réussies ; la compilation MAUI locale reste
bloquée par l'absence du SDK Android natif (`XA5300`).

> Ce qui va ici : une étape commencée et non finie, avec **l'état exact** — quel fichier,
> quelle idée, ce qui reste. Écrire deux lignes ici coûte moins qu'une demi-heure de
> reconstitution.
>
> Cas particulier : `L0-11` se déploie **en trois passages** étalés sur plusieurs jours.
> Noter lequel est fait.

---

## En attente d'un délai externe

La propagation DNS et la vérification du domaine ACS sont en attente. La réputation du
domaine d'envoi reste volontairement anticipée, conformément à `L0-9`.

> Ce qui va ici : ce qui avance sans vous et qu'il faut penser à relever.
>
> | Sujet | Lancé le | Relevable à partir du |
> |---|---|---|
> | `QT-08` — session de 48 h puis ouverture en mode avion (page jetable, `L0-12`) | | |
> | Propagation DNS des entrées ACS | `2026-09-02` | après le délai OVH annoncé (maximum 24 h) |
> | Vérification du domaine d'envoi ACS | `2026-09-02` | après propagation DNS, lors du relevé dans Azure |
> | Réputation du domaine d'envoi | *(des semaines — lancer tôt)* | |

---

## État hors dépôt

**La section qui justifie ce fichier.** Tout ce qui a été fait à la main, ou qui existe
dans Azure sans être déductible du dépôt.

### Azure

| Ressource | État | Depuis |
|---|---|---|
| Base SQL | `S1` (`Standard`, 20 DTU, 250 Go), sans pause automatique ; confirmé dans le portail après `Infra - deploy #6` | `2026-09-02 18:27` |
| Sondes de santé | Paramètres API posés dans le dépôt (`/health`, port `8080`, `L0-6`) ; Azure non modifié | — |
| Container Apps | `api`, `website`, `backOffice` à `minReplicas: 1` | `36b0e50` |
| API Entra | `AzureAd__TenantId`, `AzureAd__ClientId` et `AzureAd__Audience` configurés ; image `vpd-api:cfd43cb` active ; `/health` répond 200 | `2026-09-03` |
| Locataire Entra External ID | Créé : `Vole Papillon Damour`, tenant ID `b23c80b3-9776-4840-8255-fcbf3b3500fd`, domaine `volepapillondamour.onmicrosoft.com`, France/Europe, rattaché à l'abonnement `Florian - 15-07-2026` | `2026-09-02` |
| Application Graph de suppression | **Pas encore créée** ; `Configure-EntraApps.ps1` est prêt à créer `vpd-account-deletion-dev` et à accorder `User.ReadWrite.All` | — |
| Secret Graph dans Key Vault | **Pas encore renseigné** ; dépend de l'exécution du script et des secrets GitHub `ENTRA_GRAPH_CLIENT_ID` / `ENTRA_GRAPH_CLIENT_SECRET` | — |
| ACS Email | Créé : `vpd-acs-email-dev` dans `rg-vpd-dev`, région ARM `global`, données en France, domaine `mail.volepapillondamour.fr`, expéditeur retenu `noreply@mail.volepapillondamour.fr` ; vérification du domaine en attente de propagation | `2026-09-02` |
| Plafonds journaliers App Insights | **Non posés** | — |
| Règles d'alerte | **Aucune** | — |

### DNS — `volepapillondamour.fr`

Domaine détenu et administré par l'association, main pleine et entière.

| Enregistrement | Lot | Posé ? | Le |
|---|---|---|---|
| `TXT` propriété + SPF + DKIM sur `mail` | `L0-8` | Oui — `TXT mail = "ms-domain-verification=57bdf09a-9c44-4816-b564-9a700cb19d07"`; `TXT mail = "v=spf1 include:spf.protection.outlook.com -all"`; `CNAME selector1-azurecomm-prod-net._domainkey` → `selector1-azurecomm-prod-net._domainkey.azurecomm.net`; `CNAME selector2-azurecomm-prod-net._domainkey` → `selector2-azurecomm-prod-net._domainkey.azurecomm.net` | `2026-09-02` |
| `DMARC` | `L0-8` | Oui — `TXT _dmarc.mail = "v=DMARC1;p=none;"` | `2026-09-02` |
| `TXT` Search Console | `L0-8` | Oui — déjà présent | `2026-09-02` |
| `CNAME` + `TXT asuid` sur `livres` | **Palier 2** — le `CNAME` a besoin du FQDN de la Container App du catalogue, qui n'existe pas avant | Non | — |

### Entra

| Élément | État |
|---|---|
| Locataire | Créé : `b23c80b3-9776-4840-8255-fcbf3b3500fd` (`volepapillondamour.onmicrosoft.com`) |
| Enregistrements d'application | Créés en réel le `2026-09-02` par `Configure-EntraApps.ps1` : `vpd-api-dev` → `ebc68507-2c07-4bab-9448-2d6d489c6112` ; `vpd-catalog-dev` → `9ceb5499-d273-4d7c-b0d0-047eff9f0541` ; `vpd-scan-dev` → `cabcb17b-537f-4d87-956b-60477103e0ec` ; `vpd-backoffice-dev` → `b5e7446e-2e87-4eed-8a6a-d40b3c913c9c` ; `vpd-caisse-dev` → `427c90de-bf59-4b01-af63-dc0799248496` |
| ApiClientId / portée | `ebc68507-2c07-4bab-9448-2d6d489c6112` / `api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user` |
| Comptes administrateurs recréés | `florian.drevet_magellangroup.eu#EXT#@volepapillondamour.onmicrosoft.com` — rôle `Administration` attribué puis vérifié le `2026-09-02 21:47:41` |
| Appareils de caisse mis à jour | **Aucun** — voir `L0-10` et `L0-11`, ils ne se mettent pas à jour tout seuls |

### Appareils de caisse

**Liste à tenir, elle ne se déduit de rien** — c'est ce qu'on cherchera à chaque
livraison, et le jour où `/auth/login` disparaît, un appareil oublié est un appareil hors
service.

| Appareil | Modèle / Android | Version installée | Mise à jour le |
|---|---|---|---|
| *(à recenser en `L0-10`)* | | | |

Le **magasin de clés de signature** de l'APK n'existe pas encore. Le choix actuel est le
build direct de l'application ; noter ici plus tard *où le magasin sera conservé et qui en
aura une copie* — jamais son mot de passe. Sa création et la redistribution signée sont
reportées.

### Secrets GitHub

*Les noms seulement, jamais les valeurs.*

| Secret | État |
|---|---|
| `ENTRA_GRAPH_CLIENT_ID` | À ajouter après l'exécution de `Configure-EntraApps.ps1` |
| `ENTRA_GRAPH_CLIENT_SECRET` | À ajouter avec le contenu du fichier hors dépôt produit par le script |

---

## Mesures faites

| # | Sujet | Résultat | Le |
|---|---|---|---|
| `QT-01` | Couverture des sources bibliographiques | — | — |
| `QT-02` | Déclencheur planifié à zéro réplica | — | — |
| `QT-03` | Lecture du code-barres au navigateur | — | — |
| `QT-04` | Dimensionnement Entra | Coût tranché : gratuit à notre échelle | doc |
| `QT-07` | Connexion seule, sans inscription | — | — |
| `QT-08` *(partie jeton, `L0-12`)* | Durée de vie des jetons hors ligne | — | — |
| `QT-08` *(partie geste, `P1-5`)* | Scan possible hors ligne après 48 h | — | — |
| `QT-09` | Tenue de `S1` sur disque dur | — | — |

**Chiffres cibles du palier 0** *(à écrire en `S0-1`, avant la campagne — pas après)* :

| Mesure | Cible |
|---|---|
| Taux de lecture au premier essai | `≥ 90 %` |
| Taux de métadonnées trouvées | `≥ 85 %` |
| Cadence tenable au bout de 200 livres | `≤ 3 s par livre` |

---

## Tests manuels passés

| Test | Résultat | Le |
|---|---|---|
| *(aucun)* | | |

> Un test manuel non consigné sera refait. Noter au minimum : quoi, quand, et ce qui a été
> observé — pas seulement « OK ».

---

## Journal

Une ligne par session de travail. Le plus récent en haut.

| Date | Machine | Ce qui a avancé |
|---|---|---|
| 2026-09-03 | Windows | **S0-1/S0-2 — sonde de faisabilité locale.** Fixation préalable des cibles à `≥ 90 %` de lecture au premier essai, `≥ 85 %` de notices trouvées et `≤ 3 s` par livre. Ajout de `src/Scan` : saisie ISBN, scanette clavier, caméra `BarcodeDetector`, normalisation ISBN-10/13 et affichage consultation seule. Ajout de `GET /books/{isbn13}/metadata` avec pipeline BnF SRU puis Open Library, parsing UNIMARC/JSON typé, et intégration AppHost sur `4202` avec API locale `5257` accessible depuis le LAN. La CI compile désormais la sonde. 92 tests backend et 9 tests ChromeHeadless passent ; les builds solution, AppHost, Scan production et développement passent. Aucun déploiement ni test manuel de campagne n'a été effectué. |
| 2026-09-03 | Windows | **L0-11 — étape 8, suppression coordonnée de compte.** Sur `feat/l0-11-account-deletion`, ajout du flux `DELETE /catalog/me` avec outbox durable : Graph est appelé avant la finalisation locale, le `404` est idempotent et le worker Functions rejoue les demandes échouées par lots de 50 avec bail de cinq minutes. Ajout de la migration `20260903002636_AddAccountDeletionOutbox`, de l'application Graph et du secret Key Vault dans les scripts/Bicep, ainsi que de l'intégration Aspire Functions locale. La solution compile et les 78 tests backend passent ; Bicep et PowerShell sont valides. Aucun déploiement Azure, secret ou objet Entra n'a été créé ; les tests manuels de suppression restent à faire. |
| 2026-09-03 | Windows | **L0-11 — caisse MSAL.** Après le merge de la PR #20 dans `main`, création de `feat/l0-11-maui-msal`. Passage de `MauiCashApp` à `net10.0-android` et ajout de `Microsoft.Identity.Client` `4.88.0`, avec `MsalAuthService` en silent-first, handler Bearer Refit et callback Android `msal427c90de-bf59-4b01-af63-dc0799248496://auth`. Le test ciblé passe (1/1) et `dotnet restore` passe ; `dotnet build` reste bloqué localement par `XA5300` (SDK Android absent). Aucun appareil ni déploiement n'a été modifié ; aucun keystore n'existe. |
| 2026-09-03 | Windows | **L0-11 — BackOffice MSAL.** Après le merge de la migration 0 dans `main`, création de la branche `feat/l0-11-backoffice-msal`. Migration du login vers le redirect MSAL Angular (`5.3.1`) avec MSAL Browser (`5.20.0`), remplacement du guard maison par `MsalGuard`, sélection de l'identité active au démarrage et acquisition silencieuse de la portée API pour Axios. Suppression du cookie JWT, des services/façades d'authentification maison et des dépendances `@auth0/angular-jwt`/`ngx-cookie-service`. `npm ci`, les 5 tests ChromeHeadless et les builds production/développement passent ; seules des alertes Angular préexistantes restent. Aucun déploiement n'a été effectué. |
| 2026-09-03 | Windows | **L0-11 — migration 0 préparée.** La décision est prise de perdre les utilisateurs legacy existants et de les recréer dans Entra ; le backup/restauration vérifié par le run `33690143650` couvre ce choix. La migration `20260902223842_MigrateUsersToEntraIdentity` supprime d'abord les lignes `Users`, retire `Password`, `Salt` et `Role`, ajoute les colonnes de projection Entra et l'index `ExternalId`. Elle est volontairement non réversible et n'a pas été appliquée à la base. Les tests backend (71) et le build `.slnx` passent ; les avertissements préexistants sont listés dans la sortie de validation. |
| 2026-09-03 | Windows | **L0-11 — prérequis de l'étape 4 vérifié.** Après le merge de la PR #18, le run GitHub Actions `Database - verify point-in-time restore #33690143650` a restauré un point-in-time Azure SQL dans `vpd-sql-restore-33690143650`, attendu son état `Online`, puis lu 10 tables avec `DbSnapshot`, dont `dbo.Users` (2 lignes). Le firewall et la base temporaire ont été supprimés. La migration 0 n'est pas commencée : le plan ne fixe pas la valeur initiale de `CreatedAt` et `LastSeenAt` pour les utilisateurs existants. |
| 2026-09-03 | Windows | **L0-11 — préparation de l'étape 4.** Ajout du workflow manuel `Database - verify point-in-time restore` dans la PR #18. Il restaure une copie Azure SQL isolée au niveau S1, vérifie la lecture des tables avec `DbSnapshot`, puis nettoie la copie ; aucun workflow n'a pu être lancé avant le merge car GitHub ne répertorie pas encore ce nouveau dispatch sur la branche par défaut. La migration 0 n'est pas commencée : le plan ne fixe pas la valeur initiale de `CreatedAt` et `LastSeenAt` pour les utilisateurs existants. |
| 2026-09-03 | Windows | **L0-11 — étape 3, déploiement 1 API.** Le `ClientId` Entra `ebc68507-2c07-4bab-9448-2d6d489c6112` est désormais la valeur dev par défaut des paramètres Bicep, avec surcharge possible par `ENTRA_API_CLIENT_ID`. Le `what-if` et le déploiement `Infra - deploy` sont passés via GitHub OIDC ; `API - deploy` a construit puis activé `vpd-api:cfd43cb` sans migration de base. `GET /health` répond 200 de façon stable. Les tests backend (71) et le build `.slnx` passent ; les tests manuels Entra restent à faire. |
| 2026-09-02 | Windows | **L0-11 — étapes 1–2, configuration Entra réelle.** Après une simulation `-WhatIf` réussie, création des cinq applications `dev` et des principaux de service, publication de `access_as_user` et attribution des consentements administrateur. AppId et `ApiClientId` sont consignés dans l’état Entra ci-dessus ; `Administration` a été attribué au compte cible et vérifié. Le rapport JSON reste dans `%TEMP%\vpd-entra-dev.json`. Aucun passage API/front/MAUI ni déploiement applicatif n’a été exécuté. |
| 2026-09-02 | Windows | **L0-11 — préparation de l’exécution Entra.** Les scripts déclarent désormais leurs modules Graph requis, dont `Microsoft.Graph.Identity.SignIns` pour les consentements OAuth2 ; le README contient les URI retenues (`https://volepapillondamour.fr`, `http://localhost:4300`, `https://backoffice.volepapillondamour.fr`) et le compte cible. L’exécution reste à faire depuis un PC autorisé : la connexion Azure CLI locale est refusée par Conditional Access, tandis que le déploiement Bicep passe par GitHub OIDC. |
| 2026-09-02 | Windows | **L0-11 — déploiement 1, API.** Ajout de `Microsoft.Identity.Web` `4.14.2`, du routage d’authentification composite `Bearer` vers Entra External ID ou le JWT historique, et des politiques `Tri`, `Caisse` et `Administration`. Les paramètres Bicep injectent l’autorité, le tenant et l’audience de l’API ; le `ClientId` sera renseigné après l’enregistrement Entra. `dotnet restore`, les 71 tests backend, le build `.slnx` et la compilation Bicep passent. Aucun déploiement ni enregistrement Entra n’a été exécuté ; la suppression du JWT et la migration de la base restent pour les passages suivants. |
| 2026-09-02 | Windows | **L0-11 — prérequis Entra.** Le script `Configure-EntraApps.ps1` fusionne désormais les URI de redirection au lieu de les écraser, pose l'URI Android `msal<clientId>://auth` pour `vpd-caisse`, et simule les cinq applications en mode `-WhatIf` même lorsqu'elles sont nouvelles. L'analyse syntaxique passe. L'exécution réelle du `-WhatIf` reste en attente de PowerShell 7 et des modules Microsoft.Graph ; aucune ressource Entra n'a été écrite. La migration API/fronts/MAUI attend encore le nom de l'application Graph de suppression, le nom du secret Key Vault et les versions MSAL non fixés dans le plan. |
| 2026-09-02 | Windows | **L0-10 — Android uniquement.** Choix de l'option A : `ShopAppVpd.csproj` cible désormais `net9.0-android` uniquement ; iOS, Mac Catalyst et Windows ne sont plus des cibles de compilation. Le README et la mémoire indiquent le build direct actuel. `dotnet restore` Android réussit ; `dotnet build` est bloqué localement par l'absence du SDK Android (`XA5300`). Aucun keystore n'a été créé, et les tests manuels d'installation/remplacement sur appareil réel restent à faire. La montée vers `net10.0-android` est conservée pour `L0-11`. |
| 2026-09-02 | Windows | **L0-8/L0-9 — ressources externes et DNS.** Création du locataire Entra External ID `Vole Papillon Damour` (`b23c80b3-9776-4840-8255-fcbf3b3500fd`, `volepapillondamour.onmicrosoft.com`) rattaché à l'abonnement du projet, et création/déploiement de `vpd-acs-email-dev` dans `rg-vpd-dev` avec données en France, domaine `mail.volepapillondamour.fr` et expéditeur `noreply@mail.volepapillondamour.fr`. Publication OVH de la preuve de domaine, du SPF `-all`, des deux CNAME DKIM et du DMARC `v=DMARC1;p=none;`. La propagation DNS et la vérification ACS restent à relever ; les tests manuels de `L0-9` n'ont pas été exécutés. |
| 2026-09-02 | Windows | **L0-7 — déploiement SQL.** Le run GitHub Actions `Infra - deploy #6` a appliqué le passage de la base `vole-papillon-damour-db` à `Standard S1: 20 DTUs` ; le portail Azure confirme le niveau fixe, sans pause automatique. Le test manuel après inactivité reste à faire. Le TXT Search Console était déjà présent dans la zone DNS. L0-8/L0-9 attendent les valeurs ACS non fixées par le plan. |
| 2026-09-02 | - | **L0-7 — base SQL en S1.** Passage du paramètre dev de `GP_S_Gen5_1` serverless à `S1` (`Standard`, 20 DTU, 250 Go, sans pause automatique) et alignement de la documentation du type `DatabaseSkuConfig` du module `SqlServer`. Les deux fichiers Bicep compilent. Le déploiement Azure, le contrôle du portail et le test manuel après inactivité restent à faire ; aucun changement hors dépôt n'a été effectué. |
| 2026-09-02 | - | **L0-6 — points de santé et sondes.** Ajout de `GET /health` avec contrôle de connexion SQL, test de l'enregistrement du contrôle, sondes API readiness/liveness/startup sur `/health:8080` dans les paramètres dev, et validation locale Aspire (`200 OK`, `Healthy`). La solution backend restaure, compile et passe ses 70 tests ; les deux fichiers Bicep compilent. Aucun déploiement Azure ni test manuel de révision cassée n'a été effectué. |
| 2026-09-02 | - | **L0-5 — CI de compilation et tests.** Ajout de `.github/workflows/ci.yml` sur `push` et `pull_request` : SDK .NET `10.0.203`, solution backend `.slnx`, tests Domain/Application/Infrastructure, workload et build MAUI Android, puis `npm ci`/`npm run build` pour BackOffice et Website avec `.nvmrc`. Validation locale backend et fronts réussie ; le workflow GitHub n'a pas été lancé, et la caisse reste localement bloquée par l'absence du SDK Android natif. |
| 2026-09-02 | - | **L0-4 — socle front reproductible.** Déplacement de `link-shared-ui.mjs` dans `src/SharedUi/scripts`, ajout des hooks `prebuild`/`prestart` dans Website et BackOffice, et résolution du lien vers les `node_modules` de l'application appelante. Test Node ciblé : 3/3 ; `npm ci` puis `npm run build` réussissent dans BackOffice seul, et Website compile également. |
| 2026-09-02 | - | **Images Docker — runtime et restore.** Le Dockerfile API copie `Directory.Packages.props` avant le restore centralisé et épingle son SDK sur `10.0.203`, car le contexte Docker ne contient pas le `global.json` racine. Les images Website et BackOffice utilisent désormais Node `24.15.0`, aligné sur `.nvmrc` et leurs champs `engines`. |
| 2026-09-02 | - | **L0-3 — migrations au démarrage.** À la demande, reprise du mécanisme de `infra-pipeline-editor` : `ProjectDbContext` est migré par un hosted service Infrastructure avant que l'API soit prête, avec stratégie d'exécution EF et source de trace `DbMigrations`. Une base SQL temporaire neuve a reçu les 10 migrations existantes avant l'écoute HTTP ; avec la base Aspire, `/actuality/latest` et `/asso-events` répondent HTTP 200. La spécification technique prévoit encore une migration explicite en déploiement ; décision à réarbitrer avant une production multi-réplique. |
| 2026-09-02 | - | **L0-3 — correction du lancement frontend.** `AddJavaScriptApp` conserve la commande npm, avec `--` ajouté avant les arguments Angular. `aspire run` démarre SQL, stockage, API, Website et BackOffice ; les ports 4200 et 4201 répondent HTTP 200. |
| 2026-09-02 | - | **Format des solutions.** Conversion des solutions backend et MAUI de `.sln` vers `.slnx`, puis suppression des anciens fichiers. La restauration et la compilation backend passent avec `.slnx`. La restauration MAUI reste bloquée localement faute du workload `maui-android`. |
| 2026-09-02 | — | **L0-3 — mise à jour.** Alignement du SDK AppHost, des hébergements SQL/stockage/JavaScript et de la CLI Aspire en `13.5.3`, avec `AspireUseCliBundle=true` dans l'AppHost, après vérification de la disponibilité des packages. `dotnet restore` et `dotnet build` passent sur cette version ; le lancement manuel de l'AppHost reste à faire. |
| 2026-09-02 | — | **L0-3.** Aspire, AppHost SDK et hébergements SQL/stockage/JavaScript en `13.4.6`. Remplacement de l'intégration legacy `Aspire.Hosting.NodeJs`/`AddNpmApp` par `Aspire.Hosting.JavaScript`/`AddJavaScriptApp`, avec conservation du script `start` et des arguments Angular. CLI Aspire `13.4.6`. `dotnet restore` et `dotnet build` passent ; le lancement manuel de l'AppHost reste à faire. |
| 2026-09-02 | — | **Arbitrages.** Caisse Android seule, `R-06` ramené en `L0-11`, `Q-07` tranchée (genres depuis les sources, aucun emplacement affiché), `ENF-21` réécrit : aucun repli d'exploitation. Plan débarrassé de ses échéances, tests reformulés pour être faits seul. |
| 2026-09-02 | — | **Revue du plan.** Lot 0 renuméroté (`L0-1` à `L0-13`) : ajout de l'épinglage Node, de la reproductibilité de `SharedUi`, de la préparation de la distribution de la caisse, et découpage de `L0-11` en trois déploiements. DNS du catalogue renvoyé au palier 2. `QT-07`/`QT-08` dotées d'un support de mesure. Lot 2 : stratégie de test des fronts en `P1-2`, `R-13`/`R-14` en `P1-5`, `R-24`/`R-30` en `P1-8`, repli d'exploitation en `P1-10`. Convention de renvoi `F-nn`/`T-nn`. **Rien d'implémenté.** |
| 2026-09-02 | — | Revue de la doc technique (30 constats `R-nn`), décisions `DT-11` à `DT-16`, chapitre observabilité, plan d'exécution et ce fichier. **Rien d'implémenté.** |

---

## Rituel de sortie

Avant de quitter une machine, dans cet ordre :

1. Mettre à jour **En cours**, **État hors dépôt** et **Journal** ci-dessus.
2. Commiter, **y compris du travail incomplet**, sur une branche de travail.
3. Pousser.

**Une branche non poussée est du travail perdu** dès qu'on change de machine.

Un commit de ce fichier seul se nomme `chore(plan): ...` et ne mélange pas de code : un
conflit se résout alors en gardant les deux moitiés, sans réfléchir.
