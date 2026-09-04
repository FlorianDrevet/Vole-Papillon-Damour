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

@description('Value for containerRuntime of ContainerApp resource scan.')
param containerAppScanContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource scan.')
param containerAppScanScaling ScalingConfig
@description('Value for ingress of ContainerApp resource scan.')
param containerAppScanIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource scan.')
param containerAppScanHealthProbes HealthProbeConfig

@description('Value for containerRuntime of ContainerApp resource catalog.')
param containerAppCatalogContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of ContainerApp resource catalog.')
param containerAppCatalogScaling ScalingConfig
@description('Value for ingress of ContainerApp resource catalog.')
param containerAppCatalogIngress IngressConfig
@description('Value for healthProbes of ContainerApp resource catalog.')
param containerAppCatalogHealthProbes HealthProbeConfig

@description('Value for containerRuntime of the Functions worker Container App.')
param containerAppWorkerContainerRuntime ContainerRuntimeConfig
@description('Value for scaling of the Functions worker Container App.')
param containerAppWorkerScaling ScalingConfig

@description('Apex hostname bound to the Website Container App')
param websiteCustomDomain string
@description('WWW hostname bound to the Website Container App')
param websiteWwwCustomDomain string
@description('Hostname bound to the BackOffice Container App')
param backOfficeCustomDomain string
@description('Existing managed certificate resource name for the Website apex hostname')
param websiteCustomDomainCertificateName string
@description('Existing managed certificate resource name for the Website WWW hostname')
param websiteWwwCustomDomainCertificateName string
@description('Existing managed certificate resource name for the BackOffice hostname')
param backOfficeCustomDomainCertificateName string
@description('Hostname bound to the public catalog Container App; leave empty until DNS is ready')
param catalogCustomDomain string = ''
@description('Existing managed certificate resource name for the catalog hostname')
param catalogCustomDomainCertificateName string = ''
@description('Hostname bound to the Scan Container App; leave empty until DNS is ready')
param scanCustomDomain string = ''
@description('Existing managed certificate resource name for the Scan hostname')
param scanCustomDomainCertificateName string = ''

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
@description('Image for the Scan Container App. Empty deploys the placeholder image.')
param scanImage string = ''
@description('Image for the public catalog Container App. Empty deploys the placeholder image.')
param catalogImage string = ''
@description('Image for the account-deletion Functions Container App. Empty deploys the placeholder image.')
param workerImage string = ''

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
@description('Blob container holding book cover images (BlobSettings__BlobContainerBookCoversName)')
param blobContainerBookCovers string

// -----------------------------------------------------------------------
// ACS Email
// -----------------------------------------------------------------------

@description('Name of the Azure Communication Services Email resource')
param communicationEmailServiceName string

@description('ACS data-residency geography, not an Azure region')
param communicationEmailDataLocation string

@description('Customer-managed domain used for sending email')
param communicationEmailSendingDomain string

@description('Email address receiving operational Azure Monitor alerts')
param monitoringAlertEmail string

@description('Browser origins allowed to call the API')
param corsAllowedOrigins string[]

// -----------------------------------------------------------------------
// API application settings
// -----------------------------------------------------------------------

@description('Authority of the Microsoft Entra External ID tenant')
param entraAuthority string

@description('Tenant ID of the Microsoft Entra External ID tenant')
param entraTenantId string

@description('Application (client) ID of the protected API registration')
param entraApiClientId string

@description('Application (client) ID of the app-only Graph account-deletion registration')
param entraGraphClientId string

@description('Client secret of the app-only Graph account-deletion registration')
@secure()
param entraGraphClientSecret string

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
var corsEnvVars = [for (origin, index) in corsAllowedOrigins: {
  name: 'Cors__AllowedOrigins__${index}'
  value: origin
}]
var managedCertificateNames = concat(
  [
    websiteCustomDomainCertificateName
    websiteWwwCustomDomainCertificateName
    backOfficeCustomDomainCertificateName
  ],
  !empty(catalogCustomDomain) && !empty(catalogCustomDomainCertificateName) ? [catalogCustomDomainCertificateName] : [],
  !empty(scanCustomDomain) && !empty(scanCustomDomainCertificateName) ? [scanCustomDomainCertificateName] : []
)
var scanManagedCertificateIndex = !empty(catalogCustomDomain) && !empty(catalogCustomDomainCertificateName) ? 4 : 3

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
    managedCertificateNames: managedCertificateNames
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

module applicationInsightsScanModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsScan'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-scan', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

module applicationInsightsCatalogModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsCatalog'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-catalog', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

module applicationInsightsWorkerModule './modules/ApplicationInsights/applicationInsights.module.bicep' = {
  name: 'applicationInsightsWorker'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-worker', 'appi', env)
    tags: tags
    logAnalyticsWorkspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
  }
}

// -----------------------------------------------------------------------
// Observability rules
// -----------------------------------------------------------------------

module monitoringActionGroup './modules/Monitor/actionGroup.module.bicep' = {
  name: 'monitoringActionGroup'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd', 'alerts', env)
    emailAddress: monitoringAlertEmail
    tags: tags
  }
}

module workerHeartbeatAlert './modules/Monitor/scheduledQueryRule.module.bicep' = {
  name: 'workerHeartbeatAlert'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-worker-heartbeat', 'alert', env)
    displayName: 'VPD worker heartbeat missing'
    ruleDescription: 'The worker has not completed a sweep in the last 15 minutes.'
    workspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
    query: 'AppTraces | where Message startswith "Worker sweep completed"'
    operator: 'LessThan'
    threshold: 1
    actionGroupId: monitoringActionGroup.outputs.resourceId
    severity: 1
    tags: tags
  }
}

module workerEnrichmentHeartbeatAlert './modules/Monitor/scheduledQueryRule.module.bicep' = {
  name: 'workerEnrichmentHeartbeatAlert'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-worker-enrichment-heartbeat', 'alert', env)
    displayName: 'VPD worker enrichment heartbeat missing'
    ruleDescription: 'The worker has not completed an enrichment run in the last 65 minutes.'
    workspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
    query: 'AppTraces | where Message startswith "Worker enrichment completed"'
    operator: 'LessThan'
    threshold: 1
    actionGroupId: monitoringActionGroup.outputs.resourceId
    severity: 2
    tags: tags
  }
}

module lateAnnouncementAlert './modules/Monitor/scheduledQueryRule.module.bicep' = {
  name: 'lateAnnouncementAlert'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-book-announcements-late', 'alert', env)
    displayName: 'Book announcements are late'
    ruleDescription: 'Due book announcements remain unreleased after a worker sweep.'
    workspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
    query: 'AppTraces | where Message startswith "Book announcements are late"'
    operator: 'GreaterThan'
    threshold: 0
    actionGroupId: monitoringActionGroup.outputs.resourceId
    severity: 2
    tags: tags
  }
}

module lateAlertQueueAlert './modules/Monitor/scheduledQueryRule.module.bicep' = {
  name: 'lateAlertQueueAlert'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-book-alert-queue-late', 'alert', env)
    displayName: 'Book alert queue is late'
    ruleDescription: 'The oldest due book alert has been waiting for at least 30 minutes.'
    workspaceId: logAnalyticsWorkspaceModule.outputs.logAnalyticsWorkspaceId
    query: 'AppTraces | where Message startswith "Book alert queue is late"'
    operator: 'GreaterThan'
    threshold: 0
    actionGroupId: monitoringActionGroup.outputs.resourceId
    severity: 2
    tags: tags
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
      {
        name: blobContainerBookCovers
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
    entraGraphClientSecret: entraGraphClientSecret
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

module userAssignedIdentityScanModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityScan'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-scan', 'id', env)
    tags: tags
  }
}

module userAssignedIdentityCatalogModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityCatalog'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-catalog', 'id', env)
    tags: tags
  }
}

module userAssignedIdentityWorkerModule './modules/UserAssignedIdentity/userAssignedIdentity.module.bicep' = {
  name: 'userAssignedIdentityWorker'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-worker', 'id', env)
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

module containerAppScanAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppScanAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityScanModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

module containerAppCatalogAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppCatalogAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityCatalogModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

module containerAppWorkerAcrRoles './modules/ContainerRegistry/containerregistry.roleassignments.module.bicep' = {
  name: 'containerAppWorkerAcrRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildContainerRegistryName('vpd', 'acr', env)
    principalId: userAssignedIdentityWorkerModule.outputs.principalId
    roles: [
      RbacRoles.containerregistry.AcrPull
    ]
  }
  dependsOn: [
    containerRegistryModule
  ]
}

