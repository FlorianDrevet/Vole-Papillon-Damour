import {HttpErrorResponse} from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  OnDestroy,
  Optional,
  ViewChild,
} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {firstValueFrom} from 'rxjs';

import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerHandle, CameraScannerService} from './camera-scanner.service';
import {normalizeIsbn} from './isbn.util';
import {
  LocalScanResult,
  PersistentStorageStatus,
  ScanSessionSnapshot,
} from '../offline/scan-offline.model';
import {ScanWorkflowService} from '../offline/scan-workflow.service';
import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanSyncService} from '../offline/scan-sync.service';

@Component({
  selector: 'app-scanner',
  templateUrl: './scanner.component.html',
  styleUrl: './scanner.component.scss',
  standalone: false,
})
export class ScannerComponent implements OnInit, OnDestroy {
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
  localScan: LocalScanResult | null = null;
  pendingCount = 0;
  isOnline = typeof navigator === 'undefined' || navigator.onLine;
  persistenceStatus: PersistentStorageStatus | null = null;
  storageError: string | null = null;
  session: ScanSessionSnapshot | null = null;
  authAvailable = false;
  isAuthenticated = false;
  accountName: string | null = null;
  syncStatus: 'idle' | 'syncing' | 'success' | 'error' = 'idle';
  syncError: string | null = null;

  private cameraHandle: CameraScannerHandle | null = null;
  private lookupVersion = 0;
  private scannerBuffer = '';
  private lastScannerKeyAt = 0;
  private localModeReady = false;
  private syncInProgress = false;
  private syncTimer: number | null = null;

  constructor(
    private readonly metadataService: BookMetadataService,
    private readonly cameraScanner: CameraScannerService,
    private readonly changeDetector: ChangeDetectorRef,
    private readonly destroyRef: DestroyRef,
    @Optional() private readonly scanWorkflow: ScanWorkflowService | null,
    @Optional() private readonly scanAuth: ScanAuthService | null,
    @Optional() private readonly scanSync: ScanSyncService | null,
  ) {}

  ngOnInit(): void {
    this.authAvailable = this.scanAuth !== null;
    if (this.scanAuth) {
      this.scanAuth.account$
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(account => {
          this.isAuthenticated = account !== null;
          this.accountName = this.scanAuth?.displayName ?? null;
          this.refreshView();
          this.trySync();
        });
    }

    if (this.scanWorkflow) {
      void this.initializeLocalMode();
    }

    if (typeof window !== 'undefined') {
      this.syncTimer = window.setInterval(() => this.trySync(), 60_000);
    }
  }

  async submit(): Promise<void> {
    await this.lookup(this.isbnInput);
  }

