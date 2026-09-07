#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication, Microsoft.Graph.Applications, Microsoft.Graph.Identity.SignIns

<#
.SYNOPSIS
    Creates or updates the External ID self-service signup flow for the public catalog.

.DESCRIPTION
    The catalog starts signup with MSAL's prompt=create request. This script provisions
    the corresponding Microsoft Graph authentication events flow and associates it only
    with the catalog application. Passwords remain entirely in the Entra-hosted flow.

    The script is deliberately separate from Configure-EntraApps.ps1: application
    registration and user-flow changes have different Graph permissions and lifecycles.

.EXAMPLE
    ./Configure-EntraUserFlow.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd -Environment dev -UseDeviceCode -WhatIf

.EXAMPLE
    ./Configure-EntraUserFlow.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd -Environment dev -UseDeviceCode
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TenantId,

    [ValidatePattern('^[a-z0-9-]+$')]
    [string]$Environment = 'dev',

    [switch]$UseDeviceCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$catalogAppName = "vpd-catalog-$Environment"
$flowDisplayName = "vpd-catalog-signup-$Environment"
$flowDescription = 'Self-service account creation for public Vole Papillon d''Amour catalog members.'

function New-CatalogSignupFlowBody {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CatalogClientId,

        [Parameter(Mandatory = $true)]
        [string]$DisplayName,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    return @{
        '@odata.type' = '#microsoft.graph.externalUsersSelfServiceSignUpEventsFlow'
        displayName = $DisplayName
        description = $Description
        conditions = @{
            applications = @{
                includeApplications = @(
                    @{
                        appId = $CatalogClientId
                    }
                )
            }
        }
        onAuthenticationMethodLoadStart = @{
            '@odata.type' = '#microsoft.graph.onAuthenticationMethodLoadStartExternalUsersSelfServiceSignUp'
            identityProviders = @(
                @{
                    id = 'EmailPassword-OAUTH'
                }
            )
        }
        onInteractiveAuthFlowStart = @{
            '@odata.type' = '#microsoft.graph.onInteractiveAuthFlowStartExternalUsersSelfServiceSignUp'
            isSignUpAllowed = $true
        }
        onAttributeCollection = @{
            '@odata.type' = '#microsoft.graph.onAttributeCollectionExternalUsersSelfServiceSignUp'
            attributes = @(
                @{
                    id = 'email'
                    displayName = 'Adresse e-mail'
                    description = 'Adresse e-mail du membre.'
                    userFlowAttributeType = 'builtIn'
                    dataType = 'string'
                }
                @{
                    id = 'displayName'
                    displayName = 'Nom affiché'
                    description = 'Nom affiché du membre dans le catalogue.'
                    userFlowAttributeType = 'builtIn'
                    dataType = 'string'
                }
            )
            attributeCollectionPage = @{
                views = @(
                    @{
                        title = 'Créer votre compte'
                        description = 'Utilisez une adresse e-mail valide pour créer votre compte.'
                        inputs = @(
                            @{
                                attribute = 'email'
                                label = 'Adresse e-mail'
                                inputType = 'text'
                                hidden = $true
                                editable = $false
                                writeToDirectory = $true
                                required = $true
                                validationRegEx = '^[a-zA-Z0-9.!#$%&*+/=?^_`{|}~-]+@[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)*$'
                            }
                            @{
                                attribute = 'displayName'
                                label = 'Nom affiché'
                                inputType = 'text'
                                hidden = $false
                                editable = $true
                                writeToDirectory = $true
                                required = $false
                                validationRegEx = '^[\p{L}0-9][\p{L}0-9 .''_-]*$'
                            }
                        )
                    }
                )
            }
        }
    }
}

