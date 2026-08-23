// =======================================================================
// ContainerRegistry Module
// -----------------------------------------------------------------------
// Module: containerRegistry.module.bicep
// Description: Deploys an Azure ContainerRegistry resource
// =======================================================================

import { SkuName, PublicNetworkAccess } from './types.bicep'

@description('Azure region for the Container Registry')
param location string

@description('Name of the Container Registry')
param name string

@description('SKU of the Container Registry')
param sku SkuName = 'Basic'

@description('Whether the admin user is enabled')
param adminUserEnabled bool = false

@description('Public network access setting')
param publicNetworkAccess PublicNetworkAccess = 'Enabled'

@description('Whether zone redundancy is enabled (Premium only)')
param zoneRedundancy bool = false

@description('Resource tags')
param tags object = {}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    adminUserEnabled: adminUserEnabled
    publicNetworkAccess: publicNetworkAccess
    zoneRedundancy: zoneRedundancy ? 'Enabled' : 'Disabled'
  }
}

@description('The login server of the Container Registry')
output loginServer string = containerRegistry.properties.loginServer
