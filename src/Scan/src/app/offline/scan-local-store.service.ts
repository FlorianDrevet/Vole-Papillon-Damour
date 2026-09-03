import {Injectable} from '@angular/core';

import {
  PersistentStorageStatus,
  ScanAssociationSettings,
  ScanAssociationSettingsRecord,
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanOutboxEntry,
  ScanOutboxStatus,
  ScanSessionSnapshot,
  ScanStoreName,
  scanDatabaseName,
  scanDatabaseVersion,
  scanStoreNames,
} from './scan-offline.model';

@Injectable({providedIn: 'root'})
export class ScanLocalStoreService {
  private readonly databasePromise = this.openDatabase();

  async requestPersistentStorage(): Promise<PersistentStorageStatus> {
    const available = typeof indexedDB !== 'undefined';
    if (!available) {
      return {available: false, persisted: false, requestAttempted: false};
    }

    await this.databasePromise;

    if (typeof navigator === 'undefined' || !navigator.storage) {
      return {available: true, persisted: false, requestAttempted: false};
    }

    try {
      let persisted = navigator.storage.persisted
        ? await navigator.storage.persisted()
        : false;
      let requestAttempted = false;

      if (!persisted && navigator.storage.persist) {
        requestAttempted = true;
        persisted = await navigator.storage.persist();
      }

      return {available: true, persisted, requestAttempted};
    } catch {
      return {available: true, persisted: false, requestAttempted: true};
    }
  }

  async getCatalogBook(isbn13: string): Promise<ScanCatalogBook | null> {
    const book = await this.runRequest<ScanCatalogBook | undefined>(
      scanStoreNames.catalog,
      'readonly',
      store => store.get(isbn13),
    );
    return book ?? null;
  }

  async getCatalogBooks(): Promise<ScanCatalogBook[]> {
    return await this.runRequest<ScanCatalogBook[]>(
      scanStoreNames.catalog,
      'readonly',
      store => store.getAll(),
    ) ?? [];
  }

  async putCatalogBooks(books: readonly ScanCatalogBook[]): Promise<void> {
    if (books.length === 0) {
      return;
    }

    await this.runTransaction(
      [scanStoreNames.catalog],
      'readwrite',
      stores => {
        for (const book of books) {
          stores[scanStoreNames.catalog].put(book);
        }
      },
    );
  }

  /** Purges only the replaceable catalog projection. The outbox is never touched. */
  async clearCatalog(): Promise<void> {
    await this.runRequest(
      scanStoreNames.catalog,
      'readwrite',
      store => store.clear(),
    );
  }

  async getSettings(): Promise<ScanAssociationSettings | null> {
    const record = await this.runRequest<ScanAssociationSettingsRecord | undefined>(
      scanStoreNames.session,
      'readonly',
      store => store.get('association-settings'),
    );

    if (!record) {
      return null;
    }

    const {key: _key, ...settings} = record;
    return settings;
  }

  async saveSettings(settings: ScanAssociationSettings): Promise<void> {
    const record: ScanAssociationSettingsRecord = {
      key: 'association-settings',
      ...settings,
    };
    await this.putSessionRecord(record);
  }

  async getCatalogSyncState(): Promise<ScanCatalogSyncState | null> {
    return await this.runRequest<ScanCatalogSyncState | undefined>(
      scanStoreNames.session,
      'readonly',
      store => store.get('catalog-sync'),
    ) ?? null;
  }

  async saveCatalogSyncState(state: ScanCatalogSyncState): Promise<void> {
    await this.putSessionRecord(state);
  }

  async applyCatalogDelta(
    books: readonly ScanCatalogBook[],
    settings: ScanAssociationSettings,
    syncState: ScanCatalogSyncState,
    removedIsbn13s: readonly string[] = [],
  ): Promise<void> {
    await this.runTransaction(
      [scanStoreNames.catalog, scanStoreNames.session],
      'readwrite',
      stores => {
        for (const book of books) {
          stores[scanStoreNames.catalog].put(book);
        }
        for (const isbn13 of removedIsbn13s) {
          stores[scanStoreNames.catalog].delete(isbn13);
        }

        stores[scanStoreNames.session].put({
          key: 'association-settings',
          ...settings,
        } satisfies ScanAssociationSettingsRecord);
        stores[scanStoreNames.session].put(syncState);
      },
    );
  }

  async getSession(): Promise<ScanSessionSnapshot | null> {
    return await this.runRequest<ScanSessionSnapshot | undefined>(
      scanStoreNames.session,
      'readonly',
      store => store.get('active-session'),
    ) ?? null;
  }

  async saveSession(session: ScanSessionSnapshot): Promise<void> {
    await this.putSessionRecord(session);
  }

  async clearSession(): Promise<void> {
    await this.runRequest(
      scanStoreNames.session,
      'readwrite',
      store => store.delete('active-session'),
    );
  }