function Get-FlowIncludedApplicationIds {
    param(
        [AllowNull()]
        [object]$Flow
    )

    if ($null -eq $Flow) {
        return @()
    }

    $conditionsProperty = @($Flow.PSObject.Properties | Where-Object { $_.Name -ieq 'conditions' }) | Select-Object -First 1
    if ($null -eq $conditionsProperty -or $null -eq $conditionsProperty.Value) {
        return @()
    }

    $applicationsProperty = @($conditionsProperty.Value.PSObject.Properties | Where-Object { $_.Name -ieq 'applications' }) | Select-Object -First 1
    if ($null -eq $applicationsProperty -or $null -eq $applicationsProperty.Value) {
        return @()
    }

    $includedApplicationsProperty = @($applicationsProperty.Value.PSObject.Properties | Where-Object { $_.Name -ieq 'includeApplications' }) | Select-Object -First 1
    if ($null -eq $includedApplicationsProperty -or $null -eq $includedApplicationsProperty.Value) {
        return @()
    }

    return @(
        @($includedApplicationsProperty.Value) | ForEach-Object {
            $appIdProperty = @($_.PSObject.Properties | Where-Object { $_.Name -ieq 'appId' }) | Select-Object -First 1
            if ($null -ne $appIdProperty) {
                $appIdProperty.Value
            }
        }
    )
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$graphScopes = @(
    'Application.Read.All'
    'EventListener.ReadWrite.All'
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

$catalogApplications = @(Get-MgApplication -Filter "displayName eq '$catalogAppName'")
if ($catalogApplications.Count -eq 0) {
    throw "L'application '$catalogAppName' est introuvable dans le tenant '$TenantId'. Exécutez d'abord Configure-EntraApps.ps1."
}
if ($catalogApplications.Count -gt 1) {
    throw "Plusieurs applications portent le nom '$catalogAppName'. L'association du user flow est interrompue pour éviter une mauvaise configuration."
}

$catalogApplication = $catalogApplications[0]
if ([string]::IsNullOrWhiteSpace($catalogApplication.AppId)) {
    throw "L'application '$catalogAppName' ne possède pas de client ID exploitable."
}

$allFlows = @(Get-MgIdentityAuthenticationEventFlow -All)
$matchingFlows = @($allFlows | Where-Object { $_.DisplayName -eq $flowDisplayName })
if ($matchingFlows.Count -gt 1) {
    throw "Plusieurs user flows portent le nom '$flowDisplayName'. Supprimez le doublon manuellement avant de relancer le script."
}

$existingFlow = $null
if ($matchingFlows.Count -eq 1) {
    $existingFlow = $matchingFlows[0]
}

$catalogAssociatedFlows = @(
    $allFlows | Where-Object {
        (Get-FlowIncludedApplicationIds -Flow $_) -contains $catalogApplication.AppId
    }
)
if ($catalogAssociatedFlows.Count -gt 1) {
    throw "Le catalogue '$catalogAppName' est associé à plusieurs user flows. Corrigez la configuration External ID manuellement."
}
if ($catalogAssociatedFlows.Count -eq 1 -and ($null -eq $existingFlow -or $catalogAssociatedFlows[0].Id -ne $existingFlow.Id)) {
    throw "Le catalogue '$catalogAppName' est déjà associé au user flow '$($catalogAssociatedFlows[0].DisplayName)'. Le script ne remplace pas automatiquement un user flow existant."
}

$flowBody = New-CatalogSignupFlowBody `
    -CatalogClientId $catalogApplication.AppId `
    -DisplayName $flowDisplayName `
    -Description $flowDescription

if ($null -eq $existingFlow) {
    if ($PSCmdlet.ShouldProcess($flowDisplayName, "Créer et associer le user flow à $catalogAppName")) {
        $flow = New-MgIdentityAuthenticationEventFlow -BodyParameter $flowBody
        $action = 'created'
        $applied = $true
    }
    else {
        $flow = $null
        $action = 'would-create'
        $applied = $false
    }
}
else {
    if ($PSCmdlet.ShouldProcess($flowDisplayName, "Mettre à jour et associer le user flow à $catalogAppName")) {
        $flow = Update-MgIdentityAuthenticationEventFlow `
            -AuthenticationEventsFlowId $existingFlow.Id `
            -BodyParameter $flowBody
        $action = 'updated'
        $applied = $true
    }
    else {
        $flow = $existingFlow
        $action = 'would-update'
        $applied = $false
    }
}

[pscustomobject]@{
    Action = $action
    Applied = $applied
    FlowId = if ($null -eq $flow) { $null } else { $flow.Id }
    FlowDisplayName = $flowDisplayName
    CatalogApplication = $catalogAppName
    CatalogClientId = $catalogApplication.AppId
}
