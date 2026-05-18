param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string]$EnvironmentName,

    [Parameter(Mandatory = $true)]
    [string]$RegistryName,

    [Parameter(Mandatory = $true)]
    [string]$ApiUrl,

    [Parameter(Mandatory = $true)]
    [string]$WebsiteUrl
)

$rootPath = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$registryLoginServer = "$RegistryName.azurecr.io"

Push-Location $rootPath
try {
    az acr login --name $RegistryName

    docker build -f .\src\Backend\Vole_Papillon_Damour.Api\Dockerfile -t "$registryLoginServer/vpd-api:$EnvironmentName" .\src\Backend
    docker build -f .\src\BackOffice\Dockerfile --build-arg API_URL=$ApiUrl --build-arg WEBSITE_URL=$WebsiteUrl -t "$registryLoginServer/vpd-backoffice:$EnvironmentName" .\src
    docker build -f .\src\Website\Dockerfile --build-arg API_URL=$ApiUrl -t "$registryLoginServer/vpd-website:$EnvironmentName" .\src

    docker push "$registryLoginServer/vpd-api:$EnvironmentName"
    docker push "$registryLoginServer/vpd-backoffice:$EnvironmentName"
    docker push "$registryLoginServer/vpd-website:$EnvironmentName"
}
finally {
    Pop-Location
}
