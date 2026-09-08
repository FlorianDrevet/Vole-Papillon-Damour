#Requires -Version 7.0
#Requires -Modules Microsoft.Graph.Authentication, Microsoft.Graph.Identity.DirectoryManagement

<#
.SYNOPSIS
    Applies the Vole Papillon d'Amour branding to the External ID hosted pages.

.DESCRIPTION
    Configures the tenant-level and fr-FR company branding used by browser-
    delegated External ID sign-in and sign-up pages. The script uploads the
    catalog CSS and applies French copy without touching application secrets.

    Native authentication is not configured here. It is a separate integration
    choice that moves the authentication UI into the Angular application.

.EXAMPLE
    ./Configure-EntraBranding.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd `
        -UseDeviceCode -WhatIf

.EXAMPLE
    ./Configure-EntraBranding.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd `
        -UseDeviceCode

.EXAMPLE
    ./Configure-EntraBranding.ps1 -TenantId b23c80b3-9776-4840-8255-fcbf3b3500fd `
        -HeaderLogoPath ./branding/header-logo.png `
        -FaviconPath ./branding/favicon.png `
        -BackgroundImagePath ./branding/background.png
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TenantId,

    [ValidateNotNullOrEmpty()]
    [string]$CustomCssPath = (Join-Path $PSScriptRoot 'vpd-catalog-authentication.css'),

    [ValidateSet('fr-FR')]
    [string]$Locale = 'fr-FR',

    [string]$HeaderLogoPath,

    [string]$FaviconPath,

    [string]$BackgroundImagePath,

    [switch]$UseDeviceCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-VpdBrandingUpdateBody {
    return @{
        backgroundColor = '#eaf6fb'
        headerBackgroundColor = '#041d30'
        usernameHintText = 'Votre adresse e-mail'
        signInPageText = "Connexion sécurisée au catalogue Vole Papillon d’Amour."
        customForgotMyPasswordText = 'Mot de passe oublié ?'
        customPrivacyAndCookiesText = 'Confidentialité et cookies'
        customTermsOfUseText = "Conditions d’utilisation"
    }
}

function New-VpdBrandingLocalizationBody {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LocalizationId
    )

    $body = New-VpdBrandingUpdateBody
    $body['id'] = $LocalizationId
    return $body
}

function Get-VpdBrandingAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$AllowedExtensions,

        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Nullable[int64]]$MaximumBytes
    )

    $asset = Get-Item -LiteralPath $Path -ErrorAction Stop
    if (-not $asset.PSIsContainer -and $asset.Extension.ToLowerInvariant() -in $AllowedExtensions) {
        if ($null -ne $MaximumBytes -and $asset.Length -gt [int64]$MaximumBytes) {
            throw "$Description '$($asset.FullName)' dépasse la taille maximale de $MaximumBytes octets."
        }

        return $asset
    }

    $extensions = $AllowedExtensions -join ', '
    throw "$Description '$Path' doit être un fichier avec l'une des extensions suivantes : $extensions."
}

function Get-VpdContentType {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$Asset
    )

    switch ($Asset.Extension.ToLowerInvariant()) {
        '.css' { return 'text/css' }
        '.png' { return 'image/png' }
        '.jpg' { return 'image/jpeg' }
        '.jpeg' { return 'image/jpeg' }
        default { throw "Type MIME inconnu pour '$($Asset.FullName)'." }
    }
}

function Find-VpdBrandingLocalization {
    param(
        [AllowNull()]
        [object[]]$Localizations,

        [Parameter(Mandatory = $true)]
        [string]$LocalizationId
    )

    return @(
        $Localizations | Where-Object { $_.Id -ieq $LocalizationId }
    )
}

function Test-VpdBrandingResourceNotFound {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    $messages = @($ErrorRecord.Exception.Message)
    if ($null -ne $ErrorRecord.ErrorDetails) {
        $messages += $ErrorRecord.ErrorDetails.Message
    }

    return (($messages -join ' ') -match 'Request_ResourceNotFound|ResourceNotFound')
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$cssAsset = Get-VpdBrandingAsset `
    -Path $CustomCssPath `
    -AllowedExtensions @('.css') `
    -Description 'La feuille de style External ID' `
    -MaximumBytes 25KB

$headerLogoAsset = $null
if (-not [string]::IsNullOrWhiteSpace($HeaderLogoPath)) {
    $headerLogoAsset = Get-VpdBrandingAsset `
        -Path $HeaderLogoPath `
        -AllowedExtensions @('.png', '.jpg', '.jpeg') `
        -Description "Le logo d’en-tête External ID"
}

