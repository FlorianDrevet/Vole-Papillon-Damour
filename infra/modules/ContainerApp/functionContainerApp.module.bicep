// =======================================================================
// Azure Functions on Container Apps module
// -----------------------------------------------------------------------
// Native Functions-on-ACA apps are regular Microsoft.App/containerApps
// resources with kind=functionapp. Keeping this separate from the HTTP app
// module prevents a future API/Web change from accidentally removing the
// Functions metadata or its single-revision behavior.
// =======================================================================

import { ContainerRuntimeConfig, ScalingConfig, EnvVar, KeyVaultSecretRef } from './types.bicep'

@description('Azure region for the Function Container App')
param location string

@description('Name of the Function Container App')
param name string

@description('Resource ID of the Container App Environment')
param containerAppEnvironmentId string

@description('Container image for the Function app')
param containerImage string

@description('Container runtime configuration')
param containerRuntime ContainerRuntimeConfig

@description('Scaling boundaries for the Function app')
param scaling ScalingConfig

@description('ACR login server')
param acrLoginServer string

@description('Resource ID of the User Assigned Identity used for ACR and Key Vault')
param userAssignedIdentityId string

@description('Environment variables for the Function container')
param envVars EnvVar[] = []

@description('Secrets backed by Key Vault, made available through secretRef')
param keyVaultSecrets KeyVaultSecretRef[] = []

@description('Resource tags')
param tags object = {}

resource functionContainerApp 'Microsoft.App/containerApps@2025-07-01' = {
  name: name
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: [for secret in keyVaultSecrets: {
        name: secret.name
        keyVaultUrl: secret.keyVaultUrl
        identity: userAssignedIdentityId
      }]
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