  async addOutboxEntry(entry: ScanOutboxEntry): Promise<void> {
    await this.runRequest(
      scanStoreNames.outbox,
      'readwrite',
      store => store.add(entry),
    );
  }

  async getOutboxEntry(clientGestureId: string): Promise<ScanOutboxEntry | null> {
    return await this.runRequest<ScanOutboxEntry | undefined>(
      scanStoreNames.outbox,
      'readonly',
      store => store.get(clientGestureId),
    ) ?? null;
  }

  async listOutboxEntries(): Promise<ScanOutboxEntry[]> {
    const entries = await this.runRequest<ScanOutboxEntry[]>(
      scanStoreNames.outbox,
      'readonly',
      store => store.getAll(),
    ) ?? [];

    return entries.sort((left, right) =>
      left.createdAt.localeCompare(right.createdAt) ||
      left.clientGestureId.localeCompare(right.clientGestureId));
  }

  async listTransmittableOutboxEntries(): Promise<ScanOutboxEntry[]> {
    const entries = await this.listOutboxEntries();
    return entries.filter(entry => entry.status === 'Kept' || entry.status === 'Rejected');
  }

  async countPendingOutboxEntries(): Promise<number> {
    const entries = await this.listOutboxEntries();
    return entries.filter(entry => entry.status !== 'CancelledLocal').length;
  }