// The API and the worker read secrets: the front-ends carry no secretRef.
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

module containerAppWorkerKeyVaultRoles './modules/KeyVault/keyvault.roleassignments.module.bicep' = {
  name: 'containerAppWorkerKeyVaultRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd', 'kv', env)
    principalId: userAssignedIdentityWorkerModule.outputs.principalId
    roles: [
      RbacRoles.keyvault['Key Vault Secrets User']
    ]
  }
  dependsOn: [
    keyVaultModule
  ]
}

module containerAppWorkerApplicationInsightsRoles './modules/ApplicationInsights/applicationinsights.roleassignments.module.bicep' = {
  name: 'containerAppWorkerApplicationInsightsRoles'
  scope: applicationResourceGroup
  params: {
    name: BuildResourceName('vpd-worker', 'appi', env)
    principalId: userAssignedIdentityWorkerModule.outputs.principalId
    roles: [
      RbacRoles.monitor.MonitoringMetricsPublisher
    ]
  }
  dependsOn: [
    applicationInsightsWorkerModule
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
      {
        name: 'entra-graph-client-secret'
        keyVaultUrl: appSecretsModule.outputs.secretUris['entra-graph-client-secret']
      }
    ]
    envVars: concat([
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
        // Entra v2 access tokens carry the API application ID in `aud`.
        // The `api://<id>/access_as_user` form is the delegated scope, not
        // the audience used by the token issued for this API.
        name: 'AzureAd__Audience'
        value: entraApiClientId
      }
      {
        name: 'EntraGraph__TenantId'
        value: entraTenantId
      }
      {
        name: 'EntraGraph__ClientId'
        value: entraGraphClientId
      }
      {
        name: 'EntraGraph__ClientSecret'
        secretRef: 'entra-graph-client-secret'
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
        name: 'BlobSettings__BlobContainerBookCoversName'
        value: blobContainerBookCovers
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsApiModule.outputs.connectionString
      }
      {
        name: 'AZURE_CLIENT_ID'
        value: userAssignedIdentityApiModule.outputs.clientId
      }
    ], corsEnvVars)
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
    // The managed certificate resources were created by Azure with generated
    // names. Their names are kept in the environment parameter file so an ARM
    // PUT does not remove the existing SNI bindings.
    customDomains: [
      {
        name: websiteCustomDomain
        bindingType: 'SniEnabled'
        certificateId: containerAppEnvironmentModule.outputs.managedCertificateIds[0]
      }
      {
        name: websiteWwwCustomDomain
        bindingType: 'SniEnabled'
        certificateId: containerAppEnvironmentModule.outputs.managedCertificateIds[1]
      }
    ]
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
    customDomains: [
      {
        name: backOfficeCustomDomain
        bindingType: 'SniEnabled'
        certificateId: containerAppEnvironmentModule.outputs.managedCertificateIds[2]
      }
    ]
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

module containerAppScanModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppScan'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-scan', 'ca', env)
    tags: tags
    containerImage: empty(scanImage) ? placeholderImage : scanImage
    containerRuntime: containerAppScanContainerRuntime
    scaling: containerAppScanScaling
    ingress: containerAppScanIngress
    customDomains: !empty(scanCustomDomain) && !empty(scanCustomDomainCertificateName) ? [
      {
        name: scanCustomDomain
        bindingType: 'SniEnabled'
        certificateId: containerAppEnvironmentModule.outputs.managedCertificateIds[scanManagedCertificateIndex]
      }
    ] : []
    healthProbes: containerAppScanHealthProbes
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityScanModule.outputs.resourceId
    envVars: [
      // API_URL and the public host are compiled into the Angular bundle by
      // the Scan application pipeline. This value is still exposed for
      // runtime diagnostics and follows the same observability contract as
      // the other frontends.
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsScanModule.outputs.connectionString
      }
    ]
  }
  dependsOn: [
    containerAppScanAcrRoles
  ]
}

