// =======================================================================
// Cosmos DB Role Assignments Module
// -----------------------------------------------------------------------
// Module: cosmosDb.roleassignments.module.bicep
// Description: Assigns a Cosmos DB SQL (data-plane) role on an existing
//              Cosmos DB account. Cosmos DB data-plane RBAC uses its own
//              resource type (sqlRoleAssignments) and role definition IDs —
//              it is NOT standard Azure RBAC (Microsoft.Authorization/
//              roleAssignments), so it cannot use infra/constants.bicep.
// See: https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-setup-rbac
// =======================================================================

targetScope = 'resourceGroup'

@description('Name of the Cosmos DB account')
param name string

@description('Principal object ID to assign the role to')
param principalId string

@description('Built-in Cosmos DB SQL role definition GUID (default: Cosmos DB Built-in Data Contributor)')
param roleDefinitionGuid string = '00000000-0000-0000-0000-000000000002'

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: name
}

resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmosDbAccount
  name: guid(cosmosDbAccount.id, principalId, roleDefinitionGuid)
  properties: {
    roleDefinitionId: '${cosmosDbAccount.id}/sqlRoleDefinitions/${roleDefinitionGuid}'
    principalId: principalId
    scope: cosmosDbAccount.id
  }
}
