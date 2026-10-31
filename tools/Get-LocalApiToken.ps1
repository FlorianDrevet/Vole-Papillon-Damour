[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('admin', 'tri-volunteer', 'cashier', 'member')]
    [string]$Persona,

    [string]$KeycloakBaseUrl
)

$ErrorActionPreference = 'Stop'

$personaUsernames = @{
    admin = 'local-admin'
    'tri-volunteer' = 'local-tri'
    cashier = 'local-caisse'
    member = 'local-member'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appHostPath = Join-Path $repositoryRoot 'src/Backend/Vole_Papillon_Damour.AppHost/Vole_Papillon_Damour.AppHost.csproj'

if ([string]::IsNullOrWhiteSpace($KeycloakBaseUrl)) {
    $resourceStateJson = & aspire describe `
        --apphost $appHostPath `
        --format Json `
        --non-interactive `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to resolve the local Keycloak endpoint. Start the Aspire AppHost or pass -KeycloakBaseUrl.'
    }

    $resourceState = ($resourceStateJson -join [Environment]::NewLine) | ConvertFrom-Json
    $keycloakResource = $resourceState.resources | Where-Object { $_.displayName -eq 'keycloak' } | Select-Object -First 1
    $keycloakEndpoint = $keycloakResource.urls | Where-Object { $_.name -eq 'http' } | Select-Object -First 1

    if ($null -eq $keycloakEndpoint -or [string]::IsNullOrWhiteSpace($keycloakEndpoint.url)) {
        throw 'Aspire did not report a running Keycloak endpoint. Start the Aspire AppHost or pass -KeycloakBaseUrl.'
    }

    $KeycloakBaseUrl = ([Uri]$keycloakEndpoint.url).GetLeftPart([System.UriPartial]::Authority)
}

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
    }

if ([string]::IsNullOrWhiteSpace($response.access_token)) {
    throw "Keycloak returned no access token for persona '$Persona'."
}

$response.access_token
