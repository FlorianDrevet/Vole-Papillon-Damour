import {HttpErrorResponse} from '@angular/common/http';
import {Injectable, Optional} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanFailureKind,
  ScanOutboxEntry,
  ScanSaleOutboxEntry,
  ScanSessionCloseRequest,
  ScanSessionSnapshot,
  ScanSessionResponse,
} from './scan-offline.model';
import {ScanWorkflowService} from './scan-workflow.service';

export interface CatalogSyncSummary {
  booksReceived: number;
  booksRemoved: number;
  watermark: string;
}

export interface OutboxSyncSummary {
  sent: number;
  remaining: number;
  stoppedOnError: boolean;
  quarantined?: number;
  orphaned?: number;
}

export interface SessionSyncSummary {
  catalog: CatalogSyncSummary | null;
  outbox: OutboxSyncSummary;
  closed: boolean;
}

@Injectable({providedIn: 'root'})
export class ScanSyncService {
  private operation = Promise.resolve();

  constructor(
    private readonly api: ScanApiService,
    private readonly store: ScanLocalStoreService,
    private readonly workflow: ScanWorkflowService,
    @Optional() private readonly scanAuth: ScanAuthService | null,
  ) {}

  async syncCatalog(): Promise<CatalogSyncSummary> {
    return await this.enqueue(async () => {
      try {
        return await this.syncCatalogInternal();
      } catch (error: unknown) {
        this.handleServerAuthorizationFailure(error);
        throw error;
      }
    });
  }

  async flushOutbox(): Promise<OutboxSyncSummary> {
    return await this.enqueue(() => this.flushOutboxInternal());
  }

  async syncAll(): Promise<SessionSyncSummary> {
    return await this.enqueue(async () => {
      let catalog: CatalogSyncSummary | null = null;
      try {
        catalog = await this.syncCatalogInternal();
      } catch (error: unknown) {
        // A catalog outage must not prevent already decided gestures from being sent.
        if (this.handleServerAuthorizationFailure(error)) {
          return {
            catalog: null,
            outbox: await this.createOutboxSummary(0, true),
            closed: false,
          };
        }
      }

      const transfer = await this.flushOutboxInternal();
      const closed = await this.closeRequestedSessions();
      return {catalog, outbox: transfer, closed};
    });
  }

  async closeSession(session: ScanSessionSnapshot): Promise<void> {
    return await this.enqueue(async () => {
      const remoteSession = await firstValueFrom(this.api.openSession({
        mode: session.mode,
        targetAssoEventsId: session.targetAssoEventsId,
        clientSessionId: session.scanSessionId,
      }));
      await firstValueFrom(this.api.closeSession(remoteSession.scanSessionId, {
        closeReason: 'Manual',
      }));
    });
  }

  private async syncCatalogInternal(): Promise<CatalogSyncSummary> {
    const state = await this.store.getCatalogSyncState();
    const session = await this.workflow.getSession();
    const [optimisticEntries, optimisticSales] = await Promise.all([
      this.store.listOutboxEntries(),
      this.store.listSaleOutboxEntries(),
    ]);
    const response = await firstValueFrom(this.api.getCatalogDelta(state?.watermark ?? null));
    const visibleBooks = response.books
      .filter(book => !book.isHidden)
      .map(book => toCatalogBook(
        book,
        optimisticEntries,
        optimisticSales,
        session?.mode ?? 'AvailableNow',
      ));
    const removedIsbn13s = response.books
      .filter(book => book.isHidden)
      .map(book => book.isbn13);
    const syncState: ScanCatalogSyncState = {
      key: 'catalog-sync',
      watermark: response.nextWatermark,
      updatedAt: response.generatedAt,
    };

    await this.store.applyCatalogDelta(
      visibleBooks,
      response.settings,
      syncState,
      removedIsbn13s,
    );
    await this.workflow.recordSync(new Date(response.generatedAt));

    return {
      booksReceived: visibleBooks.length,
      booksRemoved: removedIsbn13s.length,
      watermark: response.nextWatermark,
    };
  }

