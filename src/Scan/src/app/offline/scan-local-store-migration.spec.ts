import {forceCatalogResyncForMetadataUpgrade, migrateLegacyScanOutboxEntry} from './scan-local-store-migration';

describe('scan IndexedDB migration', () => {
  it('forces one complete catalog resynchronization for existing stores after the metadata upgrade', () => {
    const calls = {catalogClears: 0, deletedKeys: [] as IDBValidKey[]};
    const catalog = {
      clear: () => calls.catalogClears++,
    } as unknown as IDBObjectStore;
    const session = {
      delete: (key: IDBValidKey) => calls.deletedKeys.push(key),
    } as unknown as IDBObjectStore;
    const transaction = {
      objectStore: jasmine.createSpy('objectStore').and.callFake((name: string) =>
        name === 'catalog' ? catalog : session),
    } as unknown as IDBTransaction;

    forceCatalogResyncForMetadataUpgrade(6, transaction);
    forceCatalogResyncForMetadataUpgrade(7, transaction);

    expect(calls.catalogClears).toBe(1);
    expect(calls.deletedKeys.length).toBe(1);
    expect(String(calls.deletedKeys[0])).toBe('catalog-sync');
  });

  it('preserves every legacy entry while assigning an actionable v3 status and reason', () => {
    const entries = [
      createLegacyEntry('gesture-undecided', 'Orphaned', null),
      createLegacyEntry('gesture-decided', 'Orphaned', true),
      createLegacyEntry('gesture-rejected', 'Quarantined', false),
      createLegacyEntry('gesture-kept', 'Kept', true),
    ];

    const migrated = entries.map(migrateLegacyScanOutboxEntry);

    expect(migrated).toHaveSize(entries.length);
    expect(migrated.map(entry => [entry.clientGestureId, entry.status, entry.setAsideReason]))
      .toEqual([
        ['gesture-undecided', 'NeedsDecision', 'undecided'],
        ['gesture-decided', 'NeedsReattach', 'no-session'],
        ['gesture-rejected', 'RejectedByServer', 'server-refused'],
        ['gesture-kept', 'Kept', null],
      ]);
  });

  function createLegacyEntry(
    clientGestureId: string,
    status: 'Orphaned' | 'Quarantined' | 'Kept',
    kept: boolean | null,
  ) {
    return {
      clientGestureId,
      scanSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: '2026-09-09T09:38:00.000Z',
      createdAt: '2026-09-09T09:38:00.100Z',
      status,
      kept,
      catalogApplied: false,
      verdict: 'FirstCopy' as const,
      quantityAvailable: 0,
      quantityAnnounced: 0,
      salesCount: 0,
      isRare: false,
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
      lastFailureKind: null,
    };
  }
});