  async lookup(rawInput: string): Promise<void> {
    const normalizedIsbn = normalizeIsbn(rawInput);
    const lookupVersion = ++this.lookupVersion;
    this.metadata = null;
    this.coverUrl = null;
    this.localScan = null;
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

    const metadataPromise = firstValueFrom(this.metadataService.getMetadata(normalizedIsbn));
    const localScanPromise = this.scanWorkflow
      ? this.scanWorkflow.recordScan(normalizedIsbn)
      : Promise.resolve(null);

    try {
      const localScan = await localScanPromise;
      if (lookupVersion === this.lookupVersion && localScan) {
        this.localScan = localScan;
        await this.refreshLocalState();
      }
    } catch {
      if (lookupVersion === this.lookupVersion) {
        this.storageError = 'Le geste n’a pas pu être conservé localement. Vérifiez le stockage du navigateur.';
        this.refreshView();
      }
    }

    try {
      const metadata = await metadataPromise;
      if (lookupVersion !== this.lookupVersion) {
        return;
      }

      this.metadata = metadata;
      this.coverUrl = metadata.coverUrl;
      this.isLoading = false;
      if (this.scanWorkflow) {
        try {
          await this.scanWorkflow.cacheMetadata(metadata);
        } catch {
          // Bibliographic metadata is best-effort; the durable scan remains in the outbox.
        }
      }
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

  async keepCurrentScan(): Promise<void> {
    await this.decideCurrentScan(true);
  }

  async rejectCurrentScan(): Promise<void> {
    await this.decideCurrentScan(false);
  }

  login(): void {
    this.scanAuth?.login();
  }

  logout(): void {
    this.scanAuth?.logout();
  }

  async syncNow(): Promise<void> {
    if (
      !this.scanSync ||
      !this.isAuthenticated ||
      !this.isOnline ||
      !this.localModeReady ||
      this.syncInProgress
    ) {
      return;
    }

    this.syncInProgress = true;
    this.syncStatus = 'syncing';
    this.syncError = null;
    this.refreshView();

    try {
      const summary = await this.scanSync.syncAll();
      if (!summary.catalog) {
        this.syncStatus = 'error';
        this.syncError = 'Le compte est connecté, mais le catalogue n’a pas pu être synchronisé (droits Tri ou réseau).';
      } else if (summary.outbox.stoppedOnError) {
        this.syncStatus = 'error';
        this.syncError = 'La file locale reste conservée et sera réessayée automatiquement.';
      } else {
        this.syncStatus = 'success';
      }
      await this.refreshLocalState();
    } catch {
      this.syncStatus = 'error';
      this.syncError = 'La synchronisation a échoué ; les gestes restent conservés localement.';
    } finally {
      this.syncInProgress = false;
      this.refreshView();
    }
  }

  get syncLabel(): string {
    switch (this.syncStatus) {
      case 'syncing':
        return 'Synchronisation…';
      case 'success':
        return 'Catalogue synchronisé';
      case 'error':
        return 'Synchronisation à reprendre';
      default:
        return 'Synchroniser';
    }
  }

  getVerdictLabel(): string {
    switch (this.localScan?.verdict.verdict) {
      case 'Wanted':
        return 'Demandé par un membre';
      case 'Selling':
        return 'Se vend bien';
      case 'TooMany':
        return 'Déjà très présent';
      case 'FirstCopy':
        return 'Premier exemplaire connu';
      default:
        return 'Verdict en attente';
    }
  }

  getVerdictDescription(): string {
    if (!this.localScan) {
      return '';
    }

    if (!this.localScan.verdict.isKnown) {
      return 'Cet ISBN n’est pas dans la dernière copie locale du catalogue. Il part tout de même dans la file durable.';
    }

    return `${this.localScan.verdict.totalKnownQuantity} exemplaire(s) connu(s), ${this.localScan.verdict.salesCount} vente(s).`;
  }

  @HostListener('window:online')
  onNetworkOnline(): void {
    this.isOnline = true;
    this.refreshView();
    this.trySync();
  }

  @HostListener('window:offline')
  onNetworkOffline(): void {
    this.isOnline = false;
    this.refreshView();
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
    if (this.syncTimer !== null && typeof window !== 'undefined') {
      window.clearInterval(this.syncTimer);
    }
  }

  private async initializeLocalMode(): Promise<void> {
    try {
      this.persistenceStatus = await this.scanWorkflow!.initialize();
      this.localScan = await this.scanWorkflow!.getLatestPendingResult();
      this.session = await this.scanWorkflow!.getSession();
      await this.refreshLocalState();
      this.localModeReady = true;

      if (!this.persistenceStatus.available) {
        this.storageError = 'Ce navigateur ne fournit pas IndexedDB : le tri hors ligne est indisponible.';
      } else if (!this.persistenceStatus.persisted) {
        this.storageError = 'Le navigateur n’a pas garanti la conservation des données hors ligne. Gardez l’application régulièrement connectée.';
      }
      this.trySync();
      this.refreshView();
    } catch {
      this.storageError = 'Le stockage local ne peut pas être initialisé. Aucun geste ne sera considéré comme conservé.';
      this.refreshView();
    }
  }

  private async decideCurrentScan(kept: boolean): Promise<void> {
    if (!this.scanWorkflow || !this.localScan || this.localScan.entry.status !== 'Pending') {
      return;
    }

    try {
      this.localScan.entry = await this.scanWorkflow.decide(
        this.localScan.entry.clientGestureId,
        kept,
      );
      await this.refreshLocalState();
      this.trySync();
      this.refreshView();
    } catch {
      this.storageError = 'La décision n’a pas pu être conservée localement.';
      this.refreshView();
    }
  }

  private async refreshLocalState(): Promise<void> {
    if (!this.scanWorkflow) {
      return;
    }

    this.pendingCount = await this.scanWorkflow.getPendingCount();
    this.session = await this.scanWorkflow.getSession();
  }

  private stopCamera(): void {
    this.cameraHandle?.stop();
    this.cameraHandle = null;
    this.cameraActive = false;
  }

  private refreshView(): void {
    this.changeDetector.markForCheck();
  }

  private trySync(): void {
    void this.syncNow();
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
