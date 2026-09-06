import {TestBed} from '@angular/core/testing';

import {
  ScanCatalogBook,
  ScanOutboxEntry,
  ScanOutboxStatus,
  ScanSaleOutboxEntry,
} from './scan-offline.model';
import {ScanLocalStoreService} from './scan-local-store.service';

describe('ScanLocalStoreService', () => {
  let service: ScanLocalStoreService;

  beforeEach(async () => {
    TestBed.configureTestingModule({providers: [ScanLocalStoreService]});
    service = TestBed.inject(ScanLocalStoreService);
    await service.clearCatalog();
    await service.clearSession();

    for (const entry of await service.listOutboxEntries()) {
      await service.deleteOutboxEntry(entry.clientGestureId);
    }
    for (const entry of await service.listSaleOutboxEntries()) {
      await service.deleteSaleOutboxEntry(entry.clientGestureId);
    }
  });

  it('keeps the catalog and outbox in separate stores', async () => {
    const catalogBook = createCatalogBook();
    const outboxEntry = createOutboxEntry();

    await service.putCatalogBooks([catalogBook]);
    await service.addOutboxEntry(outboxEntry);
    await service.clearCatalog();

    expect(await service.getCatalogBook(catalogBook.isbn13)).toBeNull();
    expect(await service.getOutboxEntry(outboxEntry.clientGestureId)).toEqual(outboxEntry);
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
    });

    expect(await service.getSession()).toEqual(session);
    expect(await service.getCatalogSyncState()).toEqual({
      key: 'catalog-sync',
      watermark: '2026-09-03T08:00:00.000Z',
      updatedAt: '2026-09-03T08:00:00.000Z',
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
    expect(await service.countPendingOutboxEntries()).toBe(3);
  });

  it('updates an outbox decision without losing the durable gesture', async () => {
    const entry = createOutboxEntry();
    await service.addOutboxEntry(entry);

    await service.updateOutboxStatus(entry.clientGestureId, 'Rejected');

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
    expect(await service.countPendingOutboxEntries()).toBe(1);
  });

  it('reports whether persistent storage is available without touching data stores', async () => {
    const status = await service.requestPersistentStorage();

    expect(status.available).toBeTrue();
    expect(typeof status.persisted).toBe('boolean');
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

  function createSaleOutboxEntry(): ScanSaleOutboxEntry {
    return {
      clientGestureId: 'sale-1',
      isbn13: '9782070363735',
      quantity: 1,
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.000Z',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }
});
