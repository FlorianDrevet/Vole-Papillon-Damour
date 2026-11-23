import {HttpErrorResponse} from '@angular/common/http';
import {ChangeDetectionStrategy, Component, ElementRef, Inject, OnDestroy, Optional, ViewChild, signal} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {Router} from '@angular/router';
import {firstValueFrom} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanApiService} from '../offline/scan-api.service';
import {ScanPublicCatalogBook} from '../offline/scan-offline.model';
import {CameraScannerHandle, CameraScannerService} from './camera-scanner.service';
import {normalizeIsbn} from './isbn.util';
import {LABEL_PRINTER, LabelPrinter} from '../label-printer/label-printer';

type BookLookupState = 'idle' | 'invalid' | 'loading' | 'found' | 'not-found' | 'unavailable';

@Component({
  selector: 'app-book-lookup-page',
  templateUrl: './book-lookup-page.component.html',
  styleUrl: './book-lookup-page.component.scss',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookLookupPageComponent implements OnDestroy {
  @ViewChild('cameraContainer', {static: true})
  private readonly cameraContainer!: ElementRef<HTMLElement>;

  readonly isbn = signal('');
  readonly lookupState = signal<BookLookupState>('idle');
  readonly book = signal<ScanPublicCatalogBook | null>(null);
  readonly nextFairLabel = signal<string | null>(null);
  readonly cameraActive = signal(false);
  readonly cameraStarting = signal(false);
  readonly cameraError = signal<string | null>(null);
  readonly reprintBusy = signal(false);
  readonly reprintStatus = signal<string | null>(null);
  readonly reprintStatusIsError = signal(false);

  private cameraHandle: CameraScannerHandle | null = null;
  private lookupVersion = 0;
  private destroyed = false;

  constructor(
    private readonly scanApi: ScanApiService,
    private readonly cameraScanner: CameraScannerService,
    private readonly router: Router,
    private readonly auth: ScanAuthService,
    @Optional() @Inject(LABEL_PRINTER) private readonly printer: LabelPrinter | null = null,
  ) {}

  get canReprintLabel(): boolean {
    return this.auth.canSort || this.auth.canManageRareBooks;
  }

  returnHome(): void {
    void this.router.navigateByUrl('/accueil');
  }

  async reprintLabel(): Promise<void> {
    const book = this.book();
    if (!book || !this.canReprintLabel || this.reprintBusy()) {
      return;
    }

    const printer = this.printer;
    if (!printer || printer.status() !== 'connected') {
      void this.router.navigateByUrl(`/etiquette?isbn=${encodeURIComponent(book.isbn13)}`);
      return;
    }

    this.reprintBusy.set(true);
    this.reprintStatusIsError.set(false);
    this.reprintStatus.set('Impression de l’étiquette…');

    try {
      await printer.print({code: book.isbn13, price: null, copies: 1});
      this.reprintStatus.set('Étiquette réimprimée.');
    } catch (error) {
      this.reprintStatus.set(error instanceof Error ? error.message : 'Impression impossible.');
      this.reprintStatusIsError.set(true);
    } finally {
      this.reprintBusy.set(false);
    }
  }

  onIsbnChanged(value: string): void {
    this.isbn.set(value);
    this.lookupVersion += 1;
    this.lookupState.set('idle');
    this.book.set(null);
    this.nextFairLabel.set(null);
    this.cameraError.set(null);
    this.reprintStatus.set(null);
    this.reprintStatusIsError.set(false);
  }

  async search(rawValue = this.isbn()): Promise<void> {
    const isbn13 = normalizeIsbn(rawValue);
    const lookupVersion = ++this.lookupVersion;
    this.book.set(null);
    this.nextFairLabel.set(null);
    this.cameraError.set(null);

    if (!isbn13) {
      this.lookupState.set('invalid');
      await this.stopCamera();
      return;
    }

    this.isbn.set(isbn13);
    this.lookupState.set('loading');
    await this.stopCamera();

    try {
      const book = await firstValueFrom(this.scanApi.getPublicCatalogBook(isbn13));
      if (this.destroyed || lookupVersion !== this.lookupVersion) {
        return;
      }

      this.book.set(book);
      this.nextFairLabel.set(book.nextFairAt ? formatFairDate(book.nextFairAt) : null);
      this.lookupState.set('found');
    } catch (error: unknown) {
      if (this.destroyed || lookupVersion !== this.lookupVersion) {
        return;
      }

      this.lookupState.set(error instanceof HttpErrorResponse && error.status === 404
        ? 'not-found'
        : 'unavailable');
    }
  }

  async startCamera(): Promise<void> {
    if (this.cameraStarting() || this.cameraActive() || this.lookupState() === 'loading') {
      return;
    }

    this.cameraError.set(null);
    this.cameraActive.set(true);
    this.cameraStarting.set(true);

    try {
      const handle = await this.cameraScanner.start(
        this.cameraContainer.nativeElement,
        isbn => {
          void this.search(isbn);
        },
        error => {
          this.cameraError.set(error.message);
          this.cameraStarting.set(false);
          void this.stopCamera();
        },
      );
      this.cameraHandle = handle;
      this.cameraStarting.set(false);
      if (!this.cameraActive()) {
        await this.stopCamera();
      }
    } catch (error: unknown) {
      this.cameraError.set(error instanceof Error
        ? error.message
        : 'La caméra ne peut pas être ouverte.');
      this.cameraStarting.set(false);
      await this.stopCamera();
    }
  }

  async closeCamera(): Promise<void> {
    this.cameraError.set(null);
    await this.stopCamera();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.lookupVersion += 1;
    void this.stopCamera();
  }

  private async stopCamera(): Promise<void> {
    const handle = this.cameraHandle;
    this.cameraHandle = null;
    this.cameraActive.set(false);
    this.cameraStarting.set(false);
    await handle?.stop();
  }
}

function formatFairDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'date à préciser';
  }

  return new Intl.DateTimeFormat('fr-FR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: 'Europe/Paris',
  }).format(date);
}
