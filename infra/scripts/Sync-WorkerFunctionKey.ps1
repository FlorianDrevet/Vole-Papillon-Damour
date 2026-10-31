[CmdletBinding()]
param(
    [string] $SubscriptionId = 'fc4fa56f-e4e7-41e9-8a0d-559fcd27b827',
    [string] $ResourceGroup = 'rg-vpd-dev',
    [string] $ContainerAppName = 'vpd-worker-ca-dev-fc',
    [string] $KeyVaultName = 'vpd-worker-fn-kv-dev-fc',
    [string] $FunctionName = 'IsbndbCatchUpManual',
    [string] $FunctionKeyName = 'local-runner',
    [string] $KeyVaultSecretName = 'function--IsbndbCatchUpManual--local-045runner',
    [string] $CredentialPath = (Join-Path $env:LOCALAPPDATA 'VolePapillonDamour\secrets\worker-function.json'),
    [switch] $Rotate
)

$ErrorActionPreference = 'Stop'

function Invoke-AzCli {
    param(
        [Parameter(Mandatory)] [string[]] $Arguments,
        [switch] $AllowFailure
    )

    $cliArguments = @($Arguments) + @('--subscription', $SubscriptionId)
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell turns native stderr (including Azure CLI extension warnings)
        # into PowerShell errors when ErrorActionPreference is Stop.
        $ErrorActionPreference = 'Continue'
        $output = & rtk proxy az @cliArguments 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            return $null
        }

        throw "Azure CLI failed while running 'az $($Arguments[0]) $($Arguments[1])'."
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Test-ExpectedAcl {
    param(
        [Parameter(Mandatory)] [object] $Acl,
        [Parameter(Mandatory)] [bool] $Directory
    )

    if ($Directory -and -not $Acl.AreAccessRulesProtected) {
        return $false
    }

    $currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $expectedSids = @($currentUserSid, 'S-1-5-18')
    $rules = @($Acl.Access)
    if ($rules.Count -ne 2) {
        return $false
    }

    $actualSids = @()
    foreach ($rule in $rules) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        $actualSids += $sid
        if ($sid -notin $expectedSids -or
            [int] $rule.FileSystemRights -ne [int] [Security.AccessControl.FileSystemRights]::FullControl -or
            $rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow) {
            return $false
        }

        if ($Directory) {
            $requiredInheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
                [Security.AccessControl.InheritanceFlags]::ObjectInherit
            if ($rule.IsInherited -or
                ($rule.InheritanceFlags -band $requiredInheritance) -ne $requiredInheritance) {
                return $false
            }
        }
    }

    return (@($actualSids | Sort-Object -Unique).Count -eq 2)
}

function Protect-PrivateDirectory {
    param([Parameter(Mandatory)] [string] $Path)

    $acl = Get-Acl -LiteralPath $Path
    if (Test-ExpectedAcl -Acl $acl -Directory $true) {
        return
    }

    $currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $userAce = "*$currentUserSid`:(OI)(CI)F"
    $systemAce = '*S-1-5-18:(OI)(CI)F'
    & "$env:SystemRoot\System32\icacls.exe" $Path '/inheritance:r' '/grant:r' $userAce $systemAce | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-ExpectedAcl -Acl (Get-Acl -LiteralPath $Path) -Directory $true)) {
        throw 'Could not restrict the credential directory to the current user and SYSTEM.'
    }
}

function Assert-PrivateFile {
    param([Parameter(Mandatory)] [string] $Path)

    $directoryPath = Split-Path -Parent $Path
    if (-not (Test-ExpectedAcl -Acl (Get-Acl -LiteralPath $directoryPath) -Directory $true) -or
        -not (Test-ExpectedAcl -Acl (Get-Acl -LiteralPath $Path) -Directory $false)) {
        throw 'The credential file ACL must grant FullControl only to the current user and SYSTEM.'
    }
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This helper creates a Windows user-only credential file and must run on Windows.'
}

$credentialPathFull = [IO.Path]::GetFullPath($CredentialPath)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
if ($credentialPathFull.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CredentialPath must be outside the repository.'
}

$directoryPath = Split-Path -Parent $credentialPathFull
New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
Protect-PrivateDirectory -Path $directoryPath

$scaleJson = Invoke-AzCli @(
    'containerapp', 'show',
    '--name', $ContainerAppName,
    '--resource-group', $ResourceGroup,
    '--query', 'properties.template.scale',
    '--output', 'json')
$originalMinReplicas = [int]((ConvertFrom-Json $scaleJson).minReplicas)
$replicasRaised = $false

