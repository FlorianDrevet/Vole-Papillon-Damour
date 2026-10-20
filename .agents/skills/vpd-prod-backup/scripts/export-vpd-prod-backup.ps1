#Requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resourceGroup = 'rg-vpd-dev'
$sqlServerName = 'vpd-sql-dev'
$databaseName = 'vole-papillon-damour-db'
$storageAccountName = 'vpdstdevfc'
$runId = '{0}-{1}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$restoreDatabaseName = "vpd-backup-$runId"
$firewallRuleName = "codex-backup-$runId"
$restorePointUtc = (Get-Date).ToUniversalTime().AddMinutes(-15).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
$desktopPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
$backupPath = Join-Path $desktopPath "VPD-Sauvegarde-Production-$runId"
$databasePath = Join-Path $backupPath 'database'
$blobRootPath = Join-Path $backupPath 'blobs'
$blobMetadataPath = Join-Path $backupPath 'blob-metadata'
$manifestPath = Join-Path $backupPath 'manifest.json'
$readmePath = Join-Path $backupPath 'README-RESTAURATION.md'
$script:firewallRuleAttempted = $false
$script:restoreDatabaseAttempted = $false
$script:cleanupFailures = [System.Collections.Generic.List[string]]::new()

function Invoke-AzJson {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = & az @Arguments --output json
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Azure CLI a échoué sur 'az $($Arguments[0])' (code $exitCode)."
    }

    $jsonText = $output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($jsonText)) {
        return $null
    }

    return ConvertFrom-Json -InputObject $jsonText
}

function Invoke-Az {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & az @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Azure CLI a échoué sur 'az $($Arguments[0])' (code $exitCode)."
    }
}

function Resolve-SqlPackagePath {
    $command = Get-Command SqlPackage -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'SqlPackage et le SDK .NET sont absents. Installe le SDK .NET puis relance la sauvegarde.'
    }

    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrWhiteSpace($localAppData)) {
        throw 'Le dossier LocalAppData est introuvable pour installer SqlPackage.'
    }

    $toolDirectory = Join-Path $localAppData 'VpdBackup\SqlPackage'
    $toolExecutable = Join-Path $toolDirectory 'sqlpackage.exe'
    if (-not (Test-Path -LiteralPath $toolExecutable)) {
        New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
        & $dotnet.Source tool install --tool-path $toolDirectory microsoft.sqlpackage --allow-roll-forward
        if ($LASTEXITCODE -ne 0) {
            throw "L'installation locale de SqlPackage a échoué. Vérifie le SDK .NET et la source NuGet nuget.org."
        }
    }

    if (-not (Test-Path -LiteralPath $toolExecutable)) {
        throw "SqlPackage n'a pas été trouvé après son installation dans $toolDirectory."
    }
    return $toolExecutable
}

