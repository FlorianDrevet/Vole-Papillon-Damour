// =======================================================================
// Azure Monitor scheduled query rule module
// =======================================================================

@description('Name of the scheduled query rule')
param name string

@description('Display name shown in Azure Monitor')
param displayName string

@description('Description of the failure being detected')
param ruleDescription string

@description('Resource ID of the Log Analytics workspace queried by the rule')
param workspaceId string

@description('Kusto query returning rows when the rule should fire')
param query string

@description('Comparison operator applied to the row count')
param operator string

@description('Threshold applied to the row count')
param threshold int

@description('Resource ID of the action group')
param actionGroupId string

@description('Severity from 0 (critical) to 4 (verbose)')
param severity int = 2

@description('Resource tags')
param tags object = {}

resource scheduledQueryRule 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: name
  location: resourceGroup().location
  kind: 'LogAlert'
  tags: tags
  properties: {
    displayName: displayName
    description: ruleDescription
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    severity: severity
    scopes: [
      workspaceId
    ]
    criteria: {
      allOf: [
        {
          query: query
          operator: operator
          threshold: threshold
          timeAggregation: 'Count'
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroupId
      ]
    }
    autoMitigate: true
    checkWorkspaceAlertsStorageConfigured: false
    skipQueryValidation: true
  }
}
