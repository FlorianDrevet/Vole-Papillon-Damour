// ──────────────────────────────────────────────────────────────────────
// Application Secrets Module
// -----------------------------------------------------------------------
// Builds the runtime connection strings from the deployed data resources and
// stores them in Key Vault. The values are assembled here rather than in
// main.bicep so that no secret ever transits through a deployment output.
//
// The secrets are declared one by one instead of as a loop: their values
// depend on listKeys(), which a for-expression cannot resolve at the start of
// the deployment (BCP178).
// ──────────────────────────────────────────────────────────────────────

@description('Name of the Key Vault receiving the secrets')
param keyVaultName string

@description('Name of the Storage Account whose access key backs the connection string')
param storageAccountName string

@description('Fully qualified domain name of the logical SQL Server')
param sqlServerFqdn string

@description('Name of the SQL database')
param sqlDatabaseName string

@description('SQL administrator login')
@secure()
param sqlAdministratorLogin string

@description('SQL administrator password')
@secure()
param sqlAdministratorLoginPassword string

@description('Signing key for the API JWT tokens')
@secure()
param jwtSecret string

@description('Client secret for the app-only Microsoft Graph account-deletion application')
@secure()
param entraGraphClientSecret string

@description('Shared secret used to authenticate ACS Event Grid delivery reports')
@secure()
@minLength(1)
param acsEmailWebhookSecret string

@description('Signing key for the one-click unsubscribe tokens carried by book alert emails')
@secure()
@minLength(1)
param bookAlertsUnsubscribeSigningKey string

@secure()
@minLength(1)
param memberCardSigningKey string

@description('Optional API key for the Google Books volumes API')
@secure()
param googleBooksApiKey string

@description('Optional long-lived Instagram Graph API access token')
@secure()
param instagramAccessToken string = ''

var sqlSecretName = 'sql-connectionstring'
var storageSecretName = 'storage-connectionstring'
var jwtSecretName = 'jwt-secret'
var entraGraphClientSecretName = 'entra-graph-client-secret'
var acsEmailWebhookSecretName = 'email-bounce-webhook-secret'
var bookAlertsUnsubscribeSigningKeySecretName = 'book-alerts-unsubscribe-signing-key'
var memberCardSigningKeySecretName = 'member-card-signing-key'
var googleBooksApiKeySecretName = 'google-books-api-key'
var instagramAccessTokenSecretName = 'instagram-access-token'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: sqlSecretName
  properties: {
    value: 'Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdministratorLogin};Password=${sqlAdministratorLoginPassword};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
  }
}

resource storageConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: storageSecretName
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: jwtSecretName
  properties: {
    value: jwtSecret
  }
}

resource entraGraphClientSecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: entraGraphClientSecretName
  properties: {
    value: entraGraphClientSecret
  }
}

resource acsEmailWebhookSecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: acsEmailWebhookSecretName
  properties: {
    value: acsEmailWebhookSecret
  }
}

resource bookAlertsUnsubscribeSigningKeyResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: bookAlertsUnsubscribeSigningKeySecretName
  properties: {
    value: bookAlertsUnsubscribeSigningKey
  }
}

resource memberCardSigningKeySecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: memberCardSigningKeySecretName
  properties: {
    value: memberCardSigningKey
  }
}

resource googleBooksApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(googleBooksApiKey)) {
  parent: keyVault
  name: googleBooksApiKeySecretName
  properties: {
    value: googleBooksApiKey
  }
}

resource instagramAccessTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(instagramAccessToken)) {
  parent: keyVault
  name: instagramAccessTokenSecretName
  properties: {
    value: instagramAccessToken
  }
}

@description('Dictionary of secret URIs keyed by secret name')
output secretUris object = {
  '${sqlSecretName}': '${keyVault.properties.vaultUri}secrets/${sqlSecretName}'
  '${storageSecretName}': '${keyVault.properties.vaultUri}secrets/${storageSecretName}'
  '${jwtSecretName}': '${keyVault.properties.vaultUri}secrets/${jwtSecretName}'
  '${entraGraphClientSecretName}': '${keyVault.properties.vaultUri}secrets/${entraGraphClientSecretName}'
  '${acsEmailWebhookSecretName}': '${keyVault.properties.vaultUri}secrets/${acsEmailWebhookSecretName}'
  '${bookAlertsUnsubscribeSigningKeySecretName}': '${keyVault.properties.vaultUri}secrets/${bookAlertsUnsubscribeSigningKeySecretName}'
  '${memberCardSigningKeySecretName}': '${keyVault.properties.vaultUri}secrets/${memberCardSigningKeySecretName}'
  '${googleBooksApiKeySecretName}': '${keyVault.properties.vaultUri}secrets/${googleBooksApiKeySecretName}'
  '${instagramAccessTokenSecretName}': '${keyVault.properties.vaultUri}secrets/${instagramAccessTokenSecretName}'
}
