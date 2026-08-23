targetScope = 'subscription'

import { EnvironmentName, environments } from './types.bicep'
import { BuildContainerRegistryName, BuildResourceGroupName, BuildResourceName } from './functions.bicep'
import { RbacRoles } from './constants.bicep'
import { ContainerRuntimeConfig, HealthProbeConfig, IngressConfig, ScalingConfig } from './modules/ContainerApp/types.bicep'
import { SkuName } from './modules/KeyVault/types.bicep'

@description('The target deployment environment')
param environmentName EnvironmentName

@description('Value for containerRuntime of ContainerApp resource bd-back.')
param containerAppBdBackContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource bd-back.')
param containerAppBdBackScaling ScalingConfig
@description('Value for ingress of ContainerApp resource bd-back.')
param containerAppBdBackIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource bd-back.')
param containerAppBdBackHealthProbes HealthProbeConfig
@description('Value for containerRuntime of ContainerApp resource bd-front.')
param containerAppBdFrontContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource bd-front.')
param containerAppBdFrontScaling ScalingConfig
@description('Value for ingress of ContainerApp resource bd-front.')
param containerAppBdFrontIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource bd-front.')
param containerAppBdFrontHealthProbes HealthProbeConfig
@description('Value for sku of KeyVault resource bd.')
param keyVaultBdSku SkuName

// The following inputs are used as environment variables for application bd-back.
// Static value
param bdBackCosmosdbDeploymentrequestscollectionname string
param bdBackCosmosdbDatabasename string
param bdBackCosmosdbUsersettingscollectionname string
param bdBackCosmosdbProjectscollectionname string
param bdBackCosmosdbRuncollectionname string
param bdBackCosmosdbProjectdefinitionscollectionname string
param bdBackAzureadInstance string
// Dynamic values from Variable group
param bdAzureAdTenantId string

@secure()
param bdAzureAdClientSecret string

param bdAzureAdClientId string
param bdAzureAdAudience string
param bdAzureAdApiScope string

param bdSpaClientId string
param bdSpaAuthority string

param bdGroupIdConsultation string
param bdGroupIdAdministration string

// Static values from bicepparam
param bdBackDevopsResourceId string

var env = environments[environmentName]

var tags = env.tags

var bdFrontOrigin = 'https://${BuildResourceName('bd-front', 'ca', env)}.${containerAppEnvironmentBdModule.outputs.defaultDomain}'
var bdFrontUri = '${bdFrontOrigin}/'

resource bootstrapDevops 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: BuildResourceGroupName('bootstrap-devops', 'rg', env)
  location: env.location
  tags: tags
}

module containerRegistryBdModule './modules/ContainerRegistry/containerRegistry.module.bicep' = {
  name: 'containerRegistryBd'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildContainerRegistryName('bd', 'acr', env)
    tags: tags
  }
}

module applicationInsightsBdModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsBdBack'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-back', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceBdModule.outputs.logAnalyticsWorkspaceId
    disableLocalAuth: true
  }
}

module applicationInsightsBdFrontModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsBdFront'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-front', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceBdModule.outputs.logAnalyticsWorkspaceId
  }
}


module containerAppBdBackModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppBdBack'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-back', 'ca', env)
    tags: tags
    containerRuntime: containerAppBdBackContainerRuntime
    scaling: containerAppBdBackScaling
    ingress: containerAppBdBackIngress
    healthProbes: containerAppBdBackHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentBdModule.outputs.id
    acrLoginServer: containerRegistryBdModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityBdBackModule.outputs.resourceId
    keyVaultSecrets: [
      {
        name: 'azuread-clientsecret'
        keyVaultUrl: bdKvSecretsModule.outputs.secretUris['bd-azuread-clientsecret']
      }
    ]
    envVars: [
      {
        name: 'AzureAd__Instance'
        value: bdBackAzureadInstance
      }
      {
        name: 'AzureAd__TenantId'
        value: bdAzureAdTenantId
      }
      {
        name: 'AzureAd__ClientId'
        value: bdAzureAdClientId
      }
      {
        name: 'AzureAd__ClientSecret'
        secretRef: 'azuread-clientsecret'
      }
      {
        name: 'AzureAd__Audience'
        value: bdAzureAdAudience
      }

      {
        name: 'GroupId__Consultation'
        value: bdGroupIdConsultation
      }
      {
        name: 'GroupId__Administration'
        value: bdGroupIdAdministration
      }

      {
        name: 'DevOps__ResourceId'
        value: bdBackDevopsResourceId
      }

      {
        name: 'ConnectionStrings__cosmosdb'
        value: cosmosDbBdModule.outputs.documentEndpoint
      }
      {
        name: 'AZURE_CLIENT_ID'
        value: userAssignedIdentityBdBackModule.outputs.clientId
      }
      {
        name: 'CosmosDB__DatabaseName'
        value: bdBackCosmosdbDatabasename
      }
      {
        name: 'CosmosDB__ProjectsContainerName'
        value: bdBackCosmosdbProjectscollectionname
      }
      {
        name: 'CosmosDB__RunContainerName'
        value: bdBackCosmosdbRuncollectionname
      }
      {
        name: 'CosmosDB__ProjectDefinitionsContainerName'
        value: bdBackCosmosdbProjectdefinitionscollectionname
      }
      {
        name: 'CosmosDB__UserSettingsContainerName'
        value: bdBackCosmosdbUsersettingscollectionname
      }
      {
        name: 'CosmosDB__DeploymentRequestsContainerName'
        value: bdBackCosmosdbDeploymentrequestscollectionname
      }
      {
        name: 'SPA__ClientId'
        value: bdSpaClientId
      }
      {
        name: 'SPA__Authority'
        value: bdSpaAuthority
      }
      {
        name: 'SPA__ApiScope'
        value: bdAzureAdApiScope
      }
      {
        name: 'SPA__RedirectUri'
        value: bdFrontUri
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsBdModule.outputs.connectionString
      }
      {
        name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
        value: 'ClientId=${userAssignedIdentityBdBackModule.outputs.clientId};Authorization=AAD'
      }
      {
        name: 'Cors__AllowedOrigins__0'
        value: bdFrontOrigin
      }
    ]
  }
}

module containerAppBdFrontModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppBdFront'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-front', 'ca', env)
    tags: tags
    containerRuntime: containerAppBdFrontContainerRuntime
    scaling: containerAppBdFrontScaling
    ingress: containerAppBdFrontIngress
    healthProbes: containerAppBdFrontHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentBdModule.outputs.id
    acrLoginServer: containerRegistryBdModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityBdFrontModule.outputs.resourceId
    envVars: [
  {
    name: 'VITE_APPINSIGHTS_CONNECTION_STRING'
    value: applicationInsightsBdFrontModule.outputs.connectionString
  }
  {
    name: 'VITE_CLIENT_ID'
    value: bdSpaClientId
  }
  {
    name: 'VITE_TENANT_ID'
    value: bdAzureAdTenantId
  }
  {
    name: 'VITE_AUTHORITY'
    value: bdSpaAuthority
  }
  {
    name: 'VITE_API_SCOPE'
    value: bdAzureAdApiScope
  }
  {
    name: 'VITE_REDIRECT_URI'
    value: bdFrontUri
  }
  {
    name: 'VITE_API_BASE_URL'
    value: 'https://${containerAppBdBackModule.outputs.containerAppFqdn}/api'
  }
]
  }
}

module keyVaultBdModule './modules/KeyVault/keyVault.module.bicep' = {
  name: 'keyVaultBd'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd', 'kv', env)
    tags: tags
    sku: keyVaultBdSku
  }
}

module logAnalyticsWorkspaceBdModule './modules/LogAnalyticsWorkspace/logAnalyticsWorkspace.module.bicep' = {
  name: 'logAnalyticsWorkspaceBd'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd', 'law', env)
    tags: tags
  }
}

