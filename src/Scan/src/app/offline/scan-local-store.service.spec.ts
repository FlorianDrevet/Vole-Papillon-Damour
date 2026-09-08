import {TestBed} from '@angular/core/testing';

import {
  ScanCatalogBook,
  ScanOutboxEntry,
  ScanOutboxStatus,
  ScanSaleOutboxEntry,
} from './scan-offline.model';
import {ScanLocalStoreService} from './scan-local-store.service';
import {clearScanCatalogForTest} from './scan-test.utils';

describe('ScanLocalStoreService', () => {
  let service: ScanLocalStoreService;

  beforeEach(async () => {
    TestBed.configureTestingModule({providers: [ScanLocalStoreService]});
    service = TestBed.inject(ScanLocalStoreService);
    await clearScanCatalogForTest(service);
    await service.clearSession();
    await service.clearSessionCloseRequests();

    for (const entry of await service.listOutboxEntries()) {
      await service.deleteOutboxEntry(entry.clientGestureId);
    }
    for (const entry of await service.listSaleOutboxEntries()) {
      await service.deleteSaleOutboxEntry(entry.clientGestureId);
    }
  });

  it('round-trips session and catalog synchronization state', async () => {
    const session = {
      key: 'active-session' as const,
      scanSessionId: 'session-1',
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow' as const,
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:01:00.000Z',
      lastSyncAt: '2026-09-03T08:00:00.000Z',
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
    };

    await service.saveSession(session);
    await service.saveCatalogSyncState({
      key: 'catalog-sync',
      watermark: '2026-09-03T08:00:00.000Z',
      updatedAt: '2026-09-03T08:00:00.000Z',
      nextFair: null,
    });

    expect(await service.getSession()).toEqual(session);
    expect(await service.getCatalogSyncState()).toEqual({
      key: 'catalog-sync',
      watermark: '2026-09-03T08:00:00.000Z',
      updatedAt: '2026-09-03T08:00:00.000Z',
      nextFair: null,
    });
  });

  it('orders the outbox by creation time and filters transmittable decisions', async () => {
    const entries = [
      createOutboxEntry('gesture-2', '2026-09-03T08:02:00.000Z', 'Kept'),
      createOutboxEntry('gesture-1', '2026-09-03T08:01:00.000Z', 'Pending'),
      createOutboxEntry('gesture-3', '2026-09-03T08:03:00.000Z', 'Rejected'),
      createOutboxEntry('gesture-4', '2026-09-03T08:04:00.000Z', 'CancelledLocal'),
    ];

    for (const entry of entries) {
      await service.addOutboxEntry(entry);
    }

    expect((await service.listOutboxEntries()).map(entry => entry.clientGestureId))
      .toEqual(['gesture-1', 'gesture-2', 'gesture-3', 'gesture-4']);
    expect((await service.listTransmittableOutboxEntries()).map(entry => entry.clientGestureId))
      .toEqual(['gesture-2', 'gesture-3']);
    expect(await service.getOutboxCounts()).toEqual({
      pendingDecisionCount: 1,
      pendingTransmissionCount: 2,
    });
  });

  it('separates pending decisions from entries waiting for transmission', async () => {
    for (const entry of [
      createOutboxEntry('pending', '2026-09-03T08:01:00.000Z', 'Pending'),
      createOutboxEntry('kept', '2026-09-03T08:02:00.000Z', 'Kept'),
      createOutboxEntry('rejected', '2026-09-03T08:03:00.000Z', 'Rejected'),
      createOutboxEntry('orphaned', '2026-09-03T08:04:00.000Z', 'Orphaned'),
      createOutboxEntry('quarantined', '2026-09-03T08:05:00.000Z', 'Quarantined'),
    ]) {
      await service.addOutboxEntry(entry);
    }
    await service.addSaleOutboxEntries(
      [
        createSaleOutboxEntry('sale-pending'),
        createSaleOutboxEntry('sale-quarantined', 'Quarantined'),
      ],
      [],
    );

    expect(await service.getOutboxCounts()).toEqual({
      pendingDecisionCount: 1,
      pendingTransmissionCount: 3,
    });
  });

  it('updates an outbox decision without losing the durable gesture', async () => {
    const entry = createOutboxEntry();
    await service.addOutboxEntry(entry);

    await service.decideOutboxEntry(entry.clientGestureId, false, 'AvailableNow');

    const updated = await service.getOutboxEntry(entry.clientGestureId);
    expect(updated?.status).toBe('Rejected');
    expect(updated?.clientGestureId).toBe(entry.clientGestureId);
  });

  it('stores a cash sale and its optimistic catalog projection atomically', async () => {
    const entry = createSaleOutboxEntry();
    const book = {...createCatalogBook(), qtyAvailable: 1, salesCount: 4};

    await service.addSaleOutboxEntries([entry], [{...book, qtyAvailable: 0, salesCount: 5}]);

    expect(await service.getSaleOutboxEntry(entry.clientGestureId)).toEqual(entry);
    expect(await service.getCatalogBook(book.isbn13)).toEqual(
      jasmine.objectContaining({qtyAvailable: 0, salesCount: 5}),
    );
    expect(await service.getOutboxCounts()).toEqual({
      pendingDecisionCount: 0,
      pendingTransmissionCount: 1,
    });
  });

  it('clears account-owned state without removing the catalog synchronization state', async () => {
    const book = createCatalogBook();
    const syncState = {
      key: 'catalog-sync' as const,
      watermark: 'watermark',
      updatedAt: '2026-09-03T08:00:00.000Z',
      nextFair: null,
    };
    const session = {
      key: 'active-session' as const,
      scanSessionId: 'session-1',
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow' as const,
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:01:00.000Z',
      lastSyncAt: '2026-09-03T08:00:00.000Z',
      scannedCount: 1,
      keptCount: 1,
      rejectedCount: 0,
    };

    await service.putCatalogBooks([book]);
    await service.saveCatalogSyncState(syncState);
    await service.saveSession(session);
    await service.saveSessionCloseRequest({
      scanSessionId: session.scanSessionId,
      mode: session.mode,
      targetAssoEventsId: null,
      closeReason: 'Manual',
      requestedAt: '2026-09-03T08:02:00.000Z',
    });
    await service.addOutboxEntry(createOutboxEntry('gesture-1', undefined, 'Kept'));
    await service.addSaleOutboxEntries([createSaleOutboxEntry()], []);

    await service.clearAccountState();

    expect(await service.getSession()).toBeNull();
    expect(await service.listSessionCloseRequests()).toEqual([]);
    expect(await service.listOutboxEntries()).toEqual([]);
    expect(await service.listSaleOutboxEntries()).toEqual([]);
    expect(await service.getCatalogBook(book.isbn13)).toEqual(book);
    expect(await service.getCatalogSyncState()).toEqual(syncState);
  });

  it('reports whether persistent storage is available without touching data stores', async () => {
    const status = await service.requestPersistentStorage();

    expect(status.available).toBeTrue();
    expect(typeof status.persisted).toBe('boolean');
  });

  it('reopens IndexedDB after another instance closes the current connection', async () => {
    const database = await (service as unknown as {
      databasePromise: Promise<IDBDatabase>;
    }).databasePromise;
    const open = spyOn(indexedDB, 'open').and.callThrough();

    database.onversionchange?.(new Event('versionchange') as unknown as IDBVersionChangeEvent);

    await service.getCatalogBooks();

    expect(open).toHaveBeenCalledOnceWith('vpd-scan', 2);
  });

  it('can retry opening IndexedDB after an upgrade was blocked by another instance', async () => {
    const realOpen = indexedDB.open.bind(indexedDB);
    let blocked = true;
    const open = spyOn(indexedDB, 'open').and.callFake((name, version) => {
      if (!blocked) {
        return realOpen(name, version);
      }

      const request = {} as IDBOpenDBRequest;
      queueMicrotask(() => request.onblocked?.(new Event('blocked') as unknown as IDBVersionChangeEvent));
      return request;
    });
    const freshService = new ScanLocalStoreService();
    let firstError: unknown;

    try {
      await freshService.getCatalogBooks();
      fail('The blocked IndexedDB open should be reported.');
    } catch (error: unknown) {
      firstError = error;
    }

    expect((firstError as {name?: string}).name).toBe('ScanLocalStoreError');
    expect((firstError as {reason?: string}).reason).toBe('blocked-by-other-instance');

    blocked = false;
    await freshService.getCatalogBooks();

    expect(open).toHaveBeenCalledTimes(2);
  });

  function createCatalogBook(): ScanCatalogBook {
    return {
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      workId: null,
      qtyAvailable: 2,
      qtyAnnounced: 1,
      salesCount: 3,
      isWanted: false,
      isRare: false,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }

  function createOutboxEntry(
    clientGestureId = 'gesture-1',
    createdAt = '2026-09-03T08:01:00.000Z',
    status: ScanOutboxStatus = 'Pending',
  ): ScanOutboxEntry {
    return {
      clientGestureId,
      scanSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: createdAt,
      createdAt,
      status,
      kept: status === 'Kept' ? true : status === 'Rejected' ? false : null,
      catalogApplied: false,
      verdict: 'FirstCopy',
      quantityAvailable: 0,
      quantityAnnounced: 0,
      salesCount: 0,
      isRare: false,
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }

  function createSaleOutboxEntry(
    clientGestureId = 'sale-1',
    status?: 'Pending' | 'Quarantined',
  ): ScanSaleOutboxEntry {
    return {
      clientGestureId,
      isbn13: '9782070363735',
      quantity: 1,
      status,
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.000Z',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }
});
