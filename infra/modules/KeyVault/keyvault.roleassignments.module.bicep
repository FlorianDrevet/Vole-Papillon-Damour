// =======================================================================
// Key Vault Role Assignment Module
// -----------------------------------------------------------------------
// Module: keyvault.roleassignments.module.bicep
// Description: Creates role assignments for Key Vault resources
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.authorization/roleassignments
// =======================================================================

import { RbacRoleType } from '../../types.bicep'

@description('The name of the Key Vault instance')
param name string

@description('The principal ID to assign the role to')
param principalId string

@description('The roles to assign to the principal')
param roles RbacRoleType[]

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: name
}

resource roleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roles: {
  scope: keyVault
  name: guid(keyVault.id, principalId, role.id)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role.id)
    principalId: principalId
    principalType: 'ServicePrincipal'
    description: role.description
  }
}]