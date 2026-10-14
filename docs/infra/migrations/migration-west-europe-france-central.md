# Migration de West Europe vers France Central

**Statut d'exécution :** migration en cours. La pile France Central et l’ACR partagé sont déployés. L’apex et les quatre CNAME publics ont été basculés vers France Central ; le certificat de `www` est actif, les autres certificats managés sont en cours d’émission. La synchronisation finale Blob et la validation complète restent à terminer. Aucune ancienne ressource n’a été supprimée.
**Révision :** 14 octobre 2026
**Groupe de ressources conservé :** rg-vpd-dev

## État réel au 14 octobre 2026

- L’ACR partagé `vpdacrshared` est créé dans `rg-core-florian`, en France Central, SKU Basic, accès administrateur désactivé. Les 300 tags des six dépôts VPD ont été copiés et vérifiés par digest. Les 99 tags des deux dépôts `mariage-edwige-henri` ont aussi été importés ; ses deux workflows GitHub déploient maintenant sur cet ACR avec OIDC. Le pipeline VPD utilise également ce registre. Les identités de déploiement ont les rôles demandés sur l’ACR et les identités ACA ont AcrPull. Les PR de livraison des changements GitHub restent à ouvrir.
- L’environnement Container Apps et les six applications, le stockage `vpdstdevfc`, le Key Vault, Log Analytics, les six Application Insights, les alertes et tests de disponibilité sont déployés en France Central dans `rg-vpd-dev`.
- La première copie de `vpdstdev` vers `vpdstdevfc` est terminée, avec vérification des volumes par conteneur et conservation des ACL. La base SQL est conservée ; aucun déploiement EF ni déplacement de base n’a été lancé. Une synchronisation finale et la prévisualisation des références Blob encore présentes attendent la stabilisation de la bascule.
- L’API cible est saine sur `/health` et utilise une image reconstruite depuis la révision de l’API actuellement en production. Website, BackOffice, Catalogue et Scan ont aussi été reconstruits depuis leurs révisions actuellement en production, avec l’API France Central. Le Worker France Central est sain avec une réplique (min=0/max=1) ; le Worker West Europe n’a aucune réplique au contrôle. Vérifier les déclenchements planifiés et notifications pendant la validation.
- Dans la zone DNS OVH, `volepapillondamour.fr` pointe vers `51.138.199.49`, et les CNAME `www`, `backoffice`, `livres` et `scan` pointent directement vers les FQDN ACA France Central. La résolution a été vérifiée depuis un résolveur public. Le certificat managé de `www.volepapillondamour.fr` est émis et HTTPS répond avec un certificat valide ; l’apex et les trois autres certificats sont en cours. L’ancien A record `20.4.178.125` correspondait à l’adresse de l’environnement ACA West Europe. Aucun service CDN actif n’a été identifié dans les services OVH consultés ; le routage configuré est DNS direct vers ACA.
- Le serveur/base SQL, ACS et les anciennes ressources West Europe sont conservés. La synchronisation finale du stockage, la réécriture ciblée des URL Blob en base et le nettoyage attendent la bascule et la validation. Le nettoyage nécessitera l’accord explicite du propriétaire.

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

## 8. Ordre d’exécution proposé

### Phase A — Préparation et point de retour arrière

- Rafraîchir l’inventaire Azure/OVH et confirmer les accès régionaux, quotas, providers, noms disponibles, limites ACA/ACR et budget de coexistence.
- Capturer un What-If de référence, les versions d’images, réglages des ressources, rôles, DNS/TTL et secrets par nom uniquement. Vérifier la sauvegarde PITR du SQL, sans restaurer ni déplacer la base.
- Abaisser le TTL des enregistrements publics assez tôt pour laisser les caches DNS expirer avant la fenêtre.
- Établir le mapping source-cible, la procédure retour arrière et la durée de conservation des ressources anciennes.

### Phase B — Bicep et accès

