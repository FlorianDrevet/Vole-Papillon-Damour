using '../main.bicep'

param environmentName = 'prod'
param location = 'FranceCentral'
param commonResourceGroupName = 'rg-vpd-common'
param applicationResourceGroupName = 'rg-vpd-prod'
param containerRegistryName = 'acrvpdcommon001'
param logAnalyticsWorkspaceName = 'law-vpd-common'
param containerAppEnvironmentName = 'cae-vpd-prod'
param apiContainerAppName = 'ca-vpd-api-prod'
param backOfficeContainerAppName = 'ca-vpd-backoffice-prod'
param websiteContainerAppName = 'ca-vpd-website-prod'
param apiImage = 'acrvpdcommon001.azurecr.io/vpd-api:prod'
param backOfficeImage = 'acrvpdcommon001.azurecr.io/vpd-backoffice:prod'
param websiteImage = 'acrvpdcommon001.azurecr.io/vpd-website:prod'
param apiMinReplicas = 2
param apiMaxReplicas = 4
param backOfficeMinReplicas = 2
param backOfficeMaxReplicas = 4
param websiteMinReplicas = 2
param websiteMaxReplicas = 4
param tags = {
  workload: 'aca'
  owner: 'vpd'
}