  private async flushOutboxInternal(): Promise<OutboxSyncSummary> {
    const entries = await this.store.listTransmittableOutboxEntries();
    const sales = await this.store.listSaleOutboxEntries();
    if (entries.length === 0 && sales.length === 0) {
      return await this.createOutboxSummary(0, false);
    }

    let sent = 0;
    let stoppedOnError = false;
    let quarantined = 0;
    let orphaned = 0;
    let authorizationFailed = false;

    const sessions = await this.getSessionDescriptors();
    const entriesBySession = groupEntriesBySession(entries);

    for (const [scanSessionId, sessionEntries] of entriesBySession) {
      if (authorizationFailed) {
        break;
      }

      const session = sessions.get(scanSessionId);
      if (!session) {
        for (const entry of sessionEntries) {
          await this.store.orphanOutboxEntry(
            entry.clientGestureId,
            'Geste décidé sans session locale correspondante ; publication suspendue.',
          );
          orphaned += 1;
        }
        continue;
      }

      const readyEntries = sessionEntries.filter(entry => isRetryDue(entry));
      if (readyEntries.length === 0) {
        continue;
      }

      let remoteSession: ScanSessionResponse;
      try {
        remoteSession = await firstValueFrom(this.api.openSession({
          mode: session.mode,
          targetAssoEventsId: session.targetAssoEventsId,
          clientSessionId: session.scanSessionId,
        }));
        await this.workflow.mergeRemoteSession(remoteSession);
      } catch (error: unknown) {
        stoppedOnError = true;
        authorizationFailed = this.handleServerAuthorizationFailure(error);
        continue;
      }

      for (const entry of readyEntries) {
        try {
          const response = await firstValueFrom(this.api.scanBook(
            remoteSession.scanSessionId,
            {
              isbn: entry.isbn13,
              kept: entry.kept === true,
              occurredAt: entry.occurredAt,
              clientGestureId: entry.clientGestureId,
            },
          ));
          await this.store.markOutboxAttempt(
            entry.clientGestureId,
            new Date().toISOString(),
            null,
            null,
          );
          await this.applyServerProjection(entry.isbn13, entry.isRare, response);
          await this.store.deleteOutboxEntry(entry.clientGestureId);
          sent += 1;
        } catch (error: unknown) {
          stoppedOnError = true;
          const failureKind = classifyFailure(error);
          const attempted = await this.store.markOutboxAttempt(
            entry.clientGestureId,
            new Date().toISOString(),
            describeError(error),
            failureKind,
          );

          if (failureKind === 'authorization') {
            authorizationFailed = this.handleServerAuthorizationFailure(error);
            break;
          }

          if (
            failureKind === 'permanent' &&
            attempted.attemptCount >= MAX_PERMANENT_ATTEMPTS
          ) {
            await this.store.quarantineOutboxEntry(
              entry.clientGestureId,
              describeError(error),
            );
            quarantined += 1;
          }
        }
      }
    }

    if (!authorizationFailed) {
      for (const sale of sales.filter(entry => isRetryDue(entry))) {
        try {
          const response = await firstValueFrom(this.api.registerSale({
            isbn: sale.isbn13,
            quantity: sale.quantity,
            occurredAt: sale.occurredAt,
            clientGestureId: sale.clientGestureId,
          }));
          await this.store.markSaleAttempt(
            sale.clientGestureId,
            new Date().toISOString(),
            null,
            null,
          );
          await this.applyServerProjection(sale.isbn13, false, response);
          await this.store.deleteSaleOutboxEntry(sale.clientGestureId);
          sent += 1;
        } catch (error: unknown) {
          stoppedOnError = true;
          const failureKind = classifyFailure(error);
          const attempted = await this.store.markSaleAttempt(
            sale.clientGestureId,
            new Date().toISOString(),
            describeError(error),
            failureKind,
          );

          if (failureKind === 'authorization') {
            this.handleServerAuthorizationFailure(error);
            break;
          }

          if (
            failureKind === 'permanent' &&
            attempted.attemptCount >= MAX_PERMANENT_ATTEMPTS
          ) {
            await this.store.quarantineSaleOutboxEntry(
              sale.clientGestureId,
              describeError(error),
            );
            quarantined += 1;
          }
        }
      }
    }

    await this.workflow.recordSync();
    const summary = await this.createOutboxSummary(sent, stoppedOnError);
    return {
      ...summary,
      quarantined: Math.max(summary.quarantined ?? 0, quarantined),
      orphaned: Math.max(summary.orphaned ?? 0, orphaned),
    };
  }