- Refactorer Bicep pour séparer région du groupe et région des ressources ; paramétrer les noms cibles, ACR et ressources SQL/ACS existantes.
- Ajouter les affectations de rôles à l’ACR externe et vérifier l’isolation régionale des identités.
- Compiler Bicep et les paramètres dev, examiner le What-If. Bloquer la suite si l’outil prévoit une suppression, un déplacement, une modification SQL/ACS ou un changement du groupe.

### Phase C — ACR et déploiements reproductibles

- Créer et configurer l’ACR partagé France Central par Azure CLI.
- Migrer les dépôts/tags, appliquer les rôles requis, tester les pulls ACA et pousser une image de test depuis les workflows OIDC.
- Basculer les workflows sur l’ACR partagé ; confirmer que tous les projets consommateurs ont une configuration documentée.

### Phase D — Stockage, coffre et supervision

- Créer stockage, Key Vault, workspace et Application Insights en France Central dans rg-vpd-dev avec les noms de coexistence.
- Recopier les blobs, valider les objets et secrets, puis préparer une seconde synchronisation finale.
- Déployer alertes et tableaux de bord ciblant les nouvelles ressources. Laisser les sources actives jusqu’à la bascule.

### Phase E — ACA de préproduction

- Créer le nouvel environnement et les six apps ; déployer des images cohérentes avec le code actuellement en service.
- Configurer les nouvelles références SQL (même serveur/base), Storage, Key Vault, ACS, Application Insights, identité et CORS.
- Tester via les FQDN ACA : health API, pages, connexion Entra, API, upload/download Blob, accès SQL, Scan, Catalog et envoi d’e-mail de test contrôlé. Vérifier les références et rôles prévus pour le Worker sans démarrer ses timers.
- Ne pas encore déployer/démarrer le Worker cible et ne pas réorienter Event Grid ou les domaines publics.
- Attacher les domaines ACA après vérification des enregistrements DNS directs : A apex vers l’IP de l’environnement et CNAME des sous-domaines directement vers les FQDN ACA. Ne pas réutiliser un certificat de l’ancien environnement. Pendant l’exécution, OVH a déjà été basculé vers France Central ; le certificat `www` est actif, ceux de l’apex et des trois autres sous-domaines restent à confirmer.

### Phase F — Bascule contrôlée

- Activer la fenêtre de maintenance courte, arrêter les nouvelles écritures Blob et arrêter l’ancien Worker.
- Exécuter la dernière copie des blobs et vérifier qu’aucune copie n’est en cours ; prévisualiser puis appliquer la réécriture d’URL Blob dans la base existante si elle est nécessaire.
- Déployer les images finales et secrets cibles. Reconfigurer la souscription Event Grid ACS vers la nouvelle API.
- Vérifier que les DNS publics OVH résolvent vers France Central et que les cinq domaines servent HTTPS avec un certificat valide ; les DNS ont déjà été basculés directement vers les ACA. Aucun service CDN OVH actif n’a été trouvé dans le compte consulté, donc la configuration actuelle ne comporte pas d’origine CDN à modifier.
- Le Worker France Central étant déjà déployé et sain, ne pas le redéployer pendant la bascule. Confirmer que le Worker West Europe reste sans réplique, puis vérifier un seul traitement de chaque timer et une seule souscription webhook active.
- Rétablir les écritures et surveiller API, erreurs, latences, stockage, e-mails et compteurs du Worker.

### Phase G — Stabilisation, retour arrière, suppression

- Garder les anciennes ressources disponibles au moins sept jours après la validation de production, ou plus longtemps si les signaux/volumes le justifient.
- Pendant cette période, ne pas republier uniquement dans l’ancien ACR et ne pas laisser tourner deux Workers. Conserver un journal des corrections et des images utilisées.
- Si le retour arrière est requis : remettre l’origine/DNS OVH vers l’ancien environnement, rebasculer les secrets et le webhook, arrêter le Worker France Central et synchroniser les blobs écrits après la bascule. Ne restaurer la base SQL que si un incident distinct l’exige ; cette migration ne déplace pas SQL.
- Après accord des critères ci-dessous, supprimer les ressources West Europe individuellement, par dépendance. Ne jamais supprimer le groupe rg-vpd-dev.

