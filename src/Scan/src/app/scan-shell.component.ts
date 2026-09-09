import {Component} from '@angular/core';
import {Router} from '@angular/router';

import {ScanAuthService} from './auth/scan-auth.service';
import {ScanSyncService} from './offline/scan-sync.service';
import {ScanStatusService} from './offline/scan-status.service';
import {ScanWorkflowService} from './offline/scan-workflow.service';
import {ScanConfirmationService} from './scan-confirmation.service';

@Component({
  selector: 'app-scan-shell',
  templateUrl: './scan-shell.component.html',
  styleUrl: './scan-shell.component.scss',
  standalone: false,
})
export class ScanShellComponent {
  private syncPromise: Promise<void> | null = null;

  constructor(
    readonly scanAuth: ScanAuthService,
    private readonly router: Router,
    private readonly workflow: ScanWorkflowService,
    private readonly scanSync: ScanSyncService,
    readonly status: ScanStatusService,
    readonly confirmation: ScanConfirmationService,
  ) {}

  get isAuthenticated(): boolean {
    const state = this.scanAuth.authState.status;
    return state === 'authorized' || state === 'degraded';
  }

  get showBack(): boolean {
    return this.currentPath !== '/accueil' && this.currentPath !== '/';
  }

  get currentPath(): string {
    return this.router.url.split('?')[0].replace(/\/$/, '') || '/';
  }

  async goBack(): Promise<void> {
    if (this.currentPath === '/tri' && await this.hasScannedBooks()) {
      const confirmed = await this.confirmation.confirm({
        title: 'Quitter le tri ?',
        message: 'La session reste ouverte et les livres déjà enregistrés restent conservés. Vous pourrez reprendre le tri depuis l’accueil.',
        confirmLabel: 'Quitter le tri',
        cancelLabel: 'Rester dans le tri',
      });
      if (!confirmed) {
        return;
      }
    }

    await this.router.navigateByUrl(this.backPath);
  }

  async syncNow(): Promise<void> {
    if (this.syncPromise || this.scanAuth.authState.status !== 'authorized') {
      return;
    }

    const operation = this.performSync();
    this.syncPromise = operation;
    try {
      await operation;
    } finally {
      if (this.syncPromise === operation) {
        this.syncPromise = null;
      }
    }
  }

  async openRecovery(): Promise<void> {
    await this.router.navigateByUrl('/reprise');
  }

  private get backPath(): string {
    switch (this.currentPath) {
      case '/tri/mode':
      case '/tri':
      case '/tri/fin':
      case '/caisse':
      case '/consulter':
      case '/reprise':
        return '/accueil';
      default:
        return '/accueil';
    }
  }

  private async hasScannedBooks(): Promise<boolean> {
    const session = await this.workflow.getSession();
    if (!session) {
      return false;
    }

    const counts = await this.workflow.getSessionCounts(session.clientSessionId);
    return counts.scannedCount > 0;
  }

  private async performSync(): Promise<void> {
    this.status.setOutboxSending();
    try {
      const summary = await this.scanSync.syncAll();
      this.status.setOutboxSummary(summary.outbox.remaining, summary.outbox.stoppedOnError);
      await this.refreshStatus();
      if (summary.outbox.stoppedOnError) {
        this.status.showBlocking('La file locale reste conservée et sera réessayée automatiquement.');
      } else if (summary.outbox.newlyOrphaned + summary.outbox.newlyQuarantined > 0) {
        const count = summary.outbox.newlyOrphaned + summary.outbox.newlyQuarantined;
        this.status.showInfo(`${count} livre${count > 1 ? 's' : ''} vient${count > 1 ? 'nent' : ''} d’être mis de côté.`);
      } else {
        this.status.clearMessage();
      }
    } catch {
      this.status.showBlocking('La synchronisation a échoué ; les livres restent conservés localement.');
    }
  }

  private async refreshStatus(): Promise<void> {
    const session = await this.workflow.getSession();
    const counts = session
      ? await this.workflow.getOutboxCounts(session.clientSessionId)
      : {pendingDecisionCount: 0, pendingTransmissionCount: 0};
    this.status.updateFromLocalState(
      await this.workflow.getCatalogSyncState(),
      counts,
      await this.workflow.getSetAsideCounts(),
    );
  }
}
