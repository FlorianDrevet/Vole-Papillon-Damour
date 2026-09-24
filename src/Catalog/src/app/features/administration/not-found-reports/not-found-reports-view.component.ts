import {DOCUMENT} from '@angular/common';
import {Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';
import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogClosedNotFoundReport,
  CatalogClosedNotFoundReports,
  CatalogNotFoundCloseAction,
  CatalogNotFoundCloseRequest,
  CatalogNotFoundQueue,
  CatalogNotFoundQueueParams,
  CatalogNotFoundQueueTarget,
  CatalogNotFoundSummary,
  CatalogNotFoundTargetKind,
} from '../../../core/catalog.models';
import {NotFoundCloseConfirmation} from './not-found-report-close-dialog.component';
import {toNotFoundCsv} from './not-found-export';

type NotFoundTab = 'open' | 'closed';
type NotFoundKindFilter = 'all' | CatalogNotFoundTargetKind;

@Component({
  selector: 'app-not-found-reports-view',
  standalone: false,
  templateUrl: './not-found-reports-view.component.html',
  styleUrls: ['./not-found-reports-view.component.scss'],
})
export class NotFoundReportsViewComponent implements OnChanges {
  @Input() accessToken = '';
  @Input() canManageEditions = false;
  @Input() canManageRareBooks = false;
  @Output() countsChanged = new EventEmitter<CatalogNotFoundSummary>();

  readonly activeTab = signal<NotFoundTab>('open');
  readonly queue = signal<CatalogNotFoundQueue | null>(null);
  readonly closedReports = signal<CatalogClosedNotFoundReports | null>(null);
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly kindFilter = signal<NotFoundKindFilter>('all');
  readonly sort = signal<CatalogNotFoundQueueParams['sort']>('most-reported');
  readonly queuePage = signal(1);
  readonly closedPage = signal(1);
  readonly closingTarget = signal<CatalogNotFoundQueueTarget | null>(null);
  readonly closingAction = signal<CatalogNotFoundCloseAction>('found');

  private closeOpener: HTMLElement | null = null;
  private readonly document = inject(DOCUMENT);

