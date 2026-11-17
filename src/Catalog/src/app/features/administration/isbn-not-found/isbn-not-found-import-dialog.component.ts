import {DOCUMENT} from '@angular/common';
import {ChangeDetectionStrategy, Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, inject, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogAdminIsbnNotFoundImportPreview,
  CatalogAdminIsbnNotFoundImportRejection,
  CatalogAdminIsbnNotFoundImportResult,
} from '../../../core/catalog.models';

type ImportDialogState = 'drop' | 'previewing' | 'preview' | 'file-error' | 'saving' | 'result' | 'save-error';

@Component({
  selector: 'app-isbn-not-found-import-dialog',
  standalone: false,
  templateUrl: './isbn-not-found-import-dialog.component.html',
  styleUrls: ['./isbn-not-found-import-dialog.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IsbnNotFoundImportDialogComponent {
  @Input() accessToken = '';
  @Output() closed = new EventEmitter<void>();
  @Output() importCompleted = new EventEmitter<CatalogAdminIsbnNotFoundImportResult>();

  readonly state = signal<ImportDialogState>('drop');
  readonly selectedFile = signal<File | null>(null);
  readonly preview = signal<CatalogAdminIsbnNotFoundImportPreview | null>(null);
  readonly result = signal<CatalogAdminIsbnNotFoundImportResult | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly dragging = signal(false);
  readonly showAllRejections = signal(false);

  @ViewChild('filePicker') private filePicker?: ElementRef<HTMLInputElement>;

  private requestVersion = 0;
  private readonly api = inject(CatalogAdminApiService);
  private readonly document = inject(DOCUMENT);

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancel();
  }

  async selectFile(file: File | null): Promise<void> {
    if (!file || !this.accessToken || this.state() === 'saving') return;

    const version = ++this.requestVersion;
    this.selectedFile.set(file);
    this.preview.set(null);
    this.result.set(null);
    this.errorMessage.set(null);
    this.showAllRejections.set(false);
    this.state.set('previewing');

    try {
      const preview = await firstValueFrom(this.api.previewIsbnNotFoundImport(this.accessToken, file));
      if (version === this.requestVersion) {
        this.preview.set(preview);
        this.state.set('preview');
      }
    } catch (error) {
      if (version === this.requestVersion) {
        this.errorMessage.set(this.describeFileError(error));
        this.state.set('file-error');
      }
    }
  }

  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;
    input.value = '';
    void this.selectFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.state() === 'drop' || this.state() === 'file-error') this.dragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files.item(0) ?? null;
    void this.selectFile(file);
  }

  chooseAnotherFile(): void {
    if (this.state() === 'saving') return;
    if (this.filePicker) {
      this.filePicker.nativeElement.click();
    } else {
      this.state.set('drop');
    }
  }

  async saveImport(): Promise<void> {
    const file = this.selectedFile();
    const preview = this.preview();
    if (!file || !preview || preview.readyCount <= 0 || !this.accessToken || this.state() !== 'preview') return;

    const version = ++this.requestVersion;
    this.errorMessage.set(null);
    this.state.set('saving');
    try {
      const result = await firstValueFrom(this.api.importIsbnNotFound(
        this.accessToken,
        file,
        preview.readyIsbns,
      ));
      if (version === this.requestVersion) {
        this.result.set(result);
        this.state.set('result');
        this.importCompleted.emit(result);
      }
    } catch (error) {
      if (version === this.requestVersion) {
        this.errorMessage.set(this.describeFileError(error));
        this.state.set('save-error');
      }
    }
  }

  cancel(): void {
    if (this.state() === 'saving') return;
    this.requestVersion++;
    this.closed.emit();
  }

  toggleRejectionDetails(): void {
    this.showAllRejections.update(show => !show);
  }

  visibleRejections(rejections: CatalogAdminIsbnNotFoundImportRejection[]): CatalogAdminIsbnNotFoundImportRejection[] {
    return this.showAllRejections() ? rejections : rejections.slice(0, 6);
  }

  remainingRejectionCount(rejections: CatalogAdminIsbnNotFoundImportRejection[]): number {
    return this.showAllRejections() ? 0 : Math.max(0, rejections.length - 6);
  }

  completedCount(result: CatalogAdminIsbnNotFoundImportResult): number {
    return Math.max(0, result.importedCount - result.withoutAuthorCount);
  }

  ignoredCount(dataRowCount: number, importedCount: number, rejectedCount: number): number {
    return Math.max(0, dataRowCount - importedCount - rejectedCount);
  }

  closeResult(): void {
    this.closed.emit();
  }

  private describeFileError(error: unknown): string {
    if (!error || typeof error !== 'object') {
      return 'Le fichier ne peut pas être lu. Vérifiez le format CSV et réessayez.';
    }

    const response = error as {error?: unknown; message?: unknown};
    if (typeof response.error === 'string' && response.error.trim()) return response.error;
    if (response.error && typeof response.error === 'object') {
      const problem = response.error as {detail?: unknown; title?: unknown; errors?: unknown};
      if (typeof problem.detail === 'string' && problem.detail.trim()) return problem.detail;
      if (typeof problem.title === 'string' && problem.title.trim() && !problem.errors) return problem.title;
      if (problem.errors && typeof problem.errors === 'object') {
        const messages = Object.values(problem.errors as Record<string, unknown>)
          .flatMap(value => Array.isArray(value) ? value : [value])
          .filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
        if (messages.length > 0) return messages.join(' ');
      }
    }
    if (typeof response.message === 'string' && response.message.trim()) return response.message;
    return 'Le fichier ne peut pas être lu. Vérifiez le format CSV et réessayez.';
  }
}
