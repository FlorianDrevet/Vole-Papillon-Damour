import {computed, Injectable, Signal, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../catalog-auth.service';
import {CatalogMemberApiService} from '../catalog-member-api.service';
import {
  CatalogSelectionItem,
  CatalogSelectionMergeEntry,
  CatalogSelectionMergeResult,
  CatalogSelectionResponse,
  CatalogSelectionTargetRequest,
} from '../catalog.models';
import {LocalSelectionEntry, SelectionRef, planSelectionMerge, selectionKey} from './selection-merge';
import {LocalSelectionStore} from './local-selection.store';

export type CatalogSelectionMode = 'local' | 'synced' | 'local-unsynced';

@Injectable({providedIn: 'root'})
export class CatalogSelectionService {
  private readonly remoteKeys = signal<ReadonlySet<string>>(new Set<string>());
  private readonly remoteSelection = signal<CatalogSelectionResponse | null>(null);
  private readonly localRevision = signal(0);
  private readonly currentMode = signal<CatalogSelectionMode>('local');
  private readonly currentPendingMerge = signal<{
    localCount: number;
    accountCount: number;
    mergedCount: number;
  } | null>(null);
  private pendingPlan: {entries: LocalSelectionEntry[]; toSend: LocalSelectionEntry[]} | null = null;

  readonly mode: Signal<CatalogSelectionMode> = computed(() =>
    this.auth.isAuthenticated() ? this.currentMode() : 'local',
  );
  readonly pendingMerge = this.currentPendingMerge.asReadonly();
  readonly keys: Signal<ReadonlySet<string>> = computed(() => {
    if (this.mode() === 'synced') {
      return this.remoteKeys();
    }

    this.localRevision();
    return new Set(this.local.list().map(entry => selectionKey(entry.ref)));
  });

  constructor(
    private readonly local: LocalSelectionStore,
    private readonly api: CatalogMemberApiService,
    private readonly auth: CatalogAuthService,
  ) {}

  async add(ref: SelectionRef, title: string): Promise<void> {
    if (this.mode() === 'synced') {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.addSelectionItem(token, targetRequest(ref)));
      await this.refresh();
      return;
    }

    this.local.add({ref, title, addedAt: new Date().toISOString()});
    this.bumpLocalRevision();
  }

  async remove(ref: SelectionRef): Promise<void> {
    if (this.mode() === 'synced') {
      const response = this.remoteSelection() ?? await this.refresh();
      const item = response?.items.find(candidate => selectionItemKey(candidate) === selectionKey(ref));
      if (!item) {
        return;
      }

      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.removeSelectionItem(token, item.id));
      await this.refresh();
      return;
    }

    this.local.remove(ref);
    this.bumpLocalRevision();
  }

  async refresh(): Promise<CatalogSelectionResponse | null> {
    if (!this.auth.isAuthenticated()) {
      return null;
    }

    const token = await this.auth.getApiAccessToken();
    const response = await firstValueFrom(this.api.getSelection(token));
    this.remoteSelection.set(response);
    this.remoteKeys.set(new Set(
      response.items
        .map(selectionItemKey)
        .filter((key): key is string => key !== null),
    ));
    return response;
  }

  async onSignedIn(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.currentMode.set('local');
    this.currentPendingMerge.set(null);
    this.pendingPlan = null;

    const localEntries = this.local.list();
    const response = await this.refresh();
    if (!response) {
      return;
    }

    if (localEntries.length === 0) {
      this.currentMode.set('synced');
      return;
    }

    const plan = planSelectionMerge(localEntries, this.remoteKeys());
    this.pendingPlan = {entries: localEntries, toSend: plan.toSend};
    if (plan.needsConfirmation) {
      this.currentPendingMerge.set({
        localCount: localEntries.length,
        accountCount: this.remoteKeys().size,
        mergedCount: this.remoteKeys().size + plan.toSend.length,
      });
      return;
    }

    await this.confirmMerge();
  }

  async confirmMerge(): Promise<CatalogSelectionMergeResult> {
    const pending = this.pendingPlan;
    if (!pending) {
      throw new Error('There is no member selection merge to confirm.');
    }

    const token = await this.auth.getApiAccessToken();
    const entries = pending.toSend.map(toMergeEntry);
    const result = await firstValueFrom(this.api.mergeSelection(token, entries));

    const rejected = new Set(result.rejected);
    const rejectedEntries = pending.toSend.filter(entry => rejected.has(referenceValue(entry.ref)));
    this.local.clear();
    for (const entry of rejectedEntries) {
      this.local.add(entry);
    }
    this.bumpLocalRevision();

    const acceptedKeys = new Set(pending.toSend
      .filter(entry => !rejected.has(referenceValue(entry.ref)))
      .map(entry => selectionKey(entry.ref)));
    this.remoteKeys.set(new Set([...this.remoteKeys(), ...acceptedKeys]));
    this.currentPendingMerge.set(null);
    this.pendingPlan = null;
    this.currentMode.set('synced');

    await this.refresh();
    return result;
  }

  declineMerge(): void {
    this.currentPendingMerge.set(null);
    this.pendingPlan = null;
    this.currentMode.set('local-unsynced');
  }

  private bumpLocalRevision(): void {
    this.localRevision.update(revision => revision + 1);
  }
}

function targetRequest(ref: SelectionRef): CatalogSelectionTargetRequest {
  return ref.kind === 'edition'
    ? {isbn13: ref.isbn13}
    : {rareBookId: ref.rareBookId};
}

function toMergeEntry(entry: LocalSelectionEntry): CatalogSelectionMergeEntry {
  return entry.ref.kind === 'edition'
    ? {isbn13: entry.ref.isbn13, addedAt: entry.addedAt}
    : {rareBookId: entry.ref.rareBookId, addedAt: entry.addedAt};
}

function referenceValue(ref: SelectionRef): string {
  return ref.kind === 'edition' ? ref.isbn13 : ref.rareBookId;
}

function selectionItemKey(item: CatalogSelectionItem): string | null {
  if (item.kind === 'edition' && item.isbn13) {
    return `edition:${item.isbn13}`;
  }

  return item.kind === 'rare' && item.rareBookId
    ? `rare:${item.rareBookId}`
    : null;
}
