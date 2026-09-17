import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {
  ScanApiService,
  ScanRareBookResponse,
} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {createScanClientId} from './scan-client-id';
import {
  ScanRareBook,
  ScanRareSaleOutboxEntry,
  ScanSessionSnapshot,
} from './scan-offline.model';

export interface ScanRareSaleReference {
  clientGestureId: string;
  rareBookId: string;
  occurredAt: string;
}

export type ScanRareSaleCancellationResult = 'local' | 'remote' | 'expired' | 'missing';

@Injectable({providedIn: 'root'})
export class ScanRareCashService {
  private operation = Promise.resolve();

  constructor(
    private readonly store: ScanLocalStoreService,
    private readonly api: ScanApiService,
  ) {}

  async listAvailable(search = ''): Promise<ScanRareBook[]> {
    return await this.enqueue(async () => {
      const query = search.trim().toLocaleLowerCase('fr-FR');
      const books = (await this.store.listRareBooks())
        .filter(book => book.status === 'Published' && !book.isSold && book.serverId)
        .filter(book => !query || [
          book.title,
          book.authorMention ?? '',
          book.isbn13 ?? '',
        ].some(value => value.toLocaleLowerCase('fr-FR').includes(query)))
        .sort((left, right) =>
          left.title.localeCompare(right.title, 'fr', {sensitivity: 'base'}) ||
          left.clientId.localeCompare(right.clientId));
      return books;
    });
  }

  async findAvailableByIsbn(isbn13: string): Promise<ScanRareBook | null> {
    return await this.enqueue(async () => {
      const book = await this.findByIsbnInternal(isbn13);
      return book?.status === 'Published' && !book.isSold && book.serverId !== null
        ? book
        : null;
    });
  }

  async findByIsbn(isbn13: string): Promise<ScanRareBook | null> {
    return await this.enqueue(() => this.findByIsbnInternal(isbn13));
  }

  async recordSales(
    rareBookIds: readonly string[],
    occurredAt = new Date(),
    session: Pick<ScanSessionSnapshot, 'clientSessionId' | 'remoteSessionId' | 'targetAssoEventsId'> | null = null,
  ): Promise<ScanRareSaleOutboxEntry[]> {
    return await this.enqueue(async () => {
      const ids = [...new Set(rareBookIds.filter(id => id.trim().length > 0))];
      if (ids.length === 0) {
        return [];
      }

      const timestamp = occurredAt.toISOString();
      const books = await this.store.listRareBooks();
      const byServerId = new Map(
        books.filter(book => book.serverId).map(book => [book.serverId as string, book]),
      );
      const entries: ScanRareSaleOutboxEntry[] = [];
      const soldBooks: ScanRareBook[] = [];

      for (const rareBookId of ids) {
        const book = byServerId.get(rareBookId);
        if (!book || !book.serverId) {
          throw new Error('Ce livre rare n’est plus disponible dans le catalogue local.');
        }
        if (book.status !== 'Published' || book.isSold) {
          throw new Error(`Le livre rare « ${book.title} » n’est plus disponible.`);
        }

        entries.push({
          clientGestureId: createScanClientId(),
          clientSessionId: session?.clientSessionId ?? null,
          rareBookId: book.serverId,
          scanSessionId: session?.remoteSessionId ?? null,
          assoEventsId: session?.targetAssoEventsId ?? null,
          occurredAt: timestamp,
          createdAt: new Date().toISOString(),
          status: 'Pending',
          attemptCount: 0,
          lastAttemptAt: null,
          lastError: null,
        });
        const sold = {
          ...book,
          isSold: true,
          updatedAt: timestamp,
        };
        byServerId.set(book.serverId, sold);
        soldBooks.push(sold);
      }

      await this.store.addRareSaleOutboxEntries(entries, soldBooks);
      return entries;
    });
  }

  async cancelSale(
    reference: ScanRareSaleReference,
    now = new Date(),
  ): Promise<ScanRareSaleCancellationResult> {
    return await this.enqueue(async () => {
      if (!isWithinCorrectionWindow(reference.occurredAt, now)) {
        return 'expired';
      }

      const localBook = await this.store.getRareBookByServerId(reference.rareBookId);
      const pending = await this.store.getRareSaleOutboxEntry(reference.clientGestureId);
      if (pending) {
        if (!localBook) {
          return 'missing';
        }

        await this.store.restoreRareBookAfterPendingSale(pending, {
          ...localBook,
          isSold: false,
          updatedAt: now.toISOString(),
        });
        return 'local';
      }

      const response = await firstValueFrom(
        this.api.restoreRareBookAvailability(reference.rareBookId),
      );
      if (localBook) {
        await this.store.putRareBooks([toLocalRareBook(response, localBook)]);
      }
      return 'remote';
    });
  }

  private async enqueue<T>(work: () => Promise<T>): Promise<T> {
    const next = this.operation.then(work, work);
    this.operation = next.then(() => undefined, () => undefined);
    return await next;
  }

  private async findByIsbnInternal(isbn13: string): Promise<ScanRareBook | null> {
    const normalized = isbn13.trim();
    return (await this.store.listRareBooks()).find(book => book.isbn13 === normalized) ?? null;
  }
}

const RARE_SALE_CORRECTION_WINDOW_MS = 30_000;

function isWithinCorrectionWindow(occurredAt: string, now: Date): boolean {
  const occurred = Date.parse(occurredAt);
  const current = now.getTime();
  return !Number.isNaN(occurred) &&
    current >= occurred &&
    current - occurred <= RARE_SALE_CORRECTION_WINDOW_MS;
}

function toLocalRareBook(
  response: ScanRareBookResponse,
  existing: ScanRareBook,
): ScanRareBook {
  return {
    ...existing,
    serverId: response.id,
    isbn13: response.isbn13,
    title: response.title,
    authorMention: response.authorMention,
    publisher: response.publisher,
    publicationYear: response.publicationYear,
    shelf: response.shelf,
    price: response.price,
    condition: response.condition,
    publicDescription: response.publicDescription,
    binding: response.binding,
    dimensions: response.dimensions,
    pageCount: response.pageCount,
    shelfLocation: response.shelfLocation,
    priceSetBy: response.priceSetBy,
    status: response.status,
    isSold: response.isSold,
    thumbnail: response.photos?.find(photo => photo.position === 0)?.blobUri ?? existing.thumbnail,
    updatedAt: response.updatedAt,
    rowVersion: response.rowVersion,
    syncStatus: 'Synced',
    lastError: null,
  };
}
