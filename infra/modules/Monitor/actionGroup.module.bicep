// =======================================================================
// Azure Monitor action group module
// =======================================================================

@description('Name of the action group')
param name string

@description('Email address receiving operational alerts')
param emailAddress string

@description('Resource tags')
param tags object = {}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    enabled: true
    groupShortName: 'VPDAlerts'
    emailReceivers: [
      {
        name: 'Project owner'
        emailAddress: emailAddress
        useCommonAlertSchema: true
      }
    ]
    armRoleReceivers: []
    automationRunbookReceivers: []
    azureAppPushReceivers: []
    azureFunctionReceivers: []
    eventHubReceivers: []
    itsmReceivers: []
    logicAppReceivers: []
    smsReceivers: []
    voiceReceivers: []
    webhookReceivers: []
  }
}

@description('Resource ID of the action group')
output resourceId string = actionGroup.id
