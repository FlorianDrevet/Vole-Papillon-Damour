import {DOCUMENT} from '@angular/common';
import {AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject} from '@angular/core';
import {
  CatalogNotFoundCloseAction,
  CatalogNotFoundCloseRequest,
  CatalogNotFoundQueueTarget,
  CatalogNotFoundWithdrawalReason,
} from '../../../core/catalog.models';

export interface NotFoundCloseConfirmation {
  action: CatalogNotFoundCloseAction;
  request: CatalogNotFoundCloseRequest;
}

@Component({
  selector: 'app-not-found-report-close-dialog',
  standalone: false,
  templateUrl: './not-found-report-close-dialog.component.html',
  styleUrls: ['./not-found-report-close-dialog.component.scss'],
})
export class NotFoundReportCloseDialogComponent implements AfterViewInit {
  @Input() target: CatalogNotFoundQueueTarget | null = null;
  @Input() action: CatalogNotFoundCloseAction = 'found';
  @Output() confirmed = new EventEmitter<NotFoundCloseConfirmation>();
  @Output() closed = new EventEmitter<void>();

  @ViewChild('dialog') private dialog?: ElementRef<HTMLElement>;

  readonly document = inject(DOCUMENT);
  foundCopies = 0;
  reason: CatalogNotFoundWithdrawalReason = 'NotFoundOnShelf';
  note = '';

  get isRare(): boolean {
    return this.target?.kind === 'rare';
  }

  withdrawalQuantity(): number {
    if (!this.target || this.isRare) {
      return 0;
    }

    return Math.max(0, this.target.quantityAvailable - this.clampedFoundCopies());
  }

  effectiveAction(): CatalogNotFoundCloseAction {
    return this.action === 'withdrawal' && !this.isRare && this.withdrawalQuantity() === 0
      ? 'found'
      : this.action;
  }

  isValid(): boolean {
    return this.note.trim().length > 0 && (this.action !== 'withdrawal' || this.isRare || this.target !== null);
  }

  submit(): void {
    if (!this.isValid() || !this.target) {
      return;
    }

    const action = this.effectiveAction();
    const note = this.note.trim();
    const request: CatalogNotFoundCloseRequest = action === 'withdrawal' && this.isRare
      ? {reason: this.reason, note}
      : action === 'withdrawal'
        ? {quantityFound: this.clampedFoundCopies(), reason: this.reason, note}
        : action === 'dismissal'
          ? {reason: this.reason, note}
          : {note};
    this.confirmed.emit({action, request});
  }

  cancel(): void {
    this.closed.emit();
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
    if (!dialog) {
      return;
    }
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])',
    ));
    if (focusable.length === 0) {
      event.preventDefault();
      dialog.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!dialog.contains(this.document.activeElement)) {
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
    const firstControl = this.dialog?.nativeElement.querySelector<HTMLElement>(
      'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled])',
    );
    firstControl?.focus();
  }

  private clampedFoundCopies(): number {
    const stock = this.target?.quantityAvailable ?? 0;
    const value = Number(this.foundCopies);
    return Number.isFinite(value) ? Math.min(stock, Math.max(0, Math.trunc(value))) : 0;
  }
}
