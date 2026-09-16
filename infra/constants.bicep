// Role definitions id
@export()
@description('RBAC roles grouped by Azure service')
var RbacRoles = {
  containerregistry: {
    AcrPull: {
      id: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
      description: 'Allows pull of images from an Azure Container Registry.'
    }
  }
  keyvault: {
    'Key Vault Secrets User': {
      id: '4633458b-17de-408a-b874-0445c86b69e6'
      description: 'Read secret contents including the secret portion of a certificate with private key.'
    }
  }
  communication: {
    CommunicationAndEmailServiceOwner: {
      id: '09976791-48a7-449e-bb21-39d1a415f350'
      description: 'Allows sending emails and managing Communication Services email resources.'
    }
  }
  monitor: {
    MonitoringMetricsPublisher: {
      id: '3913510d-42f4-4e42-8a64-420c390055eb'
      description: 'Enables publishing metrics and telemetry against Azure resources.'
    }
  }
}
