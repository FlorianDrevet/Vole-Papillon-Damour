// =======================================================================
// Azure Communication Service Module
// -----------------------------------------------------------------------
// References the shared ACS resource without updating it, then adds the new
// Worker role assignment and a parallel diagnostic setting for this region.
// =======================================================================

import { RbacRoles } from '../../constants.bicep'

@description('Name of the Azure Communication Service')
param name string

@description('Principal ID of the Worker managed identity')
param workerPrincipalId string

@description('Existing role-assignment resource name to adopt, or empty for a deterministic name')
param roleAssignmentName string = ''

@description('Resource ID of the Log Analytics workspace receiving ACS email operational logs')
param logAnalyticsWorkspaceId string = ''

@description('Name of the regional diagnostic setting')
param diagnosticSettingsName string

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: name
}

resource workerEmailRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: !empty(roleAssignmentName)
    ? roleAssignmentName
    : guid(communicationService.id, workerPrincipalId, RbacRoles.communication.CommunicationAndEmailServiceOwner.id)
  scope: communicationService
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      RbacRoles.communication.CommunicationAndEmailServiceOwner.id)
    principalId: workerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  scope: communicationService
  name: diagnosticSettingsName
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'EmailSendMailOperational'
        enabled: true
      }
      {
        category: 'EmailStatusUpdateOperational'
        enabled: true
      }
    ]
  }
}

output resourceId string = communicationService.id
output endpoint string = 'https://${name}.communication.azure.com'
