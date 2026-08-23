@export()
@description('SKU for the Container Registry')
type SkuName = 'Basic' | 'Standard' | 'Premium'

@export()
@description('Public network access setting for the Container Registry')
type PublicNetworkAccess = 'Enabled' | 'Disabled'
