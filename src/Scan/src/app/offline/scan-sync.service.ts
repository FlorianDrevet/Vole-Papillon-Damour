import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanCatalogBook,
  ScanCatalogSyncState,
  ScanOutboxEntry,
  ScanSaleOutboxEntry,
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

  private async flushOutboxInternal(): Promise<{
    summary: OutboxSyncSummary;
    remoteSession: ScanSessionResponse | null;
  }> {
    const entries = await this.store.listTransmittableOutboxEntries();
    const sales = await this.store.listSaleOutboxEntries();
    if (entries.length === 0 && sales.length === 0) {
      return {
        summary: {
          sent: 0,
          remaining: await this.store.countPendingOutboxEntries(),
          stoppedOnError: false,
        },
        remoteSession: null,
      };
    }

    let remoteSession: ScanSessionResponse | null = null;
    let sent = 0;
    let stoppedOnError = false;

    if (entries.length > 0) {
      const session = await this.workflow.getSession();
      if (!session) {
        stoppedOnError = true;
      } else {
        try {
          remoteSession = await firstValueFrom(this.api.openSession({
            mode: session.mode,
            targetAssoEventsId: session.targetAssoEventsId,
            clientSessionId: session.scanSessionId,
          }));
          await this.workflow.mergeRemoteSession(remoteSession);
        } catch {
          stoppedOnError = true;
        }
      }

      if (remoteSession) {
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
            await this.applyServerProjection(entry.isbn13, entry.isRare, response);
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
      }
    }

    for (const sale of sales) {
      try {
        const response = await firstValueFrom(this.api.registerSale({
          isbn: sale.isbn13,
          quantity: sale.quantity,
          occurredAt: sale.occurredAt,
          clientGestureId: sale.clientGestureId,
        }));
        await this.store.markSaleAttempt(sale.clientGestureId, new Date().toISOString(), null);
        await this.applyServerProjection(sale.isbn13, false, response);
        await this.store.deleteSaleOutboxEntry(sale.clientGestureId);
        sent += 1;
      } catch (error: unknown) {
        stoppedOnError = true;
        await this.store.markSaleAttempt(
          sale.clientGestureId,
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
  return error instanceof Error ? error.message : 'Network request failed.';
}
