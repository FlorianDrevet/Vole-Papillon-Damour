#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication, Microsoft.Graph.Applications, Microsoft.Graph.Identity.SignIns
<#
.SYNOPSIS
    Configure le locataire Microsoft Entra External ID du projet Vole-Papillon-Damour.

.DESCRIPTION
    Cree ou met a jour, de facon idempotente :
      - l'enregistrement d'application de l'API, qui expose la portee `access_as_user`
        et porte les roles applicatifs du projet (`Tri`, `Caisse`, `Administration`) ;
      - les enregistrements des clients : catalogue public, application de scan,
        back-office, application de caisse MAUI ;
      - les principaux de service correspondants ;
      - l'enregistrement applicatif de suppression de compte avec la permission
        applicative Microsoft Graph `User.ReadWrite.All` ;
      - le consentement administrateur du client vers la portee de l'API.

    Le script est rejouable : il retrouve les objets par `displayName`, ne recree rien
    et n'ecrase que ce qu'il gere. Les identifiants des roles applicatifs sont fixes en
    dur pour qu'une reexecution ne casse aucune attribution existante.

    Ce qu'il ne fait PAS, et qui reste manuel :
      - la creation du locataire externe lui-meme (portail Azure, ou Bicep
        `Microsoft.AzureActiveDirectory/ciamDirectories`) ;
      - le flux d'inscription en libre-service, dont l'API Graph est en `beta` pour les
        locataires externes. Voir `Configure-EntraUserFlow.ps1`.

.PARAMETER TenantId
    Identifiant du locataire externe (GUID) ou son domaine `*.onmicrosoft.com`.

.PARAMETER Environment
    Suffixe d'environnement, repris dans les noms d'application. Defaut : `dev`.

.PARAMETER CatalogRedirectUri
    Origine du site catalogue public. Ex. https://vpd-web-ca-dev.azurecontainerapps.io

.PARAMETER ScanRedirectUri
    Origine de la PWA de scan.

.PARAMETER BackOfficeRedirectUri
    Origine du back-office.

.PARAMETER UseDeviceCode
    Utilise l'authentification Graph par code appareil, notamment depuis un runner
    GitHub sans navigateur.

.PARAMETER DeletionClientSecretOutputFile
    Fichier local, hors depot, dans lequel ecrire le secret Graph cree pour
    `vpd-account-deletion-<environment>`. Le secret n'est jamais ajoute au rapport JSON.

.PARAMETER RotateDeletionClientSecret
    Cree une nouvelle credential Graph meme si l'application en possede deja une.
    Le nouveau secret n'est affiche qu'une fois et remplace le precedent dans le fichier
    de sortie fourni.

.PARAMETER WhatIf
    Affiche les operations sans les appliquer.

.EXAMPLE
    ./Configure-EntraApps.ps1 -TenantId 'vpd.onmicrosoft.com' `
        -CatalogRedirectUri 'http://localhost:4200' `
        -ScanRedirectUri 'http://localhost:4300' `
        -BackOfficeRedirectUri 'http://localhost:4400'

.NOTES
    Modules requis :
        Install-Module Microsoft.Graph.Authentication, Microsoft.Graph.Applications `
                       -Scope CurrentUser

    Le compte qui execute doit etre Administrateur d'application (ou Administrateur
    general) sur le locataire externe.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $TenantId,

    [string] $Environment = 'dev',

    [string] $CatalogRedirectUri = 'http://localhost:4200',
    [string] $ScanRedirectUri = 'http://localhost:4300',
    [string] $BackOfficeRedirectUri = 'http://localhost:4400',

    [string] $OutputFile,

    [string] $DeletionClientSecretOutputFile,

    [switch] $RotateDeletionClientSecret,

    [switch] $UseDeviceCode
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Constantes. Les GUID sont fixes : ils identifient les roles et les portees de
# facon stable d'une execution a l'autre, et surtout d'un environnement a l'autre.
# Ne jamais les regenerer : les attributions d'utilisateurs y font reference.
# ---------------------------------------------------------------------------

$ApiScopeId = 'a3f4c1e2-5b6d-4a7e-8f90-1c2d3e4f5a6b'