## 9. Critères d’acceptation et nettoyage

La bascule est validée quand :

- le What-If et le déploiement Bicep ne montrent aucune modification de région ou suppression du SQL, ACS, du groupe, des identités préservées ou des ressources globales ;
- toutes les images et tags nécessaires sont lisibles dans l’ACR partagé, chaque pipeline pertinent pousse et déploie avec OIDC, et toutes les identités de projets consommateurs sont listées ;
- les comptes Blob sont cohérents, tous les blobs sont copiés, les conteneurs et permissions répondent comme prévu et les références persistées ont été vérifiées ;
- API /health, Website, BackOffice, Catalog, Scan et parcours Entra réussissent sur les domaines publics avec certificats valides ;
- les cinq domaines publics répondent en HTTPS sur les ACA France Central avec des certificats valides ; si un CDN OVH est découvert ultérieurement, valider son origine et le bypass avant activation ;
- la base reste vpd-sql-dev / vole-papillon-damour-db, les écritures métier fonctionnent et aucun Worker ou webhook en double ne s’exécute ;
- les nouvelles traces arrivent dans le workspace France Central, les alertes et tests de disponibilité ciblent le bon environnement, et les notifications de test arrivent au bon destinataire ;
- les pipelines opérationnels (backup, import, snapshot, reset et migration de stockage) pointent toujours vers les ressources prévues.

Ordre de retrait des doublons :

1. Retirer les anciens certificats et domaines liés aux ACA après vérification DNS/CDN, puis les six anciennes applications et l’environnement vpd-cae-dev.
2. Retirer vpd-kv-dev après recherche de toutes ses références dans les ACA et workflows.
3. Retirer vpdstdev après vérification des URL conservées, des lectures/écritures récentes et d’une copie de sécurité finale.
4. Retirer l’ancien Log Analytics et les anciens Application Insights après expiration/export de la télémétrie à conserver ; désactiver les anciennes alertes et availability tests avant leur suppression.
5. Retirer vpdacrdev en dernier, après confirmation de chaque dépôt, pipeline, environnement ACA et projet externe partageant l’ACR.
6. Conserver rg-vpd-dev, vpd-sql-dev, les bases vole-papillon-damour-db et vpd-sql-import, la ressource de génération de titres France Central, ACS Email/Communication, le domaine e-mail, les identités GitHub et les ressources Entra/OIDC.

Après le nettoyage, enlever les paramètres de coexistence devenus inutiles, garder les noms finaux France Central dans Bicep et actualiser infra/README.md ainsi que cette fiche avec le mapping réel et la date de fin.

## 10. Suivi avant toute suppression

- Confirmer les certificats managés et l’accès HTTPS pour l’apex, `www`, `backoffice`, `livres` et `scan`.
- Faire la synchronisation finale de `vpdstdev` vers `vpdstdevfc`, puis lancer le mode `preview` du workflow pour compter les références à `vpdstdev.blob.core.windows.net`. Le mode `apply` ne sera utilisé que si la prévisualisation confirme des références à réécrire vers le nouveau stockage.
- Vérifier les parcours Website, BackOffice, Catalogue, Scan, API et Entra sur les domaines publics, ainsi que les écritures Blob, l’alerte/supervision, le webhook ACS et l’absence d’exécution Worker en double.
- Le dépôt `mariage-edwige-henri` a aussi migré ses workflows de déploiement vers `vpdacrshared`; conserver `acrweh` pour le retour arrière. La PR de ce projet et la PR VPD restent à valider.
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
