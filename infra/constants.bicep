// Role definitions id
@export()
@description('RBAC roles grouped by Azure service')
var RbacRoles = {
  containerregistry: {
    AcrPull: {
      id: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
      description: 'Allows pull of images from an Azure Container Registry.'
    }
    AcrPush: {
      id: '8311e382-0749-4cb8-b61a-304f252e45ec'
      description: 'Allows pull and push of images in an Azure Container Registry.'
    }
    AcrDelete: {
      id: 'c2f4ef07-c644-48eb-af81-4b1b4947fb11'
      description: 'Allows deletion of image data in an Azure Container Registry.'
    }
    Owner: {
      id: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
      description: 'Allows full management and role assignment control over the shared Azure Container Registry.'
    }
  }
  keyvault: {
    'Key Vault Secrets User': {
      id: '4633458b-17de-408a-b874-0445c86b69e6'
      description: 'Read secret contents including the secret portion of a certificate with private key.'
    }
    'Key Vault Secrets Officer': {
      id: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
      description: 'Manage secrets in the dedicated Worker invocation-key vault.'
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
