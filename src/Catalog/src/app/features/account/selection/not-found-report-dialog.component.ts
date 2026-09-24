import {AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, HostListener, input, OnDestroy, output, signal, ViewChild} from '@angular/core';

import {CatalogNotFoundLocation} from '../../../core/catalog.models';

export type NotFoundReportDialogMode = 'report' | 'login';

export interface NotFoundReportDialogItem {
  title: string;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  coverUrl: string | null;
}

export interface NotFoundReportSubmission {
  location: CatalogNotFoundLocation | null;
  comment: string | null;
}

@Component({
  selector: 'app-not-found-report-dialog',
  standalone: false,
  templateUrl: './not-found-report-dialog.component.html',
  styleUrls: ['./not-found-report-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundReportDialogComponent implements AfterViewInit, OnDestroy {
  readonly item = input.required<NotFoundReportDialogItem>();
  readonly mode = input<NotFoundReportDialogMode>('report');
  readonly sending = input(false);
  readonly submitted = output<NotFoundReportSubmission>();
  readonly closed = output<void>();
  readonly loginRequested = output<void>();

  readonly location = signal<CatalogNotFoundLocation | null>(null);
  readonly comment = signal('');

  @ViewChild('dialog') private dialog?: ElementRef<HTMLElement>;

  private previousFocus: HTMLElement | null = null;
  private focusRestored = false;

  ngAfterViewInit(): void {
    const document = this.dialog?.nativeElement.ownerDocument;
    this.previousFocus = document?.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.focusFirstControl();
  }

  metadata(): string {
    const item = this.item();
    return [item.authors, item.publisher, item.publicationYear]
      .filter(value => value !== null && value !== undefined && value !== '')
      .join(' · ');
  }

  updateComment(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    const value = textarea.value.slice(0, 280);
    if (textarea.value !== value) {
      textarea.value = value;
    }
    this.comment.set(value);
  }

  selectLocation(location: CatalogNotFoundLocation): void {
    this.location.set(location);
  }

  submit(): void {
    if (this.sending()) {
      return;
    }

    const comment = this.comment().trim();
    this.submitted.emit({location: this.location(), comment: comment || null});
  }

  requestClose(): void {
    this.closed.emit();
    this.restoreFocus();
  }

  requestLogin(): void {
    this.loginRequested.emit();
  }

  ngOnDestroy(): void {
    this.restoreFocus();
  }

  @HostListener('document:keydown', ['$event'])
  handleDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.requestClose();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const focusable = this.focusableControls();
    if (focusable.length === 0) {
      event.preventDefault();
      this.dialog?.nativeElement.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = this.dialog?.nativeElement.ownerDocument.activeElement;
    if (event.shiftKey && (active === first || !focusable.includes(active as HTMLElement))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !focusable.includes(active as HTMLElement))) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusFirstControl(): void {
    this.focusableControls()[0]?.focus();
  }

  private focusableControls(): HTMLElement[] {
    return Array.from(this.dialog?.nativeElement.querySelectorAll<HTMLElement>(
      'button:not(:disabled), input:not(:disabled), textarea:not(:disabled), select:not(:disabled), a[href], [tabindex]:not([tabindex="-1"])',
    ) ?? []);
  }

  private restoreFocus(): void {
    if (!this.focusRestored && this.previousFocus?.isConnected) {
      this.previousFocus.focus();
      this.focusRestored = true;
    }
  }
}
