import {ChangeDetectorRef, Component, EventEmitter, OnInit, Output} from '@angular/core';

import {ScanCatalogBook, ScanOutboxEntry} from './scan-offline.model';
import {recoveryActionsFor, ScanRecoveryAction} from './scan-recovery.model';
import {ScanTelemetryService} from './scan-telemetry.service';
import {ScanWorkflowService} from './scan-workflow.service';

export interface ScanRecoveryViewEntry {
  entry: ScanOutboxEntry;
  book: ScanCatalogBook | null;
  actions: readonly ScanRecoveryAction[];
}

@Component({
  selector: 'app-scan-recovery',
  templateUrl: './scan-recovery.component.html',
  styleUrls: ['./scan-recovery.component.scss'],
  standalone: false,
})
export class ScanRecoveryComponent implements OnInit {
  @Output() readonly finished = new EventEmitter<void>();

  entries: ScanRecoveryViewEntry[] = [];
  loading = true;
  errorMessage: string | null = null;

  constructor(
    private readonly workflow: ScanWorkflowService,
    private readonly telemetry: ScanTelemetryService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  async keep(view: ScanRecoveryViewEntry): Promise<void> {
    await this.workflow.decide(view.entry.clientGestureId, true);
    await this.reload();
  }

  async reject(view: ScanRecoveryViewEntry): Promise<void> {
    await this.workflow.decide(view.entry.clientGestureId, false);
    await this.reload();
  }

  async delete(view: ScanRecoveryViewEntry): Promise<void> {
    await this.workflow.deleteOutboxEntry(view.entry.clientGestureId);
    this.telemetry.trackEvent('scan_gesture_deleted', {
      clientGestureId: view.entry.clientGestureId,
      isbn13: view.entry.isbn13,
    });
    await this.reload();
  }

  async retry(view: ScanRecoveryViewEntry): Promise<void> {
    await this.workflow.retryOutboxEntry(view.entry.clientGestureId);
    await this.reload();
  }

  async abandon(view: ScanRecoveryViewEntry): Promise<void> {
    await this.delete(view);
  }

  async reattach(view: ScanRecoveryViewEntry): Promise<void> {
    await this.workflow.reattachOutboxEntryToCurrentSession(view.entry.clientGestureId);
    await this.reload();
  }

  close(): void {
    this.finished.emit();
  }

  causeLabel(entry: ScanOutboxEntry): string {
    switch (entry.setAsideReason) {
      case 'no-session':
        return 'La session d’origine est introuvable.';
      case 'other-volunteer':
        return 'Ce livre appartient à une autre session bénévole.';
      case 'undecided':
        return 'Ce livre attend encore une décision.';
      case 'server-refused':
        return 'Le serveur a refusé ce livre.';
      default:
        return 'Ce livre a été mis de côté pour reprise.';
    }
  }

  actionLabel(action: ScanRecoveryAction): string {
    switch (action) {
      case 'decide':
        return 'Décider';
      case 'reattach':
        return 'Reprendre dans cette session';
      case 'transmit':
        return 'Envoyer';
      case 'delete':
        return 'Abandonner';
      case 'retry':
        return 'Réessayer';
    }
  }

  trackByGesture(_index: number, view: ScanRecoveryViewEntry): string {
    return view.entry.clientGestureId;
  }

  private async reload(): Promise<void> {
    this.loading = true;
    this.errorMessage = null;
    try {
      const entries = await this.workflow.listSetAsideOutboxEntries();
      this.entries = await Promise.all(entries.map(async entry => ({
        entry,
        book: await this.workflow.getCatalogBook(entry.isbn13),
        actions: recoveryActionsFor(entry.status),
      })));
    } catch (error: unknown) {
      this.errorMessage = error instanceof Error
        ? error.message
        : 'La reprise locale est momentanément indisponible.';
    } finally {
      this.loading = false;
      this.changeDetector.markForCheck();
    }
  }
}