# Un role applicatif par droit metier. `Tri` et `Caisse` viennent de RG-40,
# `Administration` de ENF-18. L'absence de role vaut « membre du public » :
# c'est voulu, aucun role n'est attribue aux comptes crees en libre-service.
$AppRoles = @(
    @{
        Id          = '6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21'
        Value       = 'Tri'
        DisplayName = 'Benevole trieur'
        Description = 'Ouvre les sessions de tri et enregistre les decisions de tri (RG-40).'
    },
    @{
        Id          = '9d2e8b76-4a1c-4b3d-8e7f-2a1b0c9d8e7f'
        Value       = 'Caisse'
        DisplayName = 'Benevole caissier'
        Description = 'Ouvre le mode vente et enregistre les sorties de caisse (RG-40).'
    },
    @{
        Id          = 'c7a5e3d1-8f2b-4c6a-9d0e-3b4c5d6e7f80'
        Value       = 'Administration'
        DisplayName = 'Administrateur'
        Description = 'Acces au back-office et a la zone d''administration du site (ENF-18).'
    }
)

$ApiAppName        = "vpd-api-$Environment"
$CatalogAppName    = "vpd-catalog-$Environment"
$ScanAppName       = "vpd-scan-$Environment"
$BackOfficeAppName = "vpd-backoffice-$Environment"
$CashAppName       = "vpd-caisse-$Environment"
$DeletionAppName   = "vpd-account-deletion-$Environment"
$GraphResourceAppId = '00000003-0000-0000-c000-000000000000'
$GraphUserReadWriteAllAppRoleId = '741f803b-c850-494e-b5df-cde7c675a1ca'

# ---------------------------------------------------------------------------
# Aides
# ---------------------------------------------------------------------------

