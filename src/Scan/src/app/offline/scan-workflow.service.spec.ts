import {TestBed} from '@angular/core/testing';

import {ScanLocalStoreService} from './scan-local-store.service';
import {ScanAssociationSettings, ScanCatalogBook} from './scan-offline.model';
import {ScanVerdictService} from './scan-verdict.service';
import {ScanWorkflowService} from './scan-workflow.service';

describe('ScanWorkflowService', () => {
  let service: ScanWorkflowService;
  let store: ScanLocalStoreService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [ScanLocalStoreService, ScanVerdictService, ScanWorkflowService],
    });
    service = TestBed.inject(ScanWorkflowService);
    store = TestBed.inject(ScanLocalStoreService);
    await store.clearCatalog();
    await store.clearSession();

    for (const entry of await store.listOutboxEntries()) {
      await store.deleteOutboxEntry(entry.clientGestureId);
    }
  });

  it('creates a durable pending gesture with an immediate local verdict', async () => {
    await store.saveSettings(createSettings(2, 10));
    await store.putCatalogBooks([createBook({qtyAvailable: 1})]);

    const result = await service.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );

    expect(result.entry.status).toBe('Pending');
    expect(result.entry.scanSessionId).not.toBe('');
    expect(result.verdict.verdict).toBe('FirstCopy');
    expect((await store.listOutboxEntries()).length).toBe(1);
    expect((await store.getSession())?.lastScanAt).toBe('2026-09-03T08:01:00.000Z');
  });

  it('automatically keeps the previous pending scan when the next book is scanned', async () => {
    await store.saveSettings(createSettings(10, 10));

    const first = await service.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    const second = await service.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:02:00.000Z'),
    );

    expect((await store.getOutboxEntry(first.entry.clientGestureId))?.status).toBe('Kept');
    expect(second.verdict.totalKnownQuantity).toBe(1);
    expect((await store.getCatalogBook('9782070363735'))?.qtyAvailable).toBe(1);
  });

  it('keeps or rejects the current gesture explicitly without deleting it', async () => {
    const kept = await service.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await service.decide(kept.entry.clientGestureId, true);

    const rejected = await service.recordScan(
      '9780306406157',
      new Date('2026-09-03T08:02:00.000Z'),
    );
    await service.decide(rejected.entry.clientGestureId, false);

    expect((await store.getOutboxEntry(kept.entry.clientGestureId))?.status).toBe('Kept');
    expect((await store.getOutboxEntry(rejected.entry.clientGestureId))?.status).toBe('Rejected');
    expect((await store.getCatalogBook('9782070363735'))?.qtyAvailable).toBe(1);
    expect(await store.getCatalogBook('9780306406157')).toBeNull();
  });

  it('caches metadata without erasing locally known quantities', async () => {
    await store.putCatalogBooks([createBook({qtyAvailable: 3, title: null})]);

    await service.cacheMetadata({
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1946,
      coverUrl: null,
      source: 'BnF',
      workId: 'work-1',
      retrievedAt: '2026-09-03T08:03:00.000Z',
    });

    const book = await store.getCatalogBook('9782070363735');
    expect(book?.title).toBe('Le Petit Prince');
    expect(book?.workId).toBe('work-1');
    expect(book?.qtyAvailable).toBe(3);
  });

  it('restores the latest pending decision after reopening the scanner', async () => {
    await store.putCatalogBooks([createBook({title: 'Livre à reprendre'})]);
    const scan = await service.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:04:00.000Z'),
    );

    const restored = await service.getLatestPendingResult();

    expect(restored?.entry.clientGestureId).toBe(scan.entry.clientGestureId);
    expect(restored?.catalogBook?.title).toBe('Livre à reprendre');
    expect(restored?.verdict.verdict).toBe('FirstCopy');
  });

  function createBook(overrides: Partial<ScanCatalogBook> = {}): ScanCatalogBook {
    return {
      isbn13: '9782070363735',
      title: null,
      authors: null,
      workId: null,
      qtyAvailable: 0,
      qtyAnnounced: 0,
      salesCount: 0,
      isWanted: false,
      isRare: false,
      updatedAt: '2026-09-03T08:00:00.000Z',
      ...overrides,
    };
  }

  function createSettings(
    duplicateThreshold: number,
    demandSalesThreshold: number,
  ): ScanAssociationSettings {
    return {
      duplicateThreshold,
      demandSalesThreshold,
      deadStockMinAgeDays: 30,
      deadStockMinQuantity: 1,
      watchlistMaxItems: 100,
      alertCooldownDays: 30,
      sessionIdleTimeoutMinutes: 120,
      alertDelayMinutes: 120,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }
});
