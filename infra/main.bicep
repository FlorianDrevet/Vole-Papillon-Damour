targetScope = 'subscription'

import { EnvironmentName, environments } from './types.bicep'
import { BuildContainerRegistryName, BuildResourceGroupName, BuildResourceName, BuildStorageAccountName } from './functions.bicep'
import { RbacRoles } from './constants.bicep'
import { ContainerRuntimeConfig, HealthProbeConfig, IngressConfig, ScalingConfig } from './modules/ContainerApp/types.bicep'
import { SkuName as KeyVaultSkuName } from './modules/KeyVault/types.bicep'
import { SkuName as StorageSkuName } from './modules/StorageAccount/types.bicep'
import { DatabaseSkuConfig } from './modules/SqlServer/types.bicep'

// -----------------------------------------------------------------------
// Environment
// -----------------------------------------------------------------------

@description('The target deployment environment')
param environmentName EnvironmentName

// -----------------------------------------------------------------------
// Container Apps - runtime configuration
// -----------------------------------------------------------------------

@description('Value for containerRuntime of ContainerApp resource api.')
param containerAppApiContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource api.')
param containerAppApiScaling ScalingConfig
@description('Value for ingress of ContainerApp resource api.')
param containerAppApiIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource api.')
param containerAppApiHealthProbes HealthProbeConfig

@description('Value for containerRuntime of ContainerApp resource website.')
param containerAppWebsiteContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource website.')
param containerAppWebsiteScaling ScalingConfig
@description('Value for ingress of ContainerApp resource website.')
param containerAppWebsiteIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource website.')
param containerAppWebsiteHealthProbes HealthProbeConfig

@description('Value for containerRuntime of ContainerApp resource backoffice.')
param containerAppBackOfficeContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource backoffice.')
param containerAppBackOfficeScaling ScalingConfig
@description('Value for ingress of ContainerApp resource backoffice.')
param containerAppBackOfficeIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource backoffice.')
param containerAppBackOfficeHealthProbes HealthProbeConfig

// -----------------------------------------------------------------------
// Container images
// -----------------------------------------------------------------------
// The application pipelines own the image tag. Leave these empty when creating
// a brand-new environment; on later runs the infra pipeline reads the image
// currently running on each Container App and passes it back here, so that
// re-deploying the infra never rolls an application back to the placeholder.

@description('Image for the API Container App. Empty deploys the placeholder image.')
param apiImage string = ''
@description('Image for the Website Container App. Empty deploys the placeholder image.')
param websiteImage string = ''
@description('Image for the BackOffice Container App. Empty deploys the placeholder image.')
param backOfficeImage string = ''

// -----------------------------------------------------------------------
// Key Vault
// -----------------------------------------------------------------------

@description('SKU of the Key Vault')
param keyVaultSku KeyVaultSkuName

@description('Enable purge protection on the Key Vault. Keep false outside production.')
param keyVaultEnablePurgeProtection bool = false

// -----------------------------------------------------------------------
// SQL
// -----------------------------------------------------------------------

@description('SQL administrator login')
@secure()
param sqlAdministratorLogin string

@description('SQL administrator password')
@secure()
param sqlAdministratorLoginPassword string

@description('Region hosting the SQL Server. Kept separate from the environment region: the subscription is not allowed to provision Azure SQL everywhere.')
param sqlLocation string

@description('Name of the application database')
param sqlDatabaseName string

@description('SKU of the application database')
param sqlDatabaseSku DatabaseSkuConfig

// -----------------------------------------------------------------------
// Storage
// -----------------------------------------------------------------------

@description('SKU of the Storage Account')
param storageAccountSku StorageSkuName

@description('Blob container holding the loto images (BlobSettings__ContainerName)')
param blobContainerLotoImages string
@description('Blob container holding the actuality images (BlobSettings__ContainerActualityImagesName)')
param blobContainerActualityImages string
@description('Blob container holding the event images (BlobSettings__BlobContainerEventImagesClient)')
param blobContainerEventImages string
@description('Blob container holding the product images (BlobSettings__BlobContainerProductsImagesClient)')
param blobContainerProductImages string

