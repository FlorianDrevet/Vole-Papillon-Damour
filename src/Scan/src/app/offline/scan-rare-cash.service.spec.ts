import {TestBed} from '@angular/core/testing';
import {of} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanRareBook,
  ScanRareSaleOutboxEntry,
  ScanSessionSnapshot,
} from './scan-offline.model';
import {
  ScanRareCashService,
  ScanRareSaleReference,
} from './scan-rare-cash.service';

describe('ScanRareCashService', () => {
  let service: ScanRareCashService;
  let store: ScanLocalStoreService;
  let api: jasmine.SpyObj<ScanApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', [
      'markRareBookSold',
      'restoreRareBookAvailability',
    ]);

    await TestBed.configureTestingModule({
      providers: [
        ScanRareCashService,
        ScanLocalStoreService,
        {provide: ScanApiService, useValue: api},
      ],
    }).compileComponents();

    service = TestBed.inject(ScanRareCashService);
    store = TestBed.inject(ScanLocalStoreService);
    await store.clearRareBookState();
  });

  it('lists only published available rare books matching the local search', async () => {
    await store.putRareBooks([
      createRareBook(),
      {...createRareBook('rare-sold'), title: 'Vendu', isSold: true},
      {...createRareBook('rare-draft'), title: 'Brouillon', status: 'Draft'},
      {...createRareBook('rare-author'), title: 'Autre titre', authorMention: 'Victor Hugo'},
    ]);

    const results = await service.listAvailable('hugo');

    expect(results.map(book => book.clientId)).toEqual(['rare-author']);
  });

  it('records rare sales with session metadata and never puts a price in the outbox', async () => {
    await store.putRareBooks([createRareBook()]);
    const session = createSession();

    const entries = await service.recordSales(
      ['rare-server-1', 'rare-server-1'],
      new Date('2026-09-03T08:01:00.000Z'),
      session,
    );

    expect(entries).toHaveSize(1);
    expect(entries[0]).toEqual(jasmine.objectContaining({
      rareBookId: 'rare-server-1',
      clientSessionId: 'session-1',
      scanSessionId: 'remote-session-1',
      assoEventsId: 'asso-event-1',
    }));
    expect(entries[0]).not.toEqual(jasmine.objectContaining({price: jasmine.anything()}));
    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeTrue();
  });

  it('attaches the checkout passage to rare cash sale entries', async () => {
    await store.putRareBooks([createRareBook()]);
    const entries = await service.recordSales(
      ['rare-server-1'],
      new Date('2026-09-03T08:01:00.000Z'),
      createSession(),
      'passage-1',
    );

    expect(entries[0].checkoutPassageId).toBe('passage-1');
    expect(entries[0]).not.toEqual(jasmine.objectContaining({price: jasmine.anything()}));
  });

  it('cancels a pending rare sale atomically and restores local availability', async () => {
    await store.putRareBooks([createRareBook()]);
    const [entry] = await service.recordSales(
      ['rare-server-1'],
      new Date('2026-09-03T08:01:00.000Z'),
      createSession(),
    );

    const result = await service.cancelSale(toReference(entry), new Date('2026-09-03T08:01:10.000Z'));

    expect(result).toBe('local');
    expect(await store.getRareSaleOutboxEntry(entry.clientGestureId)).toEqual(
      jasmine.objectContaining({status: 'Cancelled'}),
    );
    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeFalse();
    expect(api.restoreRareBookAvailability).not.toHaveBeenCalled();
  });

  it('restores an already transmitted rare sale through the server correction endpoint', async () => {
    await store.putRareBooks([{...createRareBook(), isSold: true}]);
    api.restoreRareBookAvailability.and.returnValue(of({
      ...createRareBookResponse(),
      isSold: false,
    }));

    const reference: ScanRareSaleReference = {
      clientGestureId: 'rare-sale-sent',
      rareBookId: 'rare-server-1',
      occurredAt: '2026-09-03T08:01:00.000Z',
    };
    const result = await service.cancelSale(reference, new Date('2026-09-03T08:01:10.000Z'));

    expect(result).toBe('remote');
    expect(api.restoreRareBookAvailability).toHaveBeenCalledOnceWith('rare-server-1');
    expect((await store.getRareBook('rare-client-1'))?.isSold).toBeFalse();
  });

  it('does not correct a rare sale after the thirty-second correction window', async () => {
    await store.putRareBooks([createRareBook()]);
    const [entry] = await service.recordSales(
      ['rare-server-1'],
      new Date('2026-09-03T08:01:00.000Z'),
      createSession(),
    );

    const result = await service.cancelSale(toReference(entry), new Date('2026-09-03T08:01:31.000Z'));

    expect(result).toBe('expired');
    expect(await store.getRareSaleOutboxEntry(entry.clientGestureId)).not.toBeNull();
    expect(api.restoreRareBookAvailability).not.toHaveBeenCalled();
  });

  function toReference(entry: ScanRareSaleOutboxEntry): ScanRareSaleReference {
    return {
      clientGestureId: entry.clientGestureId,
      rareBookId: entry.rareBookId,
      occurredAt: entry.occurredAt,
    };
  }

  function createRareBook(clientId = 'rare-client-1'): ScanRareBook {
    return {
      clientId,
      serverId: clientId === 'rare-client-1' ? 'rare-server-1' : clientId,
      clientGestureId: clientId + '-gesture',
      isbn13: clientId === 'rare-author' ? '9780000000002' : '9780000000001',
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

  function createSession(): ScanSessionSnapshot {
    return {
      key: 'active-session',
      clientSessionId: 'session-1',
      remoteSessionId: 'remote-session-1',
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow',
      targetAssoEventsId: 'asso-event-1',
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:00:00.000Z',
      lastSyncAt: '2026-09-03T08:00:00.000Z',
    };
  }
});
