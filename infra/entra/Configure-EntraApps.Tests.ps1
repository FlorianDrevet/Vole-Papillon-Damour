#Requires -Version 7.0
#Requires -Modules Pester

$scriptPath = Join-Path $PSScriptRoot 'Configure-EntraApps.ps1'

Describe 'Merge-OptionalNameClaims' {
    BeforeAll {
        $parseErrors = $null
        $scriptAst = [System.Management.Automation.Language.Parser]::ParseFile(
            $scriptPath,
            [ref]$null,
            [ref]$parseErrors)
        $mergeFunction = $scriptAst.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq 'Merge-OptionalNameClaims'
            }, $true)

        if ($null -eq $mergeFunction) {
            throw 'Merge-OptionalNameClaims was not found.'
        }

        Set-StrictMode -Version Latest
        . ([scriptblock]::Create($mergeFunction.Extent.Text))

        $redirectFunction = $scriptAst.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq 'Merge-RedirectUris'
            }, $true)

        if ($null -eq $redirectFunction) {
            throw 'Merge-RedirectUris was not found.'
        }

        . ([scriptblock]::Create($redirectFunction.Extent.Text))
    }

    It 'ignores empty token claim collections while adding the member name claims' {
        $application = [pscustomobject]@{
            OptionalClaims = [pscustomobject]@{
                IdToken     = $null
                AccessToken = $null
                Saml2Token  = $null
            }
        }

        $result = Merge-OptionalNameClaims -Application $application -TokenType 'AccessToken'

        @($result['AccessToken'] | Where-Object { $_.Name -eq 'given_name' }).Count | Should Be 1
        @($result['AccessToken'] | Where-Object { $_.Name -eq 'family_name' }).Count | Should Be 1
    }

    It 'keeps a single existing redirect URI separate from the new URI' {
        $application = [pscustomobject]@{
            Spa = [pscustomobject]@{
                RedirectUris = 'https://existing.example'
            }
        }

        $result = Merge-RedirectUris `
            -Application $application `
            -Kind 'Spa' `
            -Uri 'https://new.example'

        @($result).Count | Should Be 2
        @($result | Where-Object { $_ -eq 'https://existing.example' }).Count | Should Be 1
        @($result | Where-Object { $_ -eq 'https://new.example' }).Count | Should Be 1
    }
}