module containerAppEnvironmentBdModule './modules/ContainerAppEnvironment/containerAppEnvironment.module.bicep' = {
  name: 'containerAppEnvironmentBd'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd', 'cae', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceBdModule.outputs.logAnalyticsWorkspaceId
  }
}

module cosmosDbBdModule './modules/CosmosDb/cosmosDb.module.bicep' = {
  name: 'cosmosDbBd'
  scope: bootstrapDevops
  params: {
    location: 'francecentral'
    name: BuildResourceName('bd', 'cosmos', env)
    tags: tags
    databaseName: bdBackCosmosdbDatabasename
    containers: [
      {
        name: bdBackCosmosdbProjectscollectionname
        partitionKeyPath: '/id'
      }
      {
        name: bdBackCosmosdbProjectdefinitionscollectionname
        partitionKeyPath: '/ProjectId'
      }
      {
        name: bdBackCosmosdbRuncollectionname
        partitionKeyPath: '/ProjectId'
      }
      {
        name: bdBackCosmosdbUsersettingscollectionname
        partitionKeyPath: '/UserEmail'
      }
      {
        name: bdBackCosmosdbDeploymentrequestscollectionname
        partitionKeyPath: '/ProjectId'
      }
    ]
  }
}

module userAssignedIdentityBdFrontModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityBdFront'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-front', 'id', env)
    tags: tags
  }
}

module userAssignedIdentityBdBackModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityBdBack'
  scope: bootstrapDevops
  params: {
    location: env.location
    name: BuildResourceName('bd-back', 'id', env)
    tags: tags
  }
}

// -- Key Vault secrets (batch per Key Vault) ------------------------------

module bdKvSecretsModule './modules/KeyVault/kvSecrets.module.bicep' = {
  name: 'bd-kv-secrets'
  scope: bootstrapDevops
  params: {
    keyVaultName: BuildResourceName('bd', 'kv', env)
    secrets: [
      {
        name: 'bd-azuread-clientsecret'
        value: bdAzureAdClientSecret
      }
    ]
  }
  dependsOn: [
    keyVaultBdModule
  ]
}

module containerAppBdFrontcontainerRegistryBdRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppBdFrontcontainerRegistryBdRoles'
  scope: bootstrapDevops
  params: {
    name: BuildContainerRegistryName('bd', 'acr', env)
    principalId: userAssignedIdentityBdFrontModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
}

module containerAppBdBackcontainerRegistryBdRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppBdBackcontainerRegistryBdRoles'
  scope: bootstrapDevops
  params: {
    name: BuildContainerRegistryName('bd', 'acr', env)
    principalId: userAssignedIdentityBdBackModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
}

module containerAppBdBackkeyVaultBdRoles './modules/KeyVault/keyvault.roleassignments.module.bicep' = {
  name: 'containerAppBdBackkeyVaultBdRoles'
  scope: bootstrapDevops
  params: {
    name: BuildResourceName('bd', 'kv', env)
    principalId: userAssignedIdentityBdBackModule.outputs.principalId
    roles: [
      RbacRoles.keyvault['Key Vault Secrets User']
    ]
  }
}

module containerAppBdBackApplicationInsightsBdRoles './modules/ApplicationInsights/applicationinsights.roleassignments.module.bicep' = {
  name: 'containerAppBdBackApplicationInsightsBdRoles'
  scope: bootstrapDevops
  params: {
    name: BuildResourceName('bd-back', 'appi', env)
    principalId: userAssignedIdentityBdBackModule.outputs.principalId
    roles: [
      RbacRoles.monitor.MonitoringMetricsPublisher
    ]
  }
  dependsOn: [
    applicationInsightsBdModule
  ]
}

module containerAppBdBackCosmosDbRoles './modules/CosmosDb/cosmosDb.roleassignments.module.bicep' = {
  name: 'containerAppBdBackCosmosDbRoles'
  scope: bootstrapDevops
  params: {
    name: BuildResourceName('bd', 'cosmos', env)
    principalId: userAssignedIdentityBdBackModule.outputs.principalId
  }
  dependsOn: [
    cosmosDbBdModule
  ]
}
