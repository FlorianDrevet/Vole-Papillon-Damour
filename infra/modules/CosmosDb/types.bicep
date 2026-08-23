@export()
@description('Default consistency level for the Cosmos DB account')
type ConsistencyLevel = 'Eventual' | 'Session' | 'BoundedStaleness' | 'Strong' | 'ConsistentPrefix'

@export()
@description('Backup policy type for the Cosmos DB account')
type BackupPolicyType = 'Periodic' | 'Continuous'

@export()
@description('A Cosmos DB SQL container to provision, with its partition key path')
type ContainerConfig = {
  @description('Name of the container')
  name: string
  @description('Partition key path, e.g. /id or /ProjectId')
  partitionKeyPath: string
}
