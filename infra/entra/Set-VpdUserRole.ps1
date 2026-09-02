#Requires -Version 7.0
<#
.SYNOPSIS
    Attribue ou retire un role applicatif Vole-Papillon-Damour a un compte du locataire.

.DESCRIPTION
    Les droits du projet sont des roles applicatifs portes par l'enregistrement de
    l'API : `Tri`, `Caisse`, `Administration`. Ce script attribue l'un d'eux a un
    utilisateur, ou le lui retire.

    Les comptes crees en libre-service par le public n'ont aucun role, et c'est voulu :
    « membre inscrit » est l'absence de role, pas un role. Ce script ne sert donc qu'aux
    benevoles et aux administrateurs.

.PARAMETER TenantId
    Identifiant du locataire externe.

.PARAMETER UserPrincipalName
    Compte vise. Accepte l'UPN complet ou l'adresse e-mail de connexion.

.PARAMETER Role
    `Tri`, `Caisse` ou `Administration`.

.PARAMETER Remove
    Retire le role au lieu de l'attribuer.

.PARAMETER Environment
    Suffixe d'environnement, pour retrouver l'application de l'API. Defaut : `dev`.

.EXAMPLE
    ./Set-VpdUserRole.ps1 -TenantId 'vpd.onmicrosoft.com' `
        -UserPrincipalName 'marie@exemple.fr' -Role Tri

.EXAMPLE
    ./Set-VpdUserRole.ps1 -TenantId 'vpd.onmicrosoft.com' `
        -UserPrincipalName 'marie@exemple.fr' -Role Caisse -Remove

.NOTES
    Le retrait d'un role ne prend effet qu'au renouvellement du jeton d'acces du
    benevole. C'est la contrepartie connue des roles portes par le jeton : pour une
    revocation immediate, il faut desactiver le compte dans le locataire.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string] $TenantId,

    [Parameter(Mandatory = $true)]
    [string] $UserPrincipalName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Tri', 'Caisse', 'Administration')]
    [string] $Role,

    [switch] $Remove,

    [string] $Environment = 'dev'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Doit rester aligne sur Configure-EntraApps.ps1.
$RoleIds = @{
    'Tri'            = '6b1f0a54-2c3d-4e5f-9a8b-7c6d5e4f3a21'
    'Caisse'         = '9d2e8b76-4a1c-4b3d-8e7f-2a1b0c9d8e7f'
    'Administration' = 'c7a5e3d1-8f2b-4c6a-9d0e-3b4c5d6e7f80'
}

$ApiAppName = "vpd-api-$Environment"

Connect-MgGraph -TenantId $TenantId `
    -Scopes 'AppRoleAssignment.ReadWrite.All', 'Application.Read.All', 'User.Read.All' `
    -NoWelcome

$apiApp = Get-MgApplication -Filter "displayName eq '$ApiAppName'"
if (-not $apiApp) {
    throw "Application '$ApiAppName' introuvable. Lancer d'abord Configure-EntraApps.ps1."
}

$apiSp = Get-MgServicePrincipal -Filter "appId eq '$($apiApp.AppId)'"
if (-not $apiSp) {
    throw "Principal de service de '$ApiAppName' introuvable."
}

$user = Get-MgUser -Filter "userPrincipalName eq '$UserPrincipalName' or mail eq '$UserPrincipalName'"
if (-not $user) {
    throw "Compte '$UserPrincipalName' introuvable dans le locataire."
}
if (@($user).Count -gt 1) {
    throw "Plusieurs comptes correspondent a '$UserPrincipalName'."
}

$roleId = $RoleIds[$Role]
$existing = Get-MgUserAppRoleAssignment -UserId $user.Id |
    Where-Object { $_.ResourceId -eq $apiSp.Id -and $_.AppRoleId -eq $roleId }

if ($Remove) {
    if (-not $existing) {
        Write-Host "$UserPrincipalName n'a pas le role $Role. Rien a faire." -ForegroundColor DarkGray
        return
    }

    if ($PSCmdlet.ShouldProcess($UserPrincipalName, "Retirer le role $Role")) {
        Remove-MgUserAppRoleAssignment -UserId $user.Id -AppRoleAssignmentId $existing.Id
        Write-Host "Role $Role retire a $UserPrincipalName." -ForegroundColor Green
        Write-Host 'Effectif au prochain renouvellement de jeton.' -ForegroundColor DarkGray
    }
    return
}

if ($existing) {
    Write-Host "$UserPrincipalName a deja le role $Role. Rien a faire." -ForegroundColor DarkGray
    return
}

if ($PSCmdlet.ShouldProcess($UserPrincipalName, "Attribuer le role $Role")) {
    New-MgUserAppRoleAssignment -UserId $user.Id -BodyParameter @{
        principalId = $user.Id
        resourceId  = $apiSp.Id
        appRoleId   = $roleId
    } | Out-Null

    Write-Host "Role $Role attribue a $UserPrincipalName." -ForegroundColor Green
}