  constructor(private readonly api: CatalogAdminApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['accessToken'] && this.accessToken) {
      void this.loadQueue();
    }
  }

  async selectTab(tab: NotFoundTab): Promise<void> {
    this.activeTab.set(tab);
    if (tab === 'closed') {
      await this.loadClosedReports();
    } else {
      await this.loadQueue();
    }
  }

  async loadQueue(): Promise<void> {
    if (!this.accessToken) {
      return;
    }

      this.loading.set(true);
      this.error.set(null);
    try {
      const kind = !this.canManageEditions && this.canManageRareBooks
        ? 'rare'
        : this.kindFilter();
      const response = await firstValueFrom(this.api.getNotFoundQueue(this.accessToken, {
        kind,
        sort: this.sort(),
        page: this.queuePage(),
        pageSize: 50,
      }));
      this.queue.set(response);
      this.queuePage.set(response.page);
      this.countsChanged.emit({
        openTargetCount: response.openTargetCount,
        overdueTargetCount: response.overdueTargetCount,
        openReportCount: response.openReportCount,
        firstReportedAt: null,
        latestComment: null,
        withdrawalMovementIds: [],
      });
    } catch {
      this.error.set('La file des livres introuvables ne peut pas être chargée pour le moment.');
    } finally {
      this.loading.set(false);
    }
  }

  async loadClosedReports(): Promise<void> {
    if (!this.accessToken) {
      return;
    }

      this.loading.set(true);
      this.error.set(null);
    try {
      this.closedReports.set(await firstValueFrom(
        this.api.getClosedNotFoundReports(this.accessToken, this.closedPage(), 50),
      ));
    } catch {
      this.error.set("L'historique des livres introuvables ne peut pas être chargé pour le moment.");
    } finally {
      this.loading.set(false);
    }
  }

  setKindFilter(kind: NotFoundKindFilter): void {
    this.kindFilter.set(kind);
    this.queuePage.set(1);
    void this.loadQueue();
  }

  setSort(sort: CatalogNotFoundQueueParams['sort']): void {
    this.sort.set(sort);
    this.queuePage.set(1);
    void this.loadQueue();
  }

  changeQueuePage(page: number): void {
    const queue = this.queue();
    if (!queue || page < 1 || page > this.pageCount(queue)) {
      return;
    }
    this.queuePage.set(page);
    void this.loadQueue();
  }

  changeClosedPage(page: number): void {
    const history = this.closedReports();
    if (!history || page < 1 || page > this.pageCount(history)) {
      return;
    }
    this.closedPage.set(page);
    void this.loadClosedReports();
  }

  openCloseDialog(target: CatalogNotFoundQueueTarget, action: CatalogNotFoundCloseAction, event: Event): void {
    this.closeOpener = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;
    this.closingTarget.set(target);
    this.closingAction.set(action);
  }

  closeDialog(): void {
    this.closingTarget.set(null);
    this.closeOpener?.focus();
    this.closeOpener = null;
  }

  async confirmClose(confirmation: NotFoundCloseConfirmation): Promise<void> {
    const target = this.closingTarget();
    if (!target || !this.accessToken) {
      return;
    }

    const reference = target.kind === 'edition' ? target.isbn13 : target.rareBookId;
    if (!reference) {
      this.error.set('Cette fiche ne possède pas de référence exploitable.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      const result = await firstValueFrom(this.api.closeNotFound(
        this.accessToken,
        {kind: target.kind, reference},
        confirmation.action,
        confirmation.request as CatalogNotFoundCloseRequest,
      ));
      this.closingTarget.set(null);
      this.closeOpener = null;
      this.queuePage.set(1);
      this.success.set(`${result.closedReportCount} signalement${result.closedReportCount === 1 ? '' : 's'} clôturé${result.closedReportCount === 1 ? '' : 's'}.`);
      await this.loadQueue();
      if (this.activeTab() === 'closed') {
        await this.loadClosedReports();
      }
      this.document.querySelector<HTMLElement>('[data-testid="not-found-tab-open"]')?.focus();
    } catch {
      this.error.set('La clôture du signalement a échoué. Vérifiez la fiche puis réessayez.');
    } finally {
      this.loading.set(false);
    }
  }

  filteredTargets(): CatalogNotFoundQueueTarget[] {
    const items = this.queue()?.items ?? [];
    const kind = !this.canManageEditions && this.canManageRareBooks ? 'rare' : this.kindFilter();
    return kind === 'all' ? items : items.filter(item => item.kind === kind);
  }

  async exportCsv(): Promise<void> {
    const current = this.queue();
    if (!current || current.totalCount === 0 || !this.accessToken) {
      return;
    }

    this.exporting.set(true);
    try {
      const kind = !this.canManageEditions && this.canManageRareBooks ? 'rare' : this.kindFilter();
      const items = [...this.filteredTargets()];
      const pageCount = Math.ceil(current.totalCount / current.pageSize);
      for (let page = 1; page <= pageCount; page += 1) {
        if (page === current.page) {
          continue;
        }
        const response = await firstValueFrom(this.api.getNotFoundQueue(this.accessToken, {
          kind,
          sort: this.sort(),
          page,
          pageSize: current.pageSize,
        }));
        items.push(...response.items);
      }
      const blob = new Blob([`\uFEFF${toNotFoundCsv(items)}`], {type: 'text/csv;charset=utf-8'});
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `livres-introuvables-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 0);
    } catch {
      this.error.set("L'export de la liste de tournée a échoué. Réessayez.");
    } finally {
      this.exporting.set(false);
    }
  }

  outcomeLabel(outcome: CatalogClosedNotFoundReport['outcome']): string {
    switch (outcome) {
      case 'Withdrawn': return 'Retiré du stock';
      case 'Found': return 'Retrouvé';
      case 'Dismissed': return 'Sans suite';
      case 'Lapsed': return 'Caduc';
      default: return outcome;
    }
  }

  outcomeClass(outcome: string): string {
    return `not-found-outcome-${outcome.toLowerCase()}`;
  }

  formatDate(value: string, includeTime = false): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return 'Date inconnue';
    }
    return new Intl.DateTimeFormat('fr-FR', includeTime
      ? {day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit'}
      : {day: 'numeric', month: 'long', year: 'numeric'}).format(date);
  }

  daysWaiting(value: string): number {
    return Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000));
  }

  pageCount(page: {pageSize: number; totalCount: number}): number {
    return page.pageSize > 0 ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  }
}
