import {Injectable} from '@angular/core';

import {
  PersistentStorageStatus,
  ScanAssociationSettings,
  ScanAssociationSettingsRecord,
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanFailureKind,
  ScanLocalStoreError,
  ScanOutboxEntry,
  ScanOutboxStatus,
  ScanSaleOutboxEntry,
  ScanSessionCloseRequest,
  ScanSessionSnapshot,
  ScanStoreName,
  scanDatabaseName,
  scanDatabaseVersion,
  scanStoreNames,
} from './scan-offline.model';

@Injectable({providedIn: 'root'})
export class ScanLocalStoreService {
  private databasePromise: Promise<IDBDatabase> | null = null;
  private database: IDBDatabase | null = null;

  async requestPersistentStorage(): Promise<PersistentStorageStatus> {
    const available = typeof indexedDB !== 'undefined';
    if (!available) {
      return {available: false, persisted: false, requestAttempted: false};
    }

    await this.getDatabase();

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

  async saveSessionCloseRequest(
    request: Omit<ScanSessionCloseRequest, 'key'>,
  ): Promise<void> {
    await this.putSessionRecord({
      key: sessionCloseRequestKey(request.scanSessionId),
      ...request,
    });
  }

  async listSessionCloseRequests(): Promise<ScanSessionCloseRequest[]> {
    const records = await this.runRequest<Array<{key: string} & Partial<ScanSessionCloseRequest>>>(
      scanStoreNames.session,
      'readonly',
      store => store.getAll(),
    ) ?? [];

    return records
      .filter(record => record.key.startsWith('close-request:'))
      .map(record => record as ScanSessionCloseRequest)
      .sort((left, right) => left.requestedAt.localeCompare(right.requestedAt));
  }

  async deleteSessionCloseRequest(scanSessionId: string): Promise<void> {
    await this.runRequest(
      scanStoreNames.session,
      'readwrite',
      store => store.delete(sessionCloseRequestKey(scanSessionId)),
    );
  }

  async clearSessionCloseRequests(): Promise<void> {
    const requests = await this.listSessionCloseRequests();
    for (const request of requests) {
      await this.deleteSessionCloseRequest(request.scanSessionId);
    }
  }

  async addOutboxEntry(entry: ScanOutboxEntry): Promise<void> {
    await this.runRequest(
      scanStoreNames.outbox,
      'readwrite',
      store => store.add(entry),
    );
  }

  async addSaleOutboxEntries(
    entries: readonly ScanSaleOutboxEntry[],
    books: readonly ScanCatalogBook[],
  ): Promise<void> {
    if (entries.length === 0) {
      return;
    }

    await this.runTransaction(
      [scanStoreNames.sales, scanStoreNames.catalog],
      'readwrite',
      stores => {
        for (const entry of entries) {
          stores[scanStoreNames.sales].add(entry);
        }
        for (const book of books) {
          stores[scanStoreNames.catalog].put(book);
        }
      },
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

  async listSaleOutboxEntries(): Promise<ScanSaleOutboxEntry[]> {
    const entries = await this.runRequest<ScanSaleOutboxEntry[]>(
      scanStoreNames.sales,
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
    const [entries, sales] = await Promise.all([
      this.listOutboxEntries(),
      this.listSaleOutboxEntries(),
    ]);
    return entries.filter(entry => entry.status !== 'CancelledLocal').length + sales.length;
  }

  async countBlockingOutboxEntries(): Promise<number> {
    const [entries, sales] = await Promise.all([
      this.listOutboxEntries(),
      this.listSaleOutboxEntries(),
    ]);
    return entries.filter(entry => isBlockingScanStatus(entry.status)).length +
      sales.filter(entry => (entry.status ?? 'Pending') !== 'Quarantined').length;
  }

  async countBlockingOutboxEntriesForSession(scanSessionId: string): Promise<number> {
    const entries = await this.listOutboxEntries();
    return entries.filter(entry =>
      entry.scanSessionId === scanSessionId && isBlockingScanStatus(entry.status)).length;
  }

  async countQuarantinedOutboxEntries(): Promise<number> {
    const [entries, sales] = await Promise.all([
      this.listOutboxEntries(),
      this.listSaleOutboxEntries(),
    ]);
    return entries.filter(entry => entry.status === 'Quarantined').length +
      sales.filter(entry => entry.status === 'Quarantined').length;
  }

  async orphanPendingOutboxEntriesFromOtherSessions(scanSessionId: string): Promise<number> {
    const entries = await this.listOutboxEntries();
    const orphanedEntries = entries.filter(entry =>
      entry.status === 'Pending' && entry.scanSessionId !== scanSessionId);

    for (const entry of orphanedEntries) {
      await this.putOutboxEntry({
        ...entry,
        status: 'Orphaned',
        lastError: 'Geste sans décision provenant d’une session précédente.',
      });
    }

    return orphanedEntries.length;
  }

  async orphanOutboxEntry(clientGestureId: string, errorMessage: string): Promise<ScanOutboxEntry> {
    const entry = await this.getOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown scan gesture: ${clientGestureId}`);
    }

    const updated = {
      ...entry,
      status: 'Orphaned' as const,
      lastError: errorMessage,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async countOrphanedOutboxEntries(): Promise<number> {
    const entries = await this.listOutboxEntries();
    return entries.filter(entry => entry.status === 'Orphaned').length;
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
      kept: status === 'Kept' ? true : status === 'Rejected' ? false : entry.kept,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async quarantineOutboxEntry(
    clientGestureId: string,
    errorMessage: string,
  ): Promise<ScanOutboxEntry> {
    const entry = await this.getOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown scan gesture: ${clientGestureId}`);
    }

    const updated = {
      ...entry,
      status: 'Quarantined' as const,
      lastError: errorMessage,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async decideOutboxEntry(
    clientGestureId: string,
    kept: boolean,
    mode: 'AvailableNow' | 'NextFair',
  ): Promise<ScanOutboxEntry> {
    const database = await this.getDatabase();

    return new Promise((resolve, reject) => {
      let transaction: IDBTransaction;
      try {
        transaction = database.transaction(
          [scanStoreNames.outbox, scanStoreNames.catalog],
          'readwrite',
        );
      } catch (error: unknown) {
        reject(this.handleClosedDatabase(database, error));
        return;
      }
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
    failureKind: ScanFailureKind | null = null,
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
      lastFailureKind: failureKind,
    };
    await this.putOutboxEntry(updated);
    return updated;
  }

  async markSaleAttempt(
    clientGestureId: string,
    attemptedAt: string,
    errorMessage: string | null,
    failureKind: ScanFailureKind | null = null,
  ): Promise<ScanSaleOutboxEntry> {
    const entry = await this.getSaleOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown sale gesture: ${clientGestureId}`);
    }

    const updated: ScanSaleOutboxEntry = {
      ...entry,
      attemptCount: entry.attemptCount + 1,
      lastAttemptAt: attemptedAt,
      lastError: errorMessage,
      lastFailureKind: failureKind,
    };
    await this.putSaleOutboxEntry(updated);
    return updated;
  }

  async quarantineSaleOutboxEntry(
    clientGestureId: string,
    errorMessage: string,
  ): Promise<ScanSaleOutboxEntry> {
    const entry = await this.getSaleOutboxEntry(clientGestureId);
    if (!entry) {
      throw new Error(`Unknown sale gesture: ${clientGestureId}`);
    }

    const updated: ScanSaleOutboxEntry = {
      ...entry,
      status: 'Quarantined',
      lastError: errorMessage,
    };
    await this.putSaleOutboxEntry(updated);
    return updated;
  }

  async getSaleOutboxEntry(clientGestureId: string): Promise<ScanSaleOutboxEntry | null> {
    return await this.runRequest<ScanSaleOutboxEntry | undefined>(
      scanStoreNames.sales,
      'readonly',
      store => store.get(clientGestureId),
    ) ?? null;
  }

  async deleteOutboxEntry(clientGestureId: string): Promise<void> {
    await this.runRequest(
      scanStoreNames.outbox,
      'readwrite',
      store => store.delete(clientGestureId),
    );
  }

  async deleteSaleOutboxEntry(clientGestureId: string): Promise<void> {
    await this.runRequest(
      scanStoreNames.sales,
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

  private async putSaleOutboxEntry(entry: ScanSaleOutboxEntry): Promise<void> {
    await this.runRequest(
      scanStoreNames.sales,
      'readwrite',
      store => store.put(entry),
    );
  }

  private getDatabase(): Promise<IDBDatabase> {
    if (this.databasePromise) {
      return this.databasePromise;
    }

    const opening = this.openDatabase();
    this.databasePromise = opening;
    void opening.catch(() => {
      if (this.databasePromise === opening) {
        this.databasePromise = null;
      }
    });
    return opening;
  }

  private openDatabase(): Promise<IDBDatabase> {
    if (typeof indexedDB === 'undefined') {
      return Promise.reject(new ScanLocalStoreError(
        'unavailable',
        'Le stockage local est indisponible dans ce navigateur.',
      ));
    }

    return new Promise((resolve, reject) => {
      const request = indexedDB.open(scanDatabaseName, scanDatabaseVersion);
      let settled = false;

      const fail = (error: unknown): void => {
        if (settled) {
          return;
        }

        settled = true;
        reject(error instanceof ScanLocalStoreError
          ? error
          : new ScanLocalStoreError(
            'unavailable',
            'Le stockage local est indisponible dans ce navigateur.',
          ));
      };

      request.onupgradeneeded = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains(scanStoreNames.catalog)) {
          database.createObjectStore(scanStoreNames.catalog, {keyPath: 'isbn13'});
        }
        if (!database.objectStoreNames.contains(scanStoreNames.outbox)) {
          database.createObjectStore(scanStoreNames.outbox, {keyPath: 'clientGestureId'});
        }
        if (!database.objectStoreNames.contains(scanStoreNames.sales)) {
          database.createObjectStore(scanStoreNames.sales, {keyPath: 'clientGestureId'});
        }
        if (!database.objectStoreNames.contains(scanStoreNames.session)) {
          database.createObjectStore(scanStoreNames.session, {keyPath: 'key'});
        }
      };

      request.onsuccess = () => {
        if (settled) {
          request.result.close();
          return;
        }

        const database = request.result;
        this.database = database;
        database.onversionchange = () => {
          if (this.database === database) {
            this.database = null;
            this.databasePromise = null;
          }
          database.close();
        };
        settled = true;
        resolve(database);
      };
      request.onerror = () => fail(request.error);
      request.onblocked = () => fail(new ScanLocalStoreError(
        'blocked-by-other-instance',
        'La base locale est bloquée par une autre instance. Fermez l’autre onglet puis réessayez.',
      ));
    });
  }

  private async runRequest<T = undefined>(
    storeName: ScanStoreName,
    mode: IDBTransactionMode,
    operation: (store: IDBObjectStore) => IDBRequest<T>,
  ): Promise<T | undefined> {
    const database = await this.getDatabase();

    return new Promise((resolve, reject) => {
      let transaction: IDBTransaction;
      try {
        transaction = database.transaction(storeName, mode);
      } catch (error: unknown) {
        reject(this.handleClosedDatabase(database, error));
        return;
      }
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
    const database = await this.getDatabase();

    return new Promise((resolve, reject) => {
      let transaction: IDBTransaction;
      try {
        transaction = database.transaction([...storeNamesToUse], mode);
      } catch (error: unknown) {
        reject(this.handleClosedDatabase(database, error));
        return;
      }
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

  private handleClosedDatabase(database: IDBDatabase, error: unknown): ScanLocalStoreError {
    if (this.database === database) {
      this.database = null;
      this.databasePromise = null;
    }

    try {
      database.close();
    } catch {
      // The connection was already closing or closed.
    }

    const errorName = typeof error === 'object' && error !== null && 'name' in error
      ? (error as {name?: unknown}).name
      : null;
    return errorName === 'InvalidStateError'
      ? new ScanLocalStoreError(
        'closed-by-other-instance',
        'La base locale a été fermée par une autre instance. Fermez l’autre onglet puis réessayez.',
      )
      : new ScanLocalStoreError(
        'unavailable',
        'Le stockage local est indisponible dans ce navigateur.',
      );
  }
}

function sessionCloseRequestKey(scanSessionId: string): string {
  return `close-request:${scanSessionId}`;
}

function isBlockingScanStatus(status: ScanOutboxEntry['status']): boolean {
  return status === 'Pending' || status === 'Kept' || status === 'Rejected';
}
