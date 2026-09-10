import {Injectable, Signal, WritableSignal, signal} from '@angular/core';

import {ScanCatalogSyncState} from './scan-offline.model';

export interface ScanStatusSnapshot {
  catalog: {state: 'fresh' | 'stale' | 'never'; syncedAt: string | null};
  outbox: {state: 'idle' | 'sending' | 'retrying'; queued: number};
  decision: {pending: number};
  setAside: {total: number};
}

export interface ScanStatusMessage {
  level: 'success' | 'info' | 'action' | 'blocking';
  text: string;
}

export interface ScanOutboxCounts {
  pendingDecisionCount: number;
  pendingTransmissionCount: number;
}

export interface ScanSetAsideCounts {
  orphaned: number;
  quarantined: number;
}

export const CATALOG_STALE_AFTER_MS = 4 * 60 * 60 * 1000;

@Injectable({providedIn: 'root'})
export class ScanStatusService {
  private readonly snapshotState: WritableSignal<ScanStatusSnapshot> = signal({
    catalog: {state: 'never', syncedAt: null},
    outbox: {state: 'idle', queued: 0},
    decision: {pending: 0},
    setAside: {total: 0},
  });
  private readonly messageState: WritableSignal<ScanStatusMessage | null> = signal(null);
  private messageTimer: number | null = null;

  readonly snapshot: Signal<ScanStatusSnapshot> = this.snapshotState.asReadonly();
  readonly message: Signal<ScanStatusMessage | null> = this.messageState.asReadonly();

  updateFromLocalState(
    catalogSyncState: ScanCatalogSyncState | null,
    outboxCounts: ScanOutboxCounts,
    setAsideCounts: ScanSetAsideCounts,
    now = new Date(),
  ): void {
    this.updateCatalog(catalogSyncState, now);
    this.updateOutbox(outboxCounts);
    this.updateSetAsideTotals(setAsideCounts);
  }

  updateCatalog(state: ScanCatalogSyncState | null, now = new Date()): void {
    if (!state) {
      this.patchSnapshot({catalog: {state: 'never', syncedAt: null}});
      return;
    }

    const timestamp = Date.parse(state.updatedAt);
    const fresh = Number.isFinite(timestamp) && now.getTime() - timestamp <= CATALOG_STALE_AFTER_MS;
    this.patchSnapshot({
      catalog: {
        state: fresh ? 'fresh' : 'stale',
        syncedAt: state.updatedAt,
      },
    });
  }

  updateOutbox(counts: ScanOutboxCounts): void {
    const queued = counts.pendingTransmissionCount;
    this.patchSnapshot({
      outbox: {
        state: queued > 0 ? 'retrying' : 'idle',
        queued,
      },
      decision: {pending: counts.pendingDecisionCount},
    });
  }

  setOutboxSending(): void {
    this.patchSnapshot({
      outbox: {
        ...this.snapshotState().outbox,
        state: 'sending',
      },
    });
  }

  setOutboxSummary(remaining: number, stoppedOnError: boolean): void {
    this.patchSnapshot({
      outbox: {
        state: stoppedOnError ? 'retrying' : remaining > 0 ? 'retrying' : 'idle',
        queued: remaining,
      },
    });
  }

  updateSetAsideTotals(counts: ScanSetAsideCounts): void {
    this.patchSnapshot({
      setAside: {total: counts.orphaned + counts.quarantined},
    });
  }

  showInfo(text: string): void {
    this.showMessage({level: 'info', text}, 8_000);
  }

  showSuccess(text: string): void {
    this.showMessage({level: 'success', text}, 3_200);
  }

  showAction(text: string): void {
    this.showMessage({level: 'action', text});
  }

  showBlocking(text: string): void {
    this.showMessage({level: 'blocking', text});
  }

  clearMessage(): void {
    if (this.messageTimer !== null && typeof window !== 'undefined') {
      window.clearTimeout(this.messageTimer);
      this.messageTimer = null;
    }
    this.messageState.set(null);
  }

  bookCountLabel(count: number): string {
    return `${count} livre${count > 1 ? 's' : ''}`;
  }

  decisionLabel(count: number): string {
    return `${this.bookCountLabel(count)} attend${count > 1 ? 'ent' : ''} une décision`;
  }

  setAsideLabel(count: number): string {
    return `${this.bookCountLabel(count)} attend${count > 1 ? 'ent' : ''} une reprise`;
  }

  outboxLabel(count: number): string {
    return `${this.bookCountLabel(count)} en attente d’envoi · départ automatique`;
  }

  private showMessage(message: ScanStatusMessage, durationMs?: number): void {
    this.clearMessage();
    this.messageState.set(message);
    if (durationMs && typeof window !== 'undefined') {
      this.messageTimer = window.setTimeout(() => {
        this.messageTimer = null;
        this.messageState.set(null);
      }, durationMs);
    }
  }

  private patchSnapshot(patch: Partial<ScanStatusSnapshot>): void {
    this.snapshotState.update(current => ({...current, ...patch}));
  }
}
