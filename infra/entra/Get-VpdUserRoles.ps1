#Requires -Version 7.0
<#
.SYNOPSIS
    Liste qui detient quel role applicatif Vole-Papillon-Damour.

.DESCRIPTION
    Restitue les attributions de roles portees par l'enregistrement de l'API. C'est la
    reponse a « qui peut tenir la caisse ? » et « qui est administrateur ? », sans
    passer par le portail.

    Les comptes sans aucun role n'apparaissent pas : ce sont les membres du public,
    et ils se comptent en centaines.

.PARAMETER TenantId
    Identifiant du locataire externe.

.PARAMETER Environment
    Suffixe d'environnement. Defaut : `dev`.

.EXAMPLE
    ./Get-VpdUserRoles.ps1 -TenantId 'vpd.onmicrosoft.com' | Format-Table
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TenantId,

    [string] $Environment = 'dev'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ApiAppName = "vpd-api-$Environment"

Connect-MgGraph -TenantId $TenantId `
    -Scopes 'Application.Read.All', 'AppRoleAssignment.ReadWrite.All', 'User.Read.All' `
    -NoWelcome

$apiApp = Get-MgApplication -Filter "displayName eq '$ApiAppName'"
if (-not $apiApp) {
    throw "Application '$ApiAppName' introuvable. Lancer d'abord Configure-EntraApps.ps1."
}

$apiSp = Get-MgServicePrincipal -Filter "appId eq '$($apiApp.AppId)'"

# Le nom lisible du role vit dans l'enregistrement d'application, l'attribution dans
# le principal de service : il faut les deux pour afficher autre chose que des GUID.
$roleNames = @{}
foreach ($role in $apiApp.AppRoles) {
    $roleNames[$role.Id.ToString()] = $role.Value
}

Get-MgServicePrincipalAppRoleAssignedTo -ServicePrincipalId $apiSp.Id -All |
    ForEach-Object {
        [PSCustomObject]@{
            Compte    = $_.PrincipalDisplayName
            Role      = $roleNames[$_.AppRoleId.ToString()]
            AttribueLe = $_.CreatedDateTime
        }
    } |
    Sort-Object Role, Compte