try {
    if ($originalMinReplicas -lt 1) {
        $replicasRaised = $true
        $null = Invoke-AzCli @(
            'containerapp', 'update',
            '--name', $ContainerAppName,
            '--resource-group', $ResourceGroup,
            '--min-replicas', '1',
            '--only-show-errors',
            '--output', 'none')

        $deadline = [DateTimeOffset]::UtcNow.AddMinutes(5)
        do {
            $replicasJson = Invoke-AzCli @(
                'containerapp', 'replica', 'list',
                '--name', $ContainerAppName,
                '--resource-group', $ResourceGroup,
                '--output', 'json')
            $runningReplicas = @((ConvertFrom-Json $replicasJson) | Where-Object {
                $_.properties.runningState -eq 'Running'
            })
            if ($runningReplicas.Count -gt 0) {
                break
            }

            if ([DateTimeOffset]::UtcNow -ge $deadline) {
                throw 'The Worker did not start a replica within five minutes.'
            }

            Start-Sleep -Seconds 5
        } while ($true)
    }

    $fqdn = Invoke-AzCli @(
        'containerapp', 'show',
        '--name', $ContainerAppName,
        '--resource-group', $ResourceGroup,
        '--query', 'properties.configuration.ingress.fqdn',
        '--output', 'tsv')
    $masterKey = Invoke-AzCli @(
        'keyvault', 'secret', 'show',
        '--vault-name', $KeyVaultName,
        '--name', 'host--masterKey--master',
        '--query', 'value',
        '--output', 'tsv')
    if ([string]::IsNullOrWhiteSpace($masterKey)) {
        throw 'The Functions master key is missing from the dedicated Key Vault.'
    }

    $keyUri = 'https://{0}/admin/functions/{1}/keys/{2}' -f $fqdn, [Uri]::EscapeDataString($FunctionName), [Uri]::EscapeDataString($FunctionKeyName)
    $headers = @{ 'x-functions-key' = $masterKey.Trim() }
    $functionKey = $null
    if (-not $Rotate) {
        try {
            $existingKey = Invoke-RestMethod -Uri $keyUri -Method Get -Headers $headers -TimeoutSec 30
            $functionKey = [string]$existingKey.value
        }
        catch {
            $statusCode = $null
            if ($_.Exception.Response -and $_.Exception.Response.PSObject.Properties.Name -contains 'StatusCode') {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
            if ($statusCode -ne 404) {
                throw 'Could not read the existing Worker function key through the Functions management API.'
            }
        }
    }

    if ($Rotate -or [string]::IsNullOrWhiteSpace($functionKey)) {
        $createdKey = Invoke-RestMethod -Uri $keyUri -Method Post -Headers $headers -TimeoutSec 30
        $functionKey = [string]$createdKey.value
    }

    $functionKey = $functionKey.Trim()
    if ([string]::IsNullOrWhiteSpace($functionKey)) {
        throw 'The Functions management API did not return the Worker function key.'
    }

    $storedSecretId = Invoke-AzCli @(
        'keyvault', 'secret', 'show',
        '--vault-name', $KeyVaultName,
        '--name', $KeyVaultSecretName,
        '--query', 'id',
        '--output', 'tsv') -AllowFailure
    if ([string]::IsNullOrWhiteSpace($storedSecretId)) {
        throw "The Worker function key was not persisted to Key Vault secret '$KeyVaultSecretName'."
    }

    $credential = [ordered]@{
        endpoint = "https://$fqdn/api/$FunctionName"
        functionName = $FunctionName
        functionKeyName = $FunctionKeyName
        functionKey = $functionKey
        keyVaultName = $KeyVaultName
        keyVaultSecretName = $KeyVaultSecretName
    }

    $stagingPath = "$credentialPathFull.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        $json = ConvertTo-Json -InputObject $credential -Depth 3
        [IO.File]::WriteAllText($stagingPath, $json, [Text.UTF8Encoding]::new($false))
        Assert-PrivateFile -Path $stagingPath
        Move-Item -LiteralPath $stagingPath -Destination $credentialPathFull -Force
        Assert-PrivateFile -Path $credentialPathFull
        Write-Output "Invocation credential saved outside the repository at '$credentialPathFull' and synchronized to Key Vault '$KeyVaultName'."
    }
    finally {
        if (Test-Path -LiteralPath $stagingPath) {
            Remove-Item -LiteralPath $stagingPath -Force
        }
    }
}
finally {
    if ($replicasRaised) {
        $null = Invoke-AzCli @(
            'containerapp', 'update',
            '--name', $ContainerAppName,
            '--resource-group', $ResourceGroup,
            '--min-replicas', [string]$originalMinReplicas,
            '--only-show-errors',
            '--output', 'none')
    }
}
