import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {
  ScanApiService,
  ScanRareBookCreateRequest,
  ScanRareBookResponse,
} from '../offline/scan-api.service';
import {ScanLocalStoreService} from '../offline/scan-local-store.service';
import {createScanClientId} from '../offline/scan-client-id';
import {
  ScanRareBook,
  ScanRareBookDraftInput,
  ScanRareBookPhotoQueueEntry,
} from '../offline/scan-offline.model';

export interface ScanRareBookSyncResult {
  created: number;
  updated: number;
  uploadedPhotos: number;
  failed: number;
}

@Injectable({providedIn: 'root'})
export class ScanRareBookService {
  private operation = Promise.resolve();

  constructor(
    private readonly store: ScanLocalStoreService,
    private readonly api: ScanApiService,
  ) {}

  async createDraft(input: ScanRareBookDraftInput): Promise<ScanRareBook> {
    return await this.enqueue(async () => {
      const clientId = createScanClientId();
      const timestamp = new Date().toISOString();
      const draft: ScanRareBook = {
        clientId,
        serverId: null,
        clientGestureId: clientId,
        isbn13: input.isbn13 ?? null,
        title: input.title.trim(),
        authorMention: input.authorMention ?? null,
        publisher: input.publisher ?? null,
        publicationYear: input.publicationYear ?? null,
        price: input.price,
        condition: input.condition,
        publicDescription: input.publicDescription ?? null,
        status: 'Draft',
        isSold: false,
        thumbnail: null,
        updatedAt: timestamp,
        rowVersion: null,
        syncStatus: 'PendingCreate',
        lastError: null,
      };
      await this.store.putRareBooks([draft]);
      return draft;
    });
  }

  async get(clientId: string): Promise<ScanRareBook | null> {
    return await this.store.getRareBook(clientId);
  }

  async list(): Promise<ScanRareBook[]> {
    return await this.store.listRareBooks();
  }

  async refreshFromServer(): Promise<ScanRareBook[]> {
    return await this.enqueue(async () => {
      const localBooks = await this.store.listRareBooks();
      const response = await firstValueFrom(this.api.getRareBooksAdmin());
      const books = response.books.map(serverBook => {
        const existing = localBooks.find(book => book.serverId === serverBook.id);
        return fromServerResponse(serverBook, existing?.clientId ?? serverBook.id);
      });
      await this.store.putRareBooks(books);
      return await this.store.listRareBooks();
    });
  }

  async listAvailable(): Promise<ScanRareBook[]> {
    return (await this.list()).filter(book =>
      book.status === 'Published' && !book.isSold);
  }

  async saveDraft(clientId: string, input: ScanRareBookDraftInput): Promise<ScanRareBook> {
    return await this.enqueue(async () => {
      const existing = await this.store.getRareBook(clientId);
      if (!existing) {
        throw new Error(`Fiche rare inconnue : ${clientId}`);
      }

      const updated: ScanRareBook = {
        ...existing,
        isbn13: input.isbn13 ?? null,
        title: input.title.trim(),
        authorMention: input.authorMention ?? null,
        publisher: input.publisher ?? null,
        publicationYear: input.publicationYear ?? null,
        price: input.price,
        condition: input.condition,
        publicDescription: input.publicDescription ?? null,
        updatedAt: new Date().toISOString(),
        syncStatus: existing.serverId ? 'PendingUpdate' : 'PendingCreate',
        lastError: null,
      };
      await this.store.putRareBooks([updated]);
      return updated;
    });
  }

