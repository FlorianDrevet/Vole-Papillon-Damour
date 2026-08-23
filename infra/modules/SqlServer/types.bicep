@export()
@description('Configuration of the Azure SQL Database SKU')
type DatabaseSkuConfig = {
  @description('SKU name, e.g. GP_S_Gen5_1 (serverless) or Basic')
  name: string

  @description('SKU tier, e.g. GeneralPurpose or Basic')
  tier: string

  @description('Hardware family, e.g. Gen5. Omit for Basic/Standard DTU tiers.')
  family: string?

  @description('Number of vCores (vCore tiers) or DTUs (DTU tiers)')
  capacity: int

  @description('Maximum database size in bytes')
  maxSizeBytes: int

  @description('Auto-pause delay in minutes for serverless tiers. Use -1 to disable auto-pause, 0 for non-serverless tiers.')
  autoPauseDelayMinutes: int
}