// -----------------------------------------------------------------------
// ACS Email
// -----------------------------------------------------------------------

@description('Name of the Azure Communication Services Email resource')
param communicationEmailServiceName string

@description('ACS data-residency geography, not an Azure region')
param communicationEmailDataLocation string

@description('Customer-managed domain used for sending email')
param communicationEmailSendingDomain string

// -----------------------------------------------------------------------
// API application settings
// -----------------------------------------------------------------------

@description('Authority of the Microsoft Entra External ID tenant')
param entraAuthority string

@description('Tenant ID of the Microsoft Entra External ID tenant')
param entraTenantId string

@description('Application (client) ID of the protected API registration')
param entraApiClientId string

@description('Signing key for the API JWT tokens')
@secure()
param jwtSecret string

@description('Issuer of the API JWT tokens')
param jwtIssuer string

@description('Audience of the API JWT tokens')
param jwtAudience string

@description('Lifetime of the API JWT tokens, in minutes')
param jwtExpiryMinutes int

// -----------------------------------------------------------------------
// Computed
// -----------------------------------------------------------------------

var env = environments[environmentName]
var tags = env.tags

// Deployed until an application pipeline pushes the first real image.
var placeholderImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

resource applicationResourceGroup 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: BuildResourceGroupName('vpd', 'rg', env)
  location: env.location
  tags: tags
}

// -----------------------------------------------------------------------
// Platform
// -----------------------------------------------------------------------

module containerRegistryModule './modules/ContainerRegistry/containerRegistry.module.bicep' = {
  name: 'containerRegistry'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildContainerRegistryName('vpd', 'acr', env)
    tags: tags
  }
}

module logAnalyticsWorkspaceModule './modules/LogAnalyticsWorkspace/logAnalyticsWorkspace.module.bicep' = {
  name: 'logAnalyticsWorkspace'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd', 'law', env)
    tags: tags
  }
}

module containerAppEnvironmentModule './modules/ContainerAppEnvironment/containerAppEnvironment.module.bicep' = {
  name: 'containerAppEnvironment'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd', 'cae', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

module keyVaultModule './modules/KeyVault/keyVault.module.bicep' = {
  name: 'keyVault'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd', 'kv', env)
    tags: tags
    sku: keyVaultSku
    enablePurgeProtection: keyVaultEnablePurgeProtection
  }
}

module communicationEmailModule './modules/CommunicationEmail/communicationEmail.module.bicep' = {
  name: 'communicationEmail'
  scope: applicationResourceGroup
  params: {
    name: communicationEmailServiceName
    dataLocation: communicationEmailDataLocation
    sendingDomain: communicationEmailSendingDomain
    tags: tags
  }
}

// -----------------------------------------------------------------------
// Observability - one Application Insights per application
// -----------------------------------------------------------------------

module applicationInsightsApiModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsApi'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-api', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

module applicationInsightsWebsiteModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsWebsite'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-web', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

module applicationInsightsBackOfficeModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsBackOffice'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-bo', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

// -----------------------------------------------------------------------
// Data
// -----------------------------------------------------------------------

module sqlServerModule './modules/SqlServer/sqlServer.module.bicep' = {
  name: 'sqlServer'
  scope: applicationResourceGroup
  params: {
    location: sqlLocation
    name: BuildResourceName('vpd', 'sql', env)
    tags: tags
    databaseName: sqlDatabaseName
    databaseSku: sqlDatabaseSku
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
  }
}

module storageAccountModule './modules/StorageAccount/storageAccount.module.bicep' = {
  name: 'storageAccount'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildStorageAccountName('vpd', 'st', env)
    tags: tags
    sku: storageAccountSku
    containers: [
      {
        name: blobContainerLotoImages
        publicAccess: 'Blob'
      }
      {
        name: blobContainerActualityImages
        publicAccess: 'Blob'
      }
      {
        name: blobContainerEventImages
        publicAccess: 'Blob'
      }
      {
        name: blobContainerProductImages
        publicAccess: 'Blob'
      }
    ]
  }
}