  private async closeRequestedSessions(): Promise<boolean> {
    const activeSession = await this.workflow.getSession();
    const requests = await this.store.listSessionCloseRequests();
    const sessions = new Map<string, LocalSessionDescriptor>();

    if (activeSession?.closeRequested) {
      sessions.set(activeSession.scanSessionId, toSessionDescriptor(activeSession));
    }
    for (const request of requests) {
      sessions.set(request.scanSessionId, toSessionDescriptor(request));
    }

    let activeSessionClosed = false;
    for (const session of sessions.values()) {
      if (await this.store.countBlockingOutboxEntriesForSession(session.scanSessionId) > 0) {
        continue;
      }

      try {
        const openedSession = await firstValueFrom(this.api.openSession({
          mode: session.mode,
          targetAssoEventsId: session.targetAssoEventsId,
          clientSessionId: session.scanSessionId,
        }));
        const closedSession = await firstValueFrom(this.api.closeSession(
          openedSession.scanSessionId,
          {closeReason: session.closeReason},
        ));
        await this.store.deleteSessionCloseRequest(session.scanSessionId);

        if (activeSession?.scanSessionId === session.scanSessionId) {
          await this.workflow.mergeRemoteSession(closedSession);
          await this.workflow.clearSession();
          activeSessionClosed = true;
        }
      } catch (error: unknown) {
        if (this.handleServerAuthorizationFailure(error)) {
          break;
        }
      }
    }

    return activeSessionClosed;
  }

  private async getSessionDescriptors(): Promise<Map<string, LocalSessionDescriptor>> {
    const sessions = new Map<string, LocalSessionDescriptor>();
    const activeSession = await this.workflow.getSession();
    if (activeSession) {
      sessions.set(activeSession.scanSessionId, toSessionDescriptor(activeSession));
    }

    for (const request of await this.store.listSessionCloseRequests()) {
      sessions.set(request.scanSessionId, toSessionDescriptor(request));
    }

    return sessions;
  }

  private async createOutboxSummary(
    sent: number,
    stoppedOnError: boolean,
  ): Promise<OutboxSyncSummary> {
    return {
      sent,
      remaining: await this.store.countBlockingOutboxEntries(),
      stoppedOnError,
      quarantined: await this.store.countQuarantinedOutboxEntries(),
      orphaned: await this.store.countOrphanedOutboxEntries(),
    };
  }

  private handleServerAuthorizationFailure(error: unknown): boolean {
    if (!isAuthorizationFailure(error)) {
      return false;
    }

    this.scanAuth?.handleServerAuthorizationFailure();
    return true;
  }

  private async applyServerProjection(
    isbn13: string,
    isRare: boolean,
    response: {
      isbn13: string;
      qtyAvailable: number;
      qtyAnnounced?: number;
      salesCount?: number;
    },
  ): Promise<void> {
    const current = await this.store.getCatalogBook(response.isbn13)
      ?? await this.store.getCatalogBook(isbn13);
    const book: ScanCatalogBook = {
      isbn13: response.isbn13,
      title: current?.title ?? null,
      authors: current?.authors ?? null,
      workId: current?.workId ?? null,
      qtyAvailable: response.qtyAvailable,
      qtyAnnounced: response.qtyAnnounced ?? current?.qtyAnnounced ?? 0,
      salesCount: response.salesCount ?? current?.salesCount ?? 0,
      isWanted: current?.isWanted ?? false,
      isRare: current?.isRare ?? isRare,
      updatedAt: new Date().toISOString(),
    };
    await this.store.putCatalogBooks([book]);
  }

  private async enqueue<T>(work: () => Promise<T>): Promise<T> {
    const next = this.operation.then(work, work);
    this.operation = next.then(() => undefined, () => undefined);
    return await next;
  }
}

