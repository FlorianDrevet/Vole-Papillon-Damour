@export()
@description('SKU name for the Storage Account')
type SkuName = 'Standard_LRS' | 'Standard_GRS' | 'Standard_ZRS' | 'Premium_LRS'

@export()
@description('Public read access level of a blob container')
type PublicAccessLevel = 'None' | 'Blob' | 'Container'

@export()
@description('A blob container to create in the Storage Account')
type BlobContainerConfig = {
  @description('Name of the blob container (lowercase)')
  name: string

  @description('Anonymous read access level. Blob = images readable by their direct URL.')
  publicAccess: PublicAccessLevel
}
