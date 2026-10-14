# Migration de West Europe vers France Central

**Statut d'exécution :** bascule France Central réalisée le 14 octobre 2026. Les ressources cibles, l’ACR partagé, les pipelines de déploiement (branches de livraison), les cinq domaines HTTPS et la réécriture des URL Blob sont en place. Le What-If et le déploiement d’alignement Bicep ont réussi (`38636020253`, `38636607517`). Les anciennes ressources sont conservées pour le retour arrière ; leur suppression attend au moins sept jours de stabilisation et une confirmation explicite du propriétaire. Les PR VPD #255 et Mariage #8 sont ouvertes, non fusionnées.
**Révision :** 14 octobre 2026
**Groupe de ressources conservé :** rg-vpd-dev

Les sections 1 à 7 conservent le périmètre, les contraintes et l’analyse de conception. Pour l’avancement réel, les validations restantes et le nettoyage, suivre les sections 8 à 10.

## État réel au 14 octobre 2026

- L’ACR partagé `vpdacrshared` est créé dans `rg-core-florian`, en France Central, SKU Basic, accès administrateur désactivé. Les 300 tags des six dépôts VPD ont été copiés et vérifiés par digest. Les 99 tags des deux dépôts `mariage-edwige-henri` ont aussi été importés ; ses workflows backend/frontend ont réussi avec OIDC (runs `38627063181` et `38627062615`). Les workflows VPD ont été adaptés dans la branche de livraison et leur CI a réussi (`38633773357`, `38636004188`). Les identités de déploiement ont les rôles requis sur l’ACR et les identités ACA ont AcrPull. Les PR VPD [#255](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/255) et Mariage [#8](https://github.com/FlorianDrevet/mariage-edwige-henri/pull/8) sont ouvertes. L’ACR contient environ 39 GB, au-dessus des 10 GB inclus au SKU Basic.
- L’environnement Container Apps et les six applications, le stockage `vpdstdevfc`, le Key Vault, Log Analytics, les six Application Insights, les alertes et tests de disponibilité sont déployés en France Central dans `rg-vpd-dev`.
- La copie initiale puis la synchronisation finale de `vpdstdev` vers `vpdstdevfc` sont terminées. Le workflow `38634898966` a vérifié chaque nom de blob source sans en trouver d’absent ; aucun blob plus récent ne restait à recopier. L’aperçu `38633960661` a identifié 346 URL SQL. L’application `38634898966` a réécrit les 346 lignes, avec zéro ancienne URL restante dans chacune des six colonnes contrôlées. La base SQL est conservée ; aucun déplacement de base ni déploiement EF n’a été lancé.
- L’API cible est saine sur `/health` et utilise une image reconstruite depuis la révision de l’API actuellement en production. Website, BackOffice, Catalogue et Scan ont aussi été reconstruits depuis leurs révisions actuellement en production, avec l’API France Central. Le Worker France Central est sain avec une réplique (min=0/max=1) ; le Worker West Europe n’a aucune réplique au contrôle. Vérifier les déclenchements planifiés et notifications pendant la validation.
- Dans la zone DNS OVH, `volepapillondamour.fr` pointe vers `51.138.199.49`, et les CNAME `www`, `backoffice`, `livres` et `scan` pointent directement vers les FQDN ACA France Central. Les cinq domaines répondent `200` en HTTPS avec validation TLS réussie ; la résolution a été vérifiée depuis un résolveur public. L’ancien A record `20.4.178.125` correspondait à l’adresse de l’environnement ACA West Europe. Aucun service CDN actif n’a été identifié dans les services OVH consultés ; le routage configuré est DNS direct vers ACA.
- Le serveur/base SQL, ACS et les anciennes ressources West Europe sont conservés. L’API cible utilise le stockage France Central ; ACS/Event Grid vise le webhook France Central. Aucun nettoyage n’a été lancé. Le nettoyage attend au moins sept jours de stabilisation ainsi que l’accord explicite du propriétaire.

## 1. Décision et périmètre

La région West Europe refuse les nouveaux déploiements pour cet abonnement. La migration se fera par remplacement parallèle dans le même groupe de ressources : créer les ressources applicatives équivalentes en France Central, valider le nouvel environnement, basculer les pipelines et le trafic, puis supprimer les anciennes ressources après la période de retour arrière.

Décisions confirmées :

- Conserver le groupe rg-vpd-dev. Sa propriété de région reste West Europe : elle décrit l’emplacement de ses métadonnées ARM, pas celui de toutes ses ressources. Les nouvelles ressources régionales seront explicitement créées en France Central.
- Garder le serveur Azure SQL vpd-sql-dev et la base vole-papillon-damour-db en place. Il n’y aura ni copie/restauration SQL, ni changement de schéma, ni migration EF pour ce chantier. La base auxiliaire vpd-sql-import reste également en place.
- Créer un nouvel ACR partagé en France Central dans `rg-core-florian`. Bicep reçoit le nom, le groupe de ressources et l’abonnement de l’ACR au lieu de créer systématiquement `vpdacrdev`.
- Garder les noms de domaines publics déjà utilisés par les ACA et les associer aux nouvelles applications correspondantes.
- Ne supprimer aucune ressource West Europe avant la validation de la bascule et la fin de la fenêtre de retour arrière.

La migration du stockage implique la copie de ses blobs. Si la base contient des URL Blob absolues, le workflow existant les réécrit dans la même base après validation. C’est une mise à jour ciblée de références, pas un déplacement de la base ou de ses données métier.

## 2. État de référence constaté

Inventaire en lecture seule du 14 octobre 2026. Il faudra le rafraîchir avant l’exécution : noms, configuration, rôles, volumes, coûts et enregistrements DNS doivent être vérifiés au début du chantier.

| Surface | Source actuelle | Cible et traitement |
| --- | --- | --- |
| Groupe de ressources | rg-vpd-dev, métadonnées en West Europe | Le même groupe ; ne pas le recréer et ne pas modifier sa région de métadonnées |
| ACR | vpdacrdev, West Europe, encore conservé pour le retour arrière | `vpdacrshared`, `rg-core-florian`, France Central, Basic, admin désactivé ; les dépôts VPD et `mariage-edwige-henri` y sont importés et les workflows correspondants poussent vers ce registre |
| Stockage Blob | vpdstdev, West Europe | Nouveau compte France Central ; copie de tous les conteneurs et de leurs blobs |
| Key Vault | vpd-kv-dev, West Europe | Nouveau coffre France Central ; reprise contrôlée des secrets et des autorisations |
| Container Apps | Environnement vpd-cae-dev et six applications en West Europe | Nouvel environnement et six applications France Central, avec des noms distincts pendant la coexistence |
| Supervision | vpd-law-dev et six Application Insights en West Europe, règles Azure Monitor et tests | Nouvel espace Log Analytics, nouvelles instances Application Insights et nouvelles règles ciblant les ressources France Central |
| SQL | vpd-sql-dev et vole-papillon-damour-db, déjà en France Central ; vpd-sql-import aussi en France Central | Conserver les mêmes ressources et chaînes de connexion SQL |
| Génération de titres | vpd-actuality-title-dev, déjà en France Central | Conserver ; vérifier seulement les références et permissions |
| Communication | ACS Email et ACS Communication Service, ressources globales, domaine mail.volepapillondamour.fr vérifié | Conserver sans recréation ni réamorçage DNS ; ne pas réinitialiser la vérification du domaine |
| Identités | Identités des applications et vpd-github-deploy-id ; cette dernière est en France Central | Conserver les identités réutilisables ; vérifier l’isolation régionale de chacune avant attachement aux nouvelles ACA |
| DNS/CDN | Zone OVH avec un A apex et quatre CNAME directs, auparavant vers l’environnement ACA West Europe | Les cinq valeurs DNS sont maintenant orientées vers l’environnement France Central. Aucun produit CDN actif n’a été trouvé dans les services consultés ; pas de CDN à reconfigurer dans l’état constaté |

Les domaines déjà déclarés dans main.dev.bicepparam sont :

- volepapillondamour.fr et www.volepapillondamour.fr vers Website ;
- backoffice.volepapillondamour.fr vers BackOffice ;
- livres.volepapillondamour.fr vers Catalog ;
- scan.volepapillondamour.fr vers Scan.

L’API et le Worker utilisent aussi leurs FQDN ACA techniques ; le Worker reste privé. Les nouvelles applications conserveront les mêmes domaines publics que leurs homologues actuelles.

## 3. Règles de conception Bicep

Le template actuel utilise env.location à la fois pour la région du groupe de ressources et pour les ressources régionales. Il faut séparer ces deux notions avant de changer la cible :

1. Garder explicitement l’emplacement actuel des métadonnées de rg-vpd-dev.
2. Ajouter une région de ressources paramétrable, définie à francecentral pour la cible.
3. Employer un suffixe ou des noms de ressources paramétrés pour distinguer la cible pendant la coexistence. Un suffixe -fc est une proposition, pas un nom arrêté. Respecter les contraintes de longueur et d’unicité globale du stockage, du coffre et de l’ACR.
4. Retourner dans les outputs Bicep les noms et FQDN réellement déployés, pour que les workflows ne reconstruisent pas les noms à partir d’une convention implicite.
5. Laisser la ressource de groupe existante intacte. Le What-If doit montrer des ajouts en France Central, sans suppression ni tentative de déplacement des ressources West Europe.

Paramètres Bicep à prévoir :

- région des nouvelles ressources ;
- suffixe de coexistence ou noms cibles explicites ;
- nom et groupe de ressources de l’ACR partagé, et identifiant d’abonnement facultatif si le registre est un jour dans une autre souscription ;
- noms cibles du stockage, du Key Vault, de l’environnement ACA, des six apps, du workspace et des six Application Insights ;
- noms de certificats du nouvel environnement, distincts des certificats liés à l’ancien environnement ;
- possibilité d’utiliser les ressources SQL et ACS existantes sans les recréer.

Le module actuel de l’ACR doit devenir une référence vers un registre existant. Les affectations de rôles seront faites au scope de cet ACR, y compris quand son groupe de ressources diffère de rg-vpd-dev. Bicep devra donner aux identités ACA l’accès au coffre, à la supervision et au registre, et configurer dans le coffre les références de connexion requises vers le stockage cible.

Les identités managées doivent être inventoriées avant réutilisation. Une identité à isolation régionale ne peut être associée qu’à une ressource source dans sa région ; si une identité actuelle est limitée à West Europe, créer son équivalent France Central et reprendre ses rôles. Une identité sans isolation régionale peut être réutilisée. Les modules d’identité Bicep ne doivent donc pas hériter aveuglément de la nouvelle région : référencer les identités réutilisées comme existantes, ou créer une identité suffixée seulement si l’isolation régionale l’impose.

Les modules SQL, ACS, Event Grid et Foundry/Cognitive Services ne doivent pas être déplacés avec le paramètre général de région. Garder leurs références et emplacements existants. Le déploiement d’infrastructure ne doit pas appliquer de migration EF à la base partagée.

## 4. ACR partagé

L’ACR n’est pas déplacé en place : créer le registre cible en France Central puis importer les images depuis vpdacrdev. Azure documente cette méthode de relocation par recréation et import des images.

Étapes :

1. Lister les projets, dépôts, tags, digests, tailles, dernières utilisations et toutes les identités qui poussent ou tirent des images. La suppression de vpdacrdev attendra la confirmation que les autres projets ont aussi cessé de l’utiliser.
2. Créer par Azure CLI un registre France Central de nom globalement unique, dans rg-vpd-dev pour ce premier chantier, sauf si l’inventaire des autres projets justifie dès maintenant un groupe central dédié. Le paramètre Bicep de groupe de ressources gardera le choix indépendant du code applicatif.
3. Choisir le SKU à partir du volume d’images, du débit et des fonctions attendues pour tous les projets ; vérifier les limites et le coût d’un ACR partagé. Garder l’accès administrateur ACR désactivé, et reporter les règles réseau, le chiffrement, la rétention et le nettoyage selon la configuration actuelle et les besoins réels.
4. Vérifier le mode d’affectation des rôles du registre avant de les créer. En mode ABAC, les rôles AcrPull/AcrPush ne donnent pas les accès aux dépôts : utiliser les rôles compatibles correspondants.
5. Donner aux identités CI/CD autorisées de chaque projet les droits complets demandés sur le registre et ses dépôts. En mode ABAC, cela implique les permissions de contrôle du registre et le rôle Container Registry Repository Contributor sans condition de dépôt. Ne donner aux identités d’exécution des ACA que les droits de lecture nécessaires au pull des images. L’identité d’infrastructure seule gère les affectations RBAC.
6. Importer tous les dépôts et tags utiles, préserver les digests et les tags immuables, puis vérifier la présence et le pull d’un échantillon de chaque application. Pendant la période de préparation, éviter qu’une nouvelle publication ne soit présente que dans un seul registre : publier dans les deux ou effectuer une dernière synchronisation avant bascule.
7. Modifier chaque pipeline de ce dépôt et chaque projet consommateur pour utiliser le nom et le groupe de ressources du registre partagé. Tester authentification OIDC, login, push, pull et déploiement avant d’envisager la suppression de vpdacrdev.

Les pipelines de ce dépôt actuellement concernés par le nom du registre sont infra-deploy, api-deploy, website-deploy, backoffice-deploy, scan-deploy, catalog-deploy, worker-deploy et books-runtime-deploy.

Référence : [relocation ACR entre régions](https://learn.microsoft.com/en-us/azure/container-registry/manual-regional-move), [import d’images ACR](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-import-images), [rôles ACR et permissions ABAC](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-abac-repository-permissions).

## 5. Stockage et Key Vault

### Stockage

Créer le compte cible en France Central avec les paramètres compatibles de vpdstdev : SKU/réplication, réseau, chiffrement, suppression réversible, règles de cycle de vie, diagnostics, accès public et conteneurs. Les conteneurs configurés aujourd’hui incluent loto-images, actuality-images, event-images, product-images et livres-rares ; l’inventaire Azure fera foi pour les éventuels conteneurs supplémentaires.

Le workflow storage-migrate.yml est réutilisable, mais ses valeurs actuelles ne correspondent pas à cette migration : source par défaut vpdstowebsite et cible vpdstdev. Le corriger pour copier vpdstdev vers le nouveau compte France Central. Vérifier les statuts de copie, le nombre et la taille des blobs, les métadonnées, le type d’accès et l’ouverture de chaque fichier échantillon. Le compte source reste intact pendant la fenêtre de retour arrière.

Prévoir deux copies : copie initiale pendant que la source est encore utilisée, puis copie différentielle finale après suspension des écritures Blob. Prévisualiser toute réécriture des URL avant son application et conserver le rapport permettant de restaurer les anciens préfixes. Si la base contient des URL du type *.blob.core.windows.net, remplacer uniquement l’hôte du compte source par celui de la cible, dans la base existante, avec le workflow actuel et son ouverture temporaire du pare-feu SQL. Fermer la règle du runner même en cas d’échec.

Au changement de trafic, les nouvelles ACA utilisent le secret de connexion du nouveau stockage. Empêcher l’ancienne application de reprendre des écritures dans l’ancien compte après la dernière copie ; sinon les deux comptes divergeraient. Le retour arrière doit inclure une copie inverse des blobs écrits après la bascule et, si nécessaire, le retour des URL en base.

Référence de copie Microsoft : [copier des blobs avec AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-blobs-copy).

### Key Vault

Créer un coffre France Central avec un nom distinct, puis rétablir le modèle d’accès, purge protection, réseau, journaux et tags réellement retenus. Reconstituer les secrets par leur source approuvée (variables GitHub protégées ou coffre actuel), sans les écrire dans le dépôt, les logs ou les outputs Bicep. Reprendre les rôles des identités API et Worker ainsi que les références de secrets déclarées dans les ACA.

Le secret SQL gardera le même serveur et la même base ; seul le secret de connexion Blob change pour pointer vers le nouveau compte. Vérifier également les secrets Entra, JWT, ACS, webhook, Worker, Google Books et Instagram selon les valeurs effectivement configurées. Une vérification de noms et de dates d’expiration ne doit jamais afficher les valeurs.

## 6. Supervision

Créer le workspace France Central puis les six Application Insights liés à ce nouveau workspace : API, Website, BackOffice, Scan, Catalog et Worker. Injecter les nouvelles connection strings dans les paramètres de déploiement ; Website, BackOffice, Scan et Catalog les embarquent au build, donc reconstruire leurs images.

Redéployer avec des noms temporaires distincts les alertes, tests de disponibilité et classeurs qui doivent coexister avec ceux de West Europe. Les règles cibles doivent viser les nouveaux composants et le nouveau workspace. Réutiliser le groupe d’action d’e-mail existant s’il est compatible, pour éviter de doubler les notifications. Ne pas créer en parallèle deux alertes identiques qui enverraient le même incident.

Les données historiques Log Analytics/Application Insights ne sont pas déplacées par la création du nouveau workspace. Garder l’ancien workspace jusqu’à expiration de la rétention requise ou exporter les données utiles. Vérifier les diagnostics plateforme des Container Apps et les requêtes de santé après bascule avant de supprimer l’ancien workspace.

Contrôles : événements API/Worker, traces SSR et navigateur, exceptions, dépendances SQL/Storage, alertes 5xx/P95/SQL, heartbeat Worker, statut ACS, tests HTTP et classeur VPD - Performance et santé. Conserver les seuils DEV et le destinataire déjà configurés.

## 7. Container Apps, domaines, OVH CDN et pipelines

Créer un nouvel environnement ACA France Central et six applications avec des noms différents des applications West Europe. Réutiliser les images du nouvel ACR et les paramètres de capacité, probes, mise à l’échelle, ingress, tags et identités après validation de leur compatibilité régionale.

| Application | Nom actuel | Domaine public à rattacher à la cible |
| --- | --- | --- |
| Website | vpd-web-ca-dev | volepapillondamour.fr et www.volepapillondamour.fr |
| BackOffice | vpd-bo-ca-dev | backoffice.volepapillondamour.fr |
| Catalog | vpd-catalog-ca-dev | livres.volepapillondamour.fr |
| Scan | vpd-scan-ca-dev | scan.volepapillondamour.fr |
| API | vpd-api-ca-dev | FQDN technique ACA ; conserver le domaine public si un domaine API est ajouté ultérieurement |
| Worker | vpd-worker-ca-dev | Aucun ingress public ; aucun domaine |

Les certificats managés actuels appartiennent à l’ancien environnement. Créer et vérifier de nouveaux certificats sur le nouvel environnement, avec de nouveaux noms. Préparer les preuves TXT asuid correspondantes. Toutefois, les certificats managés Azure exigent que le CNAME d’un sous-domaine pointe directement vers l’ACA cible ; pour le domaine racine, l’enregistrement A doit pointer vers l’adresse IP de l’environnement. Comme le DNS pointe encore vers l’ancien ACA, on ne peut pas garantir l’émission du certificat cible avant la bascule avec le seul certificat managé Azure.

Avant la fenêtre de bascule, choisir et tester l’une de ces voies TLS :

- Obtenir un certificat client valide pour les domaines, l’importer dans l’environnement ACA cible et le configurer aussi sur OVH CDN si le CDN termine le TLS public. Documenter son renouvellement et son remplacement avant expiration.
- Si le trafic doit rester directement sur ACA, déplacer le DNS vers l’ACA cible pendant une fenêtre planifiée, puis faire émettre les certificats managés Azure. Accepter que la mise en service dépende du délai d’émission et ne pas annoncer une bascule sans interruption avant validation réelle.

Azure indique qu’un CNAME intermédiaire, comme un CDN, bloque l’émission ou le renouvellement de ses certificats managés. Ne pas mettre OVH CDN devant un domaine ACA en comptant sur ces certificats. Lier les mêmes domaines aux nouvelles ACA reste requis, mais la méthode de certificat doit correspondre à l’architecture DNS retenue.

État DNS observé : les sous-domaines publics pointent directement vers l’ancien FQDN West Europe ; aucun routage CDN n’est visible dans ces enregistrements. Dans OVH, relever le ou les services CDN, les domaines déclarés, l’origine, le certificat, les règles de cache et la fonction bypass. Si un CDN est actif, mettre son origine à jour vers les FQDN ACA France Central avant la bascule, mais garder les changements DNS pour la fenêtre de cutover. La documentation OVH demande un CNAME vers son endpoint CDN pour les sous-domaines et précise qu’un domaine racine ne peut pas utiliser de CNAME ; employer uniquement le type de record racine pris en charge par le service OVH et tester le bypass. Ne jamais mettre en cache l’API, l’authentification, BackOffice ou les réponses personnalisées ; documenter les règles exactes pour les assets publics.

Au cutover, mettre à jour les CNAME des sous-domaines dans la zone OVH vers l’endpoint CDN retenu, ou vers le FQDN ACA si le CDN ne peut pas être validé dans le parcours TLS. Pour le domaine racine, conserver le routage compatible avec OVH et ACA. Vérifier le certificat présenté sur l’edge OVH et sur l’origine, le Host/SNI transmis, les redirections, les en-têtes de cache et le comportement bypass. Ne modifier ni les enregistrements e-mail mail.volepapillondamour.fr, SPF, DKIM ou DMARC.

Les workflows applicatifs utilisent actuellement des noms d’apps et un ACR codés en dur. Les adapter pour recevoir les noms réels renvoyés par Bicep et les paramètres de l’ACR partagé. Le groupe reste rg-vpd-dev. Mettre à jour en particulier :

- infra-deploy.yml : région des ressources, noms cibles, paramètres ACR/KV/storage, valeurs sorties Bicep et What-If ;
- api-deploy.yml, website-deploy.yml, backoffice-deploy.yml, scan-deploy.yml, catalog-deploy.yml, worker-deploy.yml et books-runtime-deploy.yml : ACR partagé, groupe de ressources du registre, noms cibles ACA et FQDN API ;
- main.dev.bicepparam : noms de certificats cibles, origine CORS et valeurs régionales, en supprimant l’ancien FQDN Scan West Europe des origines actives après cutover ;
- acs-email-configure.yml : nouveau Key Vault et nouvelles identités/applications seulement là où le script les référence ; garder les ressources ACS et le domaine vérifié ;
- storage-migrate.yml : compte source actuel, compte cible et valeurs d’URL ;
- local-snapshot.yml, database-backup-verify.yml, db-import.yml et database-reset-books.yml : confirmer qu’ils restent dirigés vers le même serveur/base ; ne pas les rediriger vers une copie SQL inexistante.

Les URL API et connection strings Application Insights sont injectées à la construction des images frontales : les images cibles doivent être reconstruites. Garder les domaines publics dans CORS. Les URI de redirection Entra restent identiques si les domaines restent identiques ; vérifier les parcours login/logout et le callback dans chaque front.

La souscription Event Grid des rapports ACS est une intégration globale à réorienter au moment de la bascule vers le webhook API de la nouvelle région, pas une seconde souscription qui enverrait les mêmes événements en double. De même, ne pas exécuter deux Workers simultanément : leurs timers et envois d’e-mails consomment la même base, le même stockage et le même ACS. Construire l’image cible mais ne pas déployer/démarrer le Worker avant l’arrêt de l’ancien. Si un déploiement anticipé est nécessaire, ajouter et vérifier un moyen explicite de neutraliser tous ses triggers.

Références : [certificats managés et domaines ACA](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates), [configuration du domaine sur OVH CDN](https://docs.ovhcloud.com/en/guides/network/content-delivery-network-infrastructure/cdn-infrastructure-first-domain-configuration).

## 8. Exécution et prochaines étapes

### Réalisé

- Création des ressources ACR, stockage, Key Vault, Container Apps et supervision en France Central, dans `rg-vpd-dev` pour les ressources VPD. SQL, ACS et le groupe de ressources existant sont conservés.
- Import des images VPD et Mariage dans `vpdacrshared`, rôles d’accès, OIDC et adaptation des workflows. Les déploiements de validation vers l’ACR partagé ont réussi.
- Synchronisation finale incrémentale des blobs et réécriture des 346 URL dans la même base SQL ; aucune migration de schéma/base.
- Arrêt du Worker West Europe, mise en service du Worker France Central (min=0/max=1) et réorientation du webhook ACS. Le workflow ACS `38637592645` a vérifié le domaine d’envoi, les permissions et les références de secret, puis mis à jour l’abonnement Event Grid vers l’API France Central. Il n’envoie pas d’e-mail de test.
- Bascule OVH : l’apex A vise `51.138.199.49` et les CNAME `www`, `backoffice`, `livres`, `scan` visent les FQDN ACA France Central. Les cinq certificats managés sont liés et les cinq routes HTTPS renvoient `200` avec validation TLS réussie. Aucun CDN OVH actif n’a été trouvé ; le DNS est direct vers ACA.
- Les anciennes ressources restent disponibles et n’ont pas été supprimées.

### Stabilisation et clôture

- Surveiller pendant au moins sept jours les erreurs, latences, alertes, uploads/lectures Blob, flux mail et traitement des timers du Worker. Confirmer qu’aucun traitement ni webhook ne s’exécute en double.
- Le What-If `38636020253` a réussi : aucune suppression de ressource n’est annoncée (1 création de script de validation du webhook, 35 ressources modifiées, 40 sans changement, 72 `Ignore`). ARM indique aussi qu’il ne peut pas analyser certaines affectations de rôles dynamiques avant le déploiement. Le déploiement Bicep `38636607517` a réussi et la sonde de validation du webhook a passé.
- Faire examiner les PR [VPD #255](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/255) et [Mariage #8](https://github.com/FlorianDrevet/mariage-edwige-henri/pull/8), puis les fusionner par le propriétaire avant de considérer les workflows `main` définitivement alignés.
- Documenter le mapping de chaque ancienne ressource vers sa cible et vérifier les dépendances/références avant tout nettoyage.
- Après la période de retour arrière, présenter la liste précise des anciennes ressources candidates. Demander une confirmation explicite avant toute suppression ; ne jamais supprimer le groupe, SQL, ACS, les identités toujours utilisées, `acrweh` ou `vpdacrshared`.

## 9. Critères d’acceptation et nettoyage

État des critères au 14 octobre 2026 :

- [x] Le What-If `38636020253` s’est terminé sans suppression de ressource ; il prévoyait une validation du webhook et des changements sur les ressources cibles.
- [x] Le déploiement Bicep `38636607517` a réussi. Les cinq domaines ont conservé leur liaison SNI et répondent après déploiement ; l’API `/health` est saine. Le serveur/base SQL et l’ancienne pile restent en place.
- [x] Les tags des six dépôts VPD et des deux dépôts Mariage sont présents dans l’ACR partagé ; les déploiements OIDC exécutés pour les deux projets ont réussi. Les PR qui rendent la configuration permanente sur `main` restent à examiner.
- [x] La synchronisation incrémentale finale a validé la présence de tous les blobs source ; les ACL source ont été reprises et 346 URL en base ont été réécrites, avec zéro correspondance restante vers l’ancien compte.
- [x] Les cinq routes publiques (apex, `www`, `backoffice`, `livres`, `scan`) ont renvoyé HTTP 200 en HTTPS avec validation TLS réussie ; `/health` de l’API cible a répondu sainement. Le parcours Entra complet reste à vérifier.
- [x] La base est toujours `vpd-sql-dev` / `vole-papillon-damour-db` ; aucune migration EF ou de schéma n’a été exécutée.
- [ ] Confirmer pendant la stabilisation les écritures Blob, les parcours Entra, les traces/alertes, le flux mail ACS et l’exécution unique des timers Worker/webhook.
- [ ] Vérifier que les workflows opérationnels (backup, import, snapshot et reset) pointent toujours vers les ressources attendues, puis examiner/fusionner les PR de livraison.

Le nettoyage ne commence qu’après au moins sept jours de validation et une confirmation explicite du propriétaire. Si un critère reste ouvert, prolonger la coexistence et conserver le retour arrière.

Ordre de retrait des doublons :

1. Retirer les anciens certificats et domaines liés aux ACA après vérification DNS/CDN, puis les six anciennes applications et l’environnement vpd-cae-dev.
2. Retirer vpd-kv-dev après recherche de toutes ses références dans les ACA et workflows.
3. Retirer vpdstdev après vérification des URL conservées, des lectures/écritures récentes et d’une copie de sécurité finale.
4. Retirer l’ancien Log Analytics et les anciens Application Insights après expiration/export de la télémétrie à conserver ; désactiver les anciennes alertes et availability tests avant leur suppression.
5. Retirer vpdacrdev en dernier, après confirmation de chaque dépôt, pipeline, environnement ACA et projet externe partageant l’ACR.
6. Conserver rg-vpd-dev, vpd-sql-dev, les bases vole-papillon-damour-db et vpd-sql-import, la ressource de génération de titres France Central, ACS Email/Communication, le domaine e-mail, les identités GitHub et les ressources Entra/OIDC.

Après le nettoyage, enlever les paramètres de coexistence devenus inutiles, garder les noms finaux France Central dans Bicep et actualiser infra/README.md ainsi que cette fiche avec le mapping réel et la date de fin.

## 10. Suivi avant toute suppression

- Le What-If Bicep `38636020253` et le déploiement d’alignement `38636607517` ont réussi sans suppression de ressource. La sonde webhook ACS a passé. Après le déploiement, apex et `www` redirigent vers `/accueil`, les trois autres domaines répondent directement ; les cinq contrôles finaux HTTPS ont abouti à HTTP 200 avec TLS valide.
- Les cinq domaines ont déjà répondu `200` en HTTPS avec validation TLS réussie ; refaire le contrôle après la stabilisation si un changement DNS/certificat intervient.
- Les workflows Storage (`38634898966`), ACS Email (`38621723956`, `38637592645`) et Infrastructure (`38636607517`) ont réussi. ACS est lié au webhook France Central ; aucun e-mail de test n’a été envoyé. La base conserve son serveur et son schéma ; 346 références ont été mises à jour et ne correspondent plus à l’URL source.
- Vérifier sur la durée les parcours Entra, les écritures Blob, la remontée des alertes/supervision, un e-mail de test contrôlé et l’absence d’exécution Worker en double.
- Les PR [VPD #255](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/255) et [Mariage #8](https://github.com/FlorianDrevet/mariage-edwige-henri/pull/8) sont ouvertes, non fusionnées ; les runs de validation cités sont réussis. Laisser le propriétaire les examiner/fusionner et conserver `acrweh` pour le retour arrière.
- Aucun CDN OVH n’a été trouvé dans les services consultés et l’ancien A record correspondait à l’IP de l’environnement ACA West Europe. Réexaminer uniquement si le compte révèle ultérieurement un produit CDN.
- Conserver les ressources West Europe au moins sept jours après validation de la production. Avant leur retrait, fournir le mapping final et demander une confirmation explicite ; ne jamais supprimer `rg-vpd-dev`, SQL, ACS, les identités encore utilisées, `acrweh` ou l’ACR partagé.
- Le registre Basic partagé stocke environ 39 GB pour les deux projets, au-dessus des 10 GB inclus ; surveiller le stockage et le coût, et revoir le SKU si le volume augmente.

## 11. Documentation technique de référence

- [Déplacement/réinstallation d’un ACR dans une autre région](https://learn.microsoft.com/en-us/azure/container-registry/manual-regional-move)
- [Rôles ACR et ABAC pour l’accès aux dépôts](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-abac-repository-permissions)
- [Certificats gérés ACA et enregistrements DNS](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates)
- [Isolation régionale des identités managées](https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/managed-identities-isolation-scope)
- [Copie des blobs avec AzCopy](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-blobs-copy)
- [Configuration du domaine sur OVH CDN](https://docs.ovhcloud.com/en/guides/network/content-delivery-network-infrastructure/cdn-infrastructure-first-domain-configuration)
- [Emplacement des métadonnées d’un groupe Azure Resource Manager](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/overview)
