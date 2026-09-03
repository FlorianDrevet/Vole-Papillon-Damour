import {HttpErrorResponse} from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerHandle, CameraScannerService} from './camera-scanner.service';
import {normalizeIsbn} from './isbn.util';

@Component({
  selector: 'app-scanner',
  templateUrl: './scanner.component.html',
  styleUrl: './scanner.component.scss',
  standalone: false,
})
export class ScannerComponent implements OnDestroy {
  private static readonly openLibraryCoverUrlTemplate =
    'https://covers.openlibrary.org/b/isbn/{isbn13}-L.jpg?default=false';

  @ViewChild('cameraContainer', {static: true}) private readonly cameraContainer!: ElementRef<HTMLElement>;

  isbnInput = '';
  metadata: BookMetadata | null = null;
  coverUrl: string | null = null;
  errorMessage: string | null = null;
  cameraError: string | null = null;
  isLoading = false;
  cameraActive = false;

  private cameraHandle: CameraScannerHandle | null = null;
  private lookupVersion = 0;
  private scannerBuffer = '';
  private lastScannerKeyAt = 0;

  constructor(
    private readonly metadataService: BookMetadataService,
    private readonly cameraScanner: CameraScannerService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  async submit(): Promise<void> {
    await this.lookup(this.isbnInput);
  }

  async lookup(rawInput: string): Promise<void> {
    const normalizedIsbn = normalizeIsbn(rawInput);
    const lookupVersion = ++this.lookupVersion;
    this.metadata = null;
    this.coverUrl = null;
    this.errorMessage = null;
    this.refreshView();

    if (!normalizedIsbn) {
      this.isLoading = false;
      this.errorMessage = 'Saisissez un ISBN-10 ou ISBN-13 valide.';
      this.refreshView();
      return;
    }

    this.isbnInput = normalizedIsbn;
    this.isLoading = true;
    this.refreshView();

    try {
      const metadata = await firstValueFrom(this.metadataService.getMetadata(normalizedIsbn));
      if (lookupVersion !== this.lookupVersion) {
        return;
      }

      this.metadata = metadata;
      this.coverUrl = metadata.coverUrl;
      this.isLoading = false;
      this.refreshView();
    } catch (error: unknown) {
      if (lookupVersion !== this.lookupVersion) {
        return;
      }

      this.isLoading = false;
      this.errorMessage = error instanceof HttpErrorResponse && error.status === 404
        ? 'Aucune notice bibliographique trouvée pour cet ISBN.'
        : 'La notice ne peut pas être chargée pour le moment.';
      this.refreshView();
    }
  }

  onCoverError(): void {
    if (!this.metadata) {
      return;
    }

    const fallbackCoverUrl = ScannerComponent.openLibraryCoverUrlTemplate.replace(
      '{isbn13}',
      encodeURIComponent(this.metadata.isbn13),
    );

    this.coverUrl = this.coverUrl === fallbackCoverUrl
      ? null
      : fallbackCoverUrl;
    this.refreshView();
  }

  async toggleCamera(): Promise<void> {
    if (this.cameraHandle) {
      this.stopCamera();
      return;
    }

    this.cameraError = null;
    this.cameraActive = true;
    this.refreshView();

    try {
      this.cameraHandle = await this.cameraScanner.start(
        this.cameraContainer.nativeElement,
        rawValue => {
          this.stopCamera();
          void this.lookup(rawValue);
        },
      );
    } catch (error: unknown) {
      this.cameraActive = false;
      this.cameraError = error instanceof Error
        ? error.message
        : 'La caméra ne peut pas être activée.';
      this.refreshView();
    }
  }

  async scanImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const imageFile = input.files?.[0];
    input.value = '';

    if (!imageFile) {
      return;
    }

    this.stopCamera();
    this.cameraError = null;
    this.refreshView();

    try {
      const rawValue = await this.cameraScanner.scanFile(imageFile);
      await this.lookup(rawValue);
    } catch {
      this.cameraError = 'Aucun code-barres lisible n’a été trouvé dans cette photo.';
      this.refreshView();
    }
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (this.isEditableTarget(event.target)) {
      return;
    }

    const now = Date.now();
    if (now - this.lastScannerKeyAt > 120) {
      this.scannerBuffer = '';
    }

    if (event.key === 'Enter') {
      const scannedValue = this.scannerBuffer;
      this.scannerBuffer = '';
      if (scannedValue) {
        void this.lookup(scannedValue);
      }
      return;
    }

    if (/^[0-9Xx-]$/.test(event.key)) {
      this.scannerBuffer += event.key;
      this.lastScannerKeyAt = now;
    }
  }

  ngOnDestroy(): void {
    this.stopCamera();
  }

  private stopCamera(): void {
    this.cameraHandle?.stop();
    this.cameraHandle = null;
    this.cameraActive = false;
  }

  private refreshView(): void {
    this.changeDetector.markForCheck();
  }

  private isEditableTarget(target: EventTarget | null): boolean {
    return target instanceof HTMLElement && (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.tagName === 'BUTTON' ||
      target.isContentEditable
    );
  }
}
