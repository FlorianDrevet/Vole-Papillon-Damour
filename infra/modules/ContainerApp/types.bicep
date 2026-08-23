@export()
@description('Ingress transport method for the Container App')
type TransportMethod = 'auto' | 'http' | 'http2' | 'tcp'

@export()
@description('Container runtime configuration (CPU, memory)')
type ContainerRuntimeConfig = {
  @description('CPU cores allocated to the container')
  cpuCores: string
  @description('Memory allocated to the container (e.g. 0.5Gi)')
  memoryGi: string
}

@export()
@description('Scaling configuration for the Container App')
type ScalingConfig = {
  @description('Minimum number of replicas')
  minReplicas: int
  @description('Maximum number of replicas')
  maxReplicas: int
}

@export()
@description('Ingress configuration for the Container App')
type IngressConfig = {
  @description('Whether ingress is enabled')
  enabled: bool
  @description('Target port for ingress traffic')
  targetPort: int
  @description('Whether ingress is externally accessible')
  external: bool
  @description('Transport method for ingress')
  transportMethod: TransportMethod
}

@export()
@description('Configuration for a single HTTP health probe')
type ProbeConfig = {
  @description('HTTP path for the probe (empty to disable)')
  path: string
  @description('Port for the probe (0 to disable)')
  port: int
}

@export()
@description('Health probe configuration for the Container App')
type HealthProbeConfig = {
  @description('Readiness probe configuration')
  readiness: ProbeConfig
  @description('Liveness probe configuration')
  liveness: ProbeConfig
  @description('Startup probe configuration')
  startup: ProbeConfig
}

@export()
@description('Environment variable for the container: either a plain value or a reference to an entry in keyVaultSecrets (mutually exclusive)')
type EnvVar = {
  @description('Name of the environment variable')
  name: string
  @description('Plain-text value. Omit when secretRef is set.')
  value: string?
  @description('Name of the secret (declared in keyVaultSecrets) to bind this env var to. Omit when value is set.')
  secretRef: string?
}

@export()
@description('A secret backed by a Key Vault URI, surfaced to the Container App via configuration.secrets and consumed by env vars through secretRef')
type KeyVaultSecretRef = {
  @description('Secret name within the Container App (lowercase alphanumeric and dashes only). Referenced by EnvVar.secretRef.')
  name: string
  @description('Full Key Vault secret URI, e.g. https://myvault.vault.azure.net/secrets/mysecret')
  keyVaultUrl: string
}
