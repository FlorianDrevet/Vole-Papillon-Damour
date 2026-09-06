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
| **Lot en cours** | `P2/P3` — la refonte visuelle V2 de `origin/main`, le socle API/CQRS et les parcours Catalog membre/admin sont implémentés dans le worktree `feat/catalog-p2-p3`. PR [#72](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/72) ouverte vers `main`. |
| **Prochaine action** | Faire valider la PR #72, puis appliquer la migration sur l'environnement cible. Relever ensuite les heartbeats/mesures et vérifier ACS avec un cycle d'alerte réel. |
| **Dernière machine** | Windows — `C:\Users\flori\RiderProjects\Vole-Papillon-Damour-p2-p3` |
| **Dernière mise à jour** | 2026-09-06 — parcours Catalog P2/P3 intégrés, tests et smoke locaux passés |
| **Branche** | `feat/catalog-p2-p3` — worktree dédié, PR à ouvrir après validation |

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
| Fuseau horaire du module livres | **Instants UTC ; calendrier et minuit métier en `Europe/Paris`**, conversion centralisée dans Application (`DT-17`) |
| Outbox de scan | **`Pending` local puis décision finale ; annulation locale avant transmission, mouvement inverse après transmission** (`DT-18`) |
| Fusion de fiches | **Redirection ISBN vers une fiche canonique**, sans réécriture de `BookMovements` (`DT-19`) |
| Bourse ouverte | **Intervalle `[OpenAt, CloseAt)` des seuls `AssoEvents` de type `Books`**, chevauchements refusés (`DT-20`) |
| Tests du front Scan | **Jasmine/Karma/ChromeHeadless**, IndexedDB réel et transport réseau simulé ; pas de suite E2E en v1 (`DT-21`) |

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

### État actualisé — 2026-09-06

Le catalogue Angular `src/Catalog` a été migré vers la convention visuelle V2 validée : shell
avec logo réel et dropdowns, footer association en quatre colonnes, hero éditorial avec genre
et compteur API, filtres de recherche en colonne, cartes, fiches, œuvres, compte, pages légales
et cadre administration responsive. Le lien agenda `.ics` de la prochaine bourse est généré
localement à partir des données de l'API.

Les parcours P2/P3 sont maintenant raccordés : recherche locale séparée du référentiel
externe, suivi œuvre/édition, watchlist, préférence d'alertes, désinscription authentifiée,
et espaces administration pour tableau de bord, catalogue, sessions, désengorgement,
bourses, alertes, membres et paramètres. Les rôles applicatifs restent attribués dans
Entra ; les cartons physiques ne sont pas modélisés et aucune donnée n'est simulée lorsque
l'API est indisponible. La convention visuelle est dans
[`V2-CONVENTION.md`](docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md) et les
contrats de reprise dans
[`06-reprise-front-catalogue-p2-p3.md`](docs/bourse-aux-livres/06-reprise-front-catalogue-p2-p3.md).

Validation locale : `src/Catalog` — `55` tests ChromeHeadless et `npm run build` SSR +
navigateur ; `src/BackOffice` — `15` tests ChromeHeadless et bootstrap validé ; backend —
`82` Domain, `154` Application, `66` Infrastructure, `12` API et compilation de la solution.
Le smoke SSR retourne `200` pour les routes publiques et privées ; `/compte`,
`/administration` et `/desinscription` exposent `noindex, nofollow`. Les données du backend
public distant répondaient `503` pendant le contrôle, donc l'aperçu local affiche ses états
de repli sans déclarer le catalogue distant sain.

La tranche P2/P3 de cette branche complète le backend du module livres : contrats HTTP
administration, CQRS pour les fiches, quantités, retraits, annonces, fusions, recettes,
sessions, alertes, membres et paramètres ; recherche bibliographique externe Open Library ;
correction/retrait idempotent des mouvements de session ; et persistance de la recette
facultative d'une bourse via `20260906101759_AddBookFairRevenue`. Le membre peut également
suspendre ou réactiver ses alertes via `PATCH /catalog/me/alerts`, sans pouvoir contourner
un blocage administratif.

Le BackOffice et le Catalog exposent désormais `/administration` avec vue d'ensemble,
fiches/stock, bourses/statistiques, sessions de rattrapage, alertes, membres et paramètres.
La liste des contrats, états, limites et contrôles opératoires est maintenue dans
[`docs/bourse-aux-livres/06-reprise-front-catalogue-p2-p3.md`](docs/bourse-aux-livres/06-reprise-front-catalogue-p2-p3.md).

La migration doit encore être appliquée sur la base cible ; l'envoi ACS reste désactivé tant
que le domaine n'est pas vérifié. Le smoke local ne remplace pas un test Entra réel ni le
cycle e-mail de bout en bout.

### État actualisé — 2026-09-05

`P2` est livré dans `origin/main` au commit `f3fd148` (le code catalogue a été déployé au
tag `3a6e887`). L'API expose les lectures anonymes du
catalogue — recherche, fiche ISBN, prochaine bourse, page d'œuvre et sitemap XML — et les
routes membres protégées pour le compte, la liste de recherche et la suppression de compte.
La projection exclut les fiches masquées ou redirigées, conserve les livres épuisés, et sépare
les quantités disponibles des annonces futures.

`src/Catalog` est une application Angular SSR distincte du Website, du BackOffice et de la
Scanette. Elle contient l'accueil, la recherche, le catalogue par genre, les fiches livres,
les œuvres, les pages légales, un espace membre `/compte` avec connexion Microsoft, lecture et
retrait de watchlist, et demande de suppression de compte. Le rendu public reste sans traceur,
avec canoniques, `schema.org/Book`, `robots.txt` et sitemap dynamique. Le bloc « Pas encore
reçu » reste réservé au futur raccordement du référentiel externe ; l'envoi réel des alertes
reste désactivé tant que le domaine ACS n'est pas vérifié.

La PR #56 a livré la watchlist et l'espace compte. La revue post-merge a ensuite identifié un
défaut de regroupement : la BnF fournit souvent une notice sans `WorkId`, ce qui rendait les
listes « œuvre » presque toujours vides. La PR #58 enrichit uniquement ce champ depuis Open
Library quand nécessaire, conserve tous les champs BnF comme autorité, et garde la notice BnF
si Open Library est indisponible. Elle est fusionnée au commit `6a5a736`.

Le workflow `Books runtime - deploy` `33924236821` a construit et roulé API et Worker avec le
tag partagé `6a5a736` sans nouvelle migration — les migrations SQL étaient déjà appliquées par
`33922677695`. Le workflow `Scan - deploy` `33924618301`, lancé après la PR #57, a déployé le
wildcard MSAL `/scan/*` qui ajoute le bearer token aux routes imbriquées `/scan/catalog/delta`
et `/scan/sessions`. Les deux workflows ont réussi.

La PR #59 (`dfd8e69`) corrige l'équité de reprise du Worker : une panne fournisseur horodate
`LastAttemptAt` sans consommer le budget des réponses `NotFound`, les livres `Pending` échoués
attendent une heure avant reprise, et les lignes jamais tentées restent prioritaires. Le
workflow `Books runtime - deploy` `33926622823` a roulé API et Worker avec ce tag partagé,
sans migration ; les étapes SQL/firewall ont bien été ignorées.

La PR #61 (`dcc0c23`) corrige deux défauts relevés en revue : la recherche d'une suppression de
compte en attente est désormais compatible avec SQLite/Aspire tout en conservant la requête
SQL Server optimisée, et la finalisation supprime explicitement les projections strictement
membre (watchlist, historique d'alertes, rebonds et outbox `AlertEmail`) avant d'anonymiser ou
supprimer l'utilisateur. Les mouvements de livres et sessions historiques restent conservés
quand le plan l'exige. Les tests backend passent à `291`; les deux contrôles CI de la PR
(`33929259723`, `33929261525`) sont verts.

Le workflow `Books runtime - deploy` `33929828651` a ensuite construit et roulé les images
API/Worker `vpd-api:dcc0c23` et `vpd-worker:dcc0c23` depuis le même commit, avec
`run_migrations=false`. Le déploiement est réussi et les smoke tests post-rollout sont verts.

Le diagnostic du 2026-09-05 a reproduit le symptôme signalé sur le catalogue public :
les réponses HTTP arrivaient bien, mais les pages Angular zoneless de recherche, fiche livre
et fiche œuvre ne planifiaient pas de nouvelle détection après leurs subscriptions RxJS.
La correction locale appelle `ChangeDetectorRef.markForCheck()` et couvre ces trois parcours
par des tests asynchrones. Le même flux a révélé que `ScanBook` créait une fiche `Pending`
sans déclencher le point 6 prévu par `03-backend.md` ; l'API place désormais l'ISBN canonique
dans une file dédupliquée traitée hors réponse, avec le Worker horaire comme rattrapage.
Le worktree de résolution `fix/pr-67-conflicts` passe 58 tests ChromeHeadless Catalog,
319 tests backend et les builds API/Worker/Catalog. Le chemin Blob des couvertures prévu par
la PR a été écarté : `main` utilise désormais les URL HTTPS directes de la migration
`20260906101426_ReplaceBookCoverBlobWithDirectCoverUrl`. Aucun déploiement de cette correction
n'a encore été lancé.

Smoke du 2026-09-05 : catalogue, Scanette et API répondent `200`; `/catalog/me/watchlist` sans
jeton répond `401`; `/compte` et `/administration` portent `X-Robots-Tag: noindex, nofollow`;
`GET /books/9782070612758/metadata` répond une notice BnF avec `WorkId=OL10263W`. Les CNAME,
TXT `asuid` et certificats SNI managés de `livres.volepapillondamour.fr` et
`scan.volepapillondamour.fr` restent valides. Les URI publiques Entra du catalogue et de la
Scanette sont présentes ; l'URI localhost du catalogue n'a pas été ajoutée car elle ne sert
pas le déploiement public et sa sauvegarde nécessite une confirmation interactive dans Entra.
Le redirect Entra du catalogue a aussi été vérifié depuis `/compte` dans un profil Chrome déjà
authentifié : le bouton « Se connecter avec Microsoft » revient sur `/compte` avec le compte
actif et une watchlist vide. Aucun identifiant, consentement ou donnée de test n'a été saisi.

La PR #63 (`3a6e887`) a corrigé la cohérence SEO des routes privées du catalogue : la meta
HTML statique par défaut est `noindex, nofollow`, puis l'application recalcule la directive
sur chaque navigation (`index, follow` pour les routes publiques, `noindex, nofollow` pour
`/compte` et `/administration`). Le workflow `Catalog - deploy` `33932087193` a roulé
`vpdacrdev.azurecr.io/vpd-catalog:3a6e887`. Le smoke post-déploiement vérifie les deux
signaux `noindex` sur les routes privées, `index, follow` sur `/`, ainsi que `robots.txt`
et `sitemap.xml` en `200`.

### État actualisé — 2026-09-06 (worktree couvertures)

La branche `feat/book-cover-direct-urls`, dans le worktree
`C:\Users\flori\RiderProjects\Vole-Papillon-Damour-book-cover-url`, remplace la couverture
Blob par `Books.CoverUrl`, avec `CoverSource` et `CoverCheckedAt`. Le Worker ne télécharge
plus ni ne stocke de blob : il vérifie l'URL et réessaie les couvertures absentes après 30
jours. La résolution essaie BnF puis Open Library puis Google Books ; la BnF est considérée
comme indisponible lorsqu'elle renvoie son HTTP 500 historique, et Google Books n'est retenu
que pour une édition dont l'ISBN-13 correspond exactement. La clé Google est facultative et
vient de Key Vault lorsqu'elle est configurée.

Le catalogue et la Scanette partagent maintenant un placeholder éditorial sans rayures quand
aucune URL ne fonctionne. La migration `20260906101426_ReplaceBookCoverBlobWithDirectCoverUrl`
renomme la colonne, augmente sa longueur à 2048, ajoute les métadonnées de contrôle et
efface les anciennes références de blob afin que le Worker puisse les reconstituer. Cette
branche n'a pas encore été déployée sur Azure ; l'application de la migration et les smoke
tests de fournisseurs restent à faire après la PR. Le retrait de `book-covers` de Bicep
ne supprime pas un conteneur Azure existant en déploiement incrémental : après le smoke
test, vérifier l'absence de consommateurs puis supprimer explicitement ce conteneur.

Validation locale de cette branche : suite backend complète `297` tests, Catalog `45`
tests ChromeHeadless et build SSR/production, Scan `86` tests ChromeHeadless, `4` tests de
bootstrap et build de production, compilation Bicep du template et des paramètres, script EF
de migration valide, et `graphify update .` exécuté. Les avertissements déjà présents du
dépôt (vulnérabilités NuGet/npm et dépréciations) restent à traiter séparément.

Validation : `291` tests backend locaux pour la PR #61, CI #57/#58/#59/#61/#63 au vert, 79 tests
ChromeHeadless Scan et 4 tests de bootstrap sur la PR #57, builds front et conteneurs réussis,
34 tests ChromeHeadless Catalog, build Catalog et smoke SSR local réussis, `graphify update`
exécuté. Le CI `main` final `33934370102` a ensuite validé backend, tests, MAUI Android, les quatre
builds Angular et les trois images de conteneur. Les avertissements NuGet/npm et
dépréciations GitHub Actions restent
ceux du dépôt. Les relevés manuels `QT-02` du nouveau `Sweep`/`Enrich`, `P1-9`, `P1-10` et
`P1-11`, la vérification ACS et le cycle d'e-mail de bout en bout restent à faire ; ils ne
sont pas déclarés validés à distance.

Le récit historique du lot 0 et de P1-5 ci-dessous est conservé pour la traçabilité des
états précédents ; l'état courant est décrit dans les paragraphes ci-dessus et les tableaux
opérationnels ci-dessous.

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
été appliquée à la base de développement par `Books runtime - deploy` `33908408641` après
le backup vérifié.

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
`vpd-acs-email-dev`, données en France, expéditeur `DoNotReply@mail.volepapillondamour.fr` et
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
La migration de base 0 est fusionnée dans `main` et a été appliquée à la base de développement
par `Books runtime - deploy` `33908408641`. Le
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

Le correctif d'audience de `fix/backoffice-event-update-401` a été mergé dans `main` puis
déployé par l'infrastructure. Le `401` a disparu, mais les PUT BackOffice (`/asso-events/{id}`
et `/product/{id}`) renvoient désormais `403` avec un token qui porte bien
`roles=["Administration"]`. La cause est le mapping par défaut des claims JWT : la claim
Entra `roles` n'est plus reconnue par `RequireRole`. Le correctif `fix/backoffice-authorization-403`
désactive ce mapping pour le schéma Entra et ajoute une régression qui valide réellement
`IsInRole("Administration")`. Cette branche nécessite un déploiement applicatif API après
le merge ; l'infrastructure seule ne met pas à jour le code de l'image.

Après le déploiement applicatif API du correctif des rôles, les écritures fonctionnent. Un
nouveau défaut reste visible côté BackOffice : un refresh normal après connexion peut laisser
une page blanche, alors qu'un `Ctrl+Shift+R` la débloque. Le défaut est reproduit sur le domaine
public avec `BrowserAuthError: uninitialized_public_client_application` : `AuthSessionService`
lit le cache MSAL dans son constructeur avant l'initialisation de `PublicClientApplication`.
La branche courante ajoute `provideAppInitializer(() => inject(MsalService).initialize())`
et son contrat de bootstrap ; un déploiement BackOffice sera nécessaire après le merge.

Un correctif isolé est préparé sur `fix/backoffice-msal-bootstrap` dans le worktree
`C:\Users\flori\RiderProjects\Vole-Papillon-Damour-backoffice-login-fix`. La cause de
`NG05104` était l'absence de `<app-redirect>` dans `src/BackOffice/src/index.html` alors que
`MsalRedirectComponent` était bootstrappé ; MSAL ne s'initialisait donc pas et le clic de
connexion échouait avant toute requête réseau. L'autorité CIAM des deux environnements a
également été corrigée pour inclure le tenant. Le contrat de bootstrap (2 tests), les 5 tests
Angular, le build production et un smoke local qui atteint l'écran Microsoft passent. Aucun
déploiement n'a été effectué ; après merge, vérifier l'URI SPA
`https://backoffice.volepapillondamour.fr` et le parcours de redirection sur le domaine public.

L'étape 8 de `L0-11` est implémentée côté dépôt. `DELETE /catalog/me` crée une demande
d'effacement durable dans `OutboxMessages`, appelle Microsoft Graph avec l'application
`User.ReadWrite.All`, puis supprime ou anonymise la projection locale. Un `404` Graph est
traité comme « déjà supprimé » ; une panne avant la finalisation laisse la demande rejouable.
Le worker Functions partage `Application` et `Infrastructure`, réclame au plus 50 messages
avec un bail de cinq minutes et ouvre une portée DI par déclenchement. La migration
`20260903002636_AddAccountDeletionOutbox` a été appliquée avec les autres migrations par
`Books runtime - deploy` `33908408641`. Le worker est intégré à l'AppHost pour le développement local et dispose désormais
d'un module Bicep ACA natif (`kind=functionapp`), d'une identité managée dédiée, de secrets
Key Vault et d'un pipeline `Worker - deploy`. Il reste fixé à une réplique minimum et
maximum jusqu'à la mesure du timer.

La configuration Bicep, le workflow d'infrastructure et `Configure-EntraApps.ps1` sont prêts
pour l'application `vpd-account-deletion-dev`, son secret hors dépôt et le secret Key Vault
`entra-graph-client-secret`. Les secrets GitHub `ENTRA_GRAPH_CLIENT_ID` et
`ENTRA_GRAPH_CLIENT_SECRET` sont présents dans l'environnement `development`. Après le merge
de la PR #23, l'infrastructure a été déployée par `Infra - deploy` (`33758954044`), puis
`Scan - deploy` (`33759976040`) et `Worker - deploy` (`33759966121`) ont été exécutés depuis
le commit `728939f`. Validation locale : solution `.slnx` compilée ; 94 tests backend
passés ; compilation Bicep et analyse syntaxique PowerShell passées.

La sonde `S0-2` est maintenant implémentée dans `src/Scan` : saisie ISBN, scanette
clavier, caméra avec `@zxing/browser`/ZXing en mode `TRY_HARDER` (sans dépendre de
`BarcodeDetector`), analyse de toute l'image vidéo, sélection d'une photo sur iPhone,
conversion ISBN-10 → ISBN-13 et appel public à `GET /books/{isbn13}/metadata`. L'API
interroge la BnF SRU puis Open Library en repli. La tranche `P1-5` ajoute les trois
magasins IndexedDB, le verdict local, la file durable `Pending`/`Kept`/`Rejected`, la
restauration du dernier geste, les contrats delta/session/scan, l'idempotence par
`ClientSessionId`/`ClientGestureId`, l'authentification MSAL du bénévole `Tri` et la
vidange séquentielle au retour du réseau. L'image Scan est maintenant
conteneurisée avec nginx, son ingress HTTPS public est déclaré en Bicep et le workflow
`Scan - deploy` construit le bundle avec le FQDN public de l'API et l'URI de redirection
du FQDN Scan. Les cibles `S0-1` sont
`≥ 90 %` de lecture au premier essai, `≥ 85 %` de notices trouvées et `≤ 3 s` par livre.
La caméra et la photo utilisent le décodeur ZXing ; la photo réessaie des recadrages, une
réduction et un seuillage noir/blanc pour les cas difficiles. Comme l'application utilise
le mode Angular zoneless, le composant notifie explicitement le rendu après les callbacks
asynchrones de caméra, photo et API.
Validation locale : 94 tests backend, 22 tests ChromeHeadless, build de la solution et de
l'AppHost, ainsi que les builds Scan production/développement. Le build Scan production
présente uniquement un avertissement de budget initial non bloquant. Le smoke test Aspire du
2026-09-03 retourne `200 OK` pour l'ISBN `9783140464079`; l'API et le scan sont healthy,
le worker découvre `AccountDeletionSweepFunction` et acquiert son host lock. Le défaut de
configuration Entra local (`ClientId` absent) et les erreurs DI du worker ont été corrigés.
Le déploiement `development` est opérationnel : Scan `https://vpd-scan-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io`,
API `https://vpd-api-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io`, image
Scan `vpd-scan:f478a7d`, image Worker `vpd-worker:728939f`. `/health` du Scan répond `200`
et l'appel ISBN public répond `200`; le timer Worker s'est exécuté avec succès à `13:20 UTC`.
Après les PR #25 et #26, le correctif ZXing et le verrou npm multiplateforme sont fusionnés.
Le correctif de rafraîchissement zoneless, de lecture photo et de couverture est sur
`fix/scan-async-refresh` ; le repli de couverture essaie Open Library par ISBN lorsque la
source de la notice ne sert pas l'image, puis affiche un état explicite si les deux sources
échouent. Le run `Scan - deploy` `33778535757` a déployé l'image `vpd-scan:f478a7d` ; le
smoke test public sur l'ISBN `9782070612758` renvoie une fiche BnF avec une image chargée.
La caméra live et la photo ont été testées avec succès sur iPhone. La photo de test fournie,
prise sur un écran fortement moiré, reste un cas non fiable pour un décodeur navigateur.
La campagne de mesure `S0-4` a été réalisée le 2026-09-03 sur 300 livres réels ; le
retour manuel est concluant et le flux a fonctionné sur l'ensemble de la campagne. Les
sous-mesures détaillées (taux au premier essai, délai moyen, recours manuel, cadence à
200 livres et couverture des sources) n'ont pas été chiffrées dans cette reprise ; le
verdict `S0-5` est donc favorable sur le fonctionnement observé, sans inventer de
pourcentages. La suite engagée est `P1-1`, la mesure du réveil du worker sans réplica
chaud. Le déploiement de la configuration `minReplicas: 0`, `maxReplicas: 1` a réussi via
`Infra - deploy` `33780715179` sur le commit `4acfbb2`, terminé le 2026-09-03 à 16:50 UTC
(18:50 Europe/Paris). La fenêtre de deux heures est terminée, mais `QT-02` n'est pas
relevable depuis la session Azure disponible : le worker et `vpd-law-dev` répondent
`401 Aucun accès`, car le jeton du locataire `b23c80b3-9776-4840-8255-fcbf3b3500fd`
ne correspond pas au locataire de l'abonnement `91a30855-a777-43a6-8fad-66854b9a4d1b`.
Le worker reste gelé jusqu'à un relevé effectué avec une session du bon locataire.

`P1-2` est terminé côté conception : `DT-17` à `DT-21` fixent le temps UTC, les états de l'outbox, la fusion par redirection, l'intervalle d'une bourse ouverte et la stratégie de test du front Scan. `P1-3` est implémenté localement : les agrégats `Book`, `BookMovement`, `ScanSession`, `AssociationSettings`, l'entité `BookAnnouncement`, leurs configurations EF et la migration `20260903173750_AddBookExchangeCore` sont en place. `P1-4` couvre maintenant `ScanBook`, `OpenScanSession`, `CloseScanSession`, `RegisterSale`, `VoidSale`, `AdjustQuantity`, la lecture/écriture des paramètres, le rattachement des annonces sans date, les indicateurs de fiche, `ReassignSessionMode`, la correction manuelle des métadonnées, la suppression contrôlée d'une fiche, la mise en file des alertes, les actions d'administration `RG-45`, le traitement métier de rebond `RG-31`, son ledger d'idempotence fournisseur et son endpoint ACS/Event Grid : ISBN-10/13 normalisé, redirection canonique, verdict sans appel externe, stock/annonce en transaction, geste idempotent, horloge suspecte, clôture idempotente, inversion/rejeu de session, respect des verrous de champs manuels, groupement des alertes par membre avec délai configurable, annulation ciblée des messages `Pending`, envoi forcé par échéance immédiate, recalcul des alertes encore en attente après reprise de session, compteur de rebonds consécutifs et suspension au troisième échec. Une remise réussie remet le compteur consécutif à zéro sans réactiver automatiquement une liste suspendue ; un identifiant ACS déjà enregistré ne réincrémente pas le compteur, et un identifiant déjà lié à un autre membre est rejeté ; une correction automatique ne remplace jamais un champ verrouillé ; une suppression n'est permise que sans vente, mouvement ni annonce afin de préserver le ledger append-only. Les listes de recherche correspondent à un ISBN ou à une œuvre, les membres suspendus et le cooldown sont exclus, et une annonce sans date ne produit aucune alerte. La copie de `ClientGestureId` sur les annonces est portée par `20260903175445_AddClientGestureIdToBookAnnouncements`; le lien unique d'inversion des mouvements par `20260903181307_AddSaleReversalLink`; les tables de listes/historique par `20260903185500_AddBookWatchlistsAndAlerts`; le ledger des événements de rebond et son index unique par `20260903192839_AddEmailBounceEventLedger`. Le point d'entrée `POST /integrations/acs/email-delivery-reports` désérialise le schéma Event Grid typé, répond à la validation synchrone et protège les livraisons par le secret `X-Vpd-EventGrid-Secret`; les destinataires inconnus ou sans liste sont acquittés sans effet. `P1-5` ajoute maintenant les contrats API et la PWA Scan locale : delta compact avec entrées masquées et paramètres, file IndexedDB `Pending`/`Kept`/`Rejected`, restauration du dernier geste, authentification MSAL `Tri`, idempotence `ClientSessionId`/`ClientGestureId`, service worker et vidange séquentielle au retour du réseau. La migration `20260903211547_AddWatchlistUpdatedAt` couvre aussi les changements d'état des listes dans le filigrane. Validation locale finale : 72 tests de domaine, 93 d'application et 29 d'infrastructure ; la suite `.slnx`, le build de solution et le contrôle EF sans modèle en attente passent ; les 49 tests ChromeHeadless et les builds Scan production/développement passent également. Les migrations ne sont pas encore appliquées à Azure SQL. `QT-02` reste indépendante et le worker ne doit pas être modifié avant son relevé avec le bon locataire. Il reste la file de métadonnées, l'envoi worker et les vérifications physiques `QT-03`/`QT-08`.

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

La propagation DNS OVH est relevée et correspond aux valeurs demandées par ACS. La
vérification du domaine ACS est encore en cours dans le portail ; la réputation du domaine
d'envoi reste volontairement anticipée, conformément à `L0-9`.

> Ce qui va ici : ce qui avance sans vous et qu'il faut penser à relever.
>
> | Sujet | Lancé le | Relevable à partir du |
> |---|---|---|
> | Heartbeat du nouveau `Sweep`/`Enrich` (`P1-6`/`P1-7`) | `2026-09-05`, après le run `33929828651` | après quelques cycles du timer, puis campagne de deux heures si nécessaire |
> | Migration EF et rollout API/Worker | `2026-09-04`, run `33922677695` réussi ; rollout final `33929828651` | terminé ; smoke API/public validé, heartbeat encore à relever |
> | `QT-08` — session de 48 h puis ouverture en mode avion (page jetable, `L0-12`) | | |
> | Propagation DNS des entrées ACS | `2026-09-02` | relevée le `2026-09-04`, valeurs alignées |
> | Vérification du domaine d'envoi ACS | `2026-09-02` | le portail affiche encore « Verification is underway » |
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
| Container Apps | `api`, `website`, `backOffice`, `scan` à `minReplicas: 1`; `worker` à `minReplicas: 0`, `maxReplicas: 1`, privé et `Running`; domaines publics du catalogue et de la Scanette sécurisés | `2026-09-04` |
| API Entra | `/health` répond 200 et les PUT BackOffice fonctionnent après le déploiement du correctif audience + rôles ; le correctif de page blanche reste côté image BackOffice | `2026-09-03` |
| Locataire Entra External ID | Créé : `Vole Papillon Damour`, tenant ID `b23c80b3-9776-4840-8255-fcbf3b3500fd`, domaine `volepapillondamour.onmicrosoft.com`, France/Europe, rattaché à l'abonnement `Florian - 15-07-2026` | `2026-09-02` |
| Application Graph de suppression | Créée par `Configure-EntraApps.ps1` ; permissions/consentements et principal utilisés par le worker dev vérifiés dans le flux de déploiement | `2026-09-02` |
| Secret Graph dans Key Vault | Renseigné hors dépôt pour le worker dev ; les noms des secrets GitHub sont conservés sans leurs valeurs | `2026-09-02` |
| ACS Email | Créé : `vpd-acs-email-dev` dans `rg-vpd-dev`, région ARM `global`, données en France, domaine `mail.volepapillondamour.fr`, expéditeur réel `DoNotReply@mail.volepapillondamour.fr` ; le portail affiche encore « Verification is underway » | `2026-09-04` |
| API catalogue | Image `vpdacrdev.azurecr.io/vpd-api:dcc0c23` déployée avec le Worker par `Books runtime - deploy` run `33929828651`; `/health`, `/catalog/search`, `/catalog/sitemap.xml` et metadata BnF/Open Library répondent `200` | `2026-09-05` |
| Catalogue public | Image `vpdacrdev.azurecr.io/vpd-catalog:3a6e887` déployée par `Catalog - deploy` run `33932087193`; `/`, `/robots.txt`, `/sitemap.xml` répondent `200`, les routes privées sont `noindex` côté HTML et en-tête | `2026-09-05` |
| Runtime Books | API `vpd-api:dcc0c23` et Worker `vpd-worker:dcc0c23` construits depuis le même commit ; migrations EF déjà appliquées, étapes SQL/firewall ignorées, rollout réussi | `2026-09-05` |
| Plafonds journaliers App Insights | Déclarés dans `main.bicep` à 1 Go/jour par composant ; confirmation post-déploiement à relever | `2026-09-04` |
| Règles d'alerte | Déclarées dans `main.bicep` : heartbeat absent, annonces en retard, file d'alertes en retard ; confirmation post-déploiement à relever | `2026-09-04` |

### DNS — `volepapillondamour.fr`

Domaine détenu et administré par l'association, main pleine et entière.

| Enregistrement | Lot | Posé ? | Le |
|---|---|---|---|
| `TXT` propriété + SPF + DKIM sur `mail` | `L0-8` | Oui — `TXT mail = "ms-domain-verification=57bdf09a-9c44-4816-b564-9a700cb19d07"`; `TXT mail = "v=spf1 include:spf.protection.outlook.com -all"`; `CNAME selector1-azurecomm-prod-net._domainkey` → `selector1-azurecomm-prod-net._domainkey.azurecomm.net`; `CNAME selector2-azurecomm-prod-net._domainkey` → `selector2-azurecomm-prod-net._domainkey.azurecomm.net` | `2026-09-02` |
| `DMARC` | `L0-8` | Oui — `TXT _dmarc.mail = "v=DMARC1;p=none;"` | `2026-09-02` |
| `TXT` Search Console | `L0-8` | Oui — déjà présent | `2026-09-02` |
| `CNAME` + `TXT asuid` sur `livres` | **Palier 2** — `livres` → `vpd-catalog-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io`, TXT `asuid.livres` avec le jeton ACA | Oui — certificat managé `livres.volepapillondamour.fr-vpd-cae--260904173001`, binding SNI `Secured` | `2026-09-04` |
| `CNAME` + `TXT asuid` sur `scan` | **Palier 2** — `scan` → `vpd-scan-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io`, TXT `asuid.scan` avec le même jeton ACA | Oui — certificat managé `scan.volepapillondamour.fr-vpd-cae--260904173447`, binding SNI `Secured` | `2026-09-04` |

### Entra

| Élément | État |
|---|---|
| Locataire | Créé : `b23c80b3-9776-4840-8255-fcbf3b3500fd` (`volepapillondamour.onmicrosoft.com`) |
| Enregistrements d'application | Créés en réel le `2026-09-02` par `Configure-EntraApps.ps1` : `vpd-api-dev` → `ebc68507-2c07-4bab-9448-2d6d489c6112` ; `vpd-catalog-dev` → `9ceb5499-d273-4d7c-b0d0-047eff9f0541` ; `vpd-scan-dev` → `cabcb17b-537f-4d87-956b-60477103e0ec` ; `vpd-backoffice-dev` → `b5e7446e-2e87-4eed-8a6a-d40b3c913c9c` ; `vpd-caisse-dev` → `427c90de-bf59-4b01-af63-dc0799248496` |
| URI publiques | Vérifiées dans le portail après authentification MFA : `vpd-catalog-dev` contient `https://livres.volepapillondamour.fr` et `vpd-scan-dev` contient `https://scan.volepapillondamour.fr`, en conservant les URI locales/techniques existantes. |
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
| `ENTRA_GRAPH_CLIENT_ID` | Présent dans l'environnement `development` |
| `ENTRA_GRAPH_CLIENT_SECRET` | Présent dans l'environnement `development` ; valeur jamais écrite dans le dépôt |

---

## Mesures faites

| # | Sujet | Résultat | Le |
|---|---|---|---|
| `QT-01` | Couverture des sources bibliographiques | — | — |
| `QT-02` | Déclencheur planifié à zéro réplica | **Passé pour le timer historique `AccountDeletionSweepFunction`** : 28 `Executing`, 28 `Succeeded`, 28 messages de fin, sans trou sur la fenêtre UTC `2026-09-03 16:45`–`19:05` dans `vpd-law-dev` ; le nouveau heartbeat `Sweep` reste à observer après rollout | 2026-09-04 |
| `QT-03` | Lecture du code-barres au navigateur | Campagne `S0-4` déclarée réussie sur 300 livres ; sous-mesures détaillées non consignées | 2026-09-03 |
| `QT-04` | Dimensionnement Entra | Coût tranché : gratuit à notre échelle | doc |
| `QT-07` | Connexion seule, sans inscription | — | — |
| `QT-08` *(partie jeton, `L0-12`)* | Durée de vie des jetons hors ligne | — | — |
| `QT-08` *(partie geste, `P1-5`)* | Scan possible hors ligne après 48 h | — | — |
| `QT-09` | Tenue de `S1` sur disque dur | — | — |
| `P1-9` | Mesures SQL et traitement sur dataset de développement | Non chiffré : aucun benchmark reproductible n'a été exécuté dans cette reprise ; ne pas inventer de cadence ou de volume | 2026-09-04 |

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
| Smoke post-merge — domaines publics | Catalogue `/`, `/robots.txt`, `/sitemap.xml` et Scan `/` répondent `200`; certificats ACA managés `Secured`/SNI confirmés | `2026-09-05` |
| Smoke SEO post-déploiement — catalogue | `/` contient `index, follow`; `/compte` et `/administration` contiennent et renvoient `noindex, nofollow`; `robots.txt` et `sitemap.xml` répondent `200` | `2026-09-05` |
| Smoke post-merge — sécurité API/catalogue | `/catalog/me/watchlist` sans jeton répond `401`; `/compte` et `/administration` portent `X-Robots-Tag: noindex, nofollow` | `2026-09-05` |
| `Books runtime - deploy` `33924236821` | Build/push API + Worker sur le tag partagé `6a5a736`, migrations non requises, rollout des deux Container Apps réussi | `2026-09-05` |
| `Books runtime - deploy` `33929828651` | Build/push API + Worker sur le tag partagé `dcc0c23`, correctif suppression de compte, migrations non requises et étapes SQL/firewall ignorées, rollout des deux Container Apps réussi | `2026-09-05` |
| `Scan - deploy` `33924618301` | Image Scan reconstruite depuis `64c347e`, wildcard MSAL déployé, rollout réussi | `2026-09-05` |
| Smoke runtime API après les rollouts | `/health`, `/catalog/fairs/next`, metadata ISBN BnF/Open Library répondent `200`; les domaines publics catalogue et Scan répondent également `200` | `2026-09-05` |
| Smoke test Aspire — `GET /books/9783140464079/metadata` via `http://localhost:5257` | `200 OK` après redirection HTTPS ; notice « Le petit prince » renvoyée par Open Library | `2026-09-03` |
| Démarrage Functions worker via Aspire | `AccountDeletionSweepFunction` découverte ; host lock acquis ; aucune erreur DI ni erreur de listener | `2026-09-03` |
| `QT-02` — observation du timer historique dans `vpd-law-dev` | 28 exécutions, 28 succès, 28 complétions sur la fenêtre UTC observée, sans trou ; le nouveau `Sweep` reste à relever après déploiement | `2026-09-04` |
| POC Scan — détection caméra live sur iPhone | Détection réussie et parcours jusqu'à la fiche | `2026-09-03` |
| POC Scan — détection à partir d'une photo sur iPhone | Détection réussie et parcours jusqu'à la fiche | `2026-09-03` |
| Scan public — fiche BnF `9782070612758` après `Scan - deploy` `33778535757` | `200 OK`, couverture chargée dans Chrome (`103 × 150`) | `2026-09-03` |
| Campagne `S0-4` — 300 livres réels | Test manuel déclaré concluant ; le flux a fonctionné sur les 300 livres, sans relevé chiffré des sous-mesures | `2026-09-03` |

> Un test manuel non consigné sera refait. Noter au minimum : quoi, quand, et ce qui a été
> observé — pas seulement « OK ».

---

## Journal

Une ligne par session de travail. Le plus récent en haut.

| Date | Machine | Ce qui a avancé |
|---|---|---|
| 2026-09-06 | Windows | **PR #67 — résolution des conflits et revue de pertinence.** Les correctifs de détection zoneless sur recherche, fiche livre et fiche œuvre sont conservés, ainsi que l'enrichissement bibliographique ciblé après commit d'un scan avec le Worker horaire comme repli. Le chemin Blob des couvertures, devenu obsolète après `ReplaceBookCoverBlobWithDirectCoverUrl`, est retiré de la résolution. Validation : 58 tests ChromeHeadless Catalog, 319 tests backend, builds API/Worker/Catalog et `graphify update .` passés ; aucun déploiement effectué. |
| 2026-09-06 | Windows | **P1-10 — persistance métier de la caisse.** Correction du trou identifié dans la PWA Scan : `VALIDER` ne se contente plus d'effacer l'écran. Les ventes sont conservées dans un magasin IndexedDB dédié, décrémentent immédiatement la projection locale, puis sont rejouées vers `POST /scan/sales` avec `ClientGestureId` idempotent ; la réponse réconcilie `qtyAvailable` et `salesCount`. Le rôle `Caisse` est accepté par le front, le delta catalogue est partagé entre `Tri` et `Caisse`, et l'accès UI est filtré par rôle. Validation locale : 85 tests ChromeHeadless Scan, build production Scan et 292 tests backend ; aucun déploiement ni test manuel de coupure réseau à distance. Branche `fix/scan-cash-sales`, PR à ouvrir. |
| 2026-09-05 | Windows | **Clôture de la reprise nocturne.** PR #65 fusionnée en `f3fd148`; le CI `main` `33934370102` est vert après validation du backend, de MAUI Android, des quatre fronts et des trois images de conteneur. Le dépôt est propre et aucune PR n'est ouverte. La revue finale, les décisions et les gates encore ouvertes sont consignées ci-dessus et dans `docs/bourse-aux-livres/plan/DECISIONS-2026-09-04-overnight.md`. |
| 2026-09-05 | Windows | **Revue finale et traçabilité.** Relecture des changements PR #61 et #63 : la suppression locale est transactionnelle et nettoie les projections strictement membre avant anonymisation/suppression ; le chemin SQLite évite `JSON_VALUE` et le chemin SQL Server optimisé est conservé. Le correctif robots couvre les deux routes privées présentes dans le routage, met à jour le HTML SSR et la meta client, et le smoke live est cohérent. Aucun défaut bloquant supplémentaire trouvé. Point de maintenance : si une sous-route privée est ajoutée, étendre le helper robots, le middleware SSR et les tests. PR #64 est fusionnée en `d097792`; CI `main` `33933202774` vert. Les gates ACS, heartbeats, benchmarks et tests physiques restent ouvertes. |
| 2026-09-05 | Windows | **PR #63 — cohérence SEO et dernier déploiement catalogue.** La revue a détecté que le HTML statique générique rendait les routes privées `/compte` et `/administration` indexables malgré leur en-tête `X-Robots-Tag`. Le correctif pose `noindex, nofollow` par défaut et recalcule la directive sur chaque navigation publique/privée. Validation : 34 tests ChromeHeadless Catalog, build Catalog, smoke SSR local, CI PR #63 (`33931556397`, `33931558967`) et `graphify update .`. PR [#63](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/63) fusionnée en `3a6e887`; `Catalog - deploy` `33932087193` a roulé `vpd-catalog:3a6e887`. Le smoke HTTPS confirme `index, follow` sur `/`, `noindex, nofollow` sur les deux routes privées, et `200` sur robots/sitemap. Aucun changement DNS ou Entra n'était nécessaire. |
| 2026-09-05 | Windows | **PR #61 — revue et déploiement final.** Après reproduction TDD, le lookup de suppression de compte n'utilise plus `JSON_VALUE` dans le chemin SQLite/Aspire. La revue a aussi corrigé la cascade de confidentialité : watchlists, items, historique d'alertes, rebonds et outbox `AlertEmail` sont retirés avant anonymisation ou suppression, tandis que les mouvements et sessions historiques sont conservés. Validation : 291 tests backend, `git diff --check`, tests ciblés et `graphify update .`; les CI `33929259723` et `33929261525` sont vertes. PR [#61](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/61) fusionnée en `dcc0c23`. `Books runtime - deploy` `33929828651` a roulé API/Worker avec ce tag partagé, sans migration ; smoke API/catalogue/Scan, DNS et certificats sont verts. Les heartbeats `Sweep`/`Enrich`, ACS et les campagnes physiques restent à mesurer. |
| 2026-09-04 | Windows | **Runtime Books post-merge.** Le workflow `Books runtime - deploy` `33908408641` a été lancé depuis `main` (`585a0ac`) avec `run_migrations=true` : API et Worker partagent le tag `585a0ac`, toutes les migrations EF en attente ont été appliquées à Azure SQL, la règle firewall temporaire a été supprimée et le rollout a réussi. `/health`, les routes catalogue API et les domaines publics répondent `200`. Le prochain relevé est le heartbeat `Sweep`/`Enrich`; `P1-9` à `P1-11` restent à exécuter. |
| 2026-09-04 | Windows | **Scanette — retours de test terrain.** Depuis `origin/main` fraîchement récupéré dans le worktree `Vole-Papillon-Damour-scan-workflow`, la consultation et la caisse démarrent automatiquement la caméra et la relancent après chaque lecture ; la caisse affiche ses livres sous le cadre et permet de retirer n’importe quelle ligne. `RG-04` signale un ISBN répété en moins de cinq secondes, le verdict est compacté, et la fin de session synchronise/clôt la session distante avant d’effacer le snapshot local afin qu’un nouveau tri ne reprenne pas l’ancien. Validation : 74 tests ChromeHeadless, contrat bootstrap, build production Scan et `graphify update .` ; aucun déploiement. Le contrôle visuel des écrans authentifiés et le retest iPhone restent à faire. PR [#52](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/52) ouverte. |
| 2026-09-04 | Windows | **Correctif catalogue/Scan — publication et prochaine bourse.** Le catalogue choisit désormais la bourse à venir selon `DateStart` et reconstruit les heures civiles depuis la date de la bourse, ce qui corrige le faux « 3 mars 2027 » provoqué par des heures historiques. La Scanette synchronise automatiquement les décisions, refuse de terminer avec un dernier geste `Pending`, puis conserve une demande de clôture jusqu'à confirmation serveur. Validation : 273 tests backend, 78 tests ChromeHeadless et build de production Scan. Après le rollout du runtime Books et des migrations par le run `33908408641`, les contrôles Azure en lecture seule confirment encore `books: []` et la bourse de mars sur `/catalog/fairs/next`, tandis que les événements Website listent la bourse du 7 au 12 octobre ; il reste à déployer ce correctif, puis à laisser rejouer la session locale ou à rescanner si IndexedDB a été perdue. |
| 2026-09-04 | Windows | **Déploiement post-merge et authentification.** Après le merge de la PR #45 (`9f8ec55`), les workflows `Catalog - deploy` `33906639354`, `Scan - deploy` `33906624368` et `Infra - deploy` `33906654599` sont réussis. Les deux domaines répondent en HTTPS ; les URI publiques des inscriptions `vpd-catalog-dev` et `vpd-scan-dev` sont visibles dans Entra. Le redirect Scan atteint l'écran applicatif et applique correctement le contrôle du rôle `Tri`. Le runtime API/Worker et les migrations SQL restent à lancer explicitement. |
| 2026-09-04 | Windows | **Correctif Website — horaires des trois prochains événements.** Depuis `origin/main`, le worktree `Vole-Papillon-Damour-fix-home-event-hours` corrige la carte d'accueil pour reprendre la même règle que la fiche : `hourOpenDoors` pour les bourses aux livres, `dateStart` pour le loto et les autres événements. Le test de non-régression couvre les trois types ; 67 tests ChromeHeadless, le build Website et `graphify update .` passent. Contrôles navigateur lecture seule à 390 px et 1280 px ; aucun déploiement ni changement API. PR #47 ouverte depuis la branche `fix/home-event-hours`. |
| 2026-09-04 | Windows | **Catalogue — activation API.** Le run `API - deploy` `33903473628` a construit et déployé `vpd-api:dc4ec74` sans migration SQL. Après activation de la révision, `/catalog/search` et `/catalog/sitemap.xml` répondent `200`; les smoke tests HTTPS du catalogue (`/`, `/robots.txt`, `/sitemap.xml`) et de la Scanette (`/`) répondent `200`. |
| 2026-09-04 | Windows | **Catalogue + Scan — domaines publics.** Depuis le worktree `feat/catalogue-deploy-auth`, validation OVH des CNAME/TXT `asuid` vers les deux Container Apps et validation Azure des certificats managés SNI `Secured` pour `livres.volepapillondamour.fr` et `scan.volepapillondamour.fr`. Alignement Bicep, Dockerfile, workflow Scan, documentation et URI de redirection Scan sur les origines canoniques. Validation locale : 4 tests de contrat, 61 tests ChromeHeadless, build production Scan, build Docker et compilations Bicep. Les URI Entra publiques ont ensuite été vérifiées dans le portail et le rollout Scan post-merge est réussi. |
| 2026-09-04 | Windows | **Correction du smoke metadata.** Le middleware API mappe désormais l'exception d'indisponibilité des fournisseurs bibliographiques vers `503 Service Unavailable` au lieu de `500`; le résolveur conserve son exception afin que le Worker réessaie plutôt que d'écrire un cache négatif. Test API dédié, 4 tests du résolveur et build API passent. Aucun déploiement applicatif n'a été lancé pour ce correctif ; le endpoint DEV a été retesté séparément en `200` lorsque Open Library était disponible. |
| 2026-09-04 | Windows | **P1-5 — nouveau visuel Scanette.** Intégration locale des écrans de maquette : accueil et choix de session, tri avec verdicts colorés, bandeau hors ligne, saisie manuelle, fin de session, caisse et consultation sans écriture. Ajout de la consultation catalogue locale sans geste d'outbox et conservation de la décision de mode dans IndexedDB. Validation : 53 tests ChromeHeadless, build production Scan et contrôle responsive navigateur à 390 px/1280 px. Aucun déploiement ; la persistance métier de caisse et les gates physiques restent séparées. |
| 2026-09-04 | Windows | **P1-6 à P1-8 — worker, qualité runtime et déploiement.** Ajout de `Sweep`/`Enrich`, fermeture des sessions inactives, release/rattachement des annonces, enrichissement bibliographique avec cache négatif et couvertures Blob, livraison d'alertes ACS désactivée par défaut, bourses Books annulables, plafonds/alertes App Insights, CORS par origines et workflow runtime avec migrations avant rollout. Corrections TDD de l'isolation des types dans l'outbox de suppression de comptes et de la rétention des utilisateurs référencés par l'historique Books. Suite backend complète : 247 tests passés ; build de solution sans erreur. La vérification ACS est encore « underway », et les gates physiques/P1-9 restent non déclarables à distance. |
| 2026-09-03 | Windows | **P1-5 — contrats API, PWA Scan et synchronisation hors ligne.** Ajout des contrats `GET /scan/catalog/delta`, ouverture/rejeu de session, scans et clôture sous autorisation `Tri`, avec projections compactes, suppressions masquées et reprojection lors des changements de listes. Le Scan conserve désormais `catalog`/`outbox`/`session` dans IndexedDB persistant, calcule le verdict local, restaure le dernier geste, protège les appels par MSAL et rejoue la file séquentiellement au retour du réseau ; le service worker ne met en cache que la coquille et les métadonnées publiques. Ajout de la migration locale `20260903211547_AddWatchlistUpdatedAt`, générée avec reprise de `CreatedAt` pour les lignes existantes. Validation finale : 72 tests Domain, 93 Application, 29 Infrastructure, 49 ChromeHeadless, build solution, contrôle EF et builds Scan production/développement. Aucun changement Azure, DNS, secret, déploiement ou migration de production ; `QT-02`, `QT-03` et `QT-08` restent à relever hors dépôt. |
| 2026-09-03 | Windows | **`QT-02` — relevé tenté après la fenêtre d'observation.** La session Azure ouverte est authentifiée dans le locataire `b23c80b3-9776-4840-8255-fcbf3b3500fd`, alors que l'abonnement et `vpd-law-dev` attendent `91a30855-a777-43a6-8fad-66854b9a4d1b` ; le worker et les journaux répondent `401 Aucun accès`. Aucun changement Azure n'a été effectué ; le worker reste gelé jusqu'à une session du bon locataire. |
| 2026-09-03 | Windows | **P1-4 — transport ACS/Event Grid et handshake (`RG-31`).** Ajout de `POST /integrations/acs/email-delivery-reports`, protégé par le secret partagé `X-Vpd-EventGrid-Secret`. L'endpoint désérialise les contrats Event Grid typés, renvoie `validationResponse` pour `SubscriptionValidationEvent`, transmet les rapports ACS non réussis au handler de rebond et acquitte les statuts livrés/étendus, les destinataires inconnus et les membres sans watchlist. Validation : 72 tests Domain, 88 Application, 29 Infrastructure, suite `.slnx` et build ; aucune configuration Azure ni migration n'a été appliquée. |
| 2026-09-03 | Windows | **P1-4 — ledger d'idempotence des rebonds (`RG-31`).** `RecordEmailBounce` accepte l'identifiant fournisseur, le valide, enregistre chaque événement ACS une seule fois dans `EmailBounceEvents` et protège cette identité par un index unique ; un rejeu séquentiel retourne l'état courant sans incrémenter `BounceCount`, et un identifiant déjà associé à un autre membre est rejeté. Ajout de la migration locale `20260903192839_AddEmailBounceEventLedger`, non appliquée à Azure. Validation : 72 tests Domain, 84 Application, 29 Infrastructure, suite `.slnx` et build ; le transport ACS/Event Grid, l'handshake et l'exposition API restent à faire. |
| 2026-09-03 | Windows | **P1-4 — traitement métier des rebonds (`RG-31`).** `Watchlist` compte les échecs consécutifs, suspend les alertes au troisième rebond et réinitialise le compteur après une remise réussie sans réactiver automatiquement une liste suspendue. `RecordEmailBounce` persiste la transition dans une transaction et renvoie uniquement le compteur/statut. Validation : 70 tests Domain, 82 Application, 29 Infrastructure ; aucun changement de schéma ni action Azure. L'identifiant d'événement fournisseur, le transport ACS/Event Grid, l'exposition API et l'appel worker restent à faire. |
| 2026-09-03 | Windows | **P1-4 — actions d'administration sur les alertes (`RG-45`).** Ajout en TDD de `CancelBookAlerts` et `ForceBookAlerts`. L'annulation ne touche que les lignes `AlertEmail` `Pending` de la session ; l'envoi forcé libère un éventuel bail et ramène `DueAt` à l'instant UTC, pour laisser le worker effectuer l'envoi. `ReassignSessionMode` annule puis recalcule les alertes encore en attente dans la même transaction, sans recréer celles déjà envoyées. Validation : 67 tests Domain, 80 Application, 29 Infrastructure ; aucun changement de schéma et aucune action sur Azure. Le rappel de rebond, le transport et `QT-02` restent ouverts. |
| 2026-09-03 | Windows | **P1-4 — listes de recherche et mise en file des alertes.** Ajout en TDD de `Watchlist`, `WatchlistItem`, `UserAlertHistory` et `BookAlertOutbox`. À la clôture, les entrées correspondantes sont groupées par membre dans une seule ligne `AlertEmail`, avec `DueAt` configurable, cooldown anti-répétition et exclusion des membres suspendus ; les annonces `PROCHAINE BOURSE` sans date restent hors file. Ajout de la migration `20260903185500_AddBookWatchlistsAndAlerts`, sans application Azure. Validation : 67 tests Domain, 77 Application, 27 Infrastructure, suite `.slnx`, build et EF sans modèle en attente. L’envoi worker, l’annulation/envoi forcé, les rebonds et le transport restent à faire ; `QT-02` reste ouverte. |
| 2026-09-03 | Windows | **P1-4 — métadonnées et suppression contrôlée.** Ajout en TDD de `UpdateBookMetadata` et `DeleteBook`. Les champs manuellement sélectionnés sont fusionnés dans `ManuallyEditedFields` et ne peuvent plus être remplacés par un rafraîchissement automatique ; une suppression n'est acceptée que si la fiche n'a aucun mouvement ni annonce, et les ventes sont refusées explicitement. Validation : 61 tests Domain, 76 Application, 21 Infrastructure, suite `.slnx`, build et contrôle EF sans modèle en attente. Aucun schéma ni endpoint n'a changé ; l'outbox d'alertes, le transport et `QT-02` restent à traiter. |
| 2026-09-03 | Windows | **P1-4 — socle interne élargi.** Ajout en TDD de `RegisterSale`/`VoidSale` (bourse ouverte, stock nul, horloge suspecte, rejeu et inversion tracée), `AdjustQuantity`, paramètres d'association, rattachement des annonces sans date, indicateurs rare/masqué et `ReassignSessionMode` (inversion/rejeu des entrées de session). La migration `20260903181307_AddSaleReversalLink` rend chaque mouvement d'inversion unique et traçable. Validation : 57 tests Domain, 70 Application, 21 Infrastructure, suite `.slnx`, build et modèle EF sans changement en attente. Aucun endpoint, worker, appel bibliographique ou déploiement de migration n'a été ajouté ; `QT-02` reste ouverte. |
| 2026-09-03 | Windows | **P1-4 — premier flux métier du module livres.** Ajout en TDD de `ScanBook`, `OpenScanSession` et `CloseScanSession`. Le scan normalise ISBN-10/13, suit une redirection canonique, calcule `RG-15` depuis les données internes, écrit mouvement + compteur + éventuelle annonce dans une transaction, rejoue sans doublon via `ClientGestureId`, conserve `OccurredAt`/`ReceivedAt` et marque les horloges suspectes. L'ouverture interdit deux sessions actives par bénévole et la clôture est idempotente. `ClientGestureId` est aussi recopié sur `BookAnnouncements` via la migration `20260903175445_AddClientGestureIdToBookAnnouncements`. Validation : 57 tests Domain, 47 Application, 21 Infrastructure, migration sans changement en attente et build `.slnx` sans erreur. Aucun endpoint, worker, appel bibliographique ou déploiement de migration n'a été ajouté ; `QT-02` reste ouverte. |
| 2026-09-03 | Windows | **P1-3 — domaine et persistance.** Ajout des agrégats `Book`, `BookMovement`, `ScanSession`, `AssociationSettings`, de l'entité `BookAnnouncement`, des identifiants forts, des invariants UTC/quantité/cycle, des cinq configurations EF et de la migration `20260903173750_AddBookExchangeCore`. La migration porte `rowversion`, collation `Latin1_General_100_CI_AI`, contrôles de redirection/quantité et index filtrés d'idempotence/session. Validation : 49 tests Domain, 21 tests Infrastructure et `dotnet test Vole_Papillon_Damour.slnx --no-restore` passent. La migration n'est pas appliquée à la base ; `QT-02` reste ouverte. |
| 2026-09-03 | Windows | **P1-1 — mesure du timer.** Après la campagne `S0-4` concluante sur 300 livres, passage du worker à `minReplicas: 0`, `maxReplicas: 1` via `Infra - deploy` `33780715179` (commit `4acfbb2`). L'observation de deux heures est ouverte jusqu'à 20:50 Europe/Paris ; `QT-02` reste à relever dans les journaux. |
| 2026-09-03 | Windows | **P1-2 — décisions de conception.** Pendant la fenêtre d'observation `P1-1`, ajout de `DT-17` à `DT-21` dans `docs/bourse-aux-livres/technique/01-decisions.md` : instants UTC et calendrier `Europe/Paris`, outbox à états, fusion par redirection ISBN, définition de bourse ouverte et tests Scan par synchronisation isolée avec IndexedDB/transport simulé. Les règles métier, le modèle de données et les flux techniques sont alignés ; `P1-3` a pu commencer localement sans toucher au worker. |
| 2026-09-03 | Windows | **Correctif BackOffice — refresh après connexion.** Le domaine public reproduit une page blanche sur refresh normal avec `uninitialized_public_client_application`, car `AuthSessionService` lit le cache avant l'initialisation MSAL. Ajout de `provideAppInitializer` autour de `MsalService.initialize()`, contrat de bootstrap en échec puis au vert, 9 tests ChromeHeadless et build production passants. Un déploiement BackOffice reste à faire après merge. |
| 2026-09-03 | Windows | **Correctif BackOffice — rôles Entra et 403.** Après le déploiement de l'audience, reproduction TDD d'un token v2 portant `roles=["Administration"]` : `IsInRole("Administration")` échoue avec le mapping JWT par défaut. `MapInboundClaims = false` est activé pour le schéma Entra, le test passe, et une nouvelle branche `fix/backoffice-authorization-403` est préparée pour un déploiement applicatif API. |
| 2026-09-03 | Windows | **Correctif BackOffice — audience Entra v2.** Après `git pull` de `main`, création du worktree `fix/backoffice-event-update-401`. Reproduction TDD du `401` avec un token dont `aud` est l'ID d'application API ; alignement de `AzureAd:Audience` dans `appsettings.Development.json`, `infra/main.bicep` et `Configure-EntraApps.ps1`. Validation : 97 tests backend, compilation Bicep, 9 tests ChromeHeadless et build BackOffice. Aucun déploiement Azure ; le retest des PUT `/asso-events/{id}` et `/product/{id}` reste à faire. |
| 2026-09-03 | Windows | **L0-11 — correctif BackOffice MSAL.** Sur `fix/backoffice-msal-bootstrap`, ajout de l'hôte `<app-redirect>` requis par `MsalRedirectComponent` et correction de l'autorité CIAM tenant-scoped dans les environnements BackOffice. Ajout d'un test de contrat de bootstrap ; 2 tests de bootstrap, 5 tests Angular, le build production et un smoke local jusqu'à l'écran Microsoft passent. Aucun déploiement n'a été effectué. |
| 2026-09-03 | Windows | **S0-2 — couverture de la fiche.** Le résultat garde d'abord l'URL fournie par la notice, essaie une couverture Open Library par ISBN si l'image échoue, puis rend un placeholder accessible si les deux sources sont indisponibles. Le test de non-régression porte ces deux cas ; le Scan passe 24 tests ChromeHeadless et ses builds production/développement. Les détections caméra live et photo sur iPhone ont été confirmées ; le correctif est déployé par `Scan - deploy` `33778535757` avec l'image `vpd-scan:f478a7d`. La campagne `S0-4` sur 300 livres reste à faire. |
| 2026-09-03 | Windows | **S0-2 — rendu asynchrone et lecture photo.** Sur `fix/scan-async-refresh`, ajout de la notification explicite du rendu Angular zoneless après recherche ISBN, détection caméra et analyse photo. Le fallback photo essaie des recadrages, réductions et variantes noir/blanc ; le message d'erreur est rendu immédiatement. `npm test -- --watch=false --browsers=ChromeHeadless` passe avec 22 tests, ainsi que les builds Scan production/développement. La photo fournie d'un écran moiré reste illisible en test navigateur ; le merge, le déploiement et le retest iPhone sur code imprimé restent à faire. |
| 2026-09-03 | Windows | **S0-2 — correction de détection iPhone.** Après le test réel où la caméra s'activait mais ne reconnaissait pas le code ISBN, remplacement de `html5-qrcode` par `@zxing/browser`. Le scanner analyse toute l'image avec `TRY_HARDER` et les formats EAN-13/EAN-8, UPC et QR ; le cadre affiché reste un repère visuel. Ajout de la couverture de tests du moteur et clarification de l'aide utilisateur. `npm ci`, 15 tests ChromeHeadless et le build production passent ; l'image Azure et le test manuel attendent le merge/déploiement. |
| 2026-09-03 | Windows | **S0-2 — publication HTTPS et validation Azure.** Après le merge de la PR #23 (`728939f`), le déploiement de l'infrastructure, du Scan et du Worker est passé par GitHub OIDC. Le Scan public répond `200` sur `/` et `/health`, l'appel ISBN `9783140464079` répond `200`, et le Worker `kind=functionapp` est `Healthy` avec une révision unique, sans ingress public. Le timer `AccountDeletionSweepFunction` s'est exécuté avec succès à `13:20 UTC` (`CompletedCount: 0`). Les secrets ne sont pas exposés ; le test manuel sur iPhone et la campagne `S0-4` restent à faire. |
| 2026-09-03 | Windows | **Correctif local API/worker après le smoke test S0-2.** Le `ClientId` et l'audience Entra dev sont renseignés dans `appsettings.Development.json`, ce qui supprime l'`IDW10106` sur l'endpoint metadata anonyme. Le worker n'enregistre plus l'authentification API ni MediatR/Mapster inutiles ; il démarre avec son seul service de suppression de comptes. L'AppHost transmet désormais les connexions de stockage Aspire et laisse `AzureWebJobsStorage` à l'intégration Functions, au lieu de forcer Azurite sur `127.0.0.1:10000`. Validation : API, Scan et worker healthy, host lock acquis, ISBN `9783140464079` en `200 OK`, 94 tests backend et build Release de la solution sans erreur. |
| 2026-09-03 | Windows | **S0-1/S0-2 — sonde de faisabilité locale.** Fixation préalable des cibles à `≥ 90 %` de lecture au premier essai, `≥ 85 %` de notices trouvées et `≤ 3 s` par livre. Ajout de `src/Scan` : saisie ISBN, scanette clavier, caméra `BarcodeDetector`, normalisation ISBN-10/13 et affichage consultation seule. Ajout de `GET /books/{isbn13}/metadata` avec pipeline BnF SRU puis Open Library, parsing UNIMARC/JSON typé, et intégration AppHost sur `4202` avec API locale `5257` accessible depuis le LAN. La CI compile désormais la sonde. 92 tests backend et 9 tests ChromeHeadless passent ; les builds solution, AppHost, Scan production et développement passent. Aucun déploiement ni test manuel de campagne n'a été effectué. |
| 2026-09-03 | Windows | **L0-11 — étape 8, suppression coordonnée de compte.** Sur `feat/l0-11-account-deletion`, ajout du flux `DELETE /catalog/me` avec outbox durable : Graph est appelé avant la finalisation locale, le `404` est idempotent et le worker Functions rejoue les demandes échouées par lots de 50 avec bail de cinq minutes. Ajout de la migration `20260903002636_AddAccountDeletionOutbox`, de l'application Graph et du secret Key Vault dans les scripts/Bicep, ainsi que de l'intégration Aspire Functions locale. La solution compile et les 94 tests backend passent ; Bicep et PowerShell sont valides. Aucun déploiement Azure, secret ou objet Entra n'a été créé ; les tests manuels de suppression restent à faire. |
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