  async queuePhoto(
    clientId: string,
    source: Blob,
    caption: string | null,
    position: number,
  ): Promise<ScanRareBookPhotoQueueEntry> {
    return await this.enqueue(async () => {
      const book = await this.store.getRareBook(clientId);
      if (!book) {
        throw new Error(`Fiche rare inconnue : ${clientId}`);
      }
      if (position < 0 || !Number.isInteger(position)) {
        throw new Error('La position de la photo est invalide.');
      }
      const sourceType = getSupportedPhotoType(source.type);
      if (!sourceType) {
        throw new Error('La photo doit être au format JPEG, PNG ou WebP.');
      }

      const compressed = await compressRarePhoto(source);
      const contentType = getSupportedPhotoType(compressed.type) ?? sourceType;
      await ensureStorageQuota(compressed.size);
      const entry: ScanRareBookPhotoQueueEntry = {
        queueId: createScanClientId(),
        rareBookClientId: clientId,
        rareBookServerId: book.serverId,
        blob: compressed,
        fileName: `rare-${clientId}-${position}${photoExtension(contentType)}`,
        contentType,
        caption: caption?.trim() || null,
        position,
        createdAt: new Date().toISOString(),
        attemptCount: 0,
        lastAttemptAt: null,
        lastError: null,
      };
      await this.store.addRareBookPhotoQueueEntry(entry);
      return entry;
    });
  }

  async pendingPhotoCount(): Promise<number> {
    return await this.store.countRareBookPhotoQueue();
  }

  async queuedPhotos(clientId: string): Promise<ScanRareBookPhotoQueueEntry[]> {
    return (await this.store.listRareBookPhotoQueue())
      .filter(photo => photo.rareBookClientId === clientId);
  }

  async deleteQueuedPhoto(queueId: string): Promise<void> {
    await this.enqueue(() => this.store.deleteRareBookPhotoQueueEntry(queueId));
  }

  async syncPending(): Promise<ScanRareBookSyncResult> {
    return await this.enqueue(async () => {
      const result: ScanRareBookSyncResult = {
        created: 0,
        updated: 0,
        uploadedPhotos: 0,
        failed: 0,
      };
      const books = await this.store.listRareBooks();

      for (const book of books) {
        if (book.syncStatus !== 'PendingCreate' && book.syncStatus !== 'PendingUpdate' &&
            book.syncStatus !== 'Failed') {
          continue;
        }

        if (book.syncStatus === 'Failed' && book.serverId && book.lastError?.startsWith('photo:')) {
          continue;
        }

        try {
          if (!book.serverId) {
            const response = await firstValueFrom(this.api.createRareBook(toCreateRequest(book)));
            await this.store.putRareBooks([fromServerResponse(response, book.clientId)]);
            result.created += 1;
          } else if (book.syncStatus === 'PendingUpdate' || book.syncStatus === 'Failed') {
            const response = await firstValueFrom(this.api.updateRareBook(
              book.serverId,
              {...toCreateRequest(book), rowVersion: book.rowVersion ?? ''},
            ));
            await this.store.putRareBooks([fromServerResponse(response, book.clientId)]);
            result.updated += 1;
          }
        } catch (error: unknown) {
          await this.store.putRareBooks([{
            ...book,
            syncStatus: 'Failed',
            lastError: describeError(error),
          }]);
          result.failed += 1;
        }
      }

      const queue = await this.store.listRareBookPhotoQueue();
      for (const photo of queue) {
        if (photo.attemptCount >= MAX_PHOTO_ATTEMPTS) {
          continue;
        }

        const book = await this.store.getRareBook(photo.rareBookClientId);
        if (!book?.serverId) {
          continue;
        }

        try {
          const response = await firstValueFrom(this.api.addRareBookPhoto(
            book.serverId,
            photo.blob,
            photo.caption,
            photo.fileName,
          ));
          await this.store.deleteRareBookPhotoQueueEntry(photo.queueId);
          await this.store.putRareBooks([{
            ...fromServerResponse(response, book.clientId),
            syncStatus: 'Synced',
            lastError: null,
          }]);
          result.uploadedPhotos += 1;
        } catch (error: unknown) {
          await this.store.putRareBookPhotoQueueEntry({
            ...photo,
            rareBookServerId: book.serverId,
            attemptCount: photo.attemptCount + 1,
            lastAttemptAt: new Date().toISOString(),
            lastError: describeError(error),
          });
          await this.store.putRareBooks([{
            ...book,
            syncStatus: 'Failed',
            lastError: `photo: ${describeError(error)}`,
          }]);
          result.failed += 1;
        }
      }

      return result;
    });
  }

