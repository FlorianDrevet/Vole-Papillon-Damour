import {Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogManualRequestItem,
  CatalogManualRequestQueue,
  CatalogManualRequestSummary,
  CatalogManualRequestTab,
} from '../../../core/catalog.models';
import {formatManualRequesterName, formatManualRequesterPhone, manualRequesterPhoneHref} from './manual-request-format';
import {ManualRequestDecisionMode} from './manual-request-decision-dialog.component';

@Component({
  selector: 'app-manual-requests-view',
  standalone: false,
  templateUrl: './manual-requests-view.component.html',
  styleUrls: ['./manual-requests-view.component.scss'],
})
export class ManualRequestsViewComponent implements OnChanges, OnDestroy {
  @Input() accessToken = '';
  @Output() countsChanged = new EventEmitter<CatalogManualRequestSummary>();
  @Output() requesterSelected = new EventEmitter<string | null>();

  @ViewChild('requestsHeading') private requestsHeading?: ElementRef<HTMLElement>;

  readonly activeTab = signal<CatalogManualRequestTab>('found');
  readonly queue = signal<CatalogManualRequestQueue | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly actionPending = signal<string | null>(null);
  readonly decision = signal<{mode: ManualRequestDecisionMode; item: CatalogManualRequestItem} | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private loadSequence = 0;

  constructor(private readonly api: CatalogAdminApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['accessToken'] && this.accessToken) {
      void this.loadQueue();
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
  }

  async selectTab(tab: CatalogManualRequestTab): Promise<void> {
    if (this.activeTab() === tab) {
      return;
    }
    this.activeTab.set(tab);
    this.page.set(1);
    await this.loadQueue();
  }

  updateSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => {
      this.searchTimer = null;
      void this.loadQueue();
    }, 300);
  }

  async loadQueue(): Promise<void> {
    if (!this.accessToken) {
      return;
    }

    const sequence = ++this.loadSequence;
    this.loading.set(true);
    this.error.set(null);
    try {
      const response = await firstValueFrom(this.api.getManualRequests(this.accessToken, {
        tab: this.activeTab(),
        q: this.search().trim(),
        page: this.page(),
        pageSize: 50,
      }));
      if (sequence !== this.loadSequence) {
        return;
      }
      this.queue.set(response);
      this.page.set(response.page);
      this.countsChanged.emit({
        foundCount: response.foundCount,
        foundOverdueCount: response.foundOverdueCount,
      });
    } catch {
      if (sequence === this.loadSequence) {
        this.error.set('Impossible de charger les demandes manuelles. Vérifiez la connexion puis réessayez.');
      }
    } finally {
      if (sequence === this.loadSequence) {
        this.loading.set(false);
      }
    }
  }

  async pick(item: CatalogManualRequestItem): Promise<void> {
    await this.runAction(item.requestId, async () => {
      await firstValueFrom(this.api.pickManualRequest(this.accessToken, item.requestId));
      this.success.set('Exemplaire prélevé — demande trouvée');
      await this.loadQueue();
    });
  }

  markGiven(item: CatalogManualRequestItem): void {
    this.decision.set({mode: 'given', item});
  }

  refuse(item: CatalogManualRequestItem): void {
    this.decision.set({mode: 'refuse', item});
  }

  async cancel(item: CatalogManualRequestItem): Promise<void> {
    if (item.status === 'Found') {
      this.decision.set({mode: 'cancel-found', item});
      return;
    }
    const requester = formatManualRequesterName(item.requesterFirstName, item.requesterLastName, item.requesterErased);
    if (typeof window !== 'undefined' && !window.confirm(`Annuler la demande de ${requester} ?`)) {
      return;
    }
    await this.runAction(item.requestId, async () => {
      await firstValueFrom(this.api.cancelManualRequest(this.accessToken, item.requestId));
      this.success.set('Demande annulée');
      await this.loadQueue();
    });
  }

  async confirmDecision(keepSearching: boolean): Promise<void> {
    const decision = this.decision();
    if (!decision || this.actionPending()) {
      return;
    }

    const completed = await this.runAction(decision.item.requestId, async () => {
      if (decision.mode === 'given') {
        await firstValueFrom(this.api.markManualRequestGiven(this.accessToken, decision.item.requestId));
        this.success.set('Livre marqué comme donné');
      } else if (decision.mode === 'refuse') {
        const result = await firstValueFrom(this.api.refuseManualRequest(
          this.accessToken,
          decision.item.requestId,
          {keepSearching},
        ));
        this.success.set(`Refus enregistré — ${result.notifiedMemberCount} membres prévenus`);
      } else {
        const result = await firstValueFrom(this.api.cancelManualRequest(this.accessToken, decision.item.requestId));
        this.success.set(`Demande annulée — ${result.notifiedMemberCount} membres prévenus`);
      }
      await this.loadQueue();
    });
    if (completed) {
      this.decision.set(null);
      setTimeout(() => this.requestsHeading?.nativeElement.focus());
    }
  }

  closeDecision(): void {
    if (!this.actionPending()) {
      this.decision.set(null);
    }
  }

  openRequester(requesterId: string): void {
    this.requesterSelected.emit(requesterId);
  }

  newRequester(): void {
    this.requesterSelected.emit(null);
  }

  requesterName(item: CatalogManualRequestItem): string {
    return formatManualRequesterName(item.requesterFirstName, item.requesterLastName, item.requesterErased);
  }

  requesterPhone(phone: string | null): string {
    return formatManualRequesterPhone(phone);
  }

  requesterPhoneHref(phone: string): string {
    return manualRequesterPhoneHref(phone);
  }

  foundOriginLabel(item: CatalogManualRequestItem): string {
    const by = item.foundByName || 'un bénévole';
    const date = item.foundAt ? this.formatDate(item.foundAt) : 'date inconnue';
    return item.foundOrigin === 'Scan'
      ? `Trouvé au tri par ${by} · ${date}`
      : `Prélevé en stock par ${by} · ${date}`;
  }

  scopeLabel(item: CatalogManualRequestItem): string {
    return item.scope === 'Work' ? 'toutes les éditions' : 'cette édition';
  }

  closedStatusLabel(item: CatalogManualRequestItem): string {
    const date = item.closedAt ? this.formatDate(item.closedAt) : 'date inconnue';
    switch (item.status) {
      case 'Given': return `Donné le ${date}${item.closedByName ? ` · ${item.closedByName}` : ''}`;
      case 'Refused': return `Refusé le ${date}`;
      case 'Cancelled': return `Annulé le ${date}`;
      default: return item.status;
    }
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? 'date inconnue'
      : new Intl.DateTimeFormat('fr-FR', {day: 'numeric', month: 'short', year: 'numeric'}).format(date);
  }

  daysWaiting(value: string | null): number {
    if (!value) {
      return 0;
    }
    const date = new Date(value).getTime();
    return Number.isNaN(date) ? 0 : Math.max(0, Math.floor((Date.now() - date) / 86_400_000));
  }

  private async runAction(requestId: string, action: () => Promise<void>): Promise<boolean> {
    if (!this.accessToken || this.actionPending()) {
      return false;
    }

    this.actionPending.set(requestId);
    this.error.set(null);
    this.success.set(null);
    try {
      await action();
      return true;
    } catch {
      this.error.set('Cette action n’a pas abouti. Vérifiez la connexion puis réessayez.');
      return false;
    } finally {
      this.actionPending.set(null);
    }
  }
}
