// ──────────────────────────────────────────────────────────────────────
// Key Vault Secrets Module — stores multiple secrets in a Key Vault
// ──────────────────────────────────────────────────────────────────────

@description('Name of the Key Vault')
param keyVaultName string

@description('List of secrets to store: { name: string, value: string }[]')
param secrets array

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource kvSecrets 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [for secret in secrets: {
  parent: keyVault
  name: secret.name
  properties: {
    value: secret.value
  }
}]

@description('Dictionary of secret URIs keyed by secret name')
output secretUris object = toObject(secrets, secret => secret.name, secret => '${keyVault.properties.vaultUri}secrets/${secret.name}')