function Remove-TemporaryAzureResources {
    $script:cleanupFailures.Clear()

    if ($script:firewallRuleAttempted) {
        try {
            $rules = @(Invoke-AzJson -Arguments @('sql', 'server', 'firewall-rule', 'list', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--query', '[].name'))
            if ($rules -contains $firewallRuleName) {
                Invoke-Az -Arguments @('sql', 'server', 'firewall-rule', 'delete', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--name', $firewallRuleName, '--output', 'none')
            }
            $script:firewallRuleAttempted = $false
        }
        catch {
            $script:cleanupFailures.Add("Règle de pare-feu à vérifier : $firewallRuleName")
        }
    }

    if ($script:restoreDatabaseAttempted) {
        try {
            $databases = @(Invoke-AzJson -Arguments @('sql', 'db', 'list', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--query', '[].name'))
            if ($databases -contains $restoreDatabaseName) {
                Invoke-Az -Arguments @('sql', 'db', 'delete', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--name', $restoreDatabaseName, '--yes', '--output', 'none')
            }
            $script:restoreDatabaseAttempted = $false
        }
        catch {
            $script:cleanupFailures.Add("Base temporaire à vérifier : $restoreDatabaseName")
        }
    }
}

$manifest = [ordered]@{
    status = 'Preflight'
    environment = "production (noms Azure contenant dev, confirmé par l’utilisateur)"
    createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    subscriptionName = $null
    subscriptionId = $null
    resourceGroup = $resourceGroup
    sqlServer = $sqlServerName
    database = $databaseName
    storageAccount = $storageAccountName
    databaseRestorePointUtc = $restorePointUtc
    databaseFile = $null
    databaseSizeBytes = $null
    blobContainers = @()
    cleanupWarnings = @()
}

$failureMessage = $null
$sqlPackagePath = $null

try {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) est requis. Installe-le puis connecte-toi avec az login.'
    }
    if ([string]::IsNullOrWhiteSpace($desktopPath) -or -not (Test-Path -LiteralPath $desktopPath)) {
        throw 'Le dossier Bureau Windows est introuvable.'
    }

    $account = Invoke-AzJson -Arguments @('account', 'show')
    if (-not $account.id -or -not $account.tenantId) {
        throw 'Aucune subscription Azure active. Connecte-toi avec az login et sélectionne la subscription voulue.'
    }
    $manifest.subscriptionName = $account.name
    $manifest.subscriptionId = $account.id

    $server = Invoke-AzJson -Arguments @('sql', 'server', 'show', '--resource-group', $resourceGroup, '--name', $sqlServerName)
    $sourceDatabase = Invoke-AzJson -Arguments @('sql', 'db', 'show', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--name', $databaseName)
    if ($sourceDatabase.status -ne 'Online') {
        throw "La base source n'est pas Online (état : $($sourceDatabase.status))."
    }
    if (-not $server.fullyQualifiedDomainName) {
        throw 'Le nom DNS du serveur SQL Azure est introuvable.'
    }

    # Vérifie les droits Blob avant toute création de ressource Azure.
    $containers = @(Invoke-AzJson -Arguments @('storage', 'container', 'list', '--account-name', $storageAccountName, '--auth-mode', 'login', '--include-metadata', 'true'))
    if ($containers.Count -eq 0) {
        throw "Aucun conteneur Blob visible dans $storageAccountName; vérifie la subscription et le rôle Storage Blob Data Reader."
    }

    $sqlPackagePath = Resolve-SqlPackagePath

    $existingDatabases = @(Invoke-AzJson -Arguments @('sql', 'db', 'list', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--query', '[].name'))
    if ($existingDatabases -contains $restoreDatabaseName) {
        throw "La base temporaire prévue existe déjà : $restoreDatabaseName. Aucune ressource n'a été modifiée."
    }

    $ipText = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 15).ToString().Trim()
    $ipAddress = [System.Net.IPAddress]::Parse($ipText)
    if ($ipAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
        throw "Le service IP n’a pas renvoyé une adresse IPv4 utilisable par le pare-feu Azure SQL."
    }

    & $sqlPackagePath /Version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'SqlPackage ne démarre pas correctement.'
    }

    New-Item -ItemType Directory -Path $databasePath -Force | Out-Null
    New-Item -ItemType Directory -Path $blobRootPath -Force | Out-Null
    New-Item -ItemType Directory -Path $blobMetadataPath -Force | Out-Null

    $manifest.status = 'Export SQL en cours'
    Write-Host "Cible confirmée : PRODUCTION — $resourceGroup / $sqlServerName / $databaseName"
    Write-Host "Subscription : $($account.name) ($($account.id))"
    Write-Host "Dossier de sauvegarde : $backupPath"
    Write-Host "Point SQL demandé (UTC) : $restorePointUtc"
    Write-Host 'SqlPackage peut ouvrir une fenêtre de connexion Microsoft Entra.'

    try {
        $script:restoreDatabaseAttempted = $true
        Invoke-Az -Arguments @('sql', 'db', 'restore', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--name', $databaseName, '--dest-name', $restoreDatabaseName, '--time', $restorePointUtc, '--edition', 'Standard', '--service-objective', 'S1', '--output', 'none')

        $status = ''
        for ($attempt = 1; $attempt -le 90; $attempt++) {
            $statusOutput = & az sql db show --resource-group $resourceGroup --server $sqlServerName --name $restoreDatabaseName --query status --output tsv 2>$null
            if ($LASTEXITCODE -eq 0) {
                $status = ($statusOutput -join '').Trim()
            }
            if ($status -eq 'Online') {
                break
            }
            Start-Sleep -Seconds 20
        }
        if ($status -ne 'Online') {
            throw "La copie PITR n'est pas devenue Online (dernier état : $status)."
        }

        $script:firewallRuleAttempted = $true
        Invoke-Az -Arguments @('sql', 'server', 'firewall-rule', 'create', '--resource-group', $resourceGroup, '--server', $sqlServerName, '--name', $firewallRuleName, '--start-ip-address', $ipText, '--end-ip-address', $ipText, '--output', 'none')

        $bacpacPath = Join-Path $databasePath "$databaseName-$runId.bacpac"
        $connectionString = "Server=tcp:$($server.fullyQualifiedDomainName),1433;Initial Catalog=$restoreDatabaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
        & $sqlPackagePath /Action:Export "/TargetFile:$bacpacPath" "/SourceConnectionString:$connectionString" /ua:True "/tid:$($account.tenantId)"
        if ($LASTEXITCODE -ne 0) {
            throw "SqlPackage n’a pas terminé l’export BACPAC."
        }
        if (-not (Test-Path -LiteralPath $bacpacPath) -or (Get-Item -LiteralPath $bacpacPath).Length -eq 0) {
            throw 'Le fichier BACPAC est absent ou vide.'
        }

        $manifest.databaseFile = [System.IO.Path]::GetRelativePath($backupPath, $bacpacPath)
        $manifest.databaseSizeBytes = (Get-Item -LiteralPath $bacpacPath).Length
    }
    finally {
        Remove-TemporaryAzureResources
    }

    $manifest.status = 'Téléchargement Blob en cours'
    foreach ($container in $containers) {
        $containerName = $container.name
        $containerPath = Join-Path $blobRootPath $containerName
        New-Item -ItemType Directory -Path $containerPath -Force | Out-Null
        $blobList = @(Invoke-AzJson -Arguments @('storage', 'blob', 'list', '--account-name', $storageAccountName, '--container-name', $containerName, '--auth-mode', 'login', '--include', 'm'))
        $metadataFilePath = Join-Path $blobMetadataPath "$containerName.json"
        ConvertTo-Json -InputObject $blobList -Depth 16 | Set-Content -LiteralPath $metadataFilePath -Encoding utf8
        Invoke-Az -Arguments @('storage', 'blob', 'download-batch', '--account-name', $storageAccountName, '--source', $containerName, '--destination', $containerPath, '--auth-mode', 'login', '--only-show-errors')

        $containerFiles = @(Get-ChildItem -LiteralPath $containerPath -File -Recurse)
        $containerBytes = ($containerFiles | Measure-Object -Property Length -Sum).Sum
        if ($null -eq $containerBytes) { $containerBytes = 0 }
        $publicAccess = if ($container.properties) { $container.properties.publicAccess } else { $null }
        $containerMetadata = if ($container.metadata) { $container.metadata } else { @{} }
        $manifest.blobContainers += [pscustomobject]@{
            name = $containerName
            publicAccess = $publicAccess
            containerMetadata = $containerMetadata
            fileCount = $containerFiles.Count
            sizeBytes = [long]$containerBytes
            metadataFile = [System.IO.Path]::GetRelativePath($backupPath, $metadataFilePath)
        }
    }

    $readme = @"
# Sauvegarde Vole-Papillon-Damour — production

- Cible : ``$databaseName`` sur ``$sqlServerName`` (``$resourceGroup``), environnement de production malgré le nom Azure contenant ``dev``.
- Point SQL restauré avant export (UTC) : ``$restorePointUtc``.
- Archive SQL : ``database/$databaseName-$runId.bacpac``.
- Fichiers Blob : ``blobs/<nom-du-conteneur>/...``; leurs propriétés et métadonnées figurent dans ``blob-metadata/<nom-du-conteneur>.json``.
- Les noms, accès public et tailles des conteneurs figurent dans ``manifest.json``; les empreintes SHA-256 des fichiers sont dans ``checksums.sha256``.
- Le point SQL et la copie Blob ne constituent pas une transaction distribuée : les blobs sont copiés après l’export SQL.

## Restauration SQL

Le BACPAC contient le schéma et les données SQL. Il faut l'importer avec SqlPackage ou SSMS dans une **nouvelle base**, vérifier cette base et réinitialiser les identifiants SQL contenus comme utilisateurs de base si nécessaire. Ne l'importe pas dans la base de production active. Pour retrouver un point précis dans la période de rétention, utilise de préférence la restauration PITR/LTR native d'Azure SQL.

## Restauration des fichiers

Les blobs sont copiés séparément de SQL. Crée d'abord un compte/conteneurs de destination avec l'accès public indiqué dans `manifest.json`, puis envoie chaque dossier de `blobs` dans son conteneur correspondant. Les fichiers JSON dans `blob-metadata` gardent les noms, propriétés HTTP et métadonnées pour la remise en place. Vérifie les noms et les empreintes avant de modifier la configuration de l'application. Le script ne change aucune configuration applicative et ne réécrit aucun blob Azure.

## Protection de la copie

Un fichier BACPAC n'est pas chiffré par défaut. Conserve ce dossier sur un disque protégé par BitLocker et limite son accès. Le dossier contient des données de production.
"@
    Set-Content -LiteralPath $readmePath -Value $readme -Encoding utf8

    $files = @(Get-ChildItem -LiteralPath $backupPath -File -Recurse | Where-Object { $_.Name -notin @('manifest.json', 'checksums.sha256') })
    $checksumLines = foreach ($file in $files) {
        $relativePath = [System.IO.Path]::GetRelativePath($backupPath, $file.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "{0} *{1}" -f $hash, $relativePath
    }
    Set-Content -LiteralPath (Join-Path $backupPath 'checksums.sha256') -Value $checksumLines -Encoding utf8
    $manifest.status = 'Completed'
}
catch {
    $failureMessage = $_.Exception.Message
    $manifest.status = 'Failed'
    $manifest.failure = $failureMessage
}
finally {
    if ($script:firewallRuleAttempted -or $script:restoreDatabaseAttempted) {
        Remove-TemporaryAzureResources
    }
    $manifest.cleanupWarnings = @($script:cleanupFailures)
    if ($script:cleanupFailures.Count -gt 0 -and $manifest.status -eq 'Completed') {
        $manifest.status = 'CompletedWithCleanupWarning'
    }
    if (Test-Path -LiteralPath $backupPath) {
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    }
}

if ($manifest.status -in @('Completed', 'CompletedWithCleanupWarning')) {
    Write-Host "Dossier de sauvegarde : $backupPath"
}
if ($failureMessage) {
    $cleanupDetails = if ($script:cleanupFailures.Count -gt 0) { '; nettoyage à vérifier : ' + ($script:cleanupFailures -join '; ') } else { '' }
    throw "Sauvegarde incomplète. Dossier conservé : $backupPath. Détail : $failureMessage$cleanupDetails"
}
if ($script:cleanupFailures.Count -gt 0) {
    throw ("Nettoyage Azure à vérifier : " + ($script:cleanupFailures -join '; '))
}
if ($manifest.status -eq 'Completed') {
    Write-Host 'Sauvegarde terminée.'
}
elseif ($manifest.status -eq 'CompletedWithCleanupWarning') {
    Write-Warning "Sauvegarde terminée, mais le nettoyage Azure doit être vérifié : $($manifest.cleanupWarnings -join '; ')"
}
