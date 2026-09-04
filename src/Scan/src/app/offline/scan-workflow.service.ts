import {Injectable} from '@angular/core';

import {BookMetadata} from '../scanner/book-metadata.model';
import {
  LocalScanResult,
  LocalCatalogResult,
  PersistentStorageStatus,
  ScanCatalogBook,
  ScanOutboxEntry,
  ScanSessionSnapshot,
} from './scan-offline.model';
import {ScanLocalStoreService} from './scan-local-store.service';
import {ScanVerdictService} from './scan-verdict.service';

@Injectable({providedIn: 'root'})
export class ScanWorkflowService {
  private operation = Promise.resolve();

  constructor(
    private readonly store: ScanLocalStoreService,
    private readonly verdictService: ScanVerdictService,
  ) {}

  async initialize(): Promise<PersistentStorageStatus> {
    return await this.enqueue(async () => {
      const status = await this.store.requestPersistentStorage();
      await this.ensureSession(new Date());
      return status;
    });
  }

  async getSession(): Promise<ScanSessionSnapshot | null> {
    return await this.store.getSession();
  }

  async getPendingCount(): Promise<number> {
    return await this.store.countPendingOutboxEntries();
  }

  async getLatestPendingEntry(): Promise<ScanOutboxEntry | null> {
    const entries = await this.store.listOutboxEntries();
    return entries.filter(entry => entry.status === 'Pending').at(-1) ?? null;
  }

  async getLatestPendingResult(): Promise<LocalScanResult | null> {
    const entries = await this.store.listOutboxEntries();
    const entry = entries.filter(candidate => candidate.status === 'Pending').at(-1) ?? null;
    if (!entry) {
      return null;
    }

    const catalogBook = await this.store.getCatalogBook(entry.isbn13);
    const verdict = this.verdictService.calculate(catalogBook, await this.store.getSettings());
    const entryIndex = entries.findIndex(candidate => candidate.clientGestureId === entry.clientGestureId);
    const previousEntry = entries
      .slice(0, entryIndex)
      .reverse()
      .find(candidate => candidate.scanSessionId === entry.scanSessionId && candidate.status !== 'CancelledLocal');
    return {
      entry,
      verdict,
      catalogBook,
      isImmediateRepeat: isImmediateRepeat(
        previousEntry,
        entry.isbn13,
        entry.occurredAt,
      ),
    };
  }

  async clearSession(): Promise<void> {
    return await this.enqueue(async () => await this.store.clearSession());
  }

  async lookupCatalog(isbn13: string): Promise<LocalCatalogResult> {
    return await this.enqueue(async () => {
      const catalogBook = await this.store.getCatalogBook(isbn13);
      const verdict = this.verdictService.calculate(catalogBook, await this.store.getSettings());
      return {catalogBook, verdict};
    });
  }

