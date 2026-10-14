// =======================================================================
// Reference the shared Azure OpenAI account without changing its configuration
// or model deployments, and grant access to the regional Worker identity.
// =======================================================================

@description('Name of the Azure OpenAI account')
param name string

@description('Principal ID of the Worker user-assigned identity')
param workerPrincipalId string

var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: name
}

resource workerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, workerPrincipalId, cognitiveServicesOpenAiUserRoleId)
  scope: account
  properties: {
    principalId: workerPrincipalId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      cognitiveServicesOpenAiUserRoleId)
    principalType: 'ServicePrincipal'
  }
}

@description('Endpoint used by the Worker title-generation client')
output endpoint string = account.properties.endpoint
