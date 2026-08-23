// =======================================================================
// Container App Module
// -----------------------------------------------------------------------
// Module: containerApp.module.bicep
// Description: Deploys an Azure Container App resource
// See: https://learn.microsoft.com/en-us/azure/templates/microsoft.app/containerapps
// =======================================================================

import { ContainerRuntimeConfig, ScalingConfig, IngressConfig, HealthProbeConfig, EnvVar, KeyVaultSecretRef } from './types.bicep'

@description('Azure region for the Container App')
param location string

@description('Name of the Container App')
param name string

@description('Resource ID of the Container App Environment')
param containerAppEnvironmentId string

@description('Container image (overridden by app pipeline after first deploy)')
param containerImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Container runtime configuration')
param containerRuntime ContainerRuntimeConfig

@description('Scaling configuration')
param scaling ScalingConfig

@description('Ingress configuration')
param ingress IngressConfig

@description('Health probe configuration')
param healthProbes HealthProbeConfig

@description('ACR login server (e.g. myregistry.azurecr.io)')
param acrLoginServer string

@description('Resource ID of the User Assigned Identity for ACR pull')
param userAssignedIdentityId string

@description('Environment variables for the container')
param envVars EnvVar[] = []

@description('Secrets backed by Key Vault, made available to envVars via secretRef. Resolved using the Container App\'s user-assigned identity.')
param keyVaultSecrets KeyVaultSecretRef[] = []

@description('Resource tags')
param tags object = {}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnvironmentId
    configuration: {
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: [for s in keyVaultSecrets: {
        name: s.name
        keyVaultUrl: s.keyVaultUrl
        identity: userAssignedIdentityId
      }]
      ingress: ingress.enabled ? { external: ingress.external, targetPort: ingress.targetPort, transport: ingress.transportMethod } : null
    }
    template: {
      containers: [
        {
          name: name
          image: containerImage
          resources: {
            cpu: json(containerRuntime.cpuCores)
            memory: containerRuntime.memoryGi
          }
          probes: union(
            !empty(healthProbes.readiness.path) && healthProbes.readiness.port > 0 ? [{
              type: 'Readiness'
              httpGet: {
                path: healthProbes.readiness.path
                port: healthProbes.readiness.port
              }
            }] : [],
            !empty(healthProbes.liveness.path) && healthProbes.liveness.port > 0 ? [{
              type: 'Liveness'
              httpGet: {
                path: healthProbes.liveness.path
                port: healthProbes.liveness.port
              }
            }] : [],
            !empty(healthProbes.startup.path) && healthProbes.startup.port > 0 ? [{
              type: 'Startup'
              httpGet: {
                path: healthProbes.startup.path
                port: healthProbes.startup.port
              }
            }] : []
          )
          env: envVars
        }
      ]
      scale: {
        minReplicas: scaling.minReplicas
        maxReplicas: scaling.maxReplicas
      }
    }
  }
}


// Output the fully qualified domain name (FQDN) of the Container App
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