interface LocalSessionDescriptor {
  scanSessionId: string;
  mode: 'AvailableNow' | 'NextFair';
  targetAssoEventsId: string | null;
  closeReason: 'Manual' | 'Inactivity' | 'Disconnect' | 'TokenExpired';
}

// Keep the client retry budget aligned with the backend's alert outbox policy.
const MAX_PERMANENT_ATTEMPTS = 5;
const RETRY_BACKOFF_BASE_MS = 60_000;
const RETRY_BACKOFF_MAX_MS = 15 * 60_000;

function toSessionDescriptor(
  session: ScanSessionSnapshot | ScanSessionCloseRequest,
): LocalSessionDescriptor {
  return {
    scanSessionId: session.scanSessionId,
    mode: session.mode,
    targetAssoEventsId: session.targetAssoEventsId,
    closeReason: session.closeReason ?? 'Manual',
  };
}

function groupEntriesBySession(
  entries: readonly ScanOutboxEntry[],
): Map<string, ScanOutboxEntry[]> {
  const grouped = new Map<string, ScanOutboxEntry[]>();
  for (const entry of entries) {
    const sessionEntries = grouped.get(entry.scanSessionId) ?? [];
    sessionEntries.push(entry);
    grouped.set(entry.scanSessionId, sessionEntries);
  }
  return grouped;
}

function isRetryDue(entry: {
  attemptCount: number;
  lastAttemptAt: string | null;
  lastFailureKind?: ScanFailureKind | null;
}): boolean {
  if (!entry.lastAttemptAt || entry.lastFailureKind !== 'transient') {
    return true;
  }

  const lastAttemptAt = Date.parse(entry.lastAttemptAt);
  if (!Number.isFinite(lastAttemptAt)) {
    return true;
  }

  const backoff = Math.min(
    RETRY_BACKOFF_MAX_MS,
    RETRY_BACKOFF_BASE_MS * 2 ** Math.max(0, entry.attemptCount - 1),
  );
  return Date.now() - lastAttemptAt >= backoff;
}

function classifyFailure(error: unknown): ScanFailureKind {
  const status = getHttpStatus(error);
  if (status === 401 || status === 403) {
    return 'authorization';
  }
  if (status !== null && status >= 400 && status < 500) {
    return 'permanent';
  }
  return 'transient';
}

function isAuthorizationFailure(error: unknown): boolean {
  return classifyFailure(error) === 'authorization';
}

function getHttpStatus(error: unknown): number | null {
  if (error instanceof HttpErrorResponse) {
    return error.status;
  }

  if (typeof error === 'object' && error !== null && 'status' in error) {
    const status = (error as {status?: unknown}).status;
    return typeof status === 'number' ? status : null;
  }

  return null;
}

function toCatalogBook(
  book: ScanCatalogBook & {isHidden: boolean},
  optimisticEntries: readonly ScanOutboxEntry[],
  optimisticSales: readonly ScanSaleOutboxEntry[],
  sessionMode: 'AvailableNow' | 'NextFair',
): ScanCatalogBook {
  const {isHidden: _isHidden, ...catalogBook} = book;
  const localKeptCount = optimisticEntries.filter(entry =>
    entry.status === 'Kept' &&
    entry.catalogApplied &&
    entry.isbn13 === book.isbn13).length;
  const localSaleQuantity = optimisticSales
    .filter(entry => entry.isbn13 === book.isbn13)
    .reduce((total, entry) => total + entry.quantity, 0);

  return {
    ...catalogBook,
    qtyAvailable: Math.max(0, catalogBook.qtyAvailable + (
      sessionMode === 'AvailableNow' ? localKeptCount : 0) - localSaleQuantity),
    qtyAnnounced: catalogBook.qtyAnnounced + (
      sessionMode === 'NextFair' ? localKeptCount : 0),
    salesCount: catalogBook.salesCount + localSaleQuantity,
  };
}

function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    return error.message || `HTTP ${error.status}`;
  }
  return error instanceof Error ? error.message : 'Network request failed.';
}
