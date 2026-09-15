import {Injectable, signal} from '@angular/core';

import {ScanSessionCounts, ScanSessionSnapshot} from './offline/scan-offline.model';

export interface ScanSessionSummary {
  session: ScanSessionSnapshot;
  counts: ScanSessionCounts;
  durationLabel: string;
}

@Injectable({providedIn: 'root'})
export class ScanSessionSummaryService {
  private readonly summaryState = signal<ScanSessionSummary | null>(null);
  readonly summary = this.summaryState.asReadonly();

  setSummary(
    session: ScanSessionSnapshot,
    counts: ScanSessionCounts,
    durationLabel: string,
  ): void {
    this.summaryState.set({
      session: {...session},
      counts: {...counts},
      durationLabel,
    });
  }

  clear(): void {
    this.summaryState.set(null);
  }
}
