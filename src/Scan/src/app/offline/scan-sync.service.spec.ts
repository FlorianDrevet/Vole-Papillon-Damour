import {HttpErrorResponse} from '@angular/common/http';
import {TestBed} from '@angular/core/testing';
import {of, Subject, throwError} from 'rxjs';

import {ScanApiService, ScanPassageAssociationResponse, ScanRareBookResponse} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanAssociationSettings,
  ScanCatalogBook,
  ScanCatalogRareBook,
  ScanCatalogDeltaResponse,
  ScanBookResponse,
  ScanRareBook,
  ScanPassageAssociationEntry,
  ScanRareSaleOutboxEntry,
  ScanSaleOutboxEntry,
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
      'associatePassage',
      'markRareBookSold',
      'restoreRareBookAvailability',
      'closeSession',
    ]);
    api.getCatalogDelta.and.returnValue(of(createDelta()));
    api.openSession.and.returnValue(of(createSessionResponse()));
    api.scanBook.and.returnValue(of(createScanResponse()));
    api.registerSale.and.returnValue(of(createSaleResponse()));
    api.associatePassage.and.returnValue(of({
      checkoutPassageId: 'passage-1',
      status: 'Associated',
      displayLabel: 'Camille',
      alreadyProcessed: false,
    }));
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
    for (const entry of await store.listPassageAssociations()) {
      await store.deletePassageAssociation(entry.checkoutPassageId);
    }
    await store.clearRareBookState();
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

  it('removes a locally cached rare copy when the delta carries its deletion tombstone', async () => {
    await store.putRareBooks([createRareBook()]);
    api.getCatalogDelta.and.returnValue(of({
      ...createDelta(),
      removedRareBookIds: ['rare-server-1'],
    }));

    await service.syncCatalog();

    expect(await store.getRareBook('rare-client-1')).toBeNull();
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

  it('keeps local session counters when automatic synchronization clears sent gestures', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    const session = await workflow.getSession();

    await service.flushOutbox();

    expect(await workflow.getSessionCounts(session!.clientSessionId)).toEqual({
      scannedCount: 1,
      keptCount: 1,
      rejectedCount: 0,
    });
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
    expect((await workflow.getSession())?.remoteSessionId).toBe('server-session');
    expect(await store.listOutboxEntries()).toEqual([]);
  });

  it('does not silently move gestures when the local session belongs to another account', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    await workflow.bindSessionToVolunteer('account-a');
    (service as unknown as {scanAuth: unknown}).scanAuth = {
      authState: {account: {homeAccountId: 'account-b'}},
      handleServerAuthorizationFailure: jasmine.createSpy(),
    };

    const result = await service.flushOutbox();

    expect(api.openSession).not.toHaveBeenCalled();
    expect((await store.getOutboxEntry(scan.entry.clientGestureId))?.status).toBe('Kept');
    expect(result.orphaned).toBe(0);
    expect(result.newlyOrphaned).toBe(0);
    expect(result.remaining).toBe(1);
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

  it('sends rare cash sales through the rare endpoint without ordinary sale fields', async () => {
    const rareBook = createRareBook();
    const entry = {...createRareSaleOutboxEntry(), checkoutPassageId: 'passage-1'};
    await store.putRareBooks([{...rareBook, isSold: true}]);
    await store.addRareSaleOutboxEntries([entry], []);
    api.markRareBookSold.and.returnValue(of(createRareBookResponse()));

    const result = await service.flushOutbox();

    expect(api.registerSale).not.toHaveBeenCalled();
    expect(api.markRareBookSold).toHaveBeenCalledOnceWith(
      'rare-server-1',
      jasmine.objectContaining({
        occurredAt: entry.occurredAt,
        scanSessionId: 'remote-session-1',
        assoEventsId: null,
        checkoutPassageId: 'passage-1',
      }),
    );
    const request = api.markRareBookSold.calls.mostRecent().args[1] as unknown as {
      price?: unknown;
      quantity?: unknown;
      checkoutPassageId?: unknown;
    };
    expect(request.price).toBeUndefined();
    expect(request.quantity).toBeUndefined();
    expect(request.checkoutPassageId).toBe('passage-1');
    expect(result.sent).toBe(1);
    expect(await store.listRareSaleOutboxEntries()).toEqual([]);
  });

  it('restores a rare sale when cancellation races with the in-flight sale request', async () => {
    const rareBook = createRareBook();
    const entry = createRareSaleOutboxEntry();
    const response$ = new Subject<ScanRareBookResponse>();
    await store.putRareBooks([{...rareBook, isSold: true}]);
    await store.addRareSaleOutboxEntries([entry], []);
    api.markRareBookSold.and.returnValue(response$.asObservable());
    api.restoreRareBookAvailability.and.returnValue(of({
      ...createRareBookResponse(),
      isSold: false,
    }));

    const flushing = service.flushOutbox();
    while (api.markRareBookSold.calls.count() === 0) {
      await new Promise(resolve => setTimeout(resolve, 0));
    }

    await store.restoreRareBookAfterPendingSale(entry, {
      ...rareBook,
      isSold: false,
      updatedAt: '2026-09-03T08:01:10.000Z',
    });
    response$.next(createRareBookResponse());
    response$.complete();
    await flushing;

    expect(api.restoreRareBookAvailability).toHaveBeenCalledOnceWith('rare-server-1');
    expect(await store.listRareSaleOutboxEntries()).toEqual([]);
    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeFalse();
  });

  it('replays a cancelled rare sale through the correction endpoint before clearing it', async () => {
    const rareBook = createRareBook();
    const entry = {...createRareSaleOutboxEntry(), status: 'Cancelled' as const};
    await store.putRareBooks([rareBook]);
    await store.addRareSaleOutboxEntries([entry], []);
    api.restoreRareBookAvailability.and.returnValue(of({
      ...createRareBookResponse(),
      isSold: false,
    }));

    const result = await service.flushOutbox();

    expect(api.markRareBookSold).not.toHaveBeenCalled();
    expect(api.restoreRareBookAvailability).toHaveBeenCalledOnceWith('rare-server-1');
    expect(result.sent).toBe(1);
    expect(await store.listRareSaleOutboxEntries()).toEqual([]);
    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeFalse();
  });

  it('keeps a locally cancelled rare sale available during a catalogue refresh', async () => {
    const entry = {...createRareSaleOutboxEntry(), status: 'Cancelled' as const};
    await store.putRareBooks([createRareBook()]);
    await store.addRareSaleOutboxEntries([entry], []);
    api.getCatalogDelta.and.returnValue(of({
      ...createDelta(),
      rareBooks: [createCatalogRareBook()],
    }));

    await service.syncCatalog();

    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeFalse();
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
    expect((await store.getOutboxEntry(scan.entry.clientGestureId))?.status).toBe('RejectedByServer');
    expect(result.remaining).toBe(0);
    expect(result.quarantined).toBe(1);
    expect(result.newlyQuarantined).toBe(1);
  });

  it('reopens a fresh session before sending when the remote session is already closed', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    const localSession = await workflow.getSession();
    await workflow.requestClose('Manual');
    api.openSession.and.returnValues(
      of({
        ...createSessionResponse(),
        scanSessionId: 'closed-session',
        status: 'Completed',
        endedAt: '2026-09-03T08:05:00.000Z',
        closeReason: 'Inactivity',
      }),
      of({...createSessionResponse(), scanSessionId: 'fresh-session'}),
      of({...createSessionResponse(), scanSessionId: 'fresh-session'}),
    );
    api.scanBook.and.returnValue(of({...createScanResponse(), scanSessionId: 'fresh-session'}));

    const result = await service.syncAll();
    const replacementOpen = api.openSession.calls.argsFor(1)[0];

    expect(api.openSession.calls.argsFor(0)[0].clientSessionId)
      .toBe(localSession!.clientSessionId);
    expect(replacementOpen.clientSessionId).not.toBe(localSession!.clientSessionId);
    expect(api.scanBook).toHaveBeenCalledWith(
      'fresh-session',
      jasmine.objectContaining({clientGestureId: scan.entry.clientGestureId}),
    );
    expect(api.closeSession).toHaveBeenCalledOnceWith(
      'fresh-session',
      {closeReason: 'Manual'},
    );
    expect(result.closed).toBeTrue();
    expect(await store.getOutboxEntry(scan.entry.clientGestureId)).toBeNull();
    expect(await workflow.getSession()).toBeNull();
  });

  it('reopens a fresh session when it closes between opening and sending a decided gesture', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    const localSession = await workflow.getSession();
    await workflow.requestClose('Manual');
    api.openSession.and.returnValues(
      of(createSessionResponse()),
      of({...createSessionResponse(), scanSessionId: 'fresh-session'}),
      of({...createSessionResponse(), scanSessionId: 'fresh-session'}),
    );
    api.scanBook.and.returnValues(
      throwError(() => new HttpErrorResponse({
        status: 409,
        error: {detail: 'Scan session is already closed: session-1'},
      })),
      of({...createScanResponse(), scanSessionId: 'fresh-session'}),
    );

    const result = await service.syncAll();
    const replacementOpen = api.openSession.calls.argsFor(1)[0];

    expect(api.openSession.calls.argsFor(0)[0].clientSessionId)
      .toBe(localSession!.clientSessionId);
    expect(replacementOpen.clientSessionId).not.toBe(localSession!.clientSessionId);
    expect(api.scanBook.calls.argsFor(0)[0]).toBe('session-1');
    expect(api.scanBook.calls.argsFor(1)[0]).toBe('fresh-session');
    expect(result.closed).toBeTrue();
    expect(await store.getOutboxEntry(scan.entry.clientGestureId)).toBeNull();
    expect(await workflow.getSession()).toBeNull();
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

  it('sends sales before the association of the same passage', async () => {
    const callOrder: string[] = [];
    const sale = createSaleOutboxEntry('g1', 'p1');
    await store.addSaleOutboxEntries([sale], []);
    await store.putPassageAssociation(createPassageAssociation('p1'));
    api.registerSale.and.callFake(request => {
      callOrder.push(`registerSale:${request.clientGestureId}`);
      return of(createSaleResponse());
    });
    api.associatePassage.and.callFake((passageId, _request) => {
      callOrder.push(`associatePassage:${passageId}`);
      return of(createPassageAssociationResponse(passageId));
    });

    await service.flushOutbox();

    expect(callOrder).toEqual(['registerSale:g1', 'associatePassage:p1']);
    expect(api.registerSale.calls.mostRecent().args[0].checkoutPassageId).toBe('p1');
  });

  it('removes the association once the server answered Unresolved', async () => {
    api.associatePassage.and.returnValue(of({
      checkoutPassageId: 'p1',
      status: 'Unresolved',
      displayLabel: null,
      alreadyProcessed: false,
    }));
    await store.putPassageAssociation(createPassageAssociation('p1'));

    await service.flushOutbox();

    expect(await store.listPassageAssociations()).toEqual([]);
  });

  it('keeps syncing sales when the association call fails', async () => {
    api.associatePassage.and.returnValue(throwError(() => new HttpErrorResponse({status: 0})));
    await store.addSaleOutboxEntries([createSaleOutboxEntry('g1', 'p1')], []);
    await store.putPassageAssociation(createPassageAssociation('p1'));

    await service.flushOutbox();

    expect(await store.listSaleOutboxEntries()).toEqual([]);
    expect((await store.listPassageAssociations())[0].attemptCount).toBe(1);
  });

  it('quarantines a 409 passage association conflict', async () => {
    api.associatePassage.and.returnValue(throwError(() => new HttpErrorResponse({status: 409})));
    await store.putPassageAssociation(createPassageAssociation('p1'));

    const result = await service.flushOutbox();

    expect((await store.listPassageAssociations())[0].status).toBe('Quarantined');
    expect(await store.countQuarantinedOutboxEntries()).toBe(1);
    expect(result.quarantined).toBe(1);
  });

  it('replays an association with the same passage id after a reconnection', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-07T08:00:00.000Z'));
    api.associatePassage.and.returnValues(
      throwError(() => new HttpErrorResponse({status: 0})),
      of(createPassageAssociationResponse('p1')),
    );
    await store.putPassageAssociation(createPassageAssociation('p1'));

    try {
      await service.flushOutbox();
      jasmine.clock().tick(60_000);
      await service.flushOutbox();

      expect(api.associatePassage.calls.allArgs().map(args => args[0])).toEqual(['p1', 'p1']);
      expect(await store.listPassageAssociations()).toEqual([]);
    } finally {
      jasmine.clock().uninstall();
    }
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
    expect((await store.listOutboxEntries())[0].status).toBe('Pending');
    expect(result.remaining).toBe(0);
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

  it('clears a close request when the server already closed the session', async () => {
    const scan = await workflow.recordScan(
      '9782070363735',
      new Date('2026-09-03T08:01:00.000Z'),
    );
    await workflow.decide(scan.entry.clientGestureId, true);
    await service.flushOutbox();
    await workflow.requestClose('Manual');
    api.openSession.calls.reset();
    api.closeSession.calls.reset();
    api.openSession.and.returnValue(of({
      ...createClosedSessionResponse(),
      closeReason: 'Inactivity',
    }));

    const result = await service.syncAll();

    expect(api.closeSession).not.toHaveBeenCalled();
    expect(await store.listSessionCloseRequests()).toEqual([]);
    expect(await workflow.getSession()).toBeNull();
    expect(result.closed).toBeTrue();
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
      rareBooks: [],
      removedRareBookIds: [],
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
      reusedExistingSession: false,
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

  function createRareBook(): ScanRareBook {
    return {
      clientId: 'rare-client-1',
      serverId: 'rare-server-1',
      clientGestureId: 'rare-create-1',
      isbn13: '9780000000001',
      title: 'Livre rare',
      authorMention: 'Auteur',
      publisher: 'Éditeur',
      publicationYear: 1900,
      price: 60,
      condition: 'Très bon état',
      publicDescription: null,
      status: 'Published',
      isSold: false,
      thumbnail: null,
      updatedAt: '2026-09-03T08:00:00.000Z',
      rowVersion: 'AAAA',
      syncStatus: 'Synced',
      lastError: null,
    };
  }

  function createRareBookResponse() {
    return {
      id: 'rare-server-1',
      slug: 'livre-rare',
      isbn13: '9780000000001',
      title: 'Livre rare',
      authorMention: 'Auteur',
      publisher: 'Éditeur',
      publicationYear: 1900,
      price: 60,
      condition: 'Très bon état',
      publicDescription: null,
      status: 'Published' as const,
      isSold: true,
      soldAt: '2026-09-03T08:01:00.000Z',
      soldAtFairId: null,
      soldInSessionId: null,
      createdAt: '2026-09-03T08:00:00.000Z',
      createdBy: 'volunteer-1',
      updatedAt: '2026-09-03T08:01:00.000Z',
      updatedBy: 'volunteer-1',
      rowVersion: 'BBBB',
      photos: [],
    };
  }

  function createCatalogRareBook(): ScanCatalogRareBook {
    return {
      id: 'rare-server-1',
      isbn13: '9780000000001',
      title: 'Livre rare',
      authorMention: 'Auteur',
      price: 60,
      condition: 'GoodWithFlaws',
      shortDescription: null,
      thumbnail: null,
      isAvailable: true,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }

  function createRareSaleOutboxEntry(): ScanRareSaleOutboxEntry {
    return {
      clientGestureId: 'rare-sale-1',
      clientSessionId: 'session-1',
      rareBookId: 'rare-server-1',
      scanSessionId: 'remote-session-1',
      assoEventsId: null,
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.000Z',
      status: 'Pending',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }

  function createSaleOutboxEntry(
    clientGestureId: string,
    checkoutPassageId: string,
  ): ScanSaleOutboxEntry {
    return {
      clientGestureId,
      isbn13: '9782070363735',
      quantity: 1,
      checkoutPassageId,
      status: 'Pending',
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.100Z',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }

  function createPassageAssociation(checkoutPassageId: string): ScanPassageAssociationEntry {
    return {
      checkoutPassageId,
      credential: {kind: 'qr', value: 'VPDC1.AAAA.BBBB'},
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.100Z',
      status: 'Pending',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }

  function createPassageAssociationResponse(
    checkoutPassageId: string,
  ): ScanPassageAssociationResponse {
    return {
      checkoutPassageId,
      status: 'Associated',
      displayLabel: 'Camille',
      alreadyProcessed: false,
    };
  }
});
