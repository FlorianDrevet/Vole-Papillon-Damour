import {Injectable} from '@angular/core';

import {BookMetadata} from '../scanner/book-metadata.model';
import {
  LocalScanResult,
  LocalCatalogResult,
  PersistentStorageStatus,
  ScanAssociationSettings,
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanOtherEdition,
  ScanOutboxEntry,
  ScanSaleOutboxEntry,
  ScanSessionCounts,
  ScanSessionHistoryItem,
  LocalScanCloseReason,
  ScanSessionSnapshot,
  ScanSessionClosePendingError,
  ScanSaleCancellationSummary,
} from './scan-offline.model';
import {ScanLocalStoreService} from './scan-local-store.service';
import {ScanVerdictService} from './scan-verdict.service';
import {createScanClientId} from './scan-client-id';
import {ScanTelemetryService} from './scan-telemetry.service';

@Injectable({providedIn: 'root'})
export class ScanWorkflowService {
  private operation = Promise.resolve();

  constructor(
    private readonly store: ScanLocalStoreService,
    private readonly verdictService: ScanVerdictService,
    private readonly telemetry: ScanTelemetryService,
  ) {}

  async initialize(): Promise<PersistentStorageStatus> {
    return await this.enqueue(async () => {
      const status = await this.store.requestPersistentStorage();
      return status;
    });
  }

  async requestPersistentStorage(): Promise<PersistentStorageStatus> {
    return await this.enqueue(() => this.store.requestPersistentStorage());
  }

  async getSession(): Promise<ScanSessionSnapshot | null> {
    return await this.store.getSession();
  }

  async getOutboxCounts(clientSessionId: string): Promise<{
    pendingDecisionCount: number;
    pendingTransmissionCount: number;
  }> {
    return await this.store.getOutboxCounts(clientSessionId);
  }

  async getSessionCounts(clientSessionId: string): Promise<ScanSessionCounts> {
    return await this.store.getSessionCounts(clientSessionId);
  }

  async getSessionScanHistory(clientSessionId: string): Promise<ScanSessionHistoryItem[]> {
    return await this.enqueue(async () => {
      const entries = (await this.store.listOutboxEntries()).filter(entry =>
        entry.clientSessionId === clientSessionId &&
        (entry.status === 'Kept' || entry.status === 'Rejected' || entry.status === 'Synced') &&
        !entry.removalRequested);

      return await Promise.all(entries.map(async entry => ({
        entry,
        book: await this.store.getCatalogBook(entry.isbn13),
      })));
    });
  }

