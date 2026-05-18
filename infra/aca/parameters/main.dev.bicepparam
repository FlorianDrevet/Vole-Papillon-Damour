using '../main.bicep'

param environmentName = 'dev'
param location = 'FranceCentral'
param commonResourceGroupName = 'rg-vpd-common'
param applicationResourceGroupName = 'rg-vpd-dev'
param containerRegistryName = 'acrvpdcommon001'
param logAnalyticsWorkspaceName = 'law-vpd-common'
param containerAppEnvironmentName = 'cae-vpd-dev'
param apiContainerAppName = 'ca-vpd-api-dev'
param backOfficeContainerAppName = 'ca-vpd-backoffice-dev'
param websiteContainerAppName = 'ca-vpd-website-dev'
param apiImage = 'acrvpdcommon001.azurecr.io/vpd-api:dev'
param backOfficeImage = 'acrvpdcommon001.azurecr.io/vpd-backoffice:dev'
param websiteImage = 'acrvpdcommon001.azurecr.io/vpd-website:dev'
param apiMinReplicas = 1
param apiMaxReplicas = 2
param backOfficeMinReplicas = 1
param backOfficeMaxReplicas = 2
param websiteMinReplicas = 1
param websiteMaxReplicas = 2
param tags = {
  workload: 'aca'
  owner: 'vpd'
}
