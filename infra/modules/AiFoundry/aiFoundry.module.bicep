// =======================================================================
// Azure OpenAI model module for the social actuality title generator
// =======================================================================

@description('Azure region hosting the Azure OpenAI account')
param location string

@description('Name of the Azure OpenAI account')
param name string

@description('Name of the model deployment consumed by the Worker')
param deploymentName string

@description('Model family to deploy')
param modelName string = 'gpt-4.1-nano'

@description('Model version to deploy')
param modelVersion string = '2025-04-14'

@description('Initial provisioned throughput capacity')
param capacity int = 1

@description('Principal ID of the Worker user-assigned identity')
param workerPrincipalId string

@description('Resource tags')
param tags object = {}

var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: name
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
  properties: {
    customSubDomainName: name
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: account
  name: deploymentName
  sku: {
    name: 'Standard'
    capacity: capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
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
