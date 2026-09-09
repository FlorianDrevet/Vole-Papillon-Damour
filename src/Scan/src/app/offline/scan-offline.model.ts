export const scanDatabaseName = 'vpd-scan';
export const scanDatabaseVersion = 3;

export const scanStoreNames = {
  catalog: 'catalog',
  outbox: 'outbox',
  sales: 'sales',
  session: 'session',
} as const;

export type ScanStoreName = typeof scanStoreNames[keyof typeof scanStoreNames];

export type LocalScanMode = 'AvailableNow' | 'NextFair';

export type LocalScanCloseReason = 'Manual' | 'Inactivity' | 'Disconnect' | 'TokenExpired';

export type ScanLocalStoreFailureReason =
  | 'unavailable'
  | 'closed-by-other-instance'
  | 'blocked-by-other-instance';

export class ScanLocalStoreError extends Error {
  constructor(
    readonly reason: ScanLocalStoreFailureReason,
    message: string,
  ) {
    super(message);
    this.name = 'ScanLocalStoreError';
  }
}

export class ScanSessionClosePendingError extends Error {
  constructor() {
    super('The scan session is waiting for synchronization to close.');
    this.name = 'ScanSessionClosePendingError';
  }
}

export type LocalBookVerdict = 'Wanted' | 'Selling' | 'TooMany' | 'FirstCopy';

export type ScanOutboxStatus =
  | 'Pending'
  | 'Kept'
  | 'Rejected'
  | 'CancelledLocal'
  | 'NeedsDecision'
  | 'NeedsReattach'
  | 'RejectedByServer';

export type ScanSetAsideReason =
  | 'no-session'
  | 'other-volunteer'
  | 'undecided'
  | 'server-refused';

export type ScanSaleOutboxStatus = 'Pending' | 'Quarantined';

export type ScanFailureKind = 'transient' | 'permanent' | 'authorization';

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
  nextFair: ScanNextBookFair | null;
}

export interface ScanNextBookFair {
  id: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  openAt: string;
  closeAt: string | null;
}

export interface ScanSessionSnapshot {
  key: 'active-session';
  clientSessionId: string;
  remoteSessionId: string | null;
  volunteerId: string | null;
  mode: LocalScanMode;
  targetAssoEventsId: string | null;
  startedAt: string;
  lastScanAt: string;
  lastSyncAt: string;
  closeRequested?: boolean;
  closeReason?: LocalScanCloseReason | null;
}

export interface ScanSessionCounts {
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
}

export interface ScanSessionCloseRequest {
  key: string;
  clientSessionId: string;
  remoteSessionId: string | null;
  volunteerId?: string | null;
  mode: LocalScanMode;
  targetAssoEventsId: string | null;
  closeReason: LocalScanCloseReason;
  requestedAt: string;
  startedAt?: string;
}

export interface ScanOutboxEntry {
  clientGestureId: string;
  clientSessionId: string;
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
  lastFailureKind?: ScanFailureKind | null;
  setAsideReason?: ScanSetAsideReason | null;
}

export interface ScanSaleOutboxEntry {
  clientGestureId: string;
  isbn13: string;
  quantity: number;
  status?: ScanSaleOutboxStatus;
  occurredAt: string;
  createdAt: string;
  attemptCount: number;
  lastAttemptAt: string | null;
  lastError: string | null;
  lastFailureKind?: ScanFailureKind | null;
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
  isImmediateRepeat: boolean;
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
  nextFair: ScanNextBookFair | null;
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
  reusedExistingSession: boolean;
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

export interface ScanSaleResponse {
  isbn13: string;
  saleMovementId: string;
  quantity: number;
  qtyAvailable: number;
  salesCount: number;
  assoEventsId: string | null;
  fairMatchStatus: string;
  hadNoAvailableStock: boolean;
  hadUnreleasedAnnouncement: boolean;
  isRare: boolean;
  clockSuspect: boolean;
  alreadyProcessed: boolean;
}
