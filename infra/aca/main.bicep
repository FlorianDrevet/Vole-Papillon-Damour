targetScope = 'subscription'

@description('Primary Azure region for all resources.')
param location string = 'FranceCentral'

@description('Deployment environment. Only dev and prod are supported by this template.')
@allowed([
  'dev'
  'prod'
])
param environmentName string

@description('Shared resource group used by ACR and Log Analytics.')
param commonResourceGroupName string = 'rg-vpd-common'

@description('Environment-specific resource group used by ACA resources.')
param applicationResourceGroupName string

@description('Optional extra tags merged into every resource.')
param tags object = {}

@description('Globally unique name for the shared Azure Container Registry.')
param containerRegistryName string

@description('Name of the shared Log Analytics workspace.')
param logAnalyticsWorkspaceName string

@description('Name of the Azure Container Apps managed environment.')
param containerAppEnvironmentName string

@description('Name of the API Container App.')
param apiContainerAppName string

@description('Name of the BackOffice Container App.')
param backOfficeContainerAppName string

@description('Name of the Website Container App.')
param websiteContainerAppName string

@description('Fully qualified API image, including registry, repository, and tag.')
param apiImage string

@description('Fully qualified BackOffice image, including registry, repository, and tag.')
param backOfficeImage string

@description('Fully qualified Website image, including registry, repository, and tag.')
param websiteImage string

@description('Target port exposed by the API container.')
param apiTargetPort int = 8080

@description('Target port exposed by the BackOffice container.')
param backOfficeTargetPort int = 8080

@description('Target port exposed by the Website container.')
param websiteTargetPort int = 8080

@description('Minimum replica count for the API Container App.')
param apiMinReplicas int = 1

@description('Maximum replica count for the API Container App.')
param apiMaxReplicas int = 2

@description('Minimum replica count for the BackOffice Container App.')
param backOfficeMinReplicas int = 1

@description('Maximum replica count for the BackOffice Container App.')
param backOfficeMaxReplicas int = 2

@description('Minimum replica count for the Website Container App.')
param websiteMinReplicas int = 1

@description('Maximum replica count for the Website Container App.')
param websiteMaxReplicas int = 2

@description('CPU requested by the API container.')
param apiCpu string = '1'

@description('Memory requested by the API container.')
param apiMemory string = '2Gi'

@description('CPU requested by the BackOffice container.')
param backOfficeCpu string = '0.5'

@description('Memory requested by the BackOffice container.')
param backOfficeMemory string = '1Gi'

@description('CPU requested by the Website container.')
param websiteCpu string = '0.5'

@description('Memory requested by the Website container.')
param websiteMemory string = '1Gi'

var commonTags = union(tags, {
  project: 'vpd'
  scope: 'shared'
})
var applicationTags = union(tags, {
  project: 'vpd'
  scope: 'application'
  environment: environmentName
})

resource commonResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: commonResourceGroupName
  location: location
  tags: commonTags
}

resource applicationResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: applicationResourceGroupName
  location: location
  tags: applicationTags
}

module commonResources './modules/common-resources.bicep' = {
  name: 'commonResources'
  scope: commonResourceGroup
  params: {
    location: location
    tags: commonTags
    containerRegistryName: containerRegistryName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
  }
}

module applicationResources './modules/application-resources.bicep' = {
  name: 'applicationResources'
  scope: applicationResourceGroup
  params: {
    location: location
    tags: applicationTags
    environmentName: environmentName
    containerAppEnvironmentName: containerAppEnvironmentName
    apiContainerAppName: apiContainerAppName
    backOfficeContainerAppName: backOfficeContainerAppName
    websiteContainerAppName: websiteContainerAppName
    apiImage: apiImage
    backOfficeImage: backOfficeImage
    websiteImage: websiteImage
    containerRegistryLoginServer: commonResources.outputs.containerRegistryLoginServer
    logAnalyticsWorkspaceResourceGroupName: commonResourceGroupName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    apiTargetPort: apiTargetPort
    backOfficeTargetPort: backOfficeTargetPort
    websiteTargetPort: websiteTargetPort
    apiMinReplicas: apiMinReplicas
    apiMaxReplicas: apiMaxReplicas
    backOfficeMinReplicas: backOfficeMinReplicas
    backOfficeMaxReplicas: backOfficeMaxReplicas
    websiteMinReplicas: websiteMinReplicas
    websiteMaxReplicas: websiteMaxReplicas
    apiCpu: apiCpu
    apiMemory: apiMemory
    backOfficeCpu: backOfficeCpu
    backOfficeMemory: backOfficeMemory
    websiteCpu: websiteCpu
    websiteMemory: websiteMemory
  }
}

module commonRoleAssignments './modules/common-role-assignments.bicep' = {
  name: 'commonRoleAssignments'
  scope: commonResourceGroup
  params: {
    containerRegistryName: containerRegistryName
    apiPrincipalId: applicationResources.outputs.apiPrincipalId
    backOfficePrincipalId: applicationResources.outputs.backOfficePrincipalId
    websitePrincipalId: applicationResources.outputs.websitePrincipalId
  }
}

output containerRegistryLoginServer string = commonResources.outputs.containerRegistryLoginServer
output sharedResourceGroup string = commonResourceGroup.name
output applicationResourceGroupOutput string = applicationResourceGroup.name
output apiContainerAppUrl string = applicationResources.outputs.apiContainerAppUrl
output backOfficeContainerAppUrl string = applicationResources.outputs.backOfficeContainerAppUrl
output websiteContainerAppUrl string = applicationResources.outputs.websiteContainerAppUrl
