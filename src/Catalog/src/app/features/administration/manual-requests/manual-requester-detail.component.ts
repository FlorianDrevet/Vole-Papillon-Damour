import {DOCUMENT} from '@angular/common';
import {AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, OnChanges, Output, SimpleChanges, ViewChild, inject, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogManualRequestItem,
  CatalogManualRequester,
  CatalogManualRequesterWrite,
} from '../../../core/catalog.models';
import {formatManualRequesterPhone} from './manual-request-format';

const NEW_REQUESTER_KEY = 'nouveau';

@Component({
  selector: 'app-manual-requester-detail',
  standalone: false,
  templateUrl: './manual-requester-detail.component.html',
  styleUrls: ['./manual-requester-detail.component.scss'],
})
export class ManualRequesterDetailComponent implements OnChanges, AfterViewInit {
  @Input() accessToken = '';
  @Input() requesterId = NEW_REQUESTER_KEY;
  @Output() closed = new EventEmitter<void>();
  @Output() requesterCreated = new EventEmitter<string>();
  @Output() addBookRequested = new EventEmitter<string>();

  @ViewChild('deleteDialog') private deleteDialog?: ElementRef<HTMLElement>;
  @ViewChild('deleteTrigger') private deleteTrigger?: ElementRef<HTMLButtonElement>;

  readonly document = inject(DOCUMENT);
  readonly requester = signal<CatalogManualRequester | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly consentError = signal(false);
  readonly phoneError = signal(false);
  readonly deleteDialogOpen = signal(false);

  lastName = '';
  firstName = '';
  phone = '';
  consentGiven = false;

  private previousFocus: HTMLElement | null = null;

  constructor(private readonly api: CatalogAdminApiService) {}

  get isNew(): boolean {
    return this.requesterId === NEW_REQUESTER_KEY;
  }

