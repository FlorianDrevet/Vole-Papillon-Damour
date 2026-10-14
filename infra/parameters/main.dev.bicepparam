using '../main.bicep'

param environmentName = 'development'

// Regional resources are provisioned in parallel in France Central while the
// existing West Europe resource group and SQL server/database remain in place.
param applicationResourceGroupName = 'rg-vpd-dev'
param targetLocation = 'francecentral'
param targetEnvironmentSuffix = '-dev-fc'
param targetEnvironmentShort = 'devfc'

// Shared registry lives outside the application resource group. The CI
// identity is parameterized per project and receives full access to this ACR.
param containerRegistryName = readEnvironmentVariable('ACR_NAME', 'vpdacrshared')
param containerRegistryResourceGroupName = readEnvironmentVariable('ACR_RESOURCE_GROUP', 'rg-core-florian')
param containerRegistrySubscriptionId = readEnvironmentVariable('ACR_SUBSCRIPTION_ID', 'fc4fa56f-e4e7-41e9-8a0d-559fcd27b827')
param containerRegistryCiPrincipalId = readEnvironmentVariable('ACR_CI_PRINCIPAL_ID', 'c50d25b0-17d7-41b8-87e7-4179266f224e')

// -----------------------------------------------------------------------
// Container Apps
// -----------------------------------------------------------------------
// Every image listens on 8080: the API through ASPNETCORE_URLS, the Website
// through the SSR server's PORT, the BackOffice through nginx.conf.
// All six apps use ACA's minimum Consumption allocation: 0.25 vCPU and 0.5 GiB.

param containerAppApiContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
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
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
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

// The root and WWW aliases point to the public Website. The BackOffice, catalog
// and Scan applications each keep their own hostname.
param websiteCustomDomain = 'volepapillondamour.fr'
param websiteWwwCustomDomain = 'www.volepapillondamour.fr'
param backOfficeCustomDomain = 'backoffice.volepapillondamour.fr'
// Leave the target environment unbound until its domain certificate is issued.
param websiteCustomDomainCertificateName = ''
param websiteWwwCustomDomainCertificateName = ''
param backOfficeCustomDomainCertificateName = ''
param catalogCustomDomain = 'livres.volepapillondamour.fr'
param catalogCustomDomainCertificateName = ''
param scanCustomDomain = 'scan.volepapillondamour.fr'
param scanCustomDomainCertificateName = ''

param containerAppBackOfficeContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
param containerAppBackOfficeScaling = {
  // HTTP ingress wakes the app from zero. Keep it warm for three hours after
  // the last request so an administration session does not repeatedly cold-start.
  minReplicas: 0
  maxReplicas: 2
  cooldownPeriodSeconds: 10800
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

param containerAppScanContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
param containerAppScanScaling = {
  // HTTP ingress wakes the app from zero. Keep it warm for three hours after
  // the last request so a scan session does not repeatedly cold-start.
  minReplicas: 0
  maxReplicas: 2
  cooldownPeriodSeconds: 10800
}
param containerAppScanIngress = {
  enabled: true
  targetPort: 8080
  external: true
  transportMethod: 'auto'
}
param containerAppScanHealthProbes = {
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

param containerAppCatalogContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
param containerAppCatalogScaling = {
  minReplicas: 1
  maxReplicas: 2
}
param containerAppCatalogIngress = {
  enabled: true
  targetPort: 8080
  external: true
  transportMethod: 'auto'
}
param containerAppCatalogHealthProbes = {
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

param containerAppWorkerContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
// P1-1 measures whether a timer-triggered Function wakes correctly with no
// warm host. Keep one maximum replica while observing the production timer.
param containerAppWorkerScaling = {
  minReplicas: 0
  maxReplicas: 1
}

// -----------------------------------------------------------------------
// Platform
// -----------------------------------------------------------------------

param keyVaultSku = 'standard'
param keyVaultEnablePurgeProtection = false

// Reuse the live database without redeploying its server, SKU, or firewall.
param sqlDatabaseName = 'vole-papillon-damour-db'

param storageAccountSku = 'Standard_LRS'

// ACS Email is a global ARM resource; France is the data-residency geography.
// The sending domain is customer-managed and is verified through OVH DNS after
// the infrastructure deployment exposes its generated records.
param communicationEmailServiceName = 'vpd-acs-email-dev'
param communicationEmailDataLocation = 'France'
param communicationEmailSendingDomain = 'mail.volepapillondamour.fr'
// The email service and customer-managed domain already exist and have verified
// DNS state. Keeping bootstrap disabled prevents a later infra deployment from
// issuing a domain PUT and resetting that state.
param communicationEmailCreateResources = false
param communicationServiceName = 'vpd-acs-comm-dev'
// Adopt the role assignment created by the previous ACS configuration workflow.
// Other environments can leave this empty and use Bicep's deterministic default.
param communicationServiceRoleAssignmentName = ''
param communicationEmailEventSubscriptionName = 'vpd-acs-email-delivery-reports-dev'
param communicationEmailWebhookHeaderName = 'X-Vpd-EventGrid-Secret'
param bookAlertsEmailAssociationName = 'Vole Papillon d\'Amour'
param bookAlertsEmailLogoUrl = 'https://volepapillondamour.fr/icons/vpd_icon.png'
param bookAlertsEmailUnsubscribeUrl = 'https://livres.volepapillondamour.fr/compte'
// Set in the GitHub development environment; never commit the value.
param acsEmailWebhookSecret = readEnvironmentVariable('ACS_EMAIL_WEBHOOK_SECRET', '')
param bookAlertsUnsubscribeSigningKey = readEnvironmentVariable('BOOK_ALERTS_UNSUBSCRIBE_SIGNING_KEY', '')
// Set the development secret in the GitHub environment; never commit its value.
param memberCardSigningKey = readEnvironmentVariable('MEMBER_CARD_SIGNING_KEY', '')
// Azure infrastructure alerts go to the maintainer only. The association mailbox
// (volepapillondamour@sfr.fr) is a public contact address and must not receive them.
param monitoringAlertEmail = 'afdrevet@outlook.com'
// DEV keeps availability resources deployed but inactive, so an incremental
// deployment also disables resources that were created by an earlier version.
param availabilityTestsEnabled = false
// UI/public telemetry is high-volume and not used for operational alerting.
// Keep API, worker and scan Application Insights at their module default (100%).
param applicationInsightsUiSamplingPercentage = 25
// Four evaluations per day for DEV scheduled-query alerts.
param monitoringEvaluationFrequency = 'PT6H'
param monitoringWindowSize = 'PT6H'
param monitoringQueryLookback = '6h'
param corsAllowedOrigins = [
  'https://volepapillondamour.fr'
  'https://www.volepapillondamour.fr'
  'https://backoffice.volepapillondamour.fr'
  'https://livres.volepapillondamour.fr'
  'https://scan.volepapillondamour.fr'
]

param entraAuthority = readEnvironmentVariable('ENTRA_AUTHORITY', 'https://volepapillondamour.ciamlogin.com/')
param entraTenantId = readEnvironmentVariable('ENTRA_TENANT_ID', 'b23c80b3-9776-4840-8255-fcbf3b3500fd')
param entraTenantDomain = readEnvironmentVariable('ENTRA_TENANT_DOMAIN', 'volepapillondamour.onmicrosoft.com')
// The API application ID is public configuration, recorded after L0-11 steps 1-2.
// Keep the environment-variable override for another deployment environment.
param entraApiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID', 'ebc68507-2c07-4bab-9448-2d6d489c6112')
param entraGraphClientId = readEnvironmentVariable('ENTRA_GRAPH_CLIENT_ID', '')
param entraGraphClientSecret = readEnvironmentVariable('ENTRA_GRAPH_CLIENT_SECRET', '')

// Container names must match the BlobSettings section consumed by BlobService.
param blobContainerLotoImages = 'loto-images'
param blobContainerActualityImages = 'actuality-images'
param blobContainerEventImages = 'event-images'
param blobContainerProductImages = 'product-images'
param blobContainerRareBookPhotos = 'livres-rares'

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
param googleBooksApiKey = readEnvironmentVariable('GOOGLE_BOOKS_API_KEY', '')
param instagramAccessToken = readEnvironmentVariable('INSTAGRAM_ACCESS_TOKEN', '')
param socialImportSchedule = '0 0 0,12 * * *'
param socialImportGraphApiVersion = 'v22.0'
param socialImportUserId = readEnvironmentVariable('INSTAGRAM_USER_ID', '')
param socialImportFloorDate = readEnvironmentVariable('INSTAGRAM_IMPORT_FLOOR_DATE', '')
param socialImportMaxPostsPerRun = 5
param socialImportAccessTokenIssuedAt = readEnvironmentVariable('INSTAGRAM_ACCESS_TOKEN_ISSUED_AT', '')
param titleGenerationEnabled = readEnvironmentVariable('TITLE_GENERATION_ENABLED', 'false') == 'true'
param titleGenerationAccountName = readEnvironmentVariable('TITLE_GENERATION_ACCOUNT_NAME', '')
param titleGenerationDeploymentName = readEnvironmentVariable('TITLE_GENERATION_DEPLOYMENT_NAME', 'actuality-title')
param recommendationsEnabled = readEnvironmentVariable('RECOMMENDATIONS_ENABLED', 'false') == 'true'

param apiImage = readEnvironmentVariable('API_IMAGE', '')
param websiteImage = readEnvironmentVariable('WEBSITE_IMAGE', '')
param backOfficeImage = readEnvironmentVariable('BACKOFFICE_IMAGE', '')
param scanImage = readEnvironmentVariable('SCAN_IMAGE', '')
param catalogImage = readEnvironmentVariable('CATALOG_IMAGE', '')
param workerImage = readEnvironmentVariable('WORKER_IMAGE', '')
