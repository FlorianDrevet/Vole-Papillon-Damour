# Configuration Microsoft Entra External ID

Tout ce qui se configure dans le locataire d'identité se fait **par script**, jamais à
la main dans le portail. Un clic dans le portail n'est ni rejouable, ni relisible, ni
reproductible sur un second environnement.

Cinq scripts ; la création du tenant et, si nécessaire, le mapping des claims External ID
restent manuels.

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
| `Configure-EntraUserFlow.ps1` | User flow External ID d'inscription publique, attaché au catalogue uniquement | À l'activation ou à l'évolution du parcours membre |
| `Configure-EntraBranding.ps1` | Marque française, canvas uni et CSS du formulaire hébergé External ID | À l'activation ou à l'évolution du design system |
| `Set-VpdUserRole.ps1` | Attribue ou retire `Tri`, `Caisse`, `Administration` à un compte | Au fil de l'eau |
| `Get-VpdUserRoles.ps1` | Liste qui détient quel rôle | Contrôle |

## Prérequis

```powershell
Install-Module Microsoft.Graph.Authentication, Microsoft.Graph.Applications, `
               Microsoft.Graph.Identity.SignIns, Microsoft.Graph.Identity.DirectoryManagement, `
               Microsoft.Graph.Users `
               -Scope CurrentUser
```

Le compte qui exécute doit être **Administrateur d'application** sur le locataire
externe. PowerShell 7 requis.

`Microsoft.Graph.Identity.SignIns` est nécessaire pour les cmdlets de consentement
OAuth2 et pour le user flow External ID. Les scripts déclarent eux-mêmes leurs modules
requis et s'arrêtent immédiatement si l'un d'eux manque.

`Configure-EntraBranding.ps1` utilise `Microsoft.Graph.Identity.DirectoryManagement`.
Le compte qui le lance doit disposer du consentement Graph
`OrganizationalBranding.ReadWrite.All` et du rôle Entra
**Administrateur de la personnalisation de marque organisationnelle** (*Organizational
Branding Administrator*).

La connexion Graph est indépendante de `az login`. Sur un poste autorisé, le mode
normal ouvre la connexion interactive du module Graph. Le mode `-UseDeviceCode` est
disponible si le navigateur interactif ne peut pas être utilisé :

```powershell
./Configure-EntraApps.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UseDeviceCode -WhatIf
```

## Ce qui reste manuel

Les noms collectés par le formulaire sont aussi exposés par
`Configure-EntraApps.ps1` : `given_name` est ajouté au jeton d'identité du Catalog et
`family_name` ainsi que `given_name` aux jetons d'accès de l'API. Le Catalog et l'API
acceptent également les anciennes variantes `givenName`/`surname` afin de rester
compatibles avec les configurations External ID déjà en place. Après toute modification
du user flow ou des claims, une nouvelle connexion est nécessaire pour obtenir un jeton
actualisé.

Après la première exécution réelle, contrôler le contenu du jeton avec un compte de test.
Si le tenant External ID ne reprend pas les attributs collectés malgré les claims optionnels,
ouvrir l'application gérée depuis `vpd-catalog-<environment>` puis **Single sign-on →
Attributes & Claims**, et ajouter `given_name` depuis l'attribut intégré `givenName` ainsi
que `family_name` depuis `surname`. Répéter le mapping sur l'application gérée de l'API si
le jeton d'accès ne contient pas ces deux valeurs. Cette étape ne change pas le formulaire :
elle publie seulement les attributs déjà enregistrés dans les jetons.

**La création du locataire externe lui-même.** Elle se fait une fois, depuis le portail
Azure (*Microsoft Entra External ID → Créer un locataire → External*), ou en Bicep via
`Microsoft.AzureActiveDirectory/ciamDirectories`. C'est une ressource facturée à
l'utilisateur actif mensuel, distincte du locataire de travail de l'association.

Le flux d'inscription en libre-service n'est plus une étape manuelle :
`Configure-EntraUserFlow.ps1` utilise l'API Graph v1.0, crée le formulaire hébergé par
Entra et l'attache uniquement à `vpd-catalog-<environment>`. L'application de scan et le
back-office ne doivent surtout pas en avoir un, leurs comptes étant créés par un
administrateur. Le compte qui lance ce script doit disposer des permissions
`Application.Read.All` et `EventListener.ReadWrite.All`, ainsi que du rôle Entra
**External ID User Flow Administrator**.

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

# 2. User flow d'inscription du catalogue public. La simulation ne modifie rien.
./Configure-EntraUserFlow.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -Environment 'dev' `
    -UseDeviceCode -WhatIf

