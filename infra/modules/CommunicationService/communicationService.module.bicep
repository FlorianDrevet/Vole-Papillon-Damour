// =======================================================================
// Azure Communication Service Module
// -----------------------------------------------------------------------
// Owns the ACS resource used by the book-alert worker and its least-privilege
// sending role assignment. The linked email domain is managed by the sibling
// CommunicationEmail module.
// =======================================================================

import { RbacRoles } from '../../constants.bicep'

@description('Name of the Azure Communication Service')
param name string

@description('ACS data-residency geography, for example France or Europe')
param dataLocation string

@description('Resource ID of the verified ACS Email customer-managed domain')
param linkedDomainId string

@description('Link the Email domain to the Communication Service')
param linkEmailDomain bool = true

@description('Principal ID of the Worker managed identity')
param workerPrincipalId string

@description('Existing role-assignment resource name to adopt, or empty for a deterministic name')
param roleAssignmentName string = ''

@description('Resource tags')
param tags object = {}

resource communicationService 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    dataLocation: dataLocation
    linkedDomains: linkEmailDomain ? [linkedDomainId] : []
  }
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

output resourceId string = communicationService.id
output endpoint string = 'https://${name}.communication.azure.com'
