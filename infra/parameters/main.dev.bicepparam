using '../main.bicep'

param environmentName = 'development'

// -----------------------------------------------------------------------
// Container Apps
// -----------------------------------------------------------------------
// Every image listens on 8080: the API through ASPNETCORE_URLS, the Website
// through the SSR server's PORT, the BackOffice through nginx.conf.

param containerAppApiContainerRuntime = {
  cpuCores: '0.5'
  memoryGi: '1.0Gi'
}
param containerAppApiScaling = {
  minReplicas: 1
  maxReplicas: 2
}
param containerAppApiIngress = {
  enabled: true
  targetPort: 8080
  external: true
  transportMethod: 'auto'
}
param containerAppApiHealthProbes = {
  readiness: {
    path: '/health'
    port: 8080
  }
  liveness: {
    path: '/health'
    port: 8080
  }
  startup: {
    path: '/health'
    port: 8080
  }
}

param containerAppWebsiteContainerRuntime = {
  cpuCores: '0.5'
  memoryGi: '1.0Gi'
}
param containerAppWebsiteScaling = {
  minReplicas: 1
  maxReplicas: 2
}
param containerAppWebsiteIngress = {
  enabled: true
  targetPort: 8080
  external: true
  transportMethod: 'auto'
}
param containerAppWebsiteHealthProbes = {
  readiness: {
    path: ''
    port: 0
  }
  liveness: {
    path: ''
    port: 0
  }
  startup: {
    path: ''
    port: 0
  }
}

param containerAppBackOfficeContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
param containerAppBackOfficeScaling = {
  minReplicas: 1
  maxReplicas: 2
}
param containerAppBackOfficeIngress = {
  enabled: true
  targetPort: 8080
  external: true
  transportMethod: 'auto'
}
param containerAppBackOfficeHealthProbes = {
  readiness: {
    path: ''
    port: 0
  }
  liveness: {
    path: ''
    port: 0
  }
  startup: {
    path: ''
    port: 0
  }
}

// -----------------------------------------------------------------------
// Platform
// -----------------------------------------------------------------------

param keyVaultSku = 'standard'
param keyVaultEnablePurgeProtection = false

// S1: fixed Standard tier, 20 DTUs and no automatic pause, as decided in DT-11.
// The subscription is not allowed to provision Azure SQL in West Europe
// (ProvisioningDisabled), so the database sits in France Central.
param sqlLocation = 'francecentral'

param sqlDatabaseName = 'vole-papillon-damour-db'
param sqlDatabaseSku = {
  name: 'S1'
  tier: 'Standard'
  capacity: 20
  maxSizeBytes: 268435456000
  autoPauseDelayMinutes: 0
}

param storageAccountSku = 'Standard_LRS'

// ACS Email is a global ARM resource; France is the data-residency geography.
// The sending domain is customer-managed and is verified through OVH DNS after
// the infrastructure deployment exposes its generated records.
param communicationEmailServiceName = 'vpd-acs-email-dev'
param communicationEmailDataLocation = 'France'
param communicationEmailSendingDomain = 'mail.volepapillondamour.fr'

// Container names must match the BlobSettings section consumed by BlobService.
param blobContainerLotoImages = 'loto-images'
param blobContainerActualityImages = 'actuality-images'
param blobContainerEventImages = 'event-images'
param blobContainerProductImages = 'product-images'

param jwtIssuer = 'Vole_Papillon_Damour'
param jwtAudience = 'Vole_Papillon_Damour'
param jwtExpiryMinutes = 1000

// -----------------------------------------------------------------------
// Values injected by the pipeline
// -----------------------------------------------------------------------
// Secrets come from GitHub secrets and images from the current state of the
// Container Apps - never commit a value here.

param sqlAdministratorLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN', 'vpdadmin')
param sqlAdministratorLoginPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param jwtSecret = readEnvironmentVariable('JWT_SECRET', '')

param apiImage = readEnvironmentVariable('API_IMAGE', '')
param websiteImage = readEnvironmentVariable('WEBSITE_IMAGE', '')
param backOfficeImage = readEnvironmentVariable('BACKOFFICE_IMAGE', '')