  private async enqueue<T>(work: () => Promise<T>): Promise<T> {
    const next = this.operation.then(work, work);
    this.operation = next.then(() => undefined, () => undefined);
    return await next;
  }
}

const MAX_PHOTO_ATTEMPTS = 5;

export async function compressRarePhoto(source: Blob): Promise<Blob> {
  const imageBitmapFactory = (globalThis as typeof globalThis & {
    createImageBitmap?: (image: Blob) => Promise<ImageBitmap>;
  }).createImageBitmap;
  const OffscreenCanvasConstructor = (globalThis as typeof globalThis & {
    OffscreenCanvas?: new (width: number, height: number) => OffscreenCanvas;
  }).OffscreenCanvas;

  if (!imageBitmapFactory || !OffscreenCanvasConstructor) {
    return source;
  }

  let bitmap: ImageBitmap | null = null;
  try {
    bitmap = await imageBitmapFactory(source);
    const scale = Math.min(1, 2000 / Math.max(bitmap.width, bitmap.height));
    const canvas = new OffscreenCanvasConstructor(
      Math.max(1, Math.round(bitmap.width * scale)),
      Math.max(1, Math.round(bitmap.height * scale)),
    );
    const context = canvas.getContext('2d');
    if (!context) {
      return source;
    }
    context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    return await canvas.convertToBlob({type: 'image/jpeg', quality: 0.82});
  } catch {
    return source;
  } finally {
    bitmap?.close();
  }
}

type RarePhotoContentType = 'image/jpeg' | 'image/webp' | 'image/png';

function getSupportedPhotoType(value: string): RarePhotoContentType | null {
  const normalized = value.trim().toLowerCase();
  return normalized === 'image/jpeg' || normalized === 'image/webp' || normalized === 'image/png'
    ? normalized
    : null;
}

function photoExtension(contentType: RarePhotoContentType): string {
  return contentType === 'image/jpeg'
    ? '.jpg'
    : contentType === 'image/webp'
      ? '.webp'
      : '.png';
}

async function ensureStorageQuota(incomingBytes: number): Promise<void> {
  if (typeof navigator === 'undefined' || !navigator.storage?.estimate) {
    return;
  }

  const estimate = await navigator.storage.estimate();
  if (estimate.quota && (estimate.usage ?? 0) + incomingBytes > estimate.quota * 0.8) {
    throw new Error('Le stockage local est presque plein (80 %). Libérez de la place avant d’ajouter cette photo.');
  }
}

function toCreateRequest(book: ScanRareBook): ScanRareBookCreateRequest {
  return {
    clientGestureId: book.clientGestureId,
    isbn13: book.isbn13,
    title: book.title,
    authorMention: book.authorMention,
    publisher: book.publisher,
    publicationYear: book.publicationYear,
    price: book.price,
    condition: book.condition,
    publicDescription: book.publicDescription,
  };
}

function fromServerResponse(response: ScanRareBookResponse, clientId: string): ScanRareBook {
  return {
    clientId,
    serverId: response.id,
    clientGestureId: clientId,
    isbn13: response.isbn13,
    title: response.title,
    authorMention: response.authorMention,
    publisher: response.publisher,
    publicationYear: response.publicationYear,
    price: response.price,
    condition: response.condition,
    publicDescription: response.publicDescription,
    status: response.status,
    isSold: response.isSold,
    thumbnail: response.photos?.find(photo => photo.position === 0)?.blobUri ?? null,
    updatedAt: response.updatedAt,
    rowVersion: response.rowVersion,
    syncStatus: 'Synced',
    lastError: null,
  };
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : 'La synchronisation a échoué.';
}