function Write-Step {
    param([string] $Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Detail {
    param([string] $Message)
    Write-Host "    $Message" -ForegroundColor DarkGray
}

function Get-OrNewApplication {
    <#
        Retrouve une application par son nom d'affichage, ou la cree.
        Renvoie l'objet application.
    #>
    param(
        [Parameter(Mandatory)] [string] $DisplayName,
        [hashtable] $Body = @{}
    )

    $existing = Get-MgApplication `
        -Filter "displayName eq '$DisplayName'" `
        -Property 'id','appId','displayName','spa','publicClient','requiredResourceAccess','passwordCredentials' `
        -ErrorAction SilentlyContinue

    if ($existing) {
        # `Get-MgApplication -Filter` renvoie une collection ; on refuse l'ambiguite
        # plutot que de prendre le premier au hasard.
        if (@($existing).Count -gt 1) {
            throw "Plusieurs applications portent le nom '$DisplayName'. Resoudre le doublon a la main."
        }
        Write-Detail "application '$DisplayName' deja presente ($($existing.AppId))"
        return $existing
    }

    if (-not $PSCmdlet.ShouldProcess($DisplayName, 'Creer l''enregistrement d''application')) {
        Write-Detail "simulation : l''application '$DisplayName' serait creee"
        return [pscustomobject]@{
            Id          = $null
            AppId       = '<planned>'
            DisplayName = $DisplayName
            Spa         = $null
            PublicClient = $null
            PasswordCredentials = @()
        }
    }

    $created = New-MgApplication -DisplayName $DisplayName -SignInAudience 'AzureADMyOrg' @Body
    Write-Detail "application '$DisplayName' creee ($($created.AppId))"
    return $created
}

function Get-OrNewServicePrincipal {
    param([Parameter(Mandatory)] [string] $AppId)

    if ([string]::IsNullOrWhiteSpace($AppId) -or $AppId -eq '<planned>') {
        Write-Detail 'simulation : le principal de service serait cree apres l''application'
        return $null
    }

    $existing = Get-MgServicePrincipal -Filter "appId eq '$AppId'" -ErrorAction SilentlyContinue
    if ($existing) { return $existing }

    if (-not $PSCmdlet.ShouldProcess($AppId, 'Creer le principal de service')) { return $null }

    return New-MgServicePrincipal -AppId $AppId
}

function Merge-RedirectUris {
    param(
        [Parameter(Mandatory)] $Application,
        [Parameter(Mandatory)] [ValidateSet('Spa', 'PublicClient')] [string] $Kind,
        [Parameter(Mandatory)] [string] $Uri
    )

    $currentPlatform = if ($Kind -eq 'Spa') { $Application.Spa } else { $Application.PublicClient }
    $currentUris = if ($null -ne $currentPlatform -and $null -ne $currentPlatform.RedirectUris) {
        @($currentPlatform.RedirectUris)
    } else {
        @()
    }

    return @(
        $currentUris + $Uri |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )
}

function Grant-ApiScope {
    <#
        Accorde le consentement administrateur du client vers la portee de l'API,
        pour tous les utilisateurs. Evite a chaque benevole un ecran de consentement
        qui ne veut rien dire pour lui.
    #>
    param(
        [Parameter(Mandatory)] $ClientServicePrincipal,
        [Parameter(Mandatory)] $ApiServicePrincipal,
        [Parameter(Mandatory)] [string] $Scope
    )

    $clientId = $ClientServicePrincipal.Id
    $resourceId = $ApiServicePrincipal.Id

    $existing = Get-MgOauth2PermissionGrant `
        -Filter "clientId eq '$clientId' and resourceId eq '$resourceId'" `
        -ErrorAction SilentlyContinue

    if ($existing) {
        $current = @($existing)[0]
        if ($current.Scope -eq $Scope) {
            Write-Detail "consentement deja accorde pour $($ClientServicePrincipal.DisplayName)"
            return
        }

        if ($PSCmdlet.ShouldProcess($ClientServicePrincipal.DisplayName, 'Mettre a jour le consentement')) {
            Update-MgOauth2PermissionGrant -OAuth2PermissionGrantId $current.Id -Scope $Scope
            Write-Detail "consentement mis a jour pour $($ClientServicePrincipal.DisplayName)"
        }
        return
    }

    if ($PSCmdlet.ShouldProcess($ClientServicePrincipal.DisplayName, 'Accorder le consentement administrateur')) {
        New-MgOauth2PermissionGrant -BodyParameter @{
            clientId    = $clientId
            consentType = 'AllPrincipals'
            resourceId  = $resourceId
            scope       = $Scope
        } | Out-Null
        Write-Detail "consentement accorde pour $($ClientServicePrincipal.DisplayName)"
    }
}

# ---------------------------------------------------------------------------
# 1. Connexion
# ---------------------------------------------------------------------------

Write-Step "Connexion au locataire $TenantId"

$requiredScopes = @(
    'Application.ReadWrite.All'
    'AppRoleAssignment.ReadWrite.All'
    'DelegatedPermissionGrant.ReadWrite.All'
    'Directory.ReadWrite.All'
)

$connectParameters = @{
    TenantId  = $TenantId
    Scopes    = $requiredScopes
    NoWelcome = $true
}

function Grant-GraphApplicationPermission {
    param(
        [Parameter(Mandatory)] $ClientServicePrincipal,
        [Parameter(Mandatory)] $GraphServicePrincipal,
        [Parameter(Mandatory)] [string] $AppRoleId
    )

    $existing = Get-MgServicePrincipalAppRoleAssignment `
        -ServicePrincipalId $ClientServicePrincipal.Id `
        -All `
        -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ResourceId -eq $GraphServicePrincipal.Id -and
            $_.AppRoleId -eq [Guid]$AppRoleId
        }

    if ($existing) {
        Write-Detail "permission Graph deja accordee a $($ClientServicePrincipal.DisplayName)"
        return
    }

    if ($PSCmdlet.ShouldProcess($ClientServicePrincipal.DisplayName, 'Accorder User.ReadWrite.All sur Microsoft Graph')) {
        New-MgServicePrincipalAppRoleAssignment `
            -ServicePrincipalId $ClientServicePrincipal.Id `
            -PrincipalId $ClientServicePrincipal.Id `
            -ResourceId $GraphServicePrincipal.Id `
            -AppRoleId ([Guid]$AppRoleId) | Out-Null
        Write-Detail "permission applicative User.ReadWrite.All accordee"
    }
}

function Add-DeletionClientSecret {
    param(
        [Parameter(Mandatory)] $Application
    )

    $hasCredential = @($Application.PasswordCredentials).Count -gt 0
    $needsCredential = $RotateDeletionClientSecret -or -not $hasCredential
    if (-not $needsCredential) {
        Write-Detail 'credential Graph deja presente ; aucune rotation demandee'
        return
    }

    if ($WhatIfPreference) {
        Write-Detail 'simulation : une credential Graph serait creee et restituee une seule fois'
        return
    }

    if ([string]::IsNullOrWhiteSpace($DeletionClientSecretOutputFile)) {
        throw "Le fichier -DeletionClientSecretOutputFile est obligatoire pour creer ou renouveler le secret de '$DeletionAppName'."
    }

    if (Test-Path -LiteralPath $DeletionClientSecretOutputFile) {
        throw "Le fichier de secret '$DeletionClientSecretOutputFile' existe deja. Choisir un nouveau chemin pour eviter tout ecrasement."
    }

    if (-not $PSCmdlet.ShouldProcess($Application.DisplayName, 'Creer une credential Graph')) {
        return
    }

    $password = Add-MgApplicationPassword `
        -ApplicationId $Application.Id `
        -PasswordCredential @{ displayName = $DeletionAppName }

    $password.SecretText | Set-Content -LiteralPath $DeletionClientSecretOutputFile -NoNewline -Encoding utf8
    Write-Detail "secret Graph ecrit dans '$DeletionClientSecretOutputFile' ; il ne sera plus restitue par Microsoft Graph"
}
if ($UseDeviceCode) {
    $connectParameters.UseDeviceCode = $true
    $connectParameters.ClientTimeout = 600
}

Connect-MgGraph @connectParameters
$context = Get-MgContext
Write-Detail "connecte en tant que $($context.Account) sur $($context.TenantId)"

# ---------------------------------------------------------------------------
# 2. Application de l'API : portee exposee et roles applicatifs
# ---------------------------------------------------------------------------

Write-Step 'API'

$apiApp = Get-OrNewApplication -DisplayName $ApiAppName
if (-not $apiApp) { return }

$apiScope = @{
    Id                      = $ApiScopeId
    Value                   = 'access_as_user'
    Type                    = 'User'
    IsEnabled               = $true
    AdminConsentDisplayName = 'Acceder a l''API Vole-Papillon-Damour'
    AdminConsentDescription = 'Permet a l''application appelante d''acceder a l''API au nom de l''utilisateur connecte.'
    UserConsentDisplayName  = 'Acceder a l''API en votre nom'
    UserConsentDescription  = 'Permet a l''application d''acceder a l''API en votre nom.'
}

if ($PSCmdlet.ShouldProcess($ApiAppName, 'Publier la portee et les roles applicatifs')) {
    Update-MgApplication -ApplicationId $apiApp.Id `
        -IdentifierUris @("api://$($apiApp.AppId)") `
        -Api @{
            RequestedAccessTokenVersion = 2
            Oauth2PermissionScopes      = @($apiScope)
        } `
        -AppRoles ($AppRoles | ForEach-Object {
            @{
                Id                 = $_.Id
                Value              = $_.Value
                DisplayName        = $_.DisplayName
                Description        = $_.Description
                AllowedMemberTypes = @('User')
                IsEnabled          = $true
            }
        })

    Write-Detail "portee access_as_user publiee, $($AppRoles.Count) roles applicatifs declares"
}

$apiSp = Get-OrNewServicePrincipal -AppId $apiApp.AppId

# ---------------------------------------------------------------------------
# 3. Clients
# ---------------------------------------------------------------------------

$clients = @(
    @{ Name = $CatalogAppName;    Kind = 'Spa';          Uri = $CatalogRedirectUri }
    @{ Name = $ScanAppName;       Kind = 'Spa';          Uri = $ScanRedirectUri }
    @{ Name = $BackOfficeAppName; Kind = 'Spa';          Uri = $BackOfficeRedirectUri }
    @{ Name = $CashAppName;       Kind = 'PublicClient'; Uri = 'http://localhost' }
)

$results = [ordered]@{
    TenantId    = $context.TenantId
    ApiClientId = $apiApp.AppId
    ApiScope    = "api://$($apiApp.AppId)/access_as_user"
}

foreach ($client in $clients) {
    Write-Step $client.Name

    $app = Get-OrNewApplication -DisplayName $client.Name
    if (-not $app) { continue }

    $clientUri = if ($client.Kind -eq 'PublicClient') {
        "msal$($app.AppId)://auth"
    } else {
        $client.Uri
    }

    $redirectUris = Merge-RedirectUris -Application $app -Kind $client.Kind -Uri $clientUri
    $redirect = @{ RedirectUris = $redirectUris }
    $platform = if ($client.Kind -eq 'Spa') { @{ Spa = $redirect } } else { @{ PublicClient = $redirect } }

    if ($PSCmdlet.ShouldProcess($client.Name, 'Configurer la plateforme et la permission vers l''API')) {
        Update-MgApplication -ApplicationId $app.Id @platform `
            -RequiredResourceAccess @(
                @{
                    ResourceAppId  = $apiApp.AppId
                    ResourceAccess = @(
                        @{ Id = $ApiScopeId; Type = 'Scope' }
                    )
                }
            )
        Write-Detail "redirections $($redirectUris -join ', ') ($($client.Kind))"
    }

    $sp = Get-OrNewServicePrincipal -AppId $app.AppId
    if ($sp -and $apiSp) {
        Grant-ApiScope -ClientServicePrincipal $sp -ApiServicePrincipal $apiSp -Scope 'access_as_user'
    }

    $results[$client.Name] = $app.AppId
}

