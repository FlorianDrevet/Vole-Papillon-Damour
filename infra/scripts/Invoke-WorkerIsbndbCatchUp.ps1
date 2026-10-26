[CmdletBinding()]
param(
    [string] $CredentialPath = (Join-Path $env:LOCALAPPDATA 'VolePapillonDamour\secrets\worker-function.json'),
    [switch] $Wait
)

$ErrorActionPreference = 'Stop'

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

    do {
        Start-Sleep -Seconds 10
        try {
            $status = Invoke-RestMethod -Uri $response.statusQueryGetUri -Method Get -TimeoutSec 60
        }
        catch {
            throw "Could not read Durable status for instance '$($response.id)'. The status URL contains a management key and has been withheld. Check Worker telemetry."
        }
    } while ($status.runtimeStatus -in @('Pending', 'Running', 'ContinuedAsNew'))

    if ($status.runtimeStatus -ne 'Completed') {
        throw "Manual ISBNdb catch-up ended with status '$($status.runtimeStatus)'. Check Worker telemetry for instance '$($response.id)'."
    }

    $status.output | ConvertTo-Json -Depth 5
    return
}

# Older Worker revisions return the catch-up summary synchronously.
$response | ConvertTo-Json -Depth 5
