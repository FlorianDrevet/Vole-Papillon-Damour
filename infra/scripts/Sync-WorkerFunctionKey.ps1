[CmdletBinding()]
param(
    [string] $SubscriptionId = 'fc4fa56f-e4e7-41e9-8a0d-559fcd27b827',
    [string] $ResourceGroup = 'rg-vpd-dev',
    [string] $ContainerAppName = 'vpd-worker-ca-dev-fc',
    [string] $KeyVaultName = 'vpd-worker-fn-kv-dev-fc',
    [string] $FunctionName = 'IsbndbCatchUpManual',
    [string] $FunctionKeyName = 'local-runner',
    [string] $KeyVaultSecretName = 'isbndb-catchup-manual-local-runner',
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
    $output = & rtk proxy az @cliArguments 2>$null
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        if ($AllowFailure) {
            return $null
        }

        throw "Azure CLI failed while running 'az $($Arguments[0]) $($Arguments[1])'."
    }

    return ($output -join [Environment]::NewLine).Trim()
}

function Set-PrivateAcl {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [bool] $Directory
    )

    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')

    if ($Directory) {
        $acl = [Security.AccessControl.DirectorySecurity]::new()
        $acl.SetAccessRuleProtection($true, $false)
        $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
            [Security.AccessControl.InheritanceFlags]::ObjectInherit
        foreach ($principal in @($currentUser, $system)) {
            $rule = [Security.AccessControl.FileSystemAccessRule]::new(
                $principal,
                [Security.AccessControl.FileSystemRights]::FullControl,
                $inheritance,
                [Security.AccessControl.PropagationFlags]::None,
                [Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($rule)
        }
    }
    else {
        $acl = [Security.AccessControl.FileSecurity]::new()
        $acl.SetAccessRuleProtection($true, $false)
        foreach ($principal in @($currentUser, $system)) {
            $rule = [Security.AccessControl.FileSystemAccessRule]::new(
                $principal,
                [Security.AccessControl.FileSystemRights]::FullControl,
                [Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($rule)
        }
    }

    Set-Acl -LiteralPath $Path -AclObject $acl
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This helper creates a Windows user-only credential file and must run on Windows.'
}

$credentialPathFull = [IO.Path]::GetFullPath($CredentialPath)
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ($credentialPathFull.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'CredentialPath must be outside the repository.'
}

$directoryPath = Split-Path -Parent $credentialPathFull
New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
Set-PrivateAcl -Path $directoryPath -Directory $true

$scaleJson = Invoke-AzCli @(
    'containerapp', 'show',
    '--name', $ContainerAppName,
    '--resource-group', $ResourceGroup,
    '--query', 'properties.template.scale',
    '--output', 'json')
$originalMinReplicas = [int]((ConvertFrom-Json $scaleJson).minReplicas)
$replicasRaised = $false
$temporaryKeyFile = $null

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

    $keyArguments = @(
        'containerapp', 'function', 'keys', 'show',
        '--resource-group', $ResourceGroup,
        '--name', $ContainerAppName,
        '--function-name', $FunctionName,
        '--key-type', 'functionKey',
        '--key-name', $FunctionKeyName,
        '--query', 'value',
        '--output', 'tsv')

    $functionKey = $null
    if (-not $Rotate) {
        $functionKey = Invoke-AzCli $keyArguments -AllowFailure
    }

    if ($Rotate -or [string]::IsNullOrWhiteSpace($functionKey)) {
        $null = Invoke-AzCli @(
            'containerapp', 'function', 'keys', 'set',
            '--resource-group', $ResourceGroup,
            '--name', $ContainerAppName,
            '--function-name', $FunctionName,
            '--key-type', 'functionKey',
            '--key-name', $FunctionKeyName,
            '--only-show-errors',
            '--output', 'none')
        $functionKey = Invoke-AzCli $keyArguments
    }

    $functionKey = $functionKey.Trim()
    if ([string]::IsNullOrWhiteSpace($functionKey)) {
        throw 'Azure did not return the Worker function key.'
    }

    $storedKey = Invoke-AzCli @(
        'keyvault', 'secret', 'show',
        '--vault-name', $KeyVaultName,
        '--name', $KeyVaultSecretName,
        '--query', 'value',
        '--output', 'tsv') -AllowFailure

    if ($storedKey -ne $functionKey) {
        $temporaryKeyFile = Join-Path $directoryPath ('.worker-key-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::WriteAllText($temporaryKeyFile, $functionKey, [Text.UTF8Encoding]::new($false))
        Set-PrivateAcl -Path $temporaryKeyFile -Directory $false
        $null = Invoke-AzCli @(
            'keyvault', 'secret', 'set',
            '--vault-name', $KeyVaultName,
            '--name', $KeyVaultSecretName,
            '--file', $temporaryKeyFile,
            '--encoding', 'utf-8',
            '--content-type', 'Azure Functions invocation key',
            '--query', 'id',
            '--output', 'tsv')
    }

    $fqdn = Invoke-AzCli @(
        'containerapp', 'show',
        '--name', $ContainerAppName,
        '--resource-group', $ResourceGroup,
        '--query', 'properties.configuration.ingress.fqdn',
        '--output', 'tsv')
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
        Set-PrivateAcl -Path $stagingPath -Directory $false
        Move-Item -LiteralPath $stagingPath -Destination $credentialPathFull -Force
        Set-PrivateAcl -Path $credentialPathFull -Directory $false
    }
    finally {
        if (Test-Path -LiteralPath $stagingPath) {
            Remove-Item -LiteralPath $stagingPath -Force
        }
    }

    Write-Output "Invocation credential saved outside the repository at '$credentialPathFull' and synchronized to Key Vault '$KeyVaultName'."
}
finally {
    if ($temporaryKeyFile -and (Test-Path -LiteralPath $temporaryKeyFile)) {
        Remove-Item -LiteralPath $temporaryKeyFile -Force
    }

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
