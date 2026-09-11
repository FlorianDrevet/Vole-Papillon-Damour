#Requires -Version 7.0
#Requires -Modules Pester

$scriptPath = Join-Path $PSScriptRoot 'Configure-EntraUserFlow.ps1'

Describe 'Configure-EntraUserFlow.ps1' {
    BeforeEach {
        Mock Connect-MgGraph { }
        Mock Get-MgApplication {
            @([pscustomobject]@{
                Id          = 'catalog-object-id'
                AppId       = 'catalog-client-id'
                DisplayName = 'vpd-catalog-dev'
            })
        }
        Mock Get-MgIdentityAuthenticationEventFlow { @() }
        Mock New-MgIdentityAuthenticationEventFlow {
            [pscustomobject]@{
                Id          = 'flow-id'
                DisplayName = 'vpd-catalog-signup-dev'
            }
        }
    }

    It 'creates an email/password signup flow attached only to the catalog app' {
        $result = & $scriptPath -TenantId 'tenant-id' -Environment 'dev'

        Assert-MockCalled Connect-MgGraph -Times 1 -Exactly -Scope It
        Assert-MockCalled New-MgIdentityAuthenticationEventFlow -Times 1 -Exactly -Scope It
        $result.FlowId | Should Be 'flow-id'
    }

    It 'builds the External ID flow body for the catalog application' {
        . $scriptPath -TenantId 'tenant-id' -Environment 'dev'
        $body = New-CatalogSignupFlowBody `
            -CatalogClientId 'catalog-client-id' `
            -DisplayName 'vpd-catalog-signup-dev' `
            -Description 'test flow'

        $body.'@odata.type' | Should Be '#microsoft.graph.externalUsersSelfServiceSignUpEventsFlow'
        $body.conditions.applications.includeApplications[0].appId | Should Be 'catalog-client-id'
        $body.onAuthenticationMethodLoadStart.identityProviders[0].id | Should Be 'EmailPassword-OAUTH'
        $body.onInteractiveAuthFlowStart.isSignUpAllowed | Should Be $true
        $attributes = @($body.onAttributeCollection.attributes)
        $attributes.Count | Should Be 3
        $attributes[0].id | Should Be 'email'
        $attributes[1].id | Should Be 'givenName'
        $attributes[2].id | Should Be 'surname'
        (@($attributes | Where-Object { $_.id -eq 'displayName' })).Count | Should Be 0
        $body.onAttributeCollection.attributeCollectionPage.views[0].title | Should Be 'Créer votre compte'
        $body.onAttributeCollection.attributeCollectionPage.views[0].description | Should Be 'Utilisez une adresse e-mail valide pour créer votre compte.'
    }

    It 'collects separate given name and surname fields in the hosted form' {
        . $scriptPath -TenantId 'tenant-id' -Environment 'dev'
        $body = New-CatalogSignupFlowBody `
            -CatalogClientId 'catalog-client-id' `
            -DisplayName 'vpd-catalog-signup-dev' `
            -Description 'test flow'

        $inputs = @($body.onAttributeCollection.attributeCollectionPage.views[0].inputs)
        $givenNameInput = $inputs | Where-Object { $_.attribute -eq 'givenName' } | Select-Object -First 1
        $surnameInput = $inputs | Where-Object { $_.attribute -eq 'surname' } | Select-Object -First 1

        $givenNameInput | Should Not BeNullOrEmpty
        $givenNameInput.label | Should Be 'Prénom'
        $givenNameInput.inputType | Should Be 'text'
        $givenNameInput.required | Should Be $false
        $givenNameInput.validationRegEx | Should Be '^.{0,64}$'
        [regex]::IsMatch('Élodie', $givenNameInput.validationRegEx) | Should Be $true

        $surnameInput | Should Not BeNullOrEmpty
        $surnameInput.label | Should Be 'Nom'
        $surnameInput.inputType | Should Be 'text'
        $surnameInput.required | Should Be $false
        $surnameInput.validationRegEx | Should Be '^.{0,64}$'
        [regex]::IsMatch('Noël', $surnameInput.validationRegEx) | Should Be $true

        (@($inputs | Where-Object { $_.attribute -eq 'displayName' })).Count | Should Be 0
    }

    It 'updates the existing catalog flow instead of creating a duplicate' {
        Mock Get-MgIdentityAuthenticationEventFlow {
            @([pscustomobject]@{
                Id          = 'existing-flow'
                DisplayName = 'vpd-catalog-signup-dev'
                Conditions  = [pscustomobject]@{
                    Applications = [pscustomobject]@{
                        IncludeApplications = @(
                            [pscustomobject]@{ AppId = 'catalog-client-id' }
                        )
                    }
                }
            })
        }
        Mock Update-MgIdentityAuthenticationEventFlow {
            [pscustomobject]@{
                Id          = 'existing-flow'
                DisplayName = 'vpd-catalog-signup-dev'
            }
        }

        $result = & $scriptPath -TenantId 'tenant-id' -Environment 'dev'

        Assert-MockCalled New-MgIdentityAuthenticationEventFlow -Times 0 -Exactly -Scope It
        Assert-MockCalled Update-MgIdentityAuthenticationEventFlow -Times 1 -Exactly -Scope It
        $result.Action | Should Be 'updated'
    }

    It 'supports a dry run without creating or updating the flow' {
        & $scriptPath -TenantId 'tenant-id' -Environment 'dev' -WhatIf 4>&1 | Out-Null

        Assert-MockCalled New-MgIdentityAuthenticationEventFlow -Times 0 -Exactly -Scope It
    }
}
