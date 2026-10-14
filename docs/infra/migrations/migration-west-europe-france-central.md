# Migration de West Europe vers France Central

**État :** migration terminée le 14 octobre 2026 après confirmation explicite du propriétaire. Les anciennes ressources sont supprimées; Azure finalise encore l'environnement ACA West Europe, en état `ScheduledForDelete`. Les changements de pipelines sont dans une PR séparée et restent à fusionner.
**Groupe applicatif conservé :** `rg-vpd-dev`.

## Résumé

Les ressources applicatives, le stockage, Key Vault et la supervision tournent en France Central. Les domaines publics pointent vers les nouvelles Container Apps. Le même serveur et la même base SQL ont été conservés : aucune copie de base, migration de schéma ou migration EF n'a été faite.

Le nettoyage des anciennes ressources a été lancé avant la fin de la fenêtre initiale de sept jours, à la demande explicite du propriétaire. La pile West Europe ne constitue donc plus une option de retour arrière.

## Architecture finale

| Surface | Cible et emplacement |
| --- | --- |
| Groupe applicatif | `rg-vpd-dev` (métadonnées du groupe en West Europe) |
| Container Apps Environment | `vpd-cae-dev-fc`, France Central |
| API / Website / BackOffice | `vpd-api-ca-dev-fc` / `vpd-web-ca-dev-fc` / `vpd-bo-ca-dev-fc` |
| Scan / Catalog / Worker | `vpd-scan-ca-dev-fc` / `vpd-catalog-ca-dev-fc` / `vpd-worker-ca-dev-fc` |
| Storage | `vpdstdevfc`, France Central |
| Key Vault | `vpd-kv-dev-fc`, France Central |
| Log Analytics | `vpd-law-dev-fc`, France Central |
| Application Insights | six composants `vpd-*-appi-dev-fc` |
| SQL | serveur `vpd-sql-dev`, base `vole-papillon-damour-db`, inchangés |
| ACS | ACS Email, ACS Communication et le domaine `mail.volepapillondamour.fr`, conservés |
| ACR partagé | `vpdacrshared` dans `rg-core-florian`, France Central, Basic |

## Données et ACR

- Les six dépôts VPD (300 tags) et les deux dépôts Mariage (99 tags) ont été importés dans `vpdacrshared`; les digests ont été vérifiés avant le retrait de `vpdacrdev`.
- Le registre partagé a conservé l'accès public et l'admin désactivé comme configuré. L'identité de déploiement a `Owner`; les identités des six ACA ont `AcrPull`.
- Les blobs de `vpdstdev` ont été synchronisés vers `vpdstdevfc`, puis 346 URL ont été réécrites dans la base existante. La vérification n'a trouvé aucune URL restante vers le compte source.
- L'ancien ACR `vpdacrdev` et l'ancien Storage `vpdstdev` ont été supprimés.
- Le Key Vault `vpd-kv-dev` a été supprimé avec la rétention Azure soft-delete. Il n'a pas été purgé.

## DNS, domaines et trafic

Les enregistrements OVH sont directs vers l'environnement ACA France Central. Aucun CDN OVH actif n'a été trouvé.

| Domaine | Container App | Contrôle HTTP/TLS |
| --- | --- | --- |
| `volepapillondamour.fr` | Website | `302` vers `/accueil`, TLS valide |
| `www.volepapillondamour.fr` | Website | `302` vers `/accueil`, TLS valide |
| `backoffice.volepapillondamour.fr` | BackOffice | `200`, TLS valide |
| `livres.volepapillondamour.fr` | Catalog | `200`, TLS valide |
| `scan.volepapillondamour.fr` | Scan | `200`, TLS valide |
| API `/health` | `vpd-api-ca-dev-fc` | `200`; révision active saine |

Le webhook Event Grid ACS vise l'API France Central. Le Worker France Central est le seul Worker conservé et reste limité à une réplique maximum.

## Anciennes ressources retirées

Les ressources suivantes ont été supprimées de `rg-vpd-dev` :

- les six Container Apps West Europe et leurs certificats managés;
- `vpdacrdev`, `vpdstdev`, `vpd-kv-dev` et `vpd-law-dev`;
- les six anciens Application Insights, les règles d'alerte, les tests de disponibilité, le groupe d'action et le classeur de supervision.

L'environnement `vpd-cae-dev` ne contient plus d'application. Azure a accepté sa suppression et l'affiche `ScheduledForDelete`.

Les ressources conservées sont le groupe `rg-vpd-dev`, le serveur et les bases SQL, ACS Email/Communication, le domaine d'envoi, les ressources Entra et les identités GitHub encore utilisées, la génération de titres France Central, les ressources France Central et l'ACR partagé.

## Pipelines

- La PR VPD de migration d'infrastructure [#255](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/255) et la PR de plan [#254](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/254) sont fusionnées.
- Les cinq domaines et certificats sont fixés dans `infra/parameters/main.dev.bicepparam`. Après fusion de la PR de simplification, le seul choix manuel du workflow infra sera `what-if` ou `deploy`; l'amorçage ACS et l'activation des e-mails utilisent les valeurs Bicep déjà validées.
- `ACR_NAME`, `ACR_RESOURCE_GROUP`, `ACR_SUBSCRIPTION_ID` et `ACR_CI_PRINCIPAL_ID` restent des variables GitHub nécessaires à la configuration de l'ACR partagé, pas des choix manuels à chaque lancement.
- Les workflows ponctuels `ACR - migrate images` et `Storage - migrate blobs` ont été retirés après leur exécution.
- La PR Mariage [#8](https://github.com/FlorianDrevet/mariage-edwige-henri/pull/8) reste ouverte. Fusionner ses changements avant le prochain déploiement Mariage : le workflow `main` de ce dépôt doit être aligné sur l'ACR partagé.

## Exécutions et validation

- What-If Bicep `38636020253` réussi; déploiement Bicep `38636607517` réussi.
- Synchronisation finale Blob `38634898966` réussie; les 346 URL SQL ont été réécrites.
- Vérification ACS/Event Grid `38637592645` réussie. Aucun e-mail de test n'a été envoyé.
- Les domaines et `/health` ont été contrôlés après le nettoyage; les réponses observées sont dans le tableau DNS.
- La CI de la PR VPD de migration a réussi avant sa fusion. Les contrôles de la PR Mariage restent associés à sa PR ouverte.

## Suivi restant

- Vérifier les parcours Entra de bout en bout, observer les erreurs et alertes après nettoyage, puis effectuer un e-mail ACS de test contrôlé.
- Fusionner la PR Mariage #8 avant un nouveau déploiement de ce projet.
- Le registre Basic contient environ 39 Go alors que 10 Go sont inclus; surveiller le volume et les coûts.
- Le retour arrière vers West Europe n'est plus disponible. Azure conserve temporairement le Key Vault supprimé selon sa politique soft-delete; il n'a pas été purgé.
