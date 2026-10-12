[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('admin', 'tri-volunteer', 'cashier', 'member')]
    [string]$Persona,

    [string]$KeycloakBaseUrl = 'http://localhost:8080'
)

$ErrorActionPreference = 'Stop'

$personaUsernames = @{
    admin = 'local-admin'
    'tri-volunteer' = 'local-tri'
    cashier = 'local-caisse'
    member = 'local-member'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$realmPath = Join-Path $repositoryRoot 'src/Backend/Vole_Papillon_Damour.AppHost/Realms/vpd-realm.json'
$realm = Get-Content -LiteralPath $realmPath -Raw | ConvertFrom-Json
$username = $personaUsernames[$Persona]
$user = $realm.users | Where-Object { $_.username -eq $username } | Select-Object -First 1

if ($null -eq $user) {
    throw "Persona '$Persona' was not found in the local Keycloak realm fixture."
}

$credential = $user.credentials | Where-Object { $_.type -eq 'password' } | Select-Object -First 1
if ($null -eq $credential -or [string]::IsNullOrWhiteSpace($credential.value)) {
    throw "Persona '$Persona' has no password credential in the local Keycloak realm fixture."
}

$tokenEndpoint = '{0}/realms/{1}/protocol/openid-connect/token' -f $KeycloakBaseUrl.TrimEnd('/'), $realm.realm
$response = Invoke-RestMethod `
    -Method Post `
    -Uri $tokenEndpoint `
    -ContentType 'application/x-www-form-urlencoded' `
    -Body @{
        grant_type = 'password'
        client_id = 'vpd-local-test'
        username = $user.username
        password = $credential.value
        scope = 'openid profile email'
    }

if ([string]::IsNullOrWhiteSpace($response.access_token)) {
    throw "Keycloak returned no access token for persona '$Persona'."
}

$response.access_token
