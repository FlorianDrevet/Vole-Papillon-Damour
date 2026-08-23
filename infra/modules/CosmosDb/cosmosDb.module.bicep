// =======================================================================
// Cosmos DB Module
// -----------------------------------------------------------------------
// Module: cosmosDb.module.bicep
// Description: Deploys an Azure Cosmos DB account (NoSQL API), a shared
//              SQL database, and its containers.
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.documentdb/databaseaccounts
// =======================================================================

import { ConsistencyLevel, ContainerConfig } from './types.bicep'

@description('Azure region for the Cosmos DB account')
param location string

@description('Name of the Cosmos DB account')
param name string

@description('Name of the shared SQL database')
param databaseName string

@description('Containers to provision in the shared database, with their partition key paths')
param containers ContainerConfig[]

@description('Default consistency level')
param consistencyLevel ConsistencyLevel = 'Session'

@description('Maximum staleness prefix for BoundedStaleness consistency')
param maxStalenessPrefix int = 100

@description('Maximum interval in seconds for BoundedStaleness consistency')
param maxIntervalInSeconds int = 5

@description('Whether automatic failover is enabled')
param enableAutomaticFailover bool = false

@description('Whether multiple write locations are enabled')
param enableMultipleWriteLocations bool = false

@description('Whether the free tier is enabled (1000 RU/s + 25GB free, one account per subscription)')
param enableFreeTier bool = true

@description('Shared database-level throughput (RU/s) — kept at the Free Tier maximum coverage')
param sharedThroughput int = 1000

@description('Resource tags')
param tags object = {}

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: name
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: consistencyLevel
      maxStalenessPrefix: maxStalenessPrefix
      maxIntervalInSeconds: maxIntervalInSeconds
    }
    enableAutomaticFailover: enableAutomaticFailover
    enableMultipleWriteLocations: enableMultipleWriteLocations
    enableFreeTier: enableFreeTier
    disableLocalAuth: true
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

resource sqlDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosDbAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource sqlContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = [for container in containers: {
  parent: sqlDatabase
  name: container.name
  properties: {
    resource: {
      id: container.name
      partitionKey: {
        paths: [
          container.partitionKeyPath
        ]
        kind: 'Hash'
      }
    }
  }
}]

@description('Cosmos DB account document endpoint (no key — used with managed identity auth)')
output documentEndpoint string = cosmosDbAccount.properties.documentEndpoint