# 2 bis. Après relecture, appliquer le même user flow dans le tenant.
./Configure-EntraUserFlow.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -Environment 'dev' `
    -UseDeviceCode

# 2 ter. Personnaliser la page hébergée External ID. La simulation ne modifie rien.
./Configure-EntraBranding.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UseDeviceCode -WhatIf

# 2 quater. Appliquer le branding français et le CSS de la maquette 1a, sans image de fond.
./Configure-EntraBranding.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UseDeviceCode

# Au premier passage sur un tenant External ID neuf, le script initialise d'abord
# le branding par défaut avant de lire les localisations. Le -WhatIf reste donc
# utilisable même si la ressource organizationalBranding n'existe pas encore.
# Le CSS force un canvas bleu pâle opaque et neutralise toute image de fond déjà
# enregistrée dans le tenant. Le résultat réel attendu est donc :
# localization-created, default-updated, default-css-updated, localization-css-updated.

# Des fichiers PNG/JPEG peuvent être fournis en option pour remplacer l'image de fond,
# le logo et le favicon.
# Le fichier de fond doit faire au maximum 300 KB et mesurer au plus 1920 x 1080 pixels.
# La maquette Catalog n'utilise plus d'image de fond : les anciennes images restent
# masquées par le CSS, y compris lorsque Graph ne permet pas de les supprimer (405).
# Le logo d'en-tête doit être une ressource dédiée au bandeau, pas le logo carré de l'application.
# ./Configure-EntraBranding.ps1 ... -HeaderLogoPath ./branding/header-logo.png `
#     -FaviconPath ./branding/favicon.png `
#     -BackgroundImagePath ./branding/background.png

# 3. Le premier administrateur, sans qui rien n'est administrable.
./Set-VpdUserRole.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' `
    -UserPrincipalName 'florian.drevet_magellangroup.eu#EXT#@volepapillondamour.onmicrosoft.com' `
    -Role Administration

# 4. Contrôle.
./Get-VpdUserRoles.ps1 -TenantId 'b23c80b3-9776-4840-8255-fcbf3b3500fd' | Format-Table
```

Les scripts acceptent `-WhatIf` : à utiliser systématiquement au premier passage sur un
locataire qui contient déjà quelque chose.

## Parcours hébergé ou formulaire entièrement personnalisé

Le Catalog utilise le parcours **browser-delegated** : le mot de passe est saisi dans la
page External ID hébergée par Microsoft et n'est jamais envoyé au Catalog ni à l'API.
`Configure-EntraBranding.ps1` applique le CSS du design system et un canvas bleu pâle uni,
Le formulaire d'inscription collecte l'adresse e-mail, le `givenName` (« Prénom ») et le
`surname` (« Nom ») comme attributs intégrés External ID ; les deux champs de nom restent
facultatifs et sont limités à 64 caractères chacun.
`Configure-EntraBranding.ps1` applique le CSS du design system, un fond de repli léger,
les textes français et la locale `fr-FR` demandée par le Catalog (`ui_locales` et `mkt`).
La variante actuelle reprend la maquette 1a : canvas bleu papier uni, carte centrée, ligne
supérieure Catalog, papillon près du titre, boutons orange, focus/erreurs accessibles et
footer clair. Le même CSS est utilisé par l'inscription et la connexion ; il supprime aussi
`background-image` sur les conteneurs External ID afin qu'un ancien asset du tenant ne
réapparaisse pas derrière la carte. Cette solution
conserve la sécurité et les écrans de récupération de compte du parcours géré ; le domaine
d'authentification reste toutefois celui d'External ID : ce n'est pas un formulaire HTML
servi par notre domaine.

Si le besoin devient un contrôle pixel-perfect du formulaire, l'alternative est **Native
Authentication** avec `@azure/msal-browser/custom-auth` dans Angular. Le Catalog posséderait
alors ses propres champs et messages, mais il faudrait activer Native Authentication sur
l'enregistrement de l'application et déployer un proxy serveur pour les appels, car les
endpoints natifs External ID ne supportent pas CORS. Ce choix augmente la surface de code,
la responsabilité de sécurité, les tests d'accessibilité et la maintenance des états
mot de passe, MFA, vérification d'e-mail et erreurs. Il ne faut pas remplacer cela par un
formulaire maison qui collecte le mot de passe puis l'envoie directement à Graph.

Les **custom policies** ne sont donc pas nécessaires pour le besoin actuel. Elles restent
un mécanisme distinct de l'ancien Azure AD B2C ; pour ce locataire External ID, le user flow
hébergé avec branding, ou Native Authentication si le contrôle de l'interface devient
prioritaire, sont les deux voies supportées à privilégier.

## Déploiement après merge

Cette évolution modifie le Catalog, l'API et la configuration des applications/du user flow
External ID :

- rejouer `Configure-EntraApps.ps1` après le merge (d'abord `-WhatIf`, puis sans cette
  option) pour ajouter les claims `given_name` et `family_name` aux jetons attendus ;
- rejouer `Configure-EntraUserFlow.ps1` (d'abord `-WhatIf`, puis sans cette option) pour
  conserver dans le tenant le formulaire `givenName`/`surname` ;
- exécuter `Configure-EntraBranding.ps1` de la même façon pour publier le CSS et les textes
  français ; le CSS et la marque sont stockés dans External ID, pas dans l'image runtime ;
- lancer **Catalog - deploy** et le déploiement de l'API après validation des deux simulations.

Un compte déjà connecté doit se déconnecter puis se reconnecter afin de recevoir un nouveau
jeton. Le premier passage du formulaire doit être vérifié en navigation privée sur le domaine
public, avec un compte de test, en contrôlant l'inscription, la connexion, le mot de passe
oublié, le nom affiché dans le header et l'espace compte, ainsi que le rendu mobile.

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
