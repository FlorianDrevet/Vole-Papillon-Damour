import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanOutboxEntry,
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
  ) {}

  async syncCatalog(): Promise<CatalogSyncSummary> {
    return await this.enqueue(() => this.syncCatalogInternal());
  }

  async flushOutbox(): Promise<OutboxSyncSummary> {
    const result = await this.enqueue(() => this.flushOutboxInternal());
    return result.summary;
  }

  async syncAll(): Promise<SessionSyncSummary> {
    return await this.enqueue(async () => {
      let catalog: CatalogSyncSummary | null = null;
      try {
        catalog = await this.syncCatalogInternal();
      } catch {
        // A catalog outage must not prevent already decided gestures from being sent.
      }

      const transfer = await this.flushOutboxInternal();
      const closed = await this.closeRequestedSession(
        transfer.remoteSession,
        transfer.summary,
      );
      return {catalog, outbox: transfer.summary, closed};
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
    const optimisticEntries = await this.store.listOutboxEntries();
    const response = await firstValueFrom(this.api.getCatalogDelta(state?.watermark ?? null));
    const visibleBooks = response.books
      .filter(book => !book.isHidden)
      .map(book => toCatalogBook(
        book,
        optimisticEntries,
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

  private async flushOutboxInternal(): Promise<{
    summary: OutboxSyncSummary;
    remoteSession: ScanSessionResponse | null;
  }> {
    const entries = await this.store.listTransmittableOutboxEntries();
    if (entries.length === 0) {
      return {
        summary: {
          sent: 0,
          remaining: await this.store.countPendingOutboxEntries(),
          stoppedOnError: false,
        },
        remoteSession: null,
      };
    }

    const session = await this.workflow.getSession();
    if (!session) {
      return {
        summary: {
          sent: 0,
          remaining: await this.store.countPendingOutboxEntries(),
          stoppedOnError: true,
        },
        remoteSession: null,
      };
    }

    let remoteSession: ScanSessionResponse;
    try {
      remoteSession = await firstValueFrom(this.api.openSession({
        mode: session.mode,
        targetAssoEventsId: session.targetAssoEventsId,
        clientSessionId: session.scanSessionId,
      }));
      await this.workflow.mergeRemoteSession(remoteSession);
    } catch {
      return {
        summary: {
          sent: 0,
          remaining: await this.store.countPendingOutboxEntries(),
          stoppedOnError: true,
        },
        remoteSession: null,
      };
    }

    let sent = 0;
    let stoppedOnError = false;
    for (const entry of entries) {
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
        await this.store.markOutboxAttempt(entry.clientGestureId, new Date().toISOString(), null);
        await this.applyServerProjection(entry, response);
        await this.store.deleteOutboxEntry(entry.clientGestureId);
        sent += 1;
      } catch (error: unknown) {
        stoppedOnError = true;
        await this.store.markOutboxAttempt(
          entry.clientGestureId,
          new Date().toISOString(),
          describeError(error),
        );
        break;
      }
    }

    await this.workflow.recordSync();
    return {
      summary: {
        sent,
        remaining: await this.store.countPendingOutboxEntries(),
        stoppedOnError,
      },
      remoteSession,
    };
  }

  private async closeRequestedSession(
    remoteSession: ScanSessionResponse | null,
    outbox: OutboxSyncSummary,
  ): Promise<boolean> {
    const session = await this.workflow.getSession();
    if (
      !session?.closeRequested ||
      outbox.stoppedOnError ||
      outbox.remaining > 0
    ) {
      return false;
    }

    try {
      const openedSession = remoteSession ?? await firstValueFrom(this.api.openSession({
        mode: session.mode,
        targetAssoEventsId: session.targetAssoEventsId,
        clientSessionId: session.scanSessionId,
      }));
      if (!remoteSession) {
        await this.workflow.mergeRemoteSession(openedSession);
      }

      const closedSession = await firstValueFrom(this.api.closeSession(
        openedSession.scanSessionId,
        {closeReason: session.closeReason ?? 'Manual'},
      ));
      await this.workflow.mergeRemoteSession(closedSession);
      await this.workflow.clearSession();
      return true;
    } catch {
      return false;
    }
  }

  private async applyServerProjection(
    entry: ScanOutboxEntry,
    response: {
      isbn13: string;
      qtyAvailable: number;
      qtyAnnounced: number;
    },
  ): Promise<void> {
    const current = await this.store.getCatalogBook(response.isbn13)
      ?? await this.store.getCatalogBook(entry.isbn13);
    const book: ScanCatalogBook = {
      isbn13: response.isbn13,
      title: current?.title ?? null,
      authors: current?.authors ?? null,
      workId: current?.workId ?? null,
      qtyAvailable: response.qtyAvailable,
      qtyAnnounced: response.qtyAnnounced,
      salesCount: current?.salesCount ?? 0,
      isWanted: current?.isWanted ?? false,
      isRare: current?.isRare ?? entry.isRare,
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

function toCatalogBook(
  book: ScanCatalogBook & {isHidden: boolean},
  optimisticEntries: readonly ScanOutboxEntry[],
  sessionMode: 'AvailableNow' | 'NextFair',
): ScanCatalogBook {
  const {isHidden: _isHidden, ...catalogBook} = book;
  const localKeptCount = optimisticEntries.filter(entry =>
    entry.status === 'Kept' &&
    entry.catalogApplied &&
    entry.isbn13 === book.isbn13).length;

  return {
    ...catalogBook,
    qtyAvailable: catalogBook.qtyAvailable + (
      sessionMode === 'AvailableNow' ? localKeptCount : 0),
    qtyAnnounced: catalogBook.qtyAnnounced + (
      sessionMode === 'NextFair' ? localKeptCount : 0),
  };
}

function describeError(error: unknown): string {
  return error instanceof Error ? error.message : 'Network request failed.';
}
