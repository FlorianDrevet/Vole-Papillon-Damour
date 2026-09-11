#Requires -Version 7.0
#Requires -Modules Pester

$scriptPath = Join-Path $PSScriptRoot 'Sync-EntraDisplayNames.ps1'

Describe 'Sync-EntraDisplayNames.ps1' {
    BeforeEach {
        Mock Connect-MgGraph { }
        Mock Get-MgUser {
            @(
                [pscustomobject]@{
                    Id          = 'member-1'
                    DisplayName = 'unknown'
                    GivenName   = 'Camille'
                    Surname     = 'Dupont'
                }
                [pscustomobject]@{
                    Id          = 'member-2'
                    DisplayName = 'unknown'
                    GivenName   = $null
                    Surname     = $null
                }
                [pscustomobject]@{
                    Id          = 'member-3'
                    DisplayName = 'Déjà renseigné'
                    GivenName   = 'Ada'
                    Surname     = 'Lovelace'
                }
            )
        }
        Mock Update-MgUser { }
    }

    It 'updates placeholder display names from given name and surname' {
        $result = & $scriptPath -TenantId 'tenant-id'

        Assert-MockCalled Connect-MgGraph -Times 1 -Exactly -Scope It
        Assert-MockCalled Update-MgUser -Times 1 -Exactly -Scope It -ParameterFilter {
            $UserId -eq 'member-1' -and $DisplayName -eq 'Camille Dupont'
        }
        @($result | Where-Object Action -eq 'updated').Count | Should Be 1
    }

    It 'skips users without a usable name and preserves non-placeholder names' {
        $result = & $scriptPath -TenantId 'tenant-id'

        Assert-MockCalled Update-MgUser -Times 1 -Exactly -Scope It
        ($result | Where-Object UserId -eq 'member-2').Action | Should Be 'skipped'
        ($result | Where-Object UserId -eq 'member-3').Action | Should Be 'skipped'
    }

    It 'supports a dry run without changing users' {
        $result = & $scriptPath -TenantId 'tenant-id' -WhatIf 4>&1

        Assert-MockCalled Update-MgUser -Times 0 -Exactly -Scope It
        @($result | Where-Object Action -eq 'would-update').Count | Should Be 1
    }

    It 'combines a single available name without adding a blank separator' {
        . $scriptPath -TenantId 'tenant-id'

        Get-EntraDisplayName -GivenName 'Camille' -Surname $null | Should Be 'Camille'
        Get-EntraDisplayName -GivenName $null -Surname 'Dupont' | Should Be 'Dupont'
    }
}
