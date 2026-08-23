// =======================================================================
// Azure SQL Server Module
// -----------------------------------------------------------------------
// Module: sqlServer.module.bicep
// Description: Deploys a logical Azure SQL Server and a single database
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.sql/servers
// =======================================================================

import { DatabaseSkuConfig } from './types.bicep'

@description('Azure region for the SQL Server')
param location string

@description('Name of the logical SQL Server (globally unique)')
param name string

@description('Name of the database')
param databaseName string

@description('SQL administrator login')
@secure()
param administratorLogin string

@description('SQL administrator password')
@secure()
param administratorLoginPassword string

@description('Database SKU configuration')
param databaseSku DatabaseSkuConfig

@description('Allow other Azure services (Container Apps egress) to reach the server')
param allowAzureServices bool = true

@description('Resource tags')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: databaseSku.name
    tier: databaseSku.tier
    family: databaseSku.?family
    capacity: databaseSku.capacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: databaseSku.maxSizeBytes
    zoneRedundant: false
    // Only honoured by serverless tiers; 0 means "not applicable"
    autoPauseDelay: databaseSku.autoPauseDelayMinutes != 0 ? databaseSku.autoPauseDelayMinutes : null
  }
}

// Container Apps egress IPs are not static, so the database is reached through
// the "allow Azure services" rule (start/end 0.0.0.0) rather than an IP allowlist.
resource allowAzureServicesRule 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (allowAzureServices) {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('Fully qualified domain name of the logical SQL Server')
output fullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName

@description('Name of the logical SQL Server')
output name string = sqlServer.name

@description('Name of the database')
output databaseName string = sqlDatabase.name
