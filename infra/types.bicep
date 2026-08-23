@export()
type EnvironmentName = 'development'

@export()
type EnvironmentVariables = {
  envName: string
  envShort: string
  envSuffix: string
  envPrefix: string
  location: string
  tags: object
}

@export()
var environments = {
  development: {
    envName: 'Development'
    envShort: 'dev'
    envSuffix: '-dev'
    envPrefix: 'dev-'
    location: 'westeurope'
    tags: {
      project: 'Vole-Papillon-Damour'
      environment: 'development'
      managedBy: 'bicep'
    }
  }
}

@description('Rbac Role Type')
@export()
type RbacRoleType = {
  @description('Identifier of the role')
  id: string

  @description('Name of the role')
  description: string
}
