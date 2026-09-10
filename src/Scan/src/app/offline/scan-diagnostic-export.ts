import {
  scanDatabaseVersion,
  ScanCatalogBook,
  ScanOutboxEntry,
  ScanSaleOutboxEntry,
  ScanSessionCloseRequest,
  ScanSessionCounts,
  ScanSessionSnapshot,
} from './scan-offline.model';

export interface ScanDiagnosticStoreState {
  catalogCount: number;
  recentCatalog: ScanCatalogBook[];
  session: ScanSessionSnapshot | null;
  sessionCounts?: ScanSessionCounts | null;
  closeRequests: ScanSessionCloseRequest[];
  outbox: ScanDiagnosticOutboxEntry[];
  sales: ScanSaleOutboxEntry[];
}

export type ScanDiagnosticOutboxEntry = Omit<ScanOutboxEntry, 'status'> & {
  status: ScanOutboxEntry['status'] | 'Orphaned' | 'Quarantined';
};

export interface ScanDiagnosticSession {
  clientSessionId: string;
  remoteSessionId: string | null;
  mode: ScanSessionSnapshot['mode'];
  targetAssoEventsId: string | null;
  startedAt: string;
  lastScanAt: string;
  lastSyncAt: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  closeRequested: boolean;
  closeReason: ScanSessionSnapshot['closeReason'] | null;
  hasVolunteerId: boolean;
}

export interface ScanDiagnosticCloseRequest {
  clientSessionId: string;
  remoteSessionId: string | null;
  mode: ScanSessionCloseRequest['mode'];
  targetAssoEventsId: string | null;
  closeReason: ScanSessionCloseRequest['closeReason'];
  requestedAt: string;
  startedAt?: string;
}

export interface ScanDiagnosticExport {
  exportedAt: string;
  databaseVersion: number;
  session: ScanDiagnosticSession | null;
  closeRequests: ScanDiagnosticCloseRequest[];
  outbox: Array<{
    clientGestureId: string;
    clientSessionId: string;
    isbn13: string;
    status: ScanDiagnosticOutboxEntry['status'];
    kept: boolean | null;
    catalogApplied: boolean;
    verdict: ScanOutboxEntry['verdict'];
    quantityAvailable: number;
    quantityAnnounced: number;
    salesCount: number;
    isRare: boolean;
    occurredAt: string;
    createdAt: string;
    attemptCount: number;
    lastAttemptAt: string | null;
    lastError: string | null;
    lastFailureKind: ScanOutboxEntry['lastFailureKind'] | null;
    setAsideReason: ScanOutboxEntry['setAsideReason'] | null;
  }>;
  sales: Array<{
    clientGestureId: string;
    isbn13: string;
    quantity: number;
    status: ScanSaleOutboxEntry['status'] | null;
    occurredAt: string;
    createdAt: string;
    attemptCount: number;
    lastAttemptAt: string | null;
    lastError: string | null;
    lastFailureKind: ScanSaleOutboxEntry['lastFailureKind'] | null;
  }>;
  catalogCount: number;
}

export function isScanDiagnosticRequested(search: string): boolean {
  return new URLSearchParams(search).get('diagnostic') === '1';
}

export function createScanDiagnosticExport(
  state: ScanDiagnosticStoreState,
  exportedAt = new Date().toISOString(),
): ScanDiagnosticExport {
  return {
    exportedAt,
    databaseVersion: scanDatabaseVersion,
    session: state.session ? {
      clientSessionId: state.session.clientSessionId,
      remoteSessionId: state.session.remoteSessionId,
      mode: state.session.mode,
      targetAssoEventsId: state.session.targetAssoEventsId,
      startedAt: state.session.startedAt,
      lastScanAt: state.session.lastScanAt,
      lastSyncAt: state.session.lastSyncAt,
      scannedCount: state.sessionCounts?.scannedCount ?? 0,
      keptCount: state.sessionCounts?.keptCount ?? 0,
      rejectedCount: state.sessionCounts?.rejectedCount ?? 0,
      closeRequested: state.session.closeRequested ?? false,
      closeReason: state.session.closeReason ?? null,
      hasVolunteerId: state.session.volunteerId !== null,
    } : null,
    closeRequests: state.closeRequests.map(request => ({
      clientSessionId: request.clientSessionId,
      remoteSessionId: request.remoteSessionId,
      mode: request.mode,
      targetAssoEventsId: request.targetAssoEventsId,
      closeReason: request.closeReason,
      requestedAt: request.requestedAt,
      startedAt: request.startedAt,
    })),
    outbox: state.outbox.map(entry => ({
      clientGestureId: entry.clientGestureId,
      clientSessionId: entry.clientSessionId,
      isbn13: entry.isbn13,
      status: entry.status,
      kept: entry.kept,
      catalogApplied: entry.catalogApplied,
      verdict: entry.verdict,
      quantityAvailable: entry.quantityAvailable,
      quantityAnnounced: entry.quantityAnnounced,
      salesCount: entry.salesCount,
      isRare: entry.isRare,
      occurredAt: entry.occurredAt,
      createdAt: entry.createdAt,
      attemptCount: entry.attemptCount,
      lastAttemptAt: entry.lastAttemptAt,
      lastError: entry.lastError,
      lastFailureKind: entry.lastFailureKind ?? null,
      setAsideReason: entry.setAsideReason ?? null,
    })),
    sales: state.sales.map(entry => ({
      clientGestureId: entry.clientGestureId,
      isbn13: entry.isbn13,
      quantity: entry.quantity,
      status: entry.status ?? null,
      occurredAt: entry.occurredAt,
      createdAt: entry.createdAt,
      attemptCount: entry.attemptCount,
      lastAttemptAt: entry.lastAttemptAt,
      lastError: entry.lastError,
      lastFailureKind: entry.lastFailureKind ?? null,
    })),
    catalogCount: state.catalogCount,
  };
}

export function serializeScanDiagnosticExport(
  state: ScanDiagnosticStoreState,
  exportedAt = new Date().toISOString(),
): string {
  return JSON.stringify(createScanDiagnosticExport(state, exportedAt), null, 2);
}