  async setSessionMode(mode: 'AvailableNow' | 'NextFair'): Promise<ScanSessionSnapshot> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(new Date());
      const updated = {...session, mode};
      await this.store.saveSession(updated);
      return updated;
    });
  }

  async recordSync(synchronizedAt = new Date()): Promise<void> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(synchronizedAt);
      await this.store.saveSession({
        ...session,
        lastSyncAt: synchronizedAt.toISOString(),
      });
    });
  }

  async mergeRemoteSession(remoteSession: {
    scanSessionId: string;
    volunteerId: string;
    mode: 'AvailableNow' | 'NextFair';
    targetAssoEventsId: string | null;
    startedAt: string;
    lastScanAt: string;
    lastSyncAt: string;
    scannedCount: number;
    keptCount: number;
    rejectedCount: number;
  }): Promise<void> {
    return await this.enqueue(async () => {
      const current = await this.ensureSession(new Date(remoteSession.startedAt));
      await this.store.saveSession({
        ...current,
        scanSessionId: remoteSession.scanSessionId,
        volunteerId: remoteSession.volunteerId,
        mode: remoteSession.mode,
        targetAssoEventsId: remoteSession.targetAssoEventsId,
        startedAt: remoteSession.startedAt,
        lastScanAt: remoteSession.lastScanAt,
        lastSyncAt: remoteSession.lastSyncAt,
        scannedCount: Math.max(current.scannedCount, remoteSession.scannedCount),
        keptCount: Math.max(current.keptCount, remoteSession.keptCount),
        rejectedCount: Math.max(current.rejectedCount, remoteSession.rejectedCount),
      });
    });
  }

  async recordScan(isbn13: string, occurredAt = new Date()): Promise<LocalScanResult> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(occurredAt);
      const existingEntries = await this.store.listOutboxEntries();
      const previousEntry = existingEntries
        .slice()
        .reverse()
        .find(entry => entry.scanSessionId === session.scanSessionId && entry.status !== 'CancelledLocal');
      const previousPendingEntries = existingEntries
        .filter(entry => entry.status === 'Pending');

      let keptCount = session.keptCount;
      for (const entry of previousPendingEntries) {
        const committed = await this.store.decideOutboxEntry(
          entry.clientGestureId,
          true,
          session.mode,
        );
        if (committed.status === 'Kept' && entry.status === 'Pending') {
          keptCount += 1;
        }
      }

      const catalogBook = await this.store.getCatalogBook(isbn13);
      const settings = await this.store.getSettings();
      const verdict = this.verdictService.calculate(catalogBook, settings);
      const timestamp = occurredAt.toISOString();
      const entry: ScanOutboxEntry = {
        clientGestureId: createClientId(),
        scanSessionId: session.scanSessionId,
        isbn13,
        occurredAt: timestamp,
        createdAt: new Date().toISOString(),
        status: 'Pending',
        kept: null,
        catalogApplied: false,
        verdict: verdict.verdict,
        quantityAvailable: catalogBook?.qtyAvailable ?? 0,
        quantityAnnounced: catalogBook?.qtyAnnounced ?? 0,
        salesCount: catalogBook?.salesCount ?? 0,
        isRare: verdict.isRare,
        attemptCount: 0,
        lastAttemptAt: null,
        lastError: null,
      };
      await this.store.addOutboxEntry(entry);
      await this.store.saveSession({
        ...session,
        lastScanAt: timestamp,
        keptCount,
        scannedCount: session.scannedCount + 1,
      });

      return {
        entry,
        verdict,
        catalogBook,
        isImmediateRepeat: isImmediateRepeat(previousEntry, isbn13, timestamp),
      };
    });
  }

  async decide(clientGestureId: string, kept: boolean): Promise<ScanOutboxEntry> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(new Date());
      const existing = await this.store.getOutboxEntry(clientGestureId);
      if (!existing) {
        throw new Error(`Unknown scan gesture: ${clientGestureId}`);
      }

      const decided = await this.store.decideOutboxEntry(
        clientGestureId,
        kept,
        session.mode,
      );

      if (existing.status === 'Pending') {
        await this.store.saveSession({
          ...session,
          keptCount: kept ? session.keptCount + 1 : session.keptCount,
          rejectedCount: kept ? session.rejectedCount : session.rejectedCount + 1,
        });
      }

      return decided;
    });
  }

  async cancel(clientGestureId: string): Promise<ScanOutboxEntry> {
    return await this.enqueue(async () =>
      await this.store.cancelPendingOutboxEntry(clientGestureId));
  }

  async cacheMetadata(metadata: BookMetadata): Promise<void> {
    return await this.enqueue(async () => {
      const existing = await this.store.getCatalogBook(metadata.isbn13);
      const book: ScanCatalogBook = {
        isbn13: metadata.isbn13,
        title: metadata.title,
        authors: metadata.authors,
        workId: metadata.workId,
        qtyAvailable: existing?.qtyAvailable ?? 0,
        qtyAnnounced: existing?.qtyAnnounced ?? 0,
        salesCount: existing?.salesCount ?? 0,
        isWanted: existing?.isWanted ?? false,
        isRare: existing?.isRare ?? false,
        updatedAt: metadata.retrievedAt,
      };
      await this.store.putCatalogBooks([book]);
    });
  }

  private async ensureSession(now: Date): Promise<ScanSessionSnapshot> {
    const existing = await this.store.getSession();
    if (existing) {
      return {
        ...existing,
        scannedCount: existing.scannedCount ?? 0,
        keptCount: existing.keptCount ?? 0,
        rejectedCount: existing.rejectedCount ?? 0,
      };
    }

    const timestamp = now.toISOString();
    const session: ScanSessionSnapshot = {
      key: 'active-session',
      scanSessionId: createClientId(),
      volunteerId: null,
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: timestamp,
      lastScanAt: timestamp,
      lastSyncAt: timestamp,
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
    };
    await this.store.saveSession(session);
    return session;
  }

  private async enqueue<T>(work: () => Promise<T>): Promise<T> {
    const next = this.operation.then(work, work);
    this.operation = next.then(() => undefined, () => undefined);
    return await next;
  }
}

function isImmediateRepeat(
  previousEntry: ScanOutboxEntry | undefined,
  isbn13: string,
  occurredAt: string,
): boolean {
  if (!previousEntry || previousEntry.isbn13 !== isbn13) {
    return false;
  }

  const previousTimestamp = Date.parse(previousEntry.occurredAt);
  const currentTimestamp = Date.parse(occurredAt);
  return Number.isFinite(previousTimestamp) &&
    Number.isFinite(currentTimestamp) &&
    currentTimestamp >= previousTimestamp &&
    currentTimestamp - previousTimestamp < IMMEDIATE_REPEAT_WINDOW_MS;
}

const IMMEDIATE_REPEAT_WINDOW_MS = 5_000;

function createClientId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  return `scan-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
