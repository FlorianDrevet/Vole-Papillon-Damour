import {TestBed} from '@angular/core/testing';
import {of, throwError} from 'rxjs';

import {ScanApiService, ScanRareBookResponse} from '../offline/scan-api.service';
import {ScanLocalStoreService} from '../offline/scan-local-store.service';
import {clearScanCatalogForTest} from '../offline/scan-test.utils';
import {ScanRareBookService} from './scan-rare-book.service';

describe('ScanRareBookService', () => {
  let service: ScanRareBookService;
  let store: ScanLocalStoreService;
  let api: jasmine.SpyObj<ScanApiService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', [
      'createRareBook',
      'updateRareBook',
      'addRareBookPhoto',
    ]);
    TestBed.configureTestingModule({
      providers: [
        ScanLocalStoreService,
        ScanRareBookService,
        {provide: ScanApiService, useValue: api},
      ],
    });
    store = TestBed.inject(ScanLocalStoreService);
    service = TestBed.inject(ScanRareBookService);
    await clearScanCatalogForTest(store);
    await store.clearRareBookState();
  });

  it('creates an offline draft and queues its Blob against the client identifier', async () => {
    const draft = await service.createDraft({
      title: 'Les Fables',
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
    });
    const photo = new Blob(['jpeg-bytes'], {type: 'image/jpeg'});

    const queued = await service.queuePhoto(draft.clientId, photo, 'Couverture', 0);

    expect(draft.serverId).toBeNull();
    expect(draft.status).toBe('Draft');
    expect(queued.rareBookClientId).toBe(draft.clientId);
    expect(queued.blob).toBeInstanceOf(Blob);
    expect((await store.listRareBookPhotoQueue())).toHaveSize(1);
    expect((await store.listRareBookPhotoQueue())[0].blob).toBeInstanceOf(Blob);
  });

  it('keeps the client gesture compatible with the Guid API when randomUUID is unavailable', async () => {
    const originalRandomUUID = crypto.randomUUID;
    Object.defineProperty(crypto, 'randomUUID', {configurable: true, value: undefined});

    try {
      const draft = await service.createDraft({
        title: 'Les Fables',
        shelf: 'Éditions anciennes',
        price: 60,
        condition: 'GoodWithFlaws',
      });

      expect(draft.clientGestureId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
      );
    } finally {
      Object.defineProperty(crypto, 'randomUUID', {
        configurable: true,
        value: originalRandomUUID,
      });
    }
  });

  it('creates the server fiche once, then uploads queued photos sequentially with its id', async () => {
    const draft = await service.createDraft({
      title: 'Les Fables',
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
    });
    await service.queuePhoto(draft.clientId, new Blob(['one'], {type: 'image/jpeg'}), 'Face', 0);
    await service.queuePhoto(draft.clientId, new Blob(['two'], {type: 'image/jpeg'}), 'Dos', 1);
    api.createRareBook.and.returnValue(of(createServerBook(draft.clientId)));
    api.addRareBookPhoto.and.callFake((id, file, caption) =>
      of({...createServerBook(draft.clientId), id, updatedAt: '2026-09-17T10:00:00.000Z',
        photos: [{id: `photo-${id}-${caption}`, blobUri: 'https://storage.test/photo.jpg', blobName: 'photo.jpg',
          caption: caption ?? null, position: caption === 'Face' ? 0 : 1, contentType: 'image/jpeg', sizeBytes: file.size,
          uploadedAt: '2026-09-17T10:00:00.000Z', uploadedBy: 'volunteer-1'}]}));

    const result = await service.syncPending();

    expect(api.createRareBook).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({clientGestureId: draft.clientId}),
    );
    expect(api.addRareBookPhoto.calls.allArgs().map(args => args[0])).toEqual([
      'server-rare-1',
      'server-rare-1',
    ]);
    expect(api.addRareBookPhoto.calls.allArgs().map(args => args[2])).toEqual(['Face', 'Dos']);
    expect(result.uploadedPhotos).toBe(2);
    expect((await store.getRareBook(draft.clientId))?.serverId).toBe('server-rare-1');
    expect(await store.listRareBookPhotoQueue()).toEqual([]);
  });

  it('keeps the queue after a failed upload and retries it on the next synchronization', async () => {
    const draft = await service.createDraft({
      title: 'Les Fables',
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
    });
    await service.queuePhoto(draft.clientId, new Blob(['one'], {type: 'image/jpeg'}), 'Face', 0);
    api.createRareBook.and.returnValue(of(createServerBook(draft.clientId)));
    api.addRareBookPhoto.and.returnValues(
      throwError(() => new Error('network down')),
      of({...createServerBook(draft.clientId), updatedAt: '2026-09-17T10:00:00.000Z',
        photos: [{id: 'photo-1', blobUri: 'https://storage.test/photo.jpg', blobName: 'photo.jpg', caption: 'Face',
          position: 0, contentType: 'image/jpeg', sizeBytes: 3, uploadedAt: '2026-09-17T10:00:00.000Z',
          uploadedBy: 'volunteer-1'}]}),
    );

    const first = await service.syncPending();
    const second = await service.syncPending();

    expect(first.failed).toBe(1);
    expect(second.uploadedPhotos).toBe(1);
    expect(await store.listRareBookPhotoQueue()).toEqual([]);
    expect(api.createRareBook).toHaveBeenCalledOnceWith(
      jasmine.objectContaining({clientGestureId: draft.clientId}),
    );
  });

  it('refuses a photo before IndexedDB when storage is already above eighty percent', async () => {
    const draft = await service.createDraft({
      title: 'Les Fables',
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
    });
    spyOn(navigator.storage, 'estimate').and.resolveTo({usage: 81, quota: 100});

    await expectAsync(service.queuePhoto(
      draft.clientId,
      new Blob(['jpeg-bytes'], {type: 'image/jpeg'}),
      null,
      0,
    )).toBeRejectedWithError(/stockage local/i);
    expect(await store.listRareBookPhotoQueue()).toEqual([]);
  });

  it('keeps the supported source type when the browser cannot transcode it offline', async () => {
    const draft = await service.createDraft({
      title: 'Les Fables',
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
    });
    const originalCreateImageBitmap = (globalThis as typeof globalThis & {
      createImageBitmap?: unknown;
    }).createImageBitmap;
    const originalOffscreenCanvas = (globalThis as typeof globalThis & {
      OffscreenCanvas?: unknown;
    }).OffscreenCanvas;
    Object.defineProperty(globalThis, 'createImageBitmap', {configurable: true, value: undefined});
    Object.defineProperty(globalThis, 'OffscreenCanvas', {configurable: true, value: undefined});

    try {
      const queued = await service.queuePhoto(
        draft.clientId,
        new Blob(['png-bytes'], {type: 'image/png'}),
        null,
        0,
      );

      expect(queued.contentType).toBe('image/png');
      expect(queued.fileName).toMatch(/\.png$/);
      expect(queued.blob.type).toBe('image/png');
    } finally {
      Object.defineProperty(globalThis, 'createImageBitmap', {
        configurable: true,
        value: originalCreateImageBitmap,
      });
      Object.defineProperty(globalThis, 'OffscreenCanvas', {
        configurable: true,
        value: originalOffscreenCanvas,
      });
    }
  });

  function createServerBook(_clientGestureId: string): ScanRareBookResponse {
    return {
      id: 'server-rare-1',
      slug: 'les-fables',
      isbn13: null,
      title: 'Les Fables',
      authorMention: null,
      publisher: null,
      publicationYear: null,
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
      publicDescription: null,
      binding: null,
      dimensions: null,
      pageCount: null,
      shelfLocation: null,
      priceSetBy: null,
      status: 'Draft',
      isSold: false,
      soldAt: null,
      soldAtFairId: null,
      soldInSessionId: null,
      createdAt: '2026-09-17T10:00:00.000Z',
      createdBy: 'volunteer-1',
      updatedAt: '2026-09-17T10:00:00.000Z',
      updatedBy: 'volunteer-1',
      rowVersion: 'AQ==',
      photos: [],
    };
  }
});