$faviconAsset = $null
if (-not [string]::IsNullOrWhiteSpace($FaviconPath)) {
    $faviconAsset = Get-VpdBrandingAsset `
        -Path $FaviconPath `
        -AllowedExtensions @('.png', '.jpg', '.jpeg') `
        -Description 'Le favicon External ID'
}

$backgroundImageAsset = $null
if (-not [string]::IsNullOrWhiteSpace($BackgroundImagePath)) {
    $backgroundImageAsset = Get-VpdBrandingAsset `
        -Path $BackgroundImagePath `
        -AllowedExtensions @('.png', '.jpg', '.jpeg') `
        -Description "L’image de fond External ID" `
        -MaximumBytes 300KB
}

$graphScopes = @('OrganizationalBranding.ReadWrite.All')
$connectParameters = @{
    TenantId = $TenantId
    Scopes = $graphScopes
    NoWelcome = $true
}
if ($UseDeviceCode.IsPresent) {
    $connectParameters.UseDeviceCode = $true
}

$null = Connect-MgGraph @connectParameters -ErrorAction Stop

$brandingBody = New-VpdBrandingUpdateBody
$actions = [System.Collections.Generic.List[string]]::new()

$allLocalizations = @()
try {
    $allLocalizations = @(Get-MgOrganizationBrandingLocalization -OrganizationId $TenantId -ErrorAction Stop)
}
catch {
    if (-not (Test-VpdBrandingResourceNotFound -ErrorRecord $_)) {
        throw
    }

    # En mode -WhatIf, le PATCH ci-dessus n'est volontairement pas exécuté. Il
    # est donc normal que la collection n'existe pas encore sur un tenant neuf.
    $actions.Add('localizations-assumed-empty')
}

$matchingLocalizations = @(Find-VpdBrandingLocalization -Localizations $allLocalizations -LocalizationId $Locale)
if ($matchingLocalizations.Count -gt 1) {
    throw "Plusieurs personnalisations de marque existent pour la locale '$Locale'. Supprimez le doublon manuellement avant de relancer le script."
}

$frenchLocalization = $null
if ($matchingLocalizations.Count -eq 0) {
    if ($PSCmdlet.ShouldProcess($Locale, 'Créer la personnalisation de marque française')) {
        $frenchLocalization = New-MgOrganizationBrandingLocalization `
            -OrganizationId $TenantId `
            -BodyParameter (New-VpdBrandingLocalizationBody -LocalizationId $Locale) `
            -ErrorAction Stop
        $actions.Add('localization-created')
    }
    else {
        $frenchLocalization = [pscustomobject]@{ Id = $Locale }
        $actions.Add('localization-would-create')
    }
}
else {
    $frenchLocalization = $matchingLocalizations[0]
    if ($PSCmdlet.ShouldProcess($Locale, 'Mettre à jour la personnalisation de marque française')) {
        $null = Update-MgOrganizationBrandingLocalization `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId $frenchLocalization.Id `
            -BodyParameter $brandingBody `
            -ErrorAction Stop
        $actions.Add('localization-updated')
    }
    else {
        $actions.Add('localization-would-update')
    }
}

if ($PSCmdlet.ShouldProcess('0', 'Mettre à jour la personnalisation de marque par défaut')) {
    # Dans un tenant External ID, le branding par défaut est la localisation 0.
    # Le cmdlet générique /branding renvoie 404/405 pour les ressources créées
    # par le parcours CIAM ; la même API de localisation fonctionne pour 0.
    $null = Update-MgOrganizationBrandingLocalization `
        -OrganizationId $TenantId `
        -OrganizationalBrandingLocalizationId '0' `
        -BodyParameter $brandingBody `
        -ErrorAction Stop
    $actions.Add('default-updated')
}
else {
    $actions.Add('default-would-update')
}

if ($PSCmdlet.ShouldProcess('0', 'Téléverser la feuille de style External ID par défaut')) {
    $null = Set-MgOrganizationBrandingLocalizationCustomCss `
        -OrganizationId $TenantId `
        -OrganizationalBrandingLocalizationId '0' `
        -InFile $cssAsset.FullName `
        -ContentType (Get-VpdContentType -Asset $cssAsset) `
        -ErrorAction Stop
    $actions.Add('default-css-updated')
}
else {
    $actions.Add('default-css-would-update')
}

if ($PSCmdlet.ShouldProcess($frenchLocalization.Id, 'Téléverser la feuille de style External ID française')) {
    $null = Set-MgOrganizationBrandingLocalizationCustomCss `
        -OrganizationId $TenantId `
        -OrganizationalBrandingLocalizationId $frenchLocalization.Id `
        -InFile $cssAsset.FullName `
        -ContentType (Get-VpdContentType -Asset $cssAsset) `
        -ErrorAction Stop
    $actions.Add('localization-css-updated')
}
else {
    $actions.Add('localization-css-would-update')
}

if ($null -ne $backgroundImageAsset) {
    if ($PSCmdlet.ShouldProcess('0', "Téléverser l’image de fond External ID par défaut")) {
        $null = Set-MgOrganizationBrandingLocalizationBackgroundImage `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId '0' `
            -InFile $backgroundImageAsset.FullName `
            -ContentType (Get-VpdContentType -Asset $backgroundImageAsset) `
            -ErrorAction Stop
        $actions.Add('default-background-image-updated')
    }
    else {
        $actions.Add('default-background-image-would-update')
    }

    if ($PSCmdlet.ShouldProcess($frenchLocalization.Id, "Téléverser l’image de fond External ID française")) {
        $null = Set-MgOrganizationBrandingLocalizationBackgroundImage `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId $frenchLocalization.Id `
            -InFile $backgroundImageAsset.FullName `
            -ContentType (Get-VpdContentType -Asset $backgroundImageAsset) `
            -ErrorAction Stop
        $actions.Add('localization-background-image-updated')
    }
    else {
        $actions.Add('localization-background-image-would-update')
    }
}

if ($null -ne $headerLogoAsset) {
    if ($PSCmdlet.ShouldProcess($TenantId, "Téléverser le logo d’en-tête External ID par défaut")) {
        $null = Set-MgOrganizationBrandingLocalizationHeaderLogo `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId '0' `
            -InFile $headerLogoAsset.FullName `
            -ContentType (Get-VpdContentType -Asset $headerLogoAsset) `
            -ErrorAction Stop
        $actions.Add('default-header-logo-updated')
    }

    if ($PSCmdlet.ShouldProcess($frenchLocalization.Id, "Téléverser le logo d’en-tête External ID français")) {
        $null = Set-MgOrganizationBrandingLocalizationHeaderLogo `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId $frenchLocalization.Id `
            -InFile $headerLogoAsset.FullName `
            -ContentType (Get-VpdContentType -Asset $headerLogoAsset) `
            -ErrorAction Stop
        $actions.Add('localization-header-logo-updated')
    }
}

if ($null -ne $faviconAsset) {
    if ($PSCmdlet.ShouldProcess($TenantId, 'Téléverser le favicon External ID')) {
        $null = Set-MgOrganizationBrandingLocalizationFavicon `
            -OrganizationId $TenantId `
            -OrganizationalBrandingLocalizationId '0' `
            -InFile $faviconAsset.FullName `
            -ContentType (Get-VpdContentType -Asset $faviconAsset) `
            -ErrorAction Stop
        $actions.Add('favicon-updated')
    }
}

[pscustomobject]@{
    Action = $actions -join ', '
    Applied = -not $WhatIfPreference
    TenantId = $TenantId
    Locale = $Locale
    LocalizationAction = $actions | Where-Object { $_ -like 'localization-*' } | Select-Object -First 1
    CssPath = $cssAsset.FullName
    BackgroundImagePath = if ($null -eq $backgroundImageAsset) { $null } else { $backgroundImageAsset.FullName }
    HeaderLogoPath = if ($null -eq $headerLogoAsset) { $null } else { $headerLogoAsset.FullName }
    FaviconPath = if ($null -eq $faviconAsset) { $null } else { $faviconAsset.FullName }
}
