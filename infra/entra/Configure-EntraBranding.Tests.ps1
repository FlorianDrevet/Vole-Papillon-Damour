#Requires -Version 7.0
#Requires -Modules Pester

$scriptPath = Join-Path $PSScriptRoot 'Configure-EntraBranding.ps1'
$cssPath = Join-Path $PSScriptRoot 'vpd-catalog-authentication.css'

Import-Module Microsoft.Graph.Identity.DirectoryManagement -Force

Describe 'Configure-EntraBranding.ps1' {
    BeforeEach {
        Mock Connect-MgGraph { }
        Mock Get-MgOrganizationBrandingLocalization { @() }
        Mock New-MgOrganizationBrandingLocalization {
            [pscustomobject]@{ Id = 'fr-FR' }
        }
        Mock Update-MgOrganizationBranding { }
        Mock Update-MgOrganizationBrandingLocalization { }
        Mock Set-MgOrganizationBrandingCustomCss { }
        Mock Set-MgOrganizationBrandingLocalizationCustomCss { }
        Mock Set-MgOrganizationBrandingHeaderLogo { }
        Mock Set-MgOrganizationBrandingLocalizationHeaderLogo { }
        Mock Set-MgOrganizationBrandingFavicon { }
    }

    It 'builds the French branding payload from the catalog design system' {
        . $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath
        $body = New-VpdBrandingUpdateBody

        $body.backgroundColor | Should Be '#f7fbfe'
        $body.headerBackgroundColor | Should Be '#041d30'
        $body.usernameHintText | Should Be 'Votre adresse e-mail'
        $body.customPrivacyAndCookiesText | Should Be 'Confidentialité et cookies'
        $body.customTermsOfUseText | Should Be "Conditions d’utilisation"
    }

    It 'creates the French localization and uploads CSS for both branding layers' {
        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath

        Assert-MockCalled Connect-MgGraph -Times 1 -Exactly -Scope It
        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 1 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBranding -Times 1 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingCustomCss -Times 1 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 1 -Exactly -Scope It
        $result.Locale | Should Be 'fr-FR'
        $result.CssPath | Should Be $cssPath
    }

    It 'updates an existing French localization without creating a duplicate' {
        Mock Get-MgOrganizationBrandingLocalization {
            @([pscustomobject]@{ Id = 'fr-FR' })
        }

        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath

        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBrandingLocalization -Times 1 -Exactly -Scope It
        $result.LocalizationAction | Should Be 'localization-updated'
    }

    It 'supports a dry run without changing the tenant branding' {
        & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath -WhatIf | Out-Null

        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBranding -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingCustomCss -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 0 -Exactly -Scope It
    }

}
