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
        Mock Set-MgOrganizationBrandingLocalizationBackgroundImage { }
        Mock Set-MgOrganizationBrandingHeaderLogo { }
        Mock Set-MgOrganizationBrandingLocalizationHeaderLogo { }
        Mock Set-MgOrganizationBrandingFavicon { }
    }

    It 'builds the French branding payload from the catalog design system' {
        . $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath
        $body = New-VpdBrandingUpdateBody

        $body.backgroundColor | Should Be '#eaf6fb'
        $body.headerBackgroundColor | Should Be '#041d30'
        $body.usernameHintText | Should Be 'Votre adresse e-mail'
        $body.customPrivacyAndCookiesText | Should Be 'Confidentialité et cookies'
        $body.customTermsOfUseText | Should Be "Conditions d’utilisation"
    }

    It 'creates the French localization and uploads CSS for both branding layers' {
        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath

        Assert-MockCalled Connect-MgGraph -Times 1 -Exactly -Scope It
        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 1 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBranding -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBrandingLocalization -Times 1 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingCustomCss -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 2 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 1 -Exactly -Scope It -ParameterFilter {
            $OrganizationalBrandingLocalizationId -eq '0'
        }
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 1 -Exactly -Scope It -ParameterFilter {
            $OrganizationalBrandingLocalizationId -eq 'fr-FR'
        }
        $result.Locale | Should Be 'fr-FR'
        $result.CssPath | Should Be $cssPath
    }

    It 'uploads the Catalog visual background to both branding layers when provided' {
        $backgroundPath = Join-Path $TestDrive 'papillon-background.png'
        Set-Content -LiteralPath $backgroundPath -Value 'test image'

        $result = & $scriptPath `
            -TenantId 'tenant-id' `
            -CustomCssPath $cssPath `
            -BackgroundImagePath $backgroundPath

        Assert-MockCalled Set-MgOrganizationBrandingLocalizationBackgroundImage `
            -Times 2 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationBackgroundImage `
            -Times 1 -Exactly -Scope It -ParameterFilter {
                $OrganizationalBrandingLocalizationId -eq '0'
            }
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationBackgroundImage `
            -Times 1 -Exactly -Scope It -ParameterFilter {
                $OrganizationalBrandingLocalizationId -eq 'fr-FR'
            }
        $result.BackgroundImagePath | Should Be (Get-Item -LiteralPath $backgroundPath).FullName
    }

    It 'creates the default localization before applying fresh-tenant branding' {
        $state = [pscustomobject]@{ Order = [System.Collections.Generic.List[string]]::new() }

        Mock Get-MgOrganizationBrandingLocalization {
            $state.Order.Add('read')
            throw 'Request_ResourceNotFound: branding has not been initialized'
        }
        Mock New-MgOrganizationBrandingLocalization {
            $state.Order.Add('create')
            [pscustomobject]@{ Id = 'fr-FR' }
        }
        Mock Update-MgOrganizationBrandingLocalization {
            $state.Order.Add("update:$OrganizationalBrandingLocalizationId")
        }

        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath

        $result.Applied | Should Be $true
        ($state.Order -join ',') | Should Be 'read,create,update:0'
        Assert-MockCalled Update-MgOrganizationBranding -Times 0 -Exactly -Scope It
        Assert-MockCalled Get-MgOrganizationBrandingLocalization -Times 1 -Exactly -Scope It
    }

    It 'keeps a dry run usable when the tenant has no branding resource yet' {
        Mock Get-MgOrganizationBrandingLocalization {
            throw 'Request_ResourceNotFound: branding has not been initialized'
        }

        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath -WhatIf

        $result.Applied | Should Be $false
        $result.LocalizationAction | Should Be 'localization-would-create'
    }

    It 'updates an existing French localization without creating a duplicate' {
        Mock Get-MgOrganizationBrandingLocalization {
            @([pscustomobject]@{ Id = 'fr-FR' })
        }

        $result = & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath

        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBrandingLocalization -Times 2 -Exactly -Scope It
        $result.LocalizationAction | Should Be 'localization-updated'
    }

    It 'supports a dry run without changing the tenant branding' {
        & $scriptPath -TenantId 'tenant-id' -CustomCssPath $cssPath -WhatIf | Out-Null

        Assert-MockCalled New-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBranding -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgOrganizationBrandingLocalization -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingCustomCss -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationCustomCss -Times 0 -Exactly -Scope It
        Assert-MockCalled Set-MgOrganizationBrandingLocalizationBackgroundImage -Times 0 -Exactly -Scope It
    }

    It 'keeps the hosted stylesheet aligned with the Catalog visual language' {
        $css = Get-Content -LiteralPath $cssPath -Raw

        $css | Should Match '#eaf6fb'
        $css | Should Match 'border-radius: 18px'
        $css | Should Match 'https://livres.volepapillondamour.fr/images/papillon_without_back.png'
        $css | Should Match 'z-index: 0'
        $css | Should Match 'background: transparent'
        $css | Should Match 'background-color: #eaf6fb !important'
        $css | Should Match 'align-items: center'
        $css | Should Match 'justify-content: center'
        $css | Should Match 'max-width: 520px'
        $css | Should Match 'max-width: 100vw'
        $css | Should Match 'box-sizing: border-box'
        $css | Should Match 'overflow-x: hidden'
        $css | Should Match 'padding: 12px 16px'
        $css | Should Match 'background-position: left center'
        $css | Should Match 'padding-left: 46px'
        $css | Should Match 'ext-title'
        $css | Should Match 'border-top: 4px solid #1497d6'
        $css | Should Match 'border-image: linear-gradient\(90deg'
        $css | Should Match 'prefers-reduced-motion'
    }

    It 'ships the neutral fallback background used by hosted External ID' {
        $backgroundPath = Join-Path $PSScriptRoot 'vpd-authentication-background.png'
        $background = Get-Item -LiteralPath $backgroundPath

        $background.Extension | Should Be '.png'
        $background.Length | Should BeGreaterThan 0
    }

}
