@export()
@description('Configuration of the Azure SQL Database SKU')
type DatabaseSkuConfig = {
  @description('SKU name, e.g. S1 (Standard DTU) or GP_S_Gen5_1 (serverless)')
  name: string

  @description('SKU tier, e.g. Standard, GeneralPurpose, or Basic')
  tier: string

  @description('Hardware family, e.g. Gen5. Omit for Basic/Standard DTU tiers such as S1.')
  family: string?

  @description('Number of vCores (vCore tiers) or DTUs (DTU tiers); S1 uses 20 DTUs')
  capacity: int

  @description('Maximum database size in bytes; S1 uses 250 GB')
  maxSizeBytes: int

  @description('Auto-pause delay in minutes for serverless tiers. Use -1 to disable auto-pause, 0 for non-serverless tiers such as S1.')
  autoPauseDelayMinutes: int
}