module appSecretsModule './modules/KeyVault/appSecrets.module.bicep' = {
  name: 'appSecrets'
  scope: applicationResourceGroup
  params: {
    keyVaultName: BuildResourceName('vpd', 'kv', env)
    storageAccountName: storageAccountModule.outputs.name
    sqlServerFqdn: sqlServerModule.outputs.fullyQualifiedDomainName
    sqlDatabaseName: sqlServerModule.outputs.databaseName
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorLoginPassword: sqlAdministratorLoginPassword
    jwtSecret: jwtSecret
  }
  dependsOn: [
    keyVaultModule
  ]
}

// -----------------------------------------------------------------------
// Identities - one per application, so each app only gets what it needs
// -----------------------------------------------------------------------

module userAssignedIdentityApiModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityApi'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-api', 'id', env)
    tags: tags
  }
}

module userAssignedIdentityWebsiteModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityWebsite'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-web', 'id', env)
    tags: tags
  }
}

module userAssignedIdentityBackOfficeModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityBackOffice'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-bo', 'id', env)
    tags: tags
  }
}

// -----------------------------------------------------------------------
// Role assignments
// -----------------------------------------------------------------------

module containerAppApiAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppApiAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityApiModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

module containerAppWebsiteAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppWebsiteAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityWebsiteModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

module containerAppBackOfficeAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppBackOfficeAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityBackOfficeModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

// Only the API reads secrets: the front-ends carry no secretRef.
module containerAppApiKeyVaultRoles './modules/KeyVault/keyvault.roleassignments.module.bicep' = {
  name: 'containerAppApiKeyVaultRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd', 'kv', env)
    principalId: userAssignedIdentityApiModule.outputs.principalId
    roles: [
      RbacRoles.keyvault['Key Vault Secrets User']
    ]
  }
  dependsOn: [
    keyVaultModule
  ]
}

module containerAppApiApplicationInsightsRoles './modules/ApplicationInsights/applicationinsights.roleassignments.module.bicep' = {
  name: 'containerAppApiApplicationInsightsRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-api', 'appi', env)
    principalId: userAssignedIdentityApiModule.outputs.principalId
    roles: [
      RbacRoles.monitor.MonitoringMetricsPublisher
    ]
  }
  dependsOn: [
    applicationInsightsApiModule
  ]
}

// -----------------------------------------------------------------------
// Applications
// -----------------------------------------------------------------------

module containerAppApiModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppApi'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-api', 'ca', env)
    tags: tags
    containerImage: empty(apiImage) ? placeholderImage : apiImage
    containerRuntime: containerAppApiContainerRuntime
    scaling: containerAppApiScaling
    ingress: containerAppApiIngress
    healthProbes: containerAppApiHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityApiModule.outputs.resourceId
    keyVaultSecrets: [
      {
        name: 'sql-connectionstring'
        keyVaultUrl: appSecretsModule.outputs.secretUris['sql-connectionstring']
      }
      {
        name: 'storage-connectionstring'
        keyVaultUrl: appSecretsModule.outputs.secretUris['storage-connectionstring']
      }
      {
        name: 'jwt-secret'
        keyVaultUrl: appSecretsModule.outputs.secretUris['jwt-secret']
      }
    ]
    envVars: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Production'
      }
      {
        name: 'ASPNETCORE_URLS'
        value: 'http://+:${containerAppApiIngress.targetPort}'
      }
      {
        name: 'ConnectionStrings__ProjectDatabase'
        secretRef: 'sql-connectionstring'
      }
      {
        name: 'ConnectionStrings__AzureBlobStorageConnectionString'
        secretRef: 'storage-connectionstring'
      }
      {
        name: 'JwtSettings__Secret'
        secretRef: 'jwt-secret'
      }
      {
        name: 'JwtSettings__Issuer'
        value: jwtIssuer
      }
      {
        name: 'JwtSettings__Audience'
        value: jwtAudience
      }
      {
        name: 'JwtSettings__ExpiryMinutes'
        value: string(jwtExpiryMinutes)
      }
      {
        name: 'AzureAd__Instance'
        value: entraAuthority
      }
      {
        name: 'AzureAd__TenantId'
        value: entraTenantId
      }
      {
        name: 'AzureAd__ClientId'
        value: entraApiClientId
      }
      {
        name: 'AzureAd__Audience'
        value: 'api://${entraApiClientId}'
      }
      {
        name: 'BlobSettings__ContainerName'
        value: blobContainerLotoImages
      }
      {
        name: 'BlobSettings__ContainerActualityImagesName'
        value: blobContainerActualityImages
      }
      {
        name: 'BlobSettings__BlobContainerEventImagesClient'
        value: blobContainerEventImages
      }
      {
        name: 'BlobSettings__BlobContainerProductsImagesClient'
        value: blobContainerProductImages
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsApiModule.outputs.connectionString
      }
      {
        name: 'AZURE_CLIENT_ID'
        value: userAssignedIdentityApiModule.outputs.clientId
      }
    ]
  }
  dependsOn: [
    containerAppApiAcrRoles
    containerAppApiKeyVaultRoles
  ]
}

module containerAppWebsiteModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppWebsite'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-web', 'ca', env)
    tags: tags
    containerImage: empty(websiteImage) ? placeholderImage : websiteImage
    containerRuntime: containerAppWebsiteContainerRuntime
    scaling: containerAppWebsiteScaling
    ingress: containerAppWebsiteIngress
    healthProbes: containerAppWebsiteHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityWebsiteModule.outputs.resourceId
    envVars: [
      {
        name: 'NODE_ENV'
        value: 'production'
      }
      {
        name: 'PORT'
        value: string(containerAppWebsiteIngress.targetPort)
      }
      // The Angular bundle is built with its api_url baked in by the application
      // pipeline; this one is read by the SSR server process.
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsWebsiteModule.outputs.connectionString
      }
    ]
  }
  dependsOn: [
    containerAppWebsiteAcrRoles
  ]
}

module containerAppBackOfficeModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppBackOffice'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-bo', 'ca', env)
    tags: tags
    containerImage: empty(backOfficeImage) ? placeholderImage : backOfficeImage
    containerRuntime: containerAppBackOfficeContainerRuntime
    scaling: containerAppBackOfficeScaling
    ingress: containerAppBackOfficeIngress
    healthProbes: containerAppBackOfficeHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityBackOfficeModule.outputs.resourceId
    envVars: [
      // The BackOffice image is static content served by nginx: this value is
      // exposed for tooling only, the browser SDK is wired in at build time.
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsBackOfficeModule.outputs.connectionString
      }
    ]
  }
  dependsOn: [
    containerAppBackOfficeAcrRoles
  ]
}

// -----------------------------------------------------------------------
// Outputs - consumed by the application pipelines
// -----------------------------------------------------------------------

output resourceGroupName string = applicationResourceGroup.name
output containerRegistryName string = BuildContainerRegistryName('vpd', 'acr', env)
output containerRegistryLoginServer string = containerRegistryModule.outputs.loginServer

output apiContainerAppName string = BuildResourceName('vpd-api', 'ca', env)
output websiteContainerAppName string = BuildResourceName('vpd-web', 'ca', env)
output backOfficeContainerAppName string = BuildResourceName('vpd-bo', 'ca', env)

output apiUrl string = 'https://${containerAppApiModule.outputs.containerAppFqdn}'
output websiteUrl string = 'https://${containerAppWebsiteModule.outputs.containerAppFqdn}'
output backOfficeUrl string = 'https://${containerAppBackOfficeModule.outputs.containerAppFqdn}'

output sqlServerName string = sqlServerModule.outputs.name
output sqlServerFqdn string = sqlServerModule.outputs.fullyQualifiedDomainName
output sqlDatabaseName string = sqlServerModule.outputs.databaseName
output storageAccountName string = storageAccountModule.outputs.name
output keyVaultName string = BuildResourceName('vpd', 'kv', env)
output communicationEmailServiceName string = communicationEmailModule.outputs.name
output communicationEmailSendingDomain string = communicationEmailModule.outputs.sendingDomain