  async requestSessionScanRemoval(clientGestureId: string): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.requestOutboxRemoval(clientGestureId));
  }

  async markSessionScanSynced(
    clientGestureId: string,
    remoteScanSessionId: string,
  ): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.markOutboxSynced(clientGestureId, remoteScanSessionId));
  }

  async completeSessionScanRemoval(clientGestureId: string): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.completeOutboxRemoval(clientGestureId));
  }

  async rejectSessionScanRemoval(
    clientGestureId: string,
    errorMessage: string,
  ): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.rejectOutboxRemoval(clientGestureId, errorMessage));
  }

  async deleteSessionScanHistory(clientSessionId: string): Promise<void> {
    return await this.enqueue(() => this.store.deleteSessionScanHistory(clientSessionId));
  }

  async getSetAsideCounts(): Promise<{
    orphaned: number;
    quarantined: number;
  }> {
    const [orphaned, quarantined] = await Promise.all([
      this.store.countOrphanedOutboxEntries(),
      this.store.countQuarantinedOutboxEntries(),
    ]);
    return {orphaned, quarantined};
  }

  async listSetAsideOutboxEntries(): Promise<ScanOutboxEntry[]> {
    return await this.enqueue(() => this.store.listSetAsideOutboxEntries());
  }

  async getCatalogSyncState(): Promise<ScanCatalogSyncState | null> {
    return await this.store.getCatalogSyncState();
  }

  async getSettings(): Promise<ScanAssociationSettings | null> {
    return await this.store.getSettings();
  }

  async getLatestPendingResult(): Promise<LocalScanResult | null> {
    const session = await this.store.getSession();
    if (!session) {
      return null;
    }

    const entries = await this.store.listOutboxEntries();
    const entry = entries
      .filter(candidate =>
        candidate.status === 'Pending' && candidate.clientSessionId === session.clientSessionId)
      .at(-1) ?? null;
    if (!entry) {
      return null;
    }

    const catalogBook = await this.store.getCatalogBook(entry.isbn13);
    const [settings, catalog] = await Promise.all([
      this.store.getSettings(),
      this.store.getCatalogBooks(),
    ]);
    const verdict = this.verdictService.calculate(catalogBook, settings, catalog);
    const entryIndex = entries.findIndex(candidate => candidate.clientGestureId === entry.clientGestureId);
    const previousEntry = entries
      .slice(0, entryIndex)
      .reverse()
      .find(candidate =>
        candidate.clientSessionId === entry.clientSessionId &&
        candidate.status !== 'CancelledLocal');
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

  async lookupCatalog(isbn13: string): Promise<LocalCatalogResult> {
    const queuedAt = performance.now();
    return await this.enqueue(async () => {
      const startedAt = performance.now();
      const queueWaitMs = startedAt - queuedAt;
      let catalogBookReadMs = 0;
      let settingsReadMs = 0;
      let catalogReadMs = 0;
      let verdictMs = 0;
      let catalogSize = 'unknown';
      let bookFound = 'unknown';
      let result = 'failed';

      try {
        const bookReadStartedAt = performance.now();
        const catalogBook = await this.store.getCatalogBook(isbn13);
        catalogBookReadMs = performance.now() - bookReadStartedAt;
        bookFound = String(catalogBook !== null);

        const [settings, catalog] = await Promise.all([
          measurePromise(() => this.store.getSettings(), duration => { settingsReadMs = duration; }),
          measurePromise(() => this.store.getCatalogBooks(), duration => { catalogReadMs = duration; }),
        ]);
        catalogSize = String(catalog.length);

        const verdictStartedAt = performance.now();
        const verdict = this.verdictService.calculate(catalogBook, settings, catalog);
        verdictMs = performance.now() - verdictStartedAt;
        result = 'completed';
        return {catalogBook, verdict};
      } finally {
        try {
          this.telemetry.trackEvent('scan_catalog_lookup', {
            result,
            bookFound,
            catalogSize,
            queueWaitMs: String(Math.round(queueWaitMs)),
            catalogBookReadMs: String(Math.round(catalogBookReadMs)),
            settingsReadMs: String(Math.round(settingsReadMs)),
            catalogReadMs: String(Math.round(catalogReadMs)),
            verdictMs: String(Math.round(verdictMs)),
            totalMs: String(Math.round(performance.now() - queuedAt)),
          });
        } catch {
          // Telemetry must not prevent an offline catalog lookup.
        }
      }
    });
  }

  async getCatalogBook(isbn13: string): Promise<ScanCatalogBook | null> {
    return await this.store.getCatalogBook(isbn13);
  }

  async getSiblingEditions(book: ScanCatalogBook | BookMetadata): Promise<ScanOtherEdition[]> {
    return this.verdictService.siblings(book, await this.store.getCatalogBooks())
      .filter(edition => edition.quantity > 0);
  }

  async deleteOutboxEntry(clientGestureId: string): Promise<void> {
    return await this.enqueue(() => this.store.deleteOutboxEntry(clientGestureId));
  }

  async retryOutboxEntry(clientGestureId: string): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.retryOutboxEntry(clientGestureId));
  }

  async reattachOutboxEntry(
    clientGestureId: string,
    clientSessionId: string,
  ): Promise<ScanOutboxEntry> {
    return await this.enqueue(() => this.store.reattachOutboxEntry(clientGestureId, clientSessionId));
  }

  async reattachOutboxEntryToCurrentSession(clientGestureId: string): Promise<ScanOutboxEntry> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session) {
        throw new Error('Aucune session locale active pour reprendre ce livre.');
      }
      return await this.store.reattachOutboxEntry(clientGestureId, session.clientSessionId);
    });
  }

  async reattachNeedsReattachToCurrentSession(): Promise<number> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session) {
        return 0;
      }

      const entries = await this.store.listSetAsideOutboxEntries();
      const pending = entries.filter(entry => entry.status === 'NeedsReattach');
      for (const entry of pending) {
        await this.store.reattachOutboxEntry(entry.clientGestureId, session.clientSessionId);
      }
      return pending.length;
    });
  }

  async recordCashSales(
    isbns13: readonly string[],
    occurredAt = new Date(),
    checkoutPassageId?: string | null,
  ): Promise<ScanSaleOutboxEntry[]> {
    return await this.enqueue(async () => {
      if (isbns13.length === 0) {
        return [];
      }

      const timestamp = occurredAt.toISOString();
      const booksByIsbn = new Map<string, ScanCatalogBook>();
      const entries: ScanSaleOutboxEntry[] = [];

      for (const isbn13 of isbns13) {
        const current = booksByIsbn.get(isbn13) ??
          await this.store.getCatalogBook(isbn13) ??
          createEmptyCatalogBook(isbn13, timestamp);
        const updated = {
          ...current,
          qtyAvailable: Math.max(0, current.qtyAvailable - 1),
          salesCount: current.salesCount + 1,
          updatedAt: timestamp,
        };
        booksByIsbn.set(isbn13, updated);
        entries.push({
          clientGestureId: createClientId(),
          isbn13,
          quantity: 1,
          voidClientGestureId: createScanClientId(),
          ...(checkoutPassageId ? {checkoutPassageId} : {}),
          status: 'Pending',
          occurredAt: timestamp,
          createdAt: new Date().toISOString(),
          attemptCount: 0,
          lastAttemptAt: null,
          lastError: null,
        });
      }

      await this.store.addSaleOutboxEntries(entries, [...booksByIsbn.values()]);
      return entries;
    });
  }

  async deleteSaleOutboxEntry(clientGestureId: string): Promise<boolean> {
    return await this.enqueue(async () => {
      const entry = await this.store.getSaleOutboxEntry(clientGestureId);
      if (!entry || (entry.status ?? 'Pending') !== 'Pending') {
        return false;
      }

      const cancellation = await this.store.cancelSaleOutboxEntries([clientGestureId]);
      return cancellation.localCancelledCount > 0;
    });
  }

  async cancelSaleOutboxEntries(
    clientGestureIds: readonly string[],
    now = new Date(),
  ): Promise<ScanSaleCancellationSummary & {expiredCount: number}> {
    return await this.enqueue(async () => {
      const entries = await Promise.all(
        clientGestureIds.map(clientGestureId => this.store.getSaleOutboxEntry(clientGestureId)),
      );
      let expiredCount = 0;
      const cancellableIds = entries.flatMap(entry => {
        if (!entry) {
          return [];
        }
        if (!isWithinSaleCorrectionWindow(entry.occurredAt, now)) {
          expiredCount += 1;
          return [];
        }
        return [entry.clientGestureId];
      });
      const result = await this.store.cancelSaleOutboxEntries(cancellableIds);
      return {...result, expiredCount};
    });
  }

  async hasPendingSaleOutboxEntry(clientGestureId: string): Promise<boolean> {
    return await this.enqueue(async () => {
      const entry = await this.store.getSaleOutboxEntry(clientGestureId);
      if (!entry) {
        return false;
      }

      if (!isWithinSaleCorrectionWindow(entry.occurredAt, new Date())) {
        if (entry.status === 'Synced') {
          await this.store.deleteSaleOutboxEntry(clientGestureId);
        }
        return false;
      }

      return (entry.status ?? 'Pending') !== 'Quarantined';
    });
  }

  async setSessionMode(mode: 'AvailableNow' | 'NextFair'): Promise<ScanSessionSnapshot> {
    return await this.enqueue(async () => {
      const now = new Date();
      const session = await this.ensureSession(now);

      if (session.closeRequested) {
        const nextSession = createSession(now, mode);
        await this.store.orphanPendingOutboxEntriesFromOtherSessions(nextSession.clientSessionId);
        await this.store.saveSession(nextSession);
        return nextSession;
      }

      const counts = await this.store.getSessionCounts(session.clientSessionId);
      const timestamp = now.toISOString();
      // Older builds created a local session during bootstrap. Re-anchor only
      // local-only empty sessions; a remote session already has a server-owned start.
      const updated = counts.scannedCount === 0 && session.remoteSessionId === null
        ? {...session, mode, startedAt: timestamp, lastScanAt: timestamp, lastSyncAt: timestamp}
        : {...session, mode};
      await this.store.saveSession(updated);
      return updated;
    });
  }

  async recordSync(synchronizedAt = new Date()): Promise<void> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session) {
        return;
      }

      await this.store.saveSession({
        ...session,
        lastSyncAt: synchronizedAt.toISOString(),
      });
    });
  }

  async requestClose(closeReason: LocalScanCloseReason): Promise<ScanSessionSnapshot> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(new Date());
      const updated = {
        ...session,
        closeRequested: true,
        closeReason,
      };
      await this.store.setAsidePendingOutboxEntriesForSession(
        session.clientSessionId,
        'Livre sans décision mis de côté avant la clôture de la session.',
      );
      await this.store.saveSessionCloseRequest({
        clientSessionId: session.clientSessionId,
        remoteSessionId: session.remoteSessionId,
        volunteerId: session.volunteerId,
        mode: session.mode,
        targetAssoEventsId: session.targetAssoEventsId,
        closeReason,
        requestedAt: new Date().toISOString(),
        startedAt: session.startedAt,
      });
      await this.store.saveSession(updated);
      return updated;
    });
  }

  async recoverClosedSession(clientSessionId: string): Promise<ScanSessionSnapshot | null> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session || session.clientSessionId !== clientSessionId) {
        return null;
      }

      // ClientSessionId is also the server session id, so a closed server row
      // cannot be reopened with the same idempotency key.
      const replacement = {
        ...session,
        clientSessionId: createClientId(),
        remoteSessionId: null,
      };
      await this.store.replaceSessionAfterRemoteClosure(
        session.clientSessionId,
        replacement,
      );
      return replacement;
    });
  }

  async clearSession(): Promise<void> {
    return await this.enqueue(async () => {
      await this.store.clearSession();
    });
  }

  async clearAccountState(): Promise<void> {
    return await this.enqueue(async () => {
      await this.store.clearAccountState();
    });
  }

  async bindSessionToVolunteer(volunteerId: string, allowRebind = false): Promise<void> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session || session.volunteerId === volunteerId) {
        return;
      }

      if (session.volunteerId !== null && !allowRebind) {
        return;
      }

      await this.store.saveSession({...session, volunteerId});
    });
  }

  async setAsideCurrentSessionForOtherVolunteer(): Promise<number> {
    return await this.enqueue(async () => {
      const session = await this.store.getSession();
      if (!session) {
        return 0;
      }
      return await this.store.setAsideOutboxEntriesForSession(
        session.clientSessionId,
        'Livre provenant d’une autre session bénévole ; reprise manuelle requise.',
      );
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
    reusedExistingSession: boolean;
  }): Promise<void> {
    return await this.enqueue(async () => {
      const current = await this.store.getSession();
      if (!current) {
        return;
      }

      await this.store.saveSession({
        ...current,
        remoteSessionId: remoteSession.scanSessionId,
        mode: remoteSession.mode,
        targetAssoEventsId: remoteSession.targetAssoEventsId,
        startedAt: remoteSession.startedAt,
        lastScanAt: remoteSession.lastScanAt,
        lastSyncAt: remoteSession.lastSyncAt,
      });
    });
  }

  async recordScan(isbn13: string, occurredAt = new Date()): Promise<LocalScanResult> {
    return await this.enqueue(async () => {
      const session = await this.ensureSession(occurredAt);
      if (session.closeRequested) {
        throw new ScanSessionClosePendingError();
      }

      await this.store.orphanPendingOutboxEntriesFromOtherSessions(session.clientSessionId);
      const existingEntries = await this.store.listOutboxEntries();
      const previousEntry = existingEntries
        .slice()
        .reverse()
        .find(entry =>
          entry.clientSessionId === session.clientSessionId &&
          entry.status !== 'CancelledLocal');
      const previousPendingEntries = existingEntries
        .filter(entry =>
          entry.status === 'Pending' && entry.clientSessionId === session.clientSessionId);

      for (const entry of previousPendingEntries) {
        await this.store.decideOutboxEntry(
          entry.clientGestureId,
          true,
          session.mode,
        );
      }

      const counts = await this.store.getSessionCounts(session.clientSessionId);
      const catalogBook = await this.store.getCatalogBook(isbn13);
      const settings = await this.store.getSettings();
      const verdict = this.verdictService.calculate(
        catalogBook,
        settings,
        await this.store.getCatalogBooks(),
      );
      const timestamp = occurredAt.toISOString();
      const entry: ScanOutboxEntry = {
        clientGestureId: createClientId(),
        clientSessionId: session.clientSessionId,
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
        counts: {
          ...counts,
          scannedCount: counts.scannedCount + 1,
          keptCount: counts.keptCount + previousPendingEntries.length,
          rejectedCount: counts.rejectedCount,
        },
        lastScanAt: timestamp,
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

      if (
        existing.clientSessionId !== session.clientSessionId &&
        (existing.status === 'Pending' || existing.status === 'NeedsDecision')
      ) {
        await this.store.reattachOutboxEntry(clientGestureId, session.clientSessionId);
      }

      const decided = await this.store.decideOutboxEntry(
        clientGestureId,
        kept,
        session.mode,
      );

      if (existing.status === 'Pending' || existing.status === 'NeedsDecision') {
        const currentSession = await this.store.getSession();
        if (currentSession?.clientSessionId === session.clientSessionId) {
          const counts = await this.store.getSessionCounts(session.clientSessionId);
          const movedFromAnotherSession = existing.clientSessionId !== session.clientSessionId;
          await this.store.saveSession({
            ...currentSession,
            counts: {
              ...counts,
              scannedCount: counts.scannedCount + (movedFromAnotherSession ? 1 : 0),
              keptCount: counts.keptCount + (kept ? 1 : 0),
              rejectedCount: counts.rejectedCount + (kept ? 0 : 1),
            },
          });
        }
      }

      return decided;
    });
  }

  async cacheMetadata(metadata: BookMetadata): Promise<void> {
    return await this.enqueue(async () => {
      const existing = await this.store.getCatalogBook(metadata.isbn13);
      const book: ScanCatalogBook = {
        isbn13: metadata.isbn13,
        title: metadata.title,
        authors: metadata.authors,
        workId: metadata.workId,
        workKey: metadata.workKey ?? existing?.workKey ?? null,
        publisher: metadata.publisher ?? existing?.publisher ?? null,
        publicationYear: metadata.publicationYear ?? existing?.publicationYear ?? null,
        coverUrl: metadata.coverUrl ?? existing?.coverUrl ?? null,
        otherIsbns: existing?.otherIsbns,
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
        closeRequested: existing.closeRequested ?? false,
        closeReason: existing.closeReason ?? null,
      };
    }

    const timestamp = now.toISOString();
    const session: ScanSessionSnapshot = {
      key: 'active-session',
      clientSessionId: createClientId(),
      remoteSessionId: null,
      volunteerId: null,
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: timestamp,
      lastScanAt: timestamp,
      lastSyncAt: timestamp,
      closeRequested: false,
      closeReason: null,
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

const SALE_CORRECTION_WINDOW_MS = 30_000;

function isWithinSaleCorrectionWindow(occurredAt: string, now: Date): boolean {
  const occurredAtMs = Date.parse(occurredAt);
  const nowMs = now.getTime();
  return Number.isFinite(occurredAtMs) && nowMs >= occurredAtMs &&
    nowMs - occurredAtMs <= SALE_CORRECTION_WINDOW_MS;
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

function createEmptyCatalogBook(isbn13: string, updatedAt: string): ScanCatalogBook {
  return {
    isbn13,
    title: null,
    authors: null,
    workId: null,
    qtyAvailable: 0,
    qtyAnnounced: 0,
    salesCount: 0,
    isWanted: false,
    isRare: false,
    updatedAt,
  };
}

function createSession(now: Date, mode: 'AvailableNow' | 'NextFair'): ScanSessionSnapshot {
  const timestamp = now.toISOString();
  return {
    key: 'active-session',
    clientSessionId: createClientId(),
    remoteSessionId: null,
    volunteerId: null,
    mode,
    targetAssoEventsId: null,
    startedAt: timestamp,
    lastScanAt: timestamp,
    lastSyncAt: timestamp,
    closeRequested: false,
    closeReason: null,
  };
}

async function measurePromise<T>(
  operation: () => Promise<T>,
  recordDuration: (durationMs: number) => void,
): Promise<T> {
  const startedAt = performance.now();
  try {
    return await operation();
  } finally {
    recordDuration(performance.now() - startedAt);
  }
}
