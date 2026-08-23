using '../main.bicep'

param environmentName = 'development'

param containerAppBdBackContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}
param containerAppBdBackScaling = {
  minReplicas: 0
  maxReplicas: 1
}
param containerAppBdBackIngress = {
  enabled: true
  targetPort: 80
  external: true
  transportMethod: 'auto'
}
param containerAppBdBackHealthProbes = {
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

param containerAppBdFrontContainerRuntime = {
  cpuCores: '0.25'
  memoryGi: '0.5Gi'
}

param containerAppBdFrontScaling = {
  minReplicas: 0
  maxReplicas: 1
}

param containerAppBdFrontIngress = {
  enabled: true
  targetPort: 80
  external: true
  transportMethod: 'auto'
}

param containerAppBdFrontHealthProbes = {
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

param keyVaultBdSku = 'standard'

// -----------------------------------------------------------------------
// Static values from bicepparam
// -----------------------------------------------------------------------

param bdBackCosmosdbDeploymentrequestscollectionname = 'DeploymentRequests'
param bdBackCosmosdbDatabasename = 'bootstrapper'
param bdBackCosmosdbUsersettingscollectionname = 'UserSettings'
param bdBackCosmosdbProjectscollectionname = 'RegisteredProjects'
param bdBackCosmosdbRuncollectionname = 'RunArchive'
param bdBackCosmosdbProjectdefinitionscollectionname = 'ProjectDefinitions'

param bdBackAzureadInstance = 'https://login.microsoftonline.com/'

// -----------------------------------------------------------------------
// Dynamic values from Azure DevOps Variable Group
// Keep empty here. Values must be injected by pipeline.
// -----------------------------------------------------------------------

param bdAzureAdTenantId = ''
param bdAzureAdClientSecret = ''
param bdAzureAdClientId = ''
param bdAzureAdAudience = ''
param bdAzureAdApiScope = ''

param bdSpaClientId = ''
param bdSpaAuthority = ''

param bdGroupIdConsultation = ''
param bdGroupIdAdministration = ''

param bdBackDevopsResourceId = ''