  async updateOutboxStatus(
    clientGestureId: string,
    status: ScanOutboxStatus,
  ): Promise<ScanOutboxEntry> {
    const entry = await this.getOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown scan gesture: ${clientGestureId}`);
    }

    const updated: ScanOutboxEntry = {
      ...entry,
      status,
      kept: status === 'Kept' ? true : status === 'Rejected' ? false : null,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async decideOutboxEntry(
    clientGestureId: string,
    kept: boolean,
    mode: 'AvailableNow' | 'NextFair',
  ): Promise<ScanOutboxEntry> {
    const database = await this.databasePromise;

    return new Promise((resolve, reject) => {
      const transaction = database.transaction(
        [scanStoreNames.outbox, scanStoreNames.catalog],
        'readwrite',
      );
      const outbox = transaction.objectStore(scanStoreNames.outbox);
      const catalog = transaction.objectStore(scanStoreNames.catalog);
      let updatedEntry: ScanOutboxEntry | null = null;
      let settled = false;

      const fail = (error: unknown): void => {
        if (!settled) {
          settled = true;
          reject(error);
        }
      };

      const entryRequest = outbox.get(clientGestureId);
      entryRequest.onsuccess = () => {
        const entry = entryRequest.result as ScanOutboxEntry | undefined;
        if (!entry) {
          fail(new Error(`Unknown scan gesture: ${clientGestureId}`));
          return;
        }

        if (entry.status !== 'Pending') {
          updatedEntry = entry;
          return;
        }

        updatedEntry = {
          ...entry,
          status: kept ? 'Kept' : 'Rejected',
          kept,
          catalogApplied: entry.catalogApplied,
        };

        if (!kept || entry.catalogApplied) {
          outbox.put(updatedEntry);
          return;
        }

        const bookRequest = catalog.get(entry.isbn13);
        bookRequest.onsuccess = () => {
          const currentBook = bookRequest.result as ScanCatalogBook | undefined;
          const baseBook: ScanCatalogBook = currentBook ?? {
            isbn13: entry.isbn13,
            title: null,
            authors: null,
            workId: null,
            qtyAvailable: 0,
            qtyAnnounced: 0,
            salesCount: 0,
            isWanted: false,
            isRare: false,
            updatedAt: entry.occurredAt,
          };
          const nextBook: ScanCatalogBook = {
            ...baseBook,
            qtyAvailable: mode === 'AvailableNow'
              ? baseBook.qtyAvailable + 1
              : baseBook.qtyAvailable,
            qtyAnnounced: mode === 'NextFair'
              ? baseBook.qtyAnnounced + 1
              : baseBook.qtyAnnounced,
            updatedAt: entry.occurredAt,
          };
          updatedEntry = {...updatedEntry!, catalogApplied: true};
          catalog.put(nextBook);
          outbox.put(updatedEntry);
        };
        bookRequest.onerror = () => fail(
          bookRequest.error ?? new Error('Unable to read the local catalog.'),
        );
      };
      entryRequest.onerror = () => fail(
        entryRequest.error ?? new Error('Unable to read the local outbox.'),
      );
      transaction.oncomplete = () => {
        if (!settled && updatedEntry) {
          settled = true;
          resolve(updatedEntry);
        }
      };
      transaction.onerror = () => fail(
        transaction.error ?? new Error('IndexedDB transaction failed.'),
      );
      transaction.onabort = () => fail(
        transaction.error ?? new Error('IndexedDB transaction aborted.'),
      );
    });
  }

  async cancelPendingOutboxEntry(clientGestureId: string): Promise<ScanOutboxEntry> {
    const entry = await this.getOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown scan gesture: ${clientGestureId}`);
    }

    if (entry.status !== 'Pending') {
      return entry;
    }

    return await this.updateOutboxStatus(clientGestureId, 'CancelledLocal');
  }

  async markOutboxAttempt(
    clientGestureId: string,
    attemptedAt: string,
    errorMessage: string | null,
  ): Promise<ScanOutboxEntry> {
    const entry = await this.getOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown scan gesture: ${clientGestureId}`);
    }

    const updated: ScanOutboxEntry = {
      ...entry,
      attemptCount: entry.attemptCount + 1,
      lastAttemptAt: attemptedAt,
      lastError: errorMessage,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async deleteOutboxEntry(clientGestureId: string): Promise<void> {
    await this.runRequest(
      scanStoreNames.outbox,
      'readwrite',
      store => store.delete(clientGestureId),
    );
  }

  private async putSessionRecord(record: object & {key: string}): Promise<void> {
    await this.runRequest(
      scanStoreNames.session,
      'readwrite',
      store => store.put(record),
    );
  }

  private async putOutboxEntry(entry: ScanOutboxEntry): Promise<void> {
    await this.runRequest(
      scanStoreNames.outbox,
      'readwrite',
      store => store.put(entry),
    );
  }

  private openDatabase(): Promise<IDBDatabase> {
    if (typeof indexedDB === 'undefined') {
      return Promise.reject(new Error('IndexedDB is not available in this browser.'));
    }

    return new Promise((resolve, reject) => {
      const request = indexedDB.open(scanDatabaseName, scanDatabaseVersion);

      request.onupgradeneeded = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains(scanStoreNames.catalog)) {
          database.createObjectStore(scanStoreNames.catalog, {keyPath: 'isbn13'});
        }
        if (!database.objectStoreNames.contains(scanStoreNames.outbox)) {
          database.createObjectStore(scanStoreNames.outbox, {keyPath: 'clientGestureId'});
        }
        if (!database.objectStoreNames.contains(scanStoreNames.session)) {
          database.createObjectStore(scanStoreNames.session, {keyPath: 'key'});
        }
      };

      request.onsuccess = () => {
        const database = request.result;
        database.onversionchange = () => database.close();
        resolve(database);
      };
      request.onerror = () => reject(request.error ?? new Error('Unable to open IndexedDB.'));
      request.onblocked = () => reject(new Error('IndexedDB upgrade is blocked by another tab.'));
    });
  }

  private async runRequest<T = undefined>(
    storeName: ScanStoreName,
    mode: IDBTransactionMode,
    operation: (store: IDBObjectStore) => IDBRequest<T>,
  ): Promise<T | undefined> {
    const database = await this.databasePromise;

    return new Promise((resolve, reject) => {
      const transaction = database.transaction(storeName, mode);
      const store = transaction.objectStore(storeName);
      let request: IDBRequest<T>;
      let result: T | undefined;
      let settled = false;

      const fail = (error: unknown): void => {
        if (!settled) {
          settled = true;
          reject(error);
        }
      };

      try {
        request = operation(store);
      } catch (error: unknown) {
        fail(error);
        return;
      }

      request.onsuccess = () => {
        result = request.result;
      };
      request.onerror = () => fail(request.error ?? new Error('IndexedDB request failed.'));
      transaction.oncomplete = () => {
        if (!settled) {
          settled = true;
          resolve(result);
        }
      };
      transaction.onerror = () => fail(
        transaction.error ?? new Error('IndexedDB transaction failed.'),
      );
      transaction.onabort = () => fail(
        transaction.error ?? new Error('IndexedDB transaction aborted.'),
      );
    });
  }

  private async runTransaction(
    storeNamesToUse: readonly ScanStoreName[],
    mode: IDBTransactionMode,
    operation: (stores: Record<ScanStoreName, IDBObjectStore>) => void,
  ): Promise<void> {
    const database = await this.databasePromise;

    return new Promise((resolve, reject) => {
      const transaction = database.transaction([...storeNamesToUse], mode);
      const stores = {} as Record<ScanStoreName, IDBObjectStore>;
      for (const storeName of storeNamesToUse) {
        stores[storeName] = transaction.objectStore(storeName);
      }

      let settled = false;
      const fail = (error: unknown): void => {
        if (!settled) {
          settled = true;
          reject(error);
        }
      };

      try {
        operation(stores);
      } catch (error: unknown) {
        fail(error);
        return;
      }

      transaction.oncomplete = () => {
        if (!settled) {
          settled = true;
          resolve();
        }
      };
      transaction.onerror = () => fail(
        transaction.error ?? new Error('IndexedDB transaction failed.'),
      );
      transaction.onabort = () => fail(
        transaction.error ?? new Error('IndexedDB transaction aborted.'),
      );
    });
  }
}