# ---------------------------------------------------------------------------
# 4. Application applicative de suppression de compte
# ---------------------------------------------------------------------------

Write-Step $DeletionAppName

$deletionApp = Get-OrNewApplication -DisplayName $DeletionAppName -Body @{
    RequiredResourceAccess = @(
        @{
            ResourceAppId  = $GraphResourceAppId
            ResourceAccess = @(
                @{ Id = $GraphUserReadWriteAllAppRoleId; Type = 'Role' }
            )
        }
    )
}

if ($deletionApp -and $deletionApp.AppId -ne '<planned>') {
    if ($PSCmdlet.ShouldProcess($DeletionAppName, 'Configurer la permission applicative Microsoft Graph')) {
        Update-MgApplication -ApplicationId $deletionApp.Id -RequiredResourceAccess @(
            @{
                ResourceAppId  = $GraphResourceAppId
                ResourceAccess = @(
                    @{ Id = $GraphUserReadWriteAllAppRoleId; Type = 'Role' }
                )
            }
        )
    }

    $deletionSp = Get-OrNewServicePrincipal -AppId $deletionApp.AppId
    $graphSp = Get-MgServicePrincipal -Filter "appId eq '$GraphResourceAppId'" -ErrorAction SilentlyContinue
    if (-not $graphSp) {
        throw 'Le principal de service Microsoft Graph est introuvable dans le locataire.'
    }
    if ($deletionSp) {
        Grant-GraphApplicationPermission `
            -ClientServicePrincipal $deletionSp `
            -GraphServicePrincipal $graphSp `
            -AppRoleId $GraphUserReadWriteAllAppRoleId
    }

    Add-DeletionClientSecret -Application $deletionApp
}

$results['DeletionAppClientId'] = $deletionApp.AppId

# ---------------------------------------------------------------------------
# 5. Restitution
# ---------------------------------------------------------------------------

Write-Step 'Configuration a reporter dans les applications'

$authority = "https://$($context.TenantId)/v2.0"

Write-Host ''
Write-Host '  API (appsettings.json) :' -ForegroundColor Yellow
Write-Host "    AzureAd:Instance   = https://<sous-domaine>.ciamlogin.com/"
Write-Host "    AzureAd:TenantId   = $($context.TenantId)"
Write-Host "    AzureAd:ClientId   = $($apiApp.AppId)"
Write-Host "    AzureAd:Audience   = $($apiApp.AppId)"
Write-Host ''
Write-Host '  Clients (environment.ts / MSAL) :' -ForegroundColor Yellow
foreach ($client in $clients) {
    if ($results.Contains($client.Name)) {
        Write-Host "    $($client.Name.PadRight(24)) clientId = $($results[$client.Name])"
    }
}
Write-Host "    scope = api://$($apiApp.AppId)/access_as_user"
Write-Host "    Graph deletion app clientId = $($deletionApp.AppId)"
Write-Host "    Graph permission = User.ReadWrite.All (application)"
Write-Host ''

if ($OutputFile -and $PSCmdlet.ShouldProcess($OutputFile, 'Ecrire le rapport de configuration')) {
    $results | ConvertTo-Json -Depth 3 | Set-Content -Path $OutputFile -Encoding utf8
    Write-Detail "ecrit dans $OutputFile"
}

Write-Host 'Termine. Les roles s''attribuent ensuite avec Set-VpdUserRole.ps1.' -ForegroundColor Green
