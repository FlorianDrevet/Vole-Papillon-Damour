import {DOCUMENT} from '@angular/common';
import {AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, OnDestroy, Output, ViewChild, inject, signal} from '@angular/core';

import {CatalogManualRequestItem} from '../../../core/catalog.models';

export type ManualRequestDecisionMode = 'given' | 'refuse' | 'cancel-found';

@Component({
  selector: 'app-manual-request-decision-dialog',
  standalone: false,
  templateUrl: './manual-request-decision-dialog.component.html',
  styleUrls: ['./manual-request-decision-dialog.component.scss'],
})
export class ManualRequestDecisionDialogComponent implements AfterViewInit, OnDestroy {
  @Input() mode: ManualRequestDecisionMode = 'given';
  @Input() item: CatalogManualRequestItem | null = null;
  @Input() requesterFirstName = '';
  @Input() requesterLastName = '';
  @Input() pending = false;
  @Output() confirmed = new EventEmitter<{keepSearching: boolean}>();
  @Output() closed = new EventEmitter<void>();

  @ViewChild('dialog') private dialog?: ElementRef<HTMLElement>;

  readonly document = inject(DOCUMENT);
  readonly keepSearching = signal(false);

  private previousFocus: HTMLElement | null = null;

  get requesterName(): string {
    return `${this.requesterFirstName.trim()} ${this.requesterLastName.trim().toLocaleUpperCase('fr-FR')}`.trim();
  }

  get title(): string {
    return this.item?.title || 'Ce livre';
  }

  foundEditionLabel(): string {
    if (!this.item) {
      return 'Édition trouvée';
    }
    const edition = [this.item.foundPublisher || this.item.publisher, this.item.foundPublicationYear]
      .filter(Boolean)
      .join(' · ');
    return [this.item.foundIsbn13 || this.item.isbn13, edition].filter(Boolean).join(' · ') || 'Édition trouvée';
  }

  foundOriginLabel(): string {
    if (!this.item) {
      return 'Trouvé';
    }
    const date = this.formatDate(this.item.foundAt);
    const origin = this.item.foundOrigin === 'Scan' ? 'Trouvé au tri' : 'Prélevé en stock';
    return `${origin} · ${date}`;
  }

  watcherWarning(): string {
    const count = this.item?.watcherCount ?? 0;
    if (count === 0) {
      return 'Le livre rejoint le stock. Aucun membre ne le suit.';
    }
    return count === 1
      ? 'Le livre rejoint le stock et 1 membre qui le suit sera prévenu par e-mail dès maintenant.'
      : `Le livre rejoint le stock et ${count} membres qui le suivent seront prévenus par e-mail dès maintenant.`;
  }

  releaseModeLabel(): string {
    if (this.item?.releaseMode === 'NextFair') {
      return `Annoncé · bourse du ${this.formatDate(this.item.releaseFairStart, false)}`;
    }
    return 'Disponible maintenant';
  }

  releaseModeHelp(): string {
    return `Mode de la session de scan du ${this.formatDate(this.item?.foundAt)}. Non modifiable.`;
  }

  cancelFoundSummary(): string {
    const count = this.item?.watcherCount ?? 0;
    if (count === 0) {
      return 'Le livre a déjà été mis de côté. Il rejoindra le stock. Aucun membre ne le suit.';
    }
    return count === 1
      ? 'Le livre a déjà été mis de côté. Il rejoindra le stock et 1 membre qui le suit sera prévenu dès maintenant.'
      : `Le livre a déjà été mis de côté. Il rejoindra le stock et ${count} membres qui le suivent seront prévenus dès maintenant.`;
  }

  submit(): void {
    if (!this.pending) {
      this.confirmed.emit({keepSearching: this.mode === 'refuse' && this.keepSearching()});
    }
  }

  cancel(): void {
    if (!this.pending) {
      this.closed.emit();
    }
  }

  formatDate(value: string | null | undefined, year = true): string {
    if (!value) {
      return 'date inconnue';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? 'date inconnue'
      : new Intl.DateTimeFormat('fr-FR', {
        day: 'numeric',
        month: 'short',
        ...(year ? {year: 'numeric' as const} : {}),
      }).format(date);
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancel();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }

    const dialog = this.dialog?.nativeElement;
    const focusable = dialog ? Array.from(dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
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
    this.previousFocus = this.document.activeElement instanceof HTMLElement ? this.document.activeElement : null;
    const firstControl = this.dialog?.nativeElement.querySelector<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
    );
    firstControl?.focus();
  }

  ngOnDestroy(): void {
    const previousFocus = this.previousFocus;
    if (previousFocus?.isConnected) {
      setTimeout(() => previousFocus.focus());
    }
  }
}
