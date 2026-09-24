import {HttpErrorResponse} from '@angular/common/http';
import {Injectable, Signal, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanApiService} from './scan-api.service';
import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanMemberCredential,
  ScanPassageAssociationEntry,
} from './scan-offline.model';
import {parseMemberCredential} from './scan-member-card';

export type ScanPassageAssociationState =
  | {kind: 'anonymous'}
  | {kind: 'resolving'}
  | {kind: 'associated'; displayLabel: string}
  | {kind: 'pending-offline'}
  | {kind: 'not-recognised'};

@Injectable({providedIn: 'root'})
export class ScanPassageAssociationService {
  private readonly currentState = signal<ScanPassageAssociationState>({kind: 'anonymous'});
  readonly state: Signal<ScanPassageAssociationState> = this.currentState.asReadonly();
  private credential: ScanMemberCredential | null = null;
  private submitVersion = 0;

  constructor(
    private readonly api: ScanApiService,
    private readonly store: ScanLocalStoreService,
  ) {}

  startAssociation(): void {
    this.submitVersion += 1;
    this.credential = null;
    this.currentState.set({kind: 'resolving'});
  }

  async submitCredential(raw: string): Promise<void> {
    const parsed = parseMemberCredential(raw);
    const submitVersion = ++this.submitVersion;
    if (!parsed) {
      this.credential = null;
      this.currentState.set({kind: 'not-recognised'});
      return;
    }

    this.credential = null;
    this.currentState.set({kind: 'resolving'});
    try {
      const result = await firstValueFrom(this.api.resolveMemberCard(parsed.value));
      if (submitVersion !== this.submitVersion) {
        return;
      }

      this.credential = parsed;
      this.currentState.set({kind: 'associated', displayLabel: result.displayLabel});
    } catch (error: unknown) {
      if (submitVersion !== this.submitVersion) {
        return;
      }

      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.credential = null;
        this.currentState.set({kind: 'not-recognised'});
        return;
      }

      this.credential = parsed;
      this.currentState.set({kind: 'pending-offline'});
    }
  }

  clear(): void {
    this.submitVersion += 1;
    this.credential = null;
    this.currentState.set({kind: 'anonymous'});
  }

  async commit(checkoutPassageId: string, occurredAt: Date): Promise<boolean> {
    const state = this.currentState();
    if (
      (state.kind !== 'associated' && state.kind !== 'pending-offline') ||
      this.credential === null
    ) {
      return false;
    }

    const entry: ScanPassageAssociationEntry = {
      checkoutPassageId,
      credential: this.credential,
      occurredAt: occurredAt.toISOString(),
      createdAt: new Date().toISOString(),
      status: 'Pending',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
    await this.store.putPassageAssociation(entry);
    return true;
  }
}
