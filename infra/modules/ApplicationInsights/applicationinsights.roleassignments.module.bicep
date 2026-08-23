// =======================================================================
// Application Insights Role Assignments Module
// -----------------------------------------------------------------------
// Module: applicationinsights.roleassignments.module.bicep
// Description: Assigns Azure RBAC roles on an existing Application Insights resource
// =======================================================================

targetScope = 'resourceGroup'

@description('Name of the Application Insights resource')
param name string

@description('Principal object ID to assign roles to')
param principalId string

@description('Roles to assign')
param roles array

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: name
}

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roles: {
  name: guid(applicationInsights.id, principalId, role.id)
  scope: applicationInsights
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.id)
    principalType: 'ServicePrincipal'
  }
}]
