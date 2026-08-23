// =======================================================================
// Storage Account Module
// -----------------------------------------------------------------------
// Module: storageAccount.module.bicep
// Description: Deploys a Storage Account and its blob containers
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.storage/storageaccounts
// =======================================================================

import { SkuName, BlobContainerConfig } from './types.bicep'

@description('Azure region for the Storage Account')
param location string

@description('Name of the Storage Account (globally unique, 3-24 lowercase alphanumeric chars)')
param name string

@description('SKU of the Storage Account')
param sku SkuName = 'Standard_LRS'

@description('Blob containers to create')
param containers BlobContainerConfig[] = []

@description('Resource tags')
param tags object = {}

// BlobService returns the raw blob URI to clients, so containers holding images
// need anonymous blob-level read access.
var anyContainerIsPublic = length(filter(containers, container => container.publicAccess != 'None')) > 0

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: anyContainerIsPublic
    allowSharedKeyAccess: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource blobContainers 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = [for container in containers: {
  parent: blobService
  name: container.name
  properties: {
    publicAccess: container.publicAccess
  }
}]

@description('Name of the Storage Account')
output name string = storageAccount.name

@description('Primary blob endpoint of the Storage Account')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
