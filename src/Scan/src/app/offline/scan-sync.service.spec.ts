import {HttpErrorResponse} from '@angular/common/http';
import {TestBed} from '@angular/core/testing';
import {of, throwError} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanAssociationSettings,
  ScanCatalogBook,
  ScanCatalogDeltaResponse,
  ScanBookResponse,
  ScanSaleResponse,
  ScanSessionResponse,
} from './scan-offline.model';
import {ScanSyncService} from './scan-sync.service';
import {ScanVerdictService} from './scan-verdict.service';
import {ScanWorkflowService} from './scan-workflow.service';
import {clearScanCatalogForTest} from './scan-test.utils';

describe('ScanSyncService', () => {
  let service: ScanSyncService;
  let workflow: ScanWorkflowService;
  let store: ScanLocalStoreService;
  let api: jasmine.SpyObj<ScanApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', [
      'getCatalogDelta',
      'openSession',
      'scanBook',
      'registerSale',
      'closeSession',
    ]);
    api.getCatalogDelta.and.returnValue(of(createDelta()));
    api.openSession.and.returnValue(of(createSessionResponse()));
    api.scanBook.and.returnValue(of(createScanResponse()));
    api.registerSale.and.returnValue(of(createSaleResponse()));
    api.closeSession.and.returnValue(of(createClosedSessionResponse()));

    TestBed.configureTestingModule({
      providers: [
        ScanLocalStoreService,
        ScanVerdictService,
        ScanWorkflowService,
        ScanSyncService,
        {provide: ScanApiService, useValue: api},
      ],
    });
    service = TestBed.inject(ScanSyncService);
    workflow = TestBed.inject(ScanWorkflowService);
    store = TestBed.inject(ScanLocalStoreService);
    await clearScanCatalogForTest(store);
    await store.clearSession();
    await store.clearSessionCloseRequests();
    for (const entry of await store.listOutboxEntries()) {
      await store.deleteOutboxEntry(entry.clientGestureId);
    }
    for (const entry of await store.listSaleOutboxEntries()) {
      await store.deleteSaleOutboxEntry(entry.clientGestureId);
    }
  });

  it('applies a delta, stores its watermark and removes hidden books', async () => {
    await store.putCatalogBooks([createBook('9782070363735'), createBook('9783140464079')]);

    const result = await service.syncCatalog();

    expect(result.booksReceived).toBe(1);
    expect(await store.getCatalogBook('9782070363735')).not.toBeNull();
    expect(await store.getCatalogBook('9783140464079')).toBeNull();
    expect((await store.getSettings())?.duplicateThreshold).toBe(5);
    expect((await store.getCatalogSyncState())?.watermark)
      .toBe('2026-09-03T08:00:00.000Z');
    expect((await store.getCatalogSyncState())?.nextFair).toEqual(jasmine.objectContaining({
      id: 'fair-1',
      name: 'Bourse de septembre',
    }));
  });

  it('preserves a local kept quantity when a catalog refresh precedes outbox replay', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);

    const delta = createDelta();
    delta.books[0].qtyAvailable = 0;
    api.getCatalogDelta.and.returnValue(of(delta));

    await service.syncCatalog();

    expect((await store.getCatalogBook('9782070363735'))?.qtyAvailable).toBe(1);
    expect((await store.getOutboxEntry(scan.entry.clientGestureId))?.status).toBe('Kept');
  });

  it('opens the remote session and sends decided gestures in order', async () => {
    const first = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(first.entry.clientGestureId, true);
    const localSession = await workflow.getSession();

    const second = await workflow.recordScan(
      '9783140464079',
      new Date('2026-09-03T08:02:00.000Z'),
    );
    await workflow.decide(second.entry.clientGestureId, false);

    const result = await service.flushOutbox();

    expect(api.openSession).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      mode: 'AvailableNow',
      startedAt: localSession?.startedAt,
    }));
    expect(api.scanBook.calls.count()).toBe(2);
    expect(api.scanBook.calls.argsFor(0)[1].kept).toBeTrue();
    expect(api.scanBook.calls.argsFor(1)[1].kept).toBeFalse();
    expect(result.sent).toBe(2);
    expect(result.remaining).toBe(0);
    expect(await store.listOutboxEntries()).toEqual([]);
  });

  it('rebinds offline gestures when the server resumes an existing session', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    api.openSession.and.returnValue(of({...createSessionResponse(), scanSessionId: 'server-session'}));
    api.scanBook.and.returnValue(of({...createScanResponse(), scanSessionId: 'server-session'}));

    await service.flushOutbox();

    expect(api.scanBook).toHaveBeenCalledOnceWith(
      'server-session',
      jasmine.objectContaining({clientGestureId: scan.entry.clientGestureId}),
    );
    expect((await workflow.getSession())?.scanSessionId).toBe('server-session');
    expect(await store.listOutboxEntries()).toEqual([]);
  });

  it('sends cash sales without opening a scan session and reconciles the local stock', async () => {
    await store.putCatalogBooks([createBook('9782070363735')]);
    await workflow.recordCashSales(
      ['9782070363735'],
      new Date('2026-09-03T08:01:00.000Z'),
    );

    expect((await store.getCatalogBook('9782070363735'))?.qtyAvailable).toBe(0);
    expect((await store.getCatalogBook('9782070363735'))?.salesCount).toBe(1);

    const result = await service.flushOutbox();

    expect(api.openSession).not.toHaveBeenCalled();
    expect(api.registerSale).toHaveBeenCalledOnceWith(jasmine.objectContaining({
      isbn: '9782070363735',
      quantity: 1,
    }));
    expect(result.sent).toBe(1);
    expect(result.remaining).toBe(0);
    expect(await store.listSaleOutboxEntries()).toEqual([]);
    expect((await store.getCatalogBook('9782070363735'))?.qtyAvailable).toBe(0);
    expect((await store.getCatalogBook('9782070363735'))?.salesCount).toBe(1);
  });

  it('keeps a cash sale durable after a network failure', async () => {
    await store.putCatalogBooks([createBook('9782070363735')]);
    await workflow.recordCashSales(
      ['9782070363735'],
      new Date('2026-09-03T08:01:00.000Z'),
    );
    api.registerSale.and.returnValue(throwError(() => new Error('network down')));

    const result = await service.flushOutbox();
    const durableSale = (await store.listSaleOutboxEntries())[0];

    expect(result.sent).toBe(0);
    expect(result.remaining).toBe(1);
    expect(durableSale.attemptCount).toBe(1);
    expect(durableSale.lastError).toBe('network down');
  });

  it('does not create a tri session when a caisse-only catalog sync runs', async () => {
    await service.syncCatalog();

    expect(await workflow.getSession()).toBeNull();
  });

  it('continues with later gestures when an earlier network attempt fails', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    const laterScan = await workflow.recordScan(
      '9783140464079',
      new Date('2026-09-03T08:02:00.000Z'),
    );
    await workflow.decide(laterScan.entry.clientGestureId, false);
    api.scanBook.and.returnValues(
      throwError(() => new Error('network down')),
      of({...createScanResponse(), isbn13: '9783140464079'}),
    );

    const result = await service.flushOutbox();
    const durableEntry = await store.getOutboxEntry(scan.entry.clientGestureId);

    expect(api.scanBook).toHaveBeenCalledTimes(2);
    expect(result.sent).toBe(1);
    expect(result.remaining).toBe(1);
    expect(durableEntry?.attemptCount).toBe(1);
    expect(durableEntry?.lastError).toBe('network down');
    expect(await store.getOutboxEntry(laterScan.entry.clientGestureId)).toBeNull();
  });

  it('backs off a transient failure before retrying the same gesture', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-07T08:00:00.000Z'));

    try {
      const scan = await workflow.recordScan(
        '9782070363735',
        new Date('2026-09-03T08:01:00.000Z'),
      );
      await workflow.decide(scan.entry.clientGestureId, true);
      api.scanBook.and.returnValue(throwError(() => new Error('network down')));

      await service.flushOutbox();
      await service.flushOutbox();

      expect(api.scanBook).toHaveBeenCalledOnceWith(
        'session-1',
        jasmine.objectContaining({clientGestureId: scan.entry.clientGestureId}),
      );

      jasmine.clock().tick(60_000);
      api.scanBook.and.returnValue(of(createScanResponse()));
      await service.flushOutbox();

      expect(api.scanBook).toHaveBeenCalledTimes(2);
      expect(await store.getOutboxEntry(scan.entry.clientGestureId)).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('quarantines a repeatedly rejected gesture instead of blocking the queue', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    api.scanBook.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 422,
      error: {title: 'Validation failed'},
    })));

    let result = await service.flushOutbox();
    result = await service.flushOutbox();
    result = await service.flushOutbox();
    result = await service.flushOutbox();
    result = await service.flushOutbox();

    expect(api.scanBook).toHaveBeenCalledTimes(5);
    expect((await store.getOutboxEntry(scan.entry.clientGestureId))?.status).toBe('Quarantined');
    expect(result.remaining).toBe(0);
    expect(result.quarantined).toBe(1);
  });

  it('keeps a gesture retryable when the resumed session responds with conflict', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-07T08:00:00.000Z'));

    try {
      const scan = await workflow.recordScan(
        '9782070363735',
        new Date('2026-09-03T08:01:00.000Z'),
      );
      await workflow.decide(scan.entry.clientGestureId, true);
      api.scanBook.and.returnValue(throwError(() => new HttpErrorResponse({
        status: 409,
        error: {title: 'Session conflict'},
      })));

      let result;
      for (let attempt = 0; attempt < 5; attempt += 1) {
        result = await service.flushOutbox();
        jasmine.clock().tick(15 * 60_000);
      }

      expect(api.scanBook).toHaveBeenCalledTimes(5);
      expect((await store.getOutboxEntry(scan.entry.clientGestureId))?.status).toBe('Kept');
      expect(result?.quarantined).toBe(0);
      expect(result?.remaining).toBe(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('does not retry a quarantined cash sale', async () => {
    const [sale] = await workflow.recordCashSales(
      ['9782070363735'],
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await store.quarantineSaleOutboxEntry(sale.clientGestureId, 'Validation failed');

    const result = await service.flushOutbox();

    expect(api.registerSale).not.toHaveBeenCalled();
    expect(result.remaining).toBe(0);
    expect(result.quarantined).toBe(1);
  });

  it('closes a requested session after its rejected gesture is quarantined', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    await workflow.requestClose('Manual');
    api.scanBook.and.returnValue(throwError(() => new HttpErrorResponse({status: 422})));

    let result = await service.syncAll();
    result = await service.syncAll();
    result = await service.syncAll();
    result = await service.syncAll();
    result = await service.syncAll();

    expect(api.closeSession).toHaveBeenCalledOnceWith(
      'session-1',
      {closeReason: 'Manual'},
    );
    expect(result.closed).toBeTrue();
  });

  it('does not transmit a pending decision', async () => {
    await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );

    const result = await service.flushOutbox();

    expect(api.openSession).not.toHaveBeenCalled();
    expect(api.scanBook).not.toHaveBeenCalled();
    expect(result.remaining).toBe(1);
  });

  it('closes a requested session after transmitting its decided gestures', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    await workflow.requestClose('Manual');

    const result = await service.syncAll();

    expect(api.closeSession).toHaveBeenCalledOnceWith(
      'session-1',
      {closeReason: 'Manual'},
    );
    expect(result.closed).toBeTrue();
    expect(await workflow.getSession()).toBeNull();
  });

  function createDelta(): ScanCatalogDeltaResponse {
    return {
      generatedAt: '2026-09-03T08:00:00.000Z',
      nextWatermark: '2026-09-03T08:00:00.000Z',
      nextFair: {
        id: 'fair-1',
        name: 'Bourse de septembre',
        dateStart: '2026-09-14T09:00:00+02:00',
        dateEnd: '2026-09-14T18:00:00+02:00',
        openAt: '2026-09-14T09:00:00+02:00',
        closeAt: '2026-09-14T18:00:00+02:00',
      },
      books: [
        {
          ...createBook('9782070363735'),
          isHidden: false,
        },
        {
          ...createBook('9783140464079'),
          isHidden: true,
        },
      ],
      settings: createSettings(),
    };
  }

  function createBook(isbn13: string): ScanCatalogBook {
    return {
      isbn13,
      title: 'Titre',
      authors: 'Auteur',
      workId: null,
      qtyAvailable: 1,
      qtyAnnounced: 0,
      salesCount: 0,
      isWanted: false,
      isRare: false,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }

  function createSettings(): ScanAssociationSettings {
    return {
      duplicateThreshold: 5,
      demandSalesThreshold: 1,
      deadStockMinAgeDays: 30,
      deadStockMinQuantity: 1,
      watchlistMaxItems: 100,
      alertCooldownDays: 30,
      sessionIdleTimeoutMinutes: 120,
      alertDelayMinutes: 120,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }

  function createSessionResponse(): ScanSessionResponse {
    return {
      scanSessionId: 'session-1',
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:00:00.000Z',
      lastSyncAt: '2026-09-03T08:00:00.000Z',
      lateArrivals: false,
      endedAt: null,
      closeReason: null,
      status: 'InProgress',
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
    };
  }

  function createClosedSessionResponse(): ScanSessionResponse {
    return {
      ...createSessionResponse(),
      endedAt: '2026-09-03T08:05:00.000Z',
      closeReason: 'Manual',
      status: 'Completed',
      scannedCount: 1,
      keptCount: 1,
      rejectedCount: 0,
    };
  }

  function createScanResponse(): ScanBookResponse {
    return {
      isbn13: '9782070363735',
      verdict: 'FirstCopy',
      qtyAvailable: 1,
      qtyAnnounced: 0,
      scanSessionId: 'session-1',
      movementType: 'DirectEntry',
      alreadyProcessed: false,
      clockSuspect: false,
    };
  }

  function createSaleResponse(): ScanSaleResponse {
    return {
      isbn13: '9782070363735',
      saleMovementId: 'movement-1',
      quantity: 1,
      qtyAvailable: 0,
      salesCount: 1,
      assoEventsId: null,
      fairMatchStatus: 'NoOpenFair',
      hadNoAvailableStock: false,
      hadUnreleasedAnnouncement: false,
      isRare: false,
      clockSuspect: false,
      alreadyProcessed: false,
    };
  }
});
