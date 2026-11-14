import {DOCUMENT} from '@angular/common';
import {
  AfterViewChecked,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Inject,
  Input,
  Output,
  ViewChild,
  signal,
} from '@angular/core';

import {LocalFavoriteEntry} from '../../../core/favorites/local-favorites.store';

@Component({
  selector: 'app-account-step-dialog',
  standalone: false,
  templateUrl: './account-step-dialog.component.html',
  styleUrls: ['./account-step-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountStepDialogComponent implements AfterViewChecked {
  @Input() entries: LocalFavoriteEntry[] = [];
  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly registerRequested = new EventEmitter<boolean>();

  @ViewChild('dialogPanel') private dialogPanel?: ElementRef<HTMLElement>;

  readonly fairDigestConsent = signal(false);
  private isOpen = false;
  private wasOpen = false;
  private previouslyFocused: HTMLElement | null = null;

  @Input()
  set open(value: boolean) {
    if (value && !this.isOpen) {
      this.fairDigestConsent.set(false);
    }
    this.isOpen = value;
  }

  get open(): boolean {
    return this.isOpen;
  }

  constructor(@Inject(DOCUMENT) private readonly document: Document) {}

  ngAfterViewChecked(): void {
    if (this.open && !this.wasOpen) {
      this.wasOpen = true;
      this.previouslyFocused = this.document.activeElement instanceof HTMLElement
        ? this.document.activeElement
        : null;
      queueMicrotask(() => this.dialogPanel?.nativeElement.focus());
    } else if (!this.open && this.wasOpen) {
      this.wasOpen = false;
      const previous = this.previouslyFocused;
      this.previouslyFocused = null;
      queueMicrotask(() => previous?.focus());
    }
  }

  setEmailConsent(event: Event): void {
    this.fairDigestConsent.set((event.target as HTMLInputElement).checked);
  }

  requestRegistration(): void {
    this.registerRequested.emit(this.fairDigestConsent());
  }

  close(): void {
    this.closed.emit();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }

    const panel = this.dialogPanel?.nativeElement;
    if (!panel) {
      return;
    }
    const focusable = Array.from(panel.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], input:not([disabled])',
    ));
    const first = focusable[0];
    const last = focusable.at(-1);
    if (!first || !last) {
      event.preventDefault();
      panel.focus();
    } else if (event.shiftKey && (this.document.activeElement === first || this.document.activeElement === panel)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (this.document.activeElement === last || this.document.activeElement === panel)) {
      event.preventDefault();
      first.focus();
    }
  }
}
