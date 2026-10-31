[CmdletBinding()]
param(
    [string] $CredentialPath = (Join-Path $env:LOCALAPPDATA 'VolePapillonDamour\secrets\worker-function.json'),
    [switch] $Wait
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'WorkerDurableStatus.psm1') -Force

if (-not (Test-Path -LiteralPath $CredentialPath -PathType Leaf)) {
    throw "Credential file was not found at '$CredentialPath'. Run Sync-WorkerFunctionKey.ps1 after deploying the infrastructure."
}

$credential = Get-Content -LiteralPath $CredentialPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($credential.endpoint) -or
    [string]::IsNullOrWhiteSpace($credential.functionKey)) {
    throw 'The local Worker invocation credential file is incomplete.'
}

$headers = @{ 'x-functions-key' = $credential.functionKey }
$response = Invoke-RestMethod -Uri $credential.endpoint -Method Post -Headers $headers -TimeoutSec 60

if ($response.id -and $response.statusQueryGetUri) {
    Write-Output "Manual ISBNdb catch-up accepted. Instance: $($response.id)."
    if (-not $Wait) {
        Write-Output 'Use -Wait to poll completion. Durable status links are not printed because they contain a management key.'
        return
    }

    if ([string]::IsNullOrWhiteSpace($credential.keyVaultName)) {
        throw 'The local Worker credential does not identify the dedicated Key Vault needed to read Durable status.'
    }

    $systemKeyOutput = & rtk proxy az keyvault secret show `
        --vault-name $credential.keyVaultName `
        --name 'host--systemKey--durabletask-095extension' `
        --query value `
        --output tsv 2>$null
    $systemKeyExitCode = $LASTEXITCODE
    $systemKey = ($systemKeyOutput -join [Environment]::NewLine).Trim()
    if ($systemKeyExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($systemKey)) {
        throw 'Could not read the Durable system key from the Worker Key Vault. Sign in to Azure and verify Key Vault access.'
    }

    $endpointUri = [Uri] $credential.endpoint
    $statusUri = [UriBuilder]::new($endpointUri)
    $statusUri.Scheme = 'https'
    $statusUri.Port = -1
    $statusUri.Path = '/runtime/webhooks/durabletask/instances/{0}' -f [Uri]::EscapeDataString([string] $response.id)
    $statusUri.Query = ''
    do {
        Start-Sleep -Seconds 10
        $status = Get-WorkerDurableStatus -Uri $statusUri.Uri -SystemKey $systemKey
    } while ($status.runtimeStatus -in @('Pending', 'Running', 'ContinuedAsNew'))

    if ($status.runtimeStatus -ne 'Completed') {
        throw "Manual ISBNdb catch-up ended with status '$($status.runtimeStatus)'. Check Worker telemetry for instance '$($response.id)'."
    }

    $status.output | ConvertTo-Json -Depth 5
    return
}

# Older Worker revisions return the catch-up summary synchronously.
$response | ConvertTo-Json -Depth 5
