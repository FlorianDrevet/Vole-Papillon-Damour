#Requires -Version 7.0
#Requires -Modules Pester

$configureScriptPath = Join-Path $PSScriptRoot 'Configure-EntraApps.ps1'
$setRoleScriptPath = Join-Path $PSScriptRoot 'Set-VpdUserRole.ps1'
$rareBooksRoleId = '84bf89c1-ecae-4951-90d2-03dc6012a1dd'

Describe 'LivresRares Entra role' {
    It 'declares one stable API app role with the expected identity' {
        $script = Get-Content -LiteralPath $configureScriptPath -Raw

        $script | Should Match "Id\s*=\s*'$rareBooksRoleId'\s*\r?\n\s*Value\s*=\s*'LivresRares'"
        $script | Should Match "DisplayName\s*=\s*'Benevole livres rares'"
        $script | Should Match "Description\s*=\s*'Cree et tient a jour les fiches des livres rares\s*:\s*prix, photos, etat\.'"
    }

    It 'keeps the assignment script aligned with the app role' {
        $script = Get-Content -LiteralPath $setRoleScriptPath -Raw

        $script | Should Match "ValidateSet\('Tri',\s*'Caisse',\s*'Administration',\s*'LivresRares'\)"
        $script | Should Match "'LivresRares'\s*=\s*'$rareBooksRoleId'"
    }
}
