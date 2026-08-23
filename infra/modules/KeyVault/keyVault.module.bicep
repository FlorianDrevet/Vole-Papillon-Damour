// =======================================================================
// Key Vault Module
// -----------------------------------------------------------------------
// Module: keyVault.module.bicep
// Description: Deploys an Azure Key Vault resource
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.keyvault/vaults
// =======================================================================

import { SkuName } from './types.bicep'

@description('Azure region for the Key Vault')
param location string

@description('Name of the Key Vault')
param name string

@description('SKU of the Key Vault')
param sku SkuName = 'standard'

@description('Blocks permanent deletion during the soft-delete window. Keep disabled outside production: with it enabled, a deleted vault name cannot be reused for 90 days.')
param enablePurgeProtection bool = false

@description('Resource tags')
param tags object = {}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: sku
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enablePurgeProtection: enablePurgeProtection ? true : null
    enableSoftDelete: true
  }
}
