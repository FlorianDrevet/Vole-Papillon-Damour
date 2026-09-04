export const scanDatabaseName = 'vpd-scan';
export const scanDatabaseVersion = 1;

export const scanStoreNames = {
  catalog: 'catalog',
  outbox: 'outbox',
  session: 'session',
} as const;

export type ScanStoreName = typeof scanStoreNames[keyof typeof scanStoreNames];

export type LocalScanMode = 'AvailableNow' | 'NextFair';

export type LocalBookVerdict = 'Wanted' | 'Selling' | 'TooMany' | 'FirstCopy';

export type ScanOutboxStatus = 'Pending' | 'Kept' | 'Rejected' | 'CancelledLocal';

export interface ScanCatalogBook {
  isbn13: string;
  title: string | null;
  authors: string | null;
  workId: string | null;
  qtyAvailable: number;
  qtyAnnounced: number;
  salesCount: number;
  isWanted: boolean;
  isRare: boolean;
  updatedAt: string;
}

export interface ScanAssociationSettings {
  duplicateThreshold: number;
  demandSalesThreshold: number;
  deadStockMinAgeDays: number;
  deadStockMinQuantity: number;
  watchlistMaxItems: number;
  alertCooldownDays: number;
  sessionIdleTimeoutMinutes: number;
  alertDelayMinutes: number;
  updatedAt: string;
}

export interface ScanAssociationSettingsRecord extends ScanAssociationSettings {
  key: 'association-settings';
}

export interface ScanCatalogSyncState {
  key: 'catalog-sync';
  watermark: string | null;
  updatedAt: string;
}

export interface ScanSessionSnapshot {
  key: 'active-session';
  scanSessionId: string;
  volunteerId: string | null;
  mode: LocalScanMode;
  targetAssoEventsId: string | null;
  startedAt: string;
  lastScanAt: string;
  lastSyncAt: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
}

export interface ScanOutboxEntry {
  clientGestureId: string;
  scanSessionId: string;
  isbn13: string;
  occurredAt: string;
  createdAt: string;
  status: ScanOutboxStatus;
  kept: boolean | null;
  catalogApplied: boolean;
  verdict: LocalBookVerdict;
  quantityAvailable: number;
  quantityAnnounced: number;
  salesCount: number;
  isRare: boolean;
  attemptCount: number;
  lastAttemptAt: string | null;
  lastError: string | null;
}

export interface LocalVerdict {
  verdict: LocalBookVerdict;
  totalKnownQuantity: number;
  salesCount: number;
  activeRequesterCount: number;
  isRare: boolean;
  isKnown: boolean;
}

export interface LocalScanResult {
  entry: ScanOutboxEntry;
  verdict: LocalVerdict;
  catalogBook: ScanCatalogBook | null;
}

export interface LocalCatalogResult {
  verdict: LocalVerdict;
  catalogBook: ScanCatalogBook | null;
}

export interface PersistentStorageStatus {
  available: boolean;
  persisted: boolean;
  requestAttempted: boolean;
}

export interface ScanCatalogDeltaResponse {
  generatedAt: string;
  nextWatermark: string;
  books: Array<ScanCatalogBook & {isHidden: boolean}>;
  settings: ScanAssociationSettings;
}

export interface ScanSessionResponse {
  scanSessionId: string;
  volunteerId: string;
  mode: LocalScanMode;
  targetAssoEventsId: string | null;
  startedAt: string;
  lastScanAt: string;
  lastSyncAt: string;
  lateArrivals: boolean;
  endedAt: string | null;
  closeReason: string | null;
  status: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
}

export interface ScanBookResponse {
  isbn13: string;
  verdict: LocalBookVerdict;
  qtyAvailable: number;
  qtyAnnounced: number;
  scanSessionId: string;
  movementType: string;
  alreadyProcessed: boolean;
  clockSuspect: boolean;
}