module containerAppCatalogModule './modules/ContainerApp/containerApp.module.bicep' = {
  name: 'containerAppCatalog'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-catalog', 'ca', env)
    tags: tags
    containerImage: empty(catalogImage) ? placeholderImage : catalogImage
    containerRuntime: containerAppCatalogContainerRuntime
    scaling: containerAppCatalogScaling
    ingress: containerAppCatalogIngress
    healthProbes: containerAppCatalogHealthProbes
    customDomains: !empty(catalogCustomDomain) && !empty(catalogCustomDomainCertificateName) ? [
      {
        name: catalogCustomDomain
        bindingType: 'SniEnabled'
        certificateId: containerAppEnvironmentModule.outputs.managedCertificateIds[3]
      }
    ] : []
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityCatalogModule.outputs.resourceId
    envVars: [
      // Catalog pages are public and carry no secrets. This connection string
      // only enables server-side runtime diagnostics in the same way as the
      // other frontend Container Apps.
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsCatalogModule.outputs.connectionString
      }
    ]
  }
  dependsOn: [
    containerAppCatalogAcrRoles
  ]
}

module containerAppWorkerModule './modules/ContainerApp/functionContainerApp.module.bicep' = {
  name: 'containerAppWorker'
  scope: applicationResourceGroup
  params: {
    location: env.location
    name: BuildResourceName('vpd-worker', 'ca', env)
    tags: tags
    containerImage: empty(workerImage) ? placeholderImage : workerImage
    containerRuntime: containerAppWorkerContainerRuntime
    scaling: containerAppWorkerScaling
    containerAppEnvironmentId: containerAppEnvironmentModule.outputs.id
    acrLoginServer: containerRegistryModule.outputs.loginServer
    userAssignedIdentityId: userAssignedIdentityWorkerModule.outputs.resourceId
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
        name: 'entra-graph-client-secret'
        keyVaultUrl: appSecretsModule.outputs.secretUris['entra-graph-client-secret']
      }
    ]
    envVars: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Production'
      }
      {
        name: 'FUNCTIONS_WORKER_RUNTIME'
        value: 'dotnet-isolated'
      }
      {
        name: 'FUNCTIONS_EXTENSION_VERSION'
        value: '~4'
      }
      {
        name: 'FUNCTIONS_WORKER_RUNTIME_VERSION'
        value: '10.0'
      }
      {
        name: 'AzureWebJobsScriptRoot'
        value: '/home/site/wwwroot'
      }
      {
        name: 'AzureFunctionsJobHost__Logging__Console__IsEnabled'
        value: 'true'
      }
      {
        name: 'AzureWebJobsStorage'
        secretRef: 'storage-connectionstring'
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
        name: 'BlobSettings__BlobContainerBookCoversName'
        value: blobContainerBookCovers
      }
      {
        name: 'EntraGraph__TenantId'
        value: entraTenantId
      }
      {
        name: 'EntraGraph__ClientId'
        value: entraGraphClientId
      }
      {
        name: 'EntraGraph__ClientSecret'
        secretRef: 'entra-graph-client-secret'
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: applicationInsightsWorkerModule.outputs.connectionString
      }
      {
        name: 'AZURE_CLIENT_ID'
        value: userAssignedIdentityWorkerModule.outputs.clientId
      }
    ]
  }
  dependsOn: [
    containerAppWorkerAcrRoles
    containerAppWorkerKeyVaultRoles
    containerAppWorkerApplicationInsightsRoles
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
output scanContainerAppName string = BuildResourceName('vpd-scan', 'ca', env)
output catalogContainerAppName string = BuildResourceName('vpd-catalog', 'ca', env)
output workerContainerAppName string = BuildResourceName('vpd-worker', 'ca', env)

output apiUrl string = 'https://${containerAppApiModule.outputs.containerAppFqdn}'
output websiteUrl string = 'https://${containerAppWebsiteModule.outputs.containerAppFqdn}'
output backOfficeUrl string = 'https://${containerAppBackOfficeModule.outputs.containerAppFqdn}'
output scanUrl string = 'https://${containerAppScanModule.outputs.containerAppFqdn}'
output catalogUrl string = 'https://${containerAppCatalogModule.outputs.containerAppFqdn}'

output sqlServerName string = sqlServerModule.outputs.name
output sqlServerFqdn string = sqlServerModule.outputs.fullyQualifiedDomainName
output sqlDatabaseName string = sqlServerModule.outputs.databaseName
output storageAccountName string = storageAccountModule.outputs.name
output keyVaultName string = BuildResourceName('vpd', 'kv', env)
output communicationEmailServiceName string = communicationEmailModule.outputs.name
output communicationEmailSendingDomain string = communicationEmailModule.outputs.sendingDomain