  get displayName(): string {
    const firstName = this.firstName.trim();
    const lastName = this.lastName.trim().toLocaleUpperCase('fr-FR');
    return [firstName, lastName].filter(Boolean).join(' ') || 'Nouveau demandeur';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['requesterId'] || changes['accessToken']) {
      this.error.set(null);
      this.success.set(null);
      this.consentError.set(false);
      this.phoneError.set(false);
      if (this.isNew) {
        this.requester.set(null);
        this.lastName = '';
        this.firstName = '';
        this.phone = '';
        this.consentGiven = false;
      } else if (this.accessToken) {
        void this.loadRequester();
      }
    }
  }

  async loadRequester(): Promise<void> {
    if (this.isNew || !this.accessToken) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const requester = await firstValueFrom(this.api.getManualRequester(this.accessToken, this.requesterId));
      this.requester.set(requester);
      this.lastName = requester.lastName ?? '';
      this.firstName = requester.firstName ?? '';
      this.phone = requester.phone ?? '';
      this.consentGiven = true;
    } catch {
      this.error.set('Impossible de charger cette fiche demandeur. Vérifiez la connexion puis réessayez.');
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.saving() || !this.accessToken) {
      return;
    }

    this.error.set(null);
    this.success.set(null);
    this.consentError.set(this.isNew && !this.consentGiven);
    this.phoneError.set(!this.isValidPhone(this.phone));
    if (this.consentError() || this.phoneError() || !this.lastName.trim() || !this.firstName.trim()) {
      return;
    }

    this.saving.set(true);
    const request: CatalogManualRequesterWrite = {
      lastName: this.lastName.trim(),
      firstName: this.firstName.trim(),
      phone: this.phone.trim(),
    };
    try {
      if (this.isNew) {
        const created = await firstValueFrom(this.api.createManualRequester(this.accessToken, {
          ...request,
          consentGiven: this.consentGiven,
        }));
        this.requesterCreated.emit(created.requesterId);
        this.addBookRequested.emit(created.requesterId);
      } else {
        await firstValueFrom(this.api.updateManualRequester(this.accessToken, this.requesterId, request));
        this.success.set('Coordonnées enregistrées');
        await this.loadRequester();
      }
    } catch {
      this.error.set('Les coordonnées n’ont pas pu être enregistrées. Vérifiez la connexion puis réessayez.');
    } finally {
      this.saving.set(false);
    }
  }

  requestDelete(): void {
    this.previousFocus = this.document.activeElement instanceof HTMLElement ? this.document.activeElement : null;
    this.deleteDialogOpen.set(true);
    setTimeout(() => this.deleteDialog?.nativeElement.querySelector<HTMLElement>('button:not([disabled])')?.focus());
  }

  cancelDelete(): void {
    this.deleteDialogOpen.set(false);
    this.previousFocus?.focus();
    this.previousFocus = null;
  }

  async confirmDelete(): Promise<void> {
    if (this.deleting() || this.isNew || !this.accessToken) {
      return;
    }

    this.deleting.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.deleteManualRequester(this.accessToken, this.requesterId));
      this.deleteDialogOpen.set(false);
      this.closed.emit();
    } catch {
      this.error.set('Le demandeur n’a pas pu être supprimé. Vérifiez la connexion puis réessayez.');
    } finally {
      this.deleting.set(false);
    }
  }

  addBook(): void {
    if (!this.isNew && !this.requester()?.erased) {
      this.addBookRequested.emit(this.requesterId);
    }
  }

  async pick(item: CatalogManualRequestItem): Promise<void> {
    await this.runRequestAction(item, async () => {
      await firstValueFrom(this.api.pickManualRequest(this.accessToken, item.requestId));
      this.success.set('Exemplaire prélevé — demande trouvée');
    });
  }

  async markGiven(item: CatalogManualRequestItem): Promise<void> {
    await this.runRequestAction(item, async () => {
      await firstValueFrom(this.api.markManualRequestGiven(this.accessToken, item.requestId));
      this.success.set('Livre marqué comme donné');
    });
  }

  async refuse(item: CatalogManualRequestItem): Promise<void> {
    await this.runRequestAction(item, async () => {
      const result = await firstValueFrom(this.api.refuseManualRequest(this.accessToken, item.requestId, {keepSearching: false}));
      this.success.set(`Refus enregistré — ${result.notifiedMemberCount} membres prévenus`);
    });
  }

  async cancelRequest(item: CatalogManualRequestItem): Promise<void> {
    await this.runRequestAction(item, async () => {
      await firstValueFrom(this.api.cancelManualRequest(this.accessToken, item.requestId));
      this.success.set('Demande annulée');
    });
  }

  statusLabel(item: CatalogManualRequestItem): string {
    if (item.status === 'Found') {
      return 'Trouvé · à remettre';
    }
    if (item.status === 'Searching') {
      return 'En recherche';
    }
    if (item.status === 'Given') {
      return `Donné le ${this.formatDate(item.closedAt)}${item.closedByName ? ` · ${item.closedByName}` : ''}`;
    }
    if (item.status === 'Refused') {
      return `Refusé le ${this.formatDate(item.closedAt)}`;
    }
    return `Annulé le ${this.formatDate(item.closedAt)}`;
  }

  scopeLabel(item: CatalogManualRequestItem): string {
    return item.scope === 'Work' ? 'Toutes les éditions' : 'Cette édition';
  }

  requesterSummary(value: CatalogManualRequester): string {
    const count = value.requests.length;
    const found = value.requests.filter(request => request.status === 'Found').length;
    const author = value.consentRecordedByName || 'l’administration';
    return `Accord recueilli par ${author} le ${this.formatDate(value.consentRecordedAt)} · ${count} demande${count === 1 ? '' : 's'}, ${found} trouvée${found === 1 ? '' : 's'}`;
  }

  requestDeletionSummary(): string {
    const requests = this.requester()?.requests ?? [];
    const searchingCount = requests.filter(item => item.status === 'Searching').length;
    const found = requests.filter(item => item.status === 'Found');
    const clauses: string[] = [];
    if (searchingCount > 0) {
      clauses.push(`Ses ${searchingCount} demandes en recherche sont annulées.`);
    }
    if (found.length === 1) {
      clauses.push(`${found[0].title || 'Le livre'}, trouvé et non remis, rejoint le stock et les membres qui le suivent sont prévenus.`);
    } else if (found.length > 1) {
      clauses.push(`Ses ${found.length} livres trouvés et non remis rejoignent le stock et les membres qui les suivent sont prévenus.`);
    }
    clauses.push('Ses coordonnées sont effacées définitivement.');
    return clauses.join(' ');
  }

  formatPhone(value: string | null): string {
    return formatManualRequesterPhone(value);
  }

  formatDate(value: string | null): string {
    if (!value) {
      return 'date inconnue';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? 'date inconnue'
      : new Intl.DateTimeFormat('fr-FR', {day: 'numeric', month: 'short', year: 'numeric'}).format(date);
  }

  private async runRequestAction(item: CatalogManualRequestItem, action: () => Promise<void>): Promise<void> {
    if (!this.accessToken) {
      return;
    }
    this.error.set(null);
    this.success.set(null);
    try {
      await action();
      await this.loadRequester();
    } catch {
      this.error.set('Cette action n’a pas abouti. Vérifiez la connexion puis réessayez.');
    }
  }

  private isValidPhone(value: string): boolean {
    const normalized = value.trim().replace(/[\s().-]/g, '');
    return /^(?:0[1-9]\d{8}|\+[1-9]\d{7,14})$/.test(normalized);
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (!this.deleteDialogOpen()) {
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelDelete();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }

    const dialog = this.deleteDialog?.nativeElement;
    const focusable = dialog ? Array.from(dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
    )) : [];
    if (focusable.length === 0) {
      event.preventDefault();
      dialog?.focus();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!dialog?.contains(this.document.activeElement)) {
      event.preventDefault();
      (event.shiftKey ? last : first).focus();
    } else if (event.shiftKey && this.document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && this.document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  ngAfterViewInit(): void {
    if (this.deleteDialogOpen()) {
      this.deleteDialog?.nativeElement.querySelector<HTMLElement>('button:not([disabled])')?.focus();
    }
  }
}
