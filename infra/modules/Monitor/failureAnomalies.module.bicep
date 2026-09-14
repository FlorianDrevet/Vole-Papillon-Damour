// =======================================================================
// Application Insights Failure Anomalies smart detector module
// -----------------------------------------------------------------------
// Azure creates this rule on its own and routes it to the built-in
// "Application Insights Smart Detection" action group. Declaring it with the
// same name takes ownership of the rule, so its notifications follow the
// project action group instead of an implicit recipient list.
// =======================================================================

@description('Name of the Application Insights component')
param applicationInsightsName string

@description('Resource ID of the Application Insights component')
param applicationInsightsId string

@description('Resource ID of the action group')
param actionGroupId string

@description('Resource tags')
param tags object = {}

resource failureAnomalies 'Microsoft.AlertsManagement/smartDetectorAlertRules@2021-04-01' = {
  name: 'Failure Anomalies - ${applicationInsightsName}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Detects an abnormal rise in failed requests or dependency calls compared with the learned baseline.'
    state: 'Enabled'
    severity: 'Sev2'
    frequency: 'PT1M'
    detector: {
      id: 'FailureAnomaliesDetector'
    }
    scope: [
      applicationInsightsId
    ]
    actionGroups: {
      groupIds: [
        actionGroupId
      ]
    }
  }
}
