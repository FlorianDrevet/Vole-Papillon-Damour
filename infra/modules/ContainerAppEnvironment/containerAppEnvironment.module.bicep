// =======================================================================
// Container App Environment Module
// -----------------------------------------------------------------------
// Module: containerAppEnvironment.module.bicep
// Description: Deploys an Azure Container App Environment resource
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.app/managedenvironments
// =======================================================================

import { WorkloadProfileType } from './types.bicep'

@description('Azure region for the Container App Environment')
param location string

@description('Name of the Container App Environment')
param name string

@description('Workload profile type')
param workloadProfileType WorkloadProfileType = 'Consumption'

@description('Whether the internal load balancer is enabled')
param internalLoadBalancerEnabled bool = false

@description('Whether zone redundancy is enabled')
param zoneRedundancyEnabled bool = false

@description('Resource ID of the Log Analytics workspace. When provided, logs are routed to this workspace via Azure Monitor - no shared key required.')
param logAnalyticsWorkspaceId string = ''

@description('Resource tags')
param tags object = {}

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    zoneRedundant: zoneRedundancyEnabled
    vnetConfiguration: {
      internal: internalLoadBalancerEnabled
    }
    appLogsConfiguration: logAnalyticsWorkspaceId != '' ? { destination: 'azure-monitor' } : null
    workloadProfiles: [
      {
        name: workloadProfileType
        workloadProfileType: workloadProfileType
      }
    ]
  }
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  scope: containerAppEnv
  name: 'containerAppEnvLogs'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
  }
}

@description('The resource ID of the Container App Environment')
output id string = containerAppEnv.id

@description('The default domain of the Container App Environment')
output defaultDomain string = containerAppEnv.properties.defaultDomain
