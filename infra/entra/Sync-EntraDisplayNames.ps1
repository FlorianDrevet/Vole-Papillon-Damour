#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication, Microsoft.Graph.Users

<#
.SYNOPSIS
    Rebuilds placeholder External ID display names from givenName and surname.

.DESCRIPTION
    Existing External ID members created before the separate name fields were
    enabled can still have displayName set to "unknown". This script updates
    only placeholder display names and never overwrites an existing real name.

    Use -WhatIf first. The delegated Graph permission User.ReadWrite.All
    requires administrator consent in the tenant.

.EXAMPLE
    ./Sync-EntraDisplayNames.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd -UseDeviceCode -WhatIf

.EXAMPLE
    ./Sync-EntraDisplayNames.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd -UseDeviceCode
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TenantId,

    [switch]$UseDeviceCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EntraDisplayName {
    param(
        [AllowNull()]
        [string]$GivenName,

        [AllowNull()]
        [string]$Surname
    )

    $parts = @(@($GivenName, $Surname) |
        ForEach-Object {
            if (-not [string]::IsNullOrWhiteSpace($_)) {
                $_.Trim()
            }
        })

    if ($parts.Count -eq 0) {
        return $null
    }

    return [string]::Join(' ', [string[]]$parts)
}

function Test-EntraDisplayNamePlaceholder {
    param(
        [AllowNull()]
        [string]$DisplayName
    )

    return [string]::IsNullOrWhiteSpace($DisplayName) -or
        $DisplayName.Trim() -match '^(unknown|undefined|null)$'
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$graphScopes = @(
    'User.Read.All'
    'User.ReadWrite.All'
)
$connectParameters = @{
    TenantId = $TenantId
    Scopes = $graphScopes
    NoWelcome = $true
}
if ($UseDeviceCode.IsPresent) {
    $connectParameters.UseDeviceCode = $true
}

$null = Connect-MgGraph @connectParameters
$users = @(Get-MgUser -All -Property @('id', 'displayName', 'givenName', 'surname'))
$updatedCount = 0
$skippedCount = 0

foreach ($user in $users) {
    if (-not (Test-EntraDisplayNamePlaceholder -DisplayName $user.DisplayName)) {
        $skippedCount++
        [pscustomobject]@{
            Action = 'skipped'
            UserId = $user.Id
            DisplayName = $user.DisplayName
            Reason = 'display-name-already-set'
        }
        continue
    }

    $displayName = Get-EntraDisplayName -GivenName $user.GivenName -Surname $user.Surname
    if ([string]::IsNullOrWhiteSpace($displayName)) {
        $skippedCount++
        [pscustomobject]@{
            Action = 'skipped'
            UserId = $user.Id
            DisplayName = $user.DisplayName
            Reason = 'givenName-and-surname-are-empty'
        }
        continue
    }

    if ($PSCmdlet.ShouldProcess($user.Id, "Set Entra displayName to '$displayName'")) {
        Update-MgUser -UserId $user.Id -DisplayName $displayName
        $updatedCount++
        [pscustomobject]@{
            Action = 'updated'
            UserId = $user.Id
            DisplayName = $displayName
            Reason = 'placeholder-replaced'
        }
    }
    else {
        [pscustomobject]@{
            Action = 'would-update'
            UserId = $user.Id
            DisplayName = $displayName
            Reason = 'what-if'
        }
    }
}

Write-Host "Entra displayName synchronization: $updatedCount updated, $skippedCount skipped."
