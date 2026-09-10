import {
  createScanDiagnosticExport,
  isScanDiagnosticRequested,
  serializeScanDiagnosticExport,
  ScanDiagnosticStoreState,
} from './scan-diagnostic-export';

describe('scan diagnostic export', () => {
  it('only enables diagnostic mode for the exact diagnostic query value', () => {
    expect(isScanDiagnosticRequested('?diagnostic=1')).toBeTrue();
    expect(isScanDiagnosticRequested('?foo=1&diagnostic=1')).toBeTrue();
    expect(isScanDiagnosticRequested('?diagnostic=0')).toBeFalse();
    expect(isScanDiagnosticRequested('?diagnostic=true')).toBeFalse();
    expect(isScanDiagnosticRequested('')).toBeFalse();
  });

  it('serializes a complete state without account identities or credentials', () => {
    const state: ScanDiagnosticStoreState = {
      catalogCount: 7,
      recentCatalog: [],
      session: {
        key: 'active-session',
        clientSessionId: 'session-1',
        remoteSessionId: null,
        volunteerId: 'entra-account-1',
        mode: 'AvailableNow',
        targetAssoEventsId: null,
        startedAt: '2026-09-09T09:12:00.000Z',
        lastScanAt: '2026-09-09T09:41:00.000Z',
        lastSyncAt: '2026-09-09T09:41:12.000Z',
        closeRequested: false,
        closeReason: null,
      },
      closeRequests: [{
        key: 'close-request:session-1',
        clientSessionId: 'session-1',
        remoteSessionId: null,
        volunteerId: 'entra-account-1',
        mode: 'AvailableNow',
        targetAssoEventsId: null,
        closeReason: 'Manual',
        requestedAt: '2026-09-09T09:42:00.000Z',
      }],
      sessionCounts: {scannedCount: 2, keptCount: 1, rejectedCount: 0},
      outbox: [
        createOutboxEntry('Orphaned', 'gesture-orphaned'),
        createOutboxEntry('Quarantined', 'gesture-quarantined'),
      ],
      sales: [],
    };

    const serialized = serializeScanDiagnosticExport(state, '2026-09-09T09:43:00.000Z');
    const exported = JSON.parse(serialized) as ReturnType<typeof createScanDiagnosticExport>;

    expect(exported.databaseVersion).toBe(3);
    expect(exported.session?.hasVolunteerId).toBeTrue();
    expect(exported.outbox.map(entry => entry.status)).toEqual(['Orphaned', 'Quarantined']);
    expect(serialized).not.toContain('volunteerId');
    expect(serialized).not.toContain('homeAccountId');
    expect(serialized).not.toContain('accessToken');
    expect(serialized).not.toContain('idToken');
    expect(serialized).not.toContain('refreshToken');
    expect(serialized).not.toContain('msal');
  });

  function createOutboxEntry(
    status: 'Orphaned' | 'Quarantined',
    clientGestureId: string,
  ) {
    return {
      clientGestureId,
      clientSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: '2026-09-09T09:38:00.000Z',
      createdAt: '2026-09-09T09:38:00.100Z',
      status,
      kept: status === 'Quarantined' ? true : null,
      catalogApplied: false,
      verdict: 'FirstCopy' as const,
      quantityAvailable: 0,
      quantityAnnounced: 0,
      salesCount: 0,
      isRare: false,
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: 'diagnostic error',
      lastFailureKind: null,
    };
  }
});
