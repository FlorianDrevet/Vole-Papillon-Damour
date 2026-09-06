# Configuration Microsoft Entra External ID

Tout ce qui se configure dans le locataire d'identité se fait **par script**, jamais à
la main dans le portail. Un clic dans le portail n'est ni rejouable, ni relisible, ni
reproductible sur un second environnement.

Trois scripts, et une seule chose qui reste manuelle.

`Configure-EntraApps.ps1` cree aussi `vpd-account-deletion-<environment>`. Cette
application n'est pas un client interactif : elle recoit les permissions applicatives
Microsoft Graph `User.ReadWrite.All`, `Application.Read.All` et
`AppRoleAssignment.ReadWrite.All`. L'API l'utilise pour creer les comptes internes,
lister les roles et gerer les attributions ; l'API et le worker l'utilisent aussi pour
supprimer un objet utilisateur apres une demande d'effacement. Son secret est cree une seule fois,
ecrit dans un fichier explicitement choisi **hors du depot**, puis transmis au secret
GitHub `ENTRA_GRAPH_CLIENT_SECRET`. Le rapport JSON ne contient jamais cette valeur.

| Script | Rôle | Fréquence |
|---|---|---|
| `Configure-EntraApps.ps1` | Enregistrements d'application, portée exposée, rôles applicatifs, consentements | À chaque évolution de la configuration |
| `Set-VpdUserRole.ps1` | Attribue ou retire `Tri`, `Caisse`, `Administration` à un compte | Au fil de l'eau |
| `Get-VpdUserRoles.ps1` | Liste qui détient quel rôle | Contrôle |

## Prérequis

```powershell
Install-Module Microsoft.Graph.Authentication, Microsoft.Graph.Applications, `
               Microsoft.Graph.Identity.SignIns, Microsoft.Graph.Users `
               -Scope CurrentUser
```

Le compte qui exécute doit être **Administrateur d'application** sur le locataire
externe. PowerShell 7 requis.

`Microsoft.Graph.Identity.SignIns` est nécessaire pour les cmdlets de consentement
OAuth2 utilisées par `Configure-EntraApps.ps1`. Les scripts déclarent eux-mêmes leurs
modules requis et s'arrêtent immédiatement si l'un d'eux manque.

La connexion Graph est indépendante de `az login`. Sur un poste autorisé, le mode
normal ouvre la connexion interactive du module Graph. Le mode `-UseDeviceCode` est
disponible si le navigateur interactif ne peut pas être utilisé :

```powershell
./Configure-EntraApps.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UseDeviceCode -WhatIf
```

## Ce qui reste manuel

**La création du locataire externe lui-même.** Elle se fait une fois, depuis le portail
Azure (*Microsoft Entra External ID → Créer un locataire → External*), ou en Bicep via
`Microsoft.AzureActiveDirectory/ciamDirectories`. C'est une ressource facturée à
l'utilisateur actif mensuel, distincte du locataire de travail de l'association.

**Le flux d'inscription en libre-service**, tant que son API Graph reste en `beta` pour
les locataires externes. Il ne concerne que le catalogue public : l'application de scan
et le back-office ne doivent surtout pas en avoir un, leurs comptes étant créés par un
administrateur. Voir `QT-07` dans
[`09-questions-techniques.md`](../../docs/bourse-aux-livres/technique/09-questions-techniques.md).

## Ordre d'exécution

```powershell
# 1. Enregistrements et rôles. Rejouable sans effet de bord.
# Les URI existantes sont fusionnées, jamais remplacées : les origines locales et
# les anciens FQDN temporaires restent donc utilisables après l'ajout des domaines
# publics.
./Configure-EntraApps.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -Environment 'dev' `
    -CatalogRedirectUri    'https://livres.volepapillondamour.fr' `
    -ScanRedirectUri       'https://scan.volepapillondamour.fr' `
    -BackOfficeRedirectUri 'https://backoffice.volepapillondamour.fr' `
    -WhatIf

# 1 bis. Après relecture de la simulation, rejouer exactement la même commande
# sans -WhatIf et conserver le rapport hors du dépôt.
./Configure-EntraApps.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -Environment 'dev' `
    -CatalogRedirectUri    'https://livres.volepapillondamour.fr' `
    -ScanRedirectUri       'https://scan.volepapillondamour.fr' `
    -BackOfficeRedirectUri 'https://backoffice.volepapillondamour.fr' `
    -OutputFile ./entra-dev.json `
    -DeletionClientSecretOutputFile "$env:TEMP\vpd-entra-graph-secret-dev.txt"

# 1 ter. Reporter la valeur DeletionAppClientId du JSON comme secret GitHub
# ENTRA_GRAPH_CLIENT_ID, et le contenu du fichier comme ENTRA_GRAPH_CLIENT_SECRET
# dans l'environnement development. infra-deploy les injecte dans Key Vault ;
# aucune valeur n'est ajoutee a un fichier suivi par Git.

# 2. Le premier administrateur, sans qui rien n'est administrable.
./Set-VpdUserRole.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UserPrincipalName 'florian.drevet_magellangroup.eu#EXT#@volepapillondamour.onmicrosoft.com' `
    -Role Administration

# 3. Contrôle.
./Get-VpdUserRoles.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' | Format-Table
```

Les scripts acceptent `-WhatIf` : à utiliser systématiquement au premier passage sur un
locataire qui contient déjà quelque chose.

## Le modèle de droits en trois lignes

Les droits sont des **rôles applicatifs** déclarés sur l'enregistrement de l'API, et
attribués directement aux comptes. Ils arrivent dans la revendication `roles` du jeton
d'accès, que l'API lit sans aucun aller-retour.

| Rôle | Ouvre | Origine |
|---|---|---|
| `Tri` | Sessions de tri, décisions gardé/écarté | `RG-40` |
| `Caisse` | Mode vente, scan de sortie | `RG-40` |
| `Administration` | Back-office et zone d'administration du site | `ENF-18` |

**Un membre du public n'a aucun rôle**, et c'est le point important : « membre inscrit »
est l'absence de rôle, pas un rôle. Sans cela, il faudrait attribuer un rôle à chaque
inscription en libre-service, donc automatiser une écriture dans l'annuaire à chaque
création de compte. Ici, l'inscription publique n'écrit rien de plus qu'un compte.

Le raisonnement complet, y compris pourquoi ce ne sont pas des groupes, est en
[`10-identite-et-droits.md`](../../docs/bourse-aux-livres/technique/10-identite-et-droits.md).

## Les GUID sont fixes, ne pas les régénérer

`Configure-EntraApps.ps1` porte en dur les identifiants de la portée et des trois rôles.
C'est délibéré : les attributions faites aux bénévoles pointent vers ces GUID. Les
régénérer reviendrait à révoquer silencieusement tout le monde.
