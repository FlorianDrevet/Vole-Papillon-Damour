// =======================================================================
// Application Insights Module
// -----------------------------------------------------------------------
// Module: applicationInsights.module.bicep
// Description: Deploys an Azure Application Insights resource
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.insights/components
// =======================================================================

import { IngestionMode } from './types.bicep'

@description('Azure region for the Application Insights resource')
param location string

@description('Name of the Application Insights resource')
param name string

@description('Resource ID of the Log Analytics workspace')
param logAnalyticsWorkspaceId string

@description('Sampling percentage (0-100)')
param samplingPercentage int = 100

@description('Number of days to retain data')
param retentionInDays int = 90

@description('Whether IP masking is disabled')
param disableIpMasking bool = false

@description('Whether local authentication is disabled')
param disableLocalAuth bool = false

@description('Ingestion mode for telemetry data')
param ingestionMode IngestionMode = 'LogAnalytics'

@description('Daily data volume cap in GB for this Application Insights component')
@minValue(1)
param dailyCapGb int = 1

@description('Resource tags')
param tags object = {}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    SamplingPercentage: samplingPercentage
    RetentionInDays: retentionInDays
    DisableIpMasking: disableIpMasking
    DisableLocalAuth: disableLocalAuth
    IngestionMode: ingestionMode
  }
}

// Workspace-based Application Insights does not expose the daily component cap
// on the parent resource. Keep it explicit as the pricingPlans child resource.
resource pricingPlan 'Microsoft.Insights/components/pricingPlans@2017-10-01' = {
  parent: applicationInsights
  name: 'current'
  properties: {
    cap: dailyCapGb
    planType: 'Basic'
    stopSendNotificationWhenHitCap: false
  }
}

output connectionString string = applicationInsights.properties.ConnectionString
