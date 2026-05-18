targetScope = 'resourceGroup'

@allowed([
  'dev'
  'prod'
])
param environmentName string
param location string
param tags object
param containerAppEnvironmentName string
param apiContainerAppName string
param backOfficeContainerAppName string
param websiteContainerAppName string
param apiImage string
param backOfficeImage string
param websiteImage string
param containerRegistryLoginServer string
param logAnalyticsWorkspaceResourceGroupName string
param logAnalyticsWorkspaceName string
param apiTargetPort int
param backOfficeTargetPort int
param websiteTargetPort int
param apiMinReplicas int
param apiMaxReplicas int
param backOfficeMinReplicas int
param backOfficeMaxReplicas int
param websiteMinReplicas int
param websiteMaxReplicas int
param apiCpu string
param apiMemory string
param backOfficeCpu string
param backOfficeMemory string
param websiteCpu string
param websiteMemory string

var aspNetCoreEnvironment = environmentName == 'prod' ? 'Production' : 'Development'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  scope: resourceGroup(subscription().subscriptionId, logAnalyticsWorkspaceResourceGroupName)
  name: logAnalyticsWorkspaceName
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: listKeys('${logAnalyticsWorkspace.id}/sharedKeys', '2020-08-01').primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

resource apiContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiContainerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: apiTargetPort
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://0.0.0.0:8080'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: aspNetCoreEnvironment
            }
          ]
          resources: {
            cpu: json(apiCpu)
            memory: apiMemory
          }
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: apiMaxReplicas
      }
    }
  }
}

resource backOfficeContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: backOfficeContainerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: backOfficeTargetPort
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'backoffice'
          image: backOfficeImage
          resources: {
            cpu: json(backOfficeCpu)
            memory: backOfficeMemory
          }
        }
      ]
      scale: {
        minReplicas: backOfficeMinReplicas
        maxReplicas: backOfficeMaxReplicas
      }
    }
  }
}

resource websiteContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: websiteContainerAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: websiteTargetPort
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'website'
          image: websiteImage
          env: [
            {
              name: 'NODE_ENV'
              value: 'production'
            }
            {
              name: 'PORT'
              value: '8080'
            }
          ]
          resources: {
            cpu: json(websiteCpu)
            memory: websiteMemory
          }
        }
      ]
      scale: {
        minReplicas: websiteMinReplicas
        maxReplicas: websiteMaxReplicas
      }
    }
  }
}

output apiPrincipalId string = apiContainerApp.identity.principalId
output backOfficePrincipalId string = backOfficeContainerApp.identity.principalId
output websitePrincipalId string = websiteContainerApp.identity.principalId
output apiContainerAppUrl string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'
output backOfficeContainerAppUrl string = 'https://${backOfficeContainerApp.properties.configuration.ingress.fqdn}'
output websiteContainerAppUrl string = 'https://${websiteContainerApp.properties.configuration.ingress.fqdn}'
