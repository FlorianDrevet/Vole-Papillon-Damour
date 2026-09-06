import {HttpErrorResponse} from '@angular/common/http';
import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  Optional,
  ViewChild,
} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {firstValueFrom} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {
  LocalCatalogResult,
  LocalScanMode,
  LocalScanResult,
  PersistentStorageStatus,
  ScanCatalogBook,
  ScanSessionSnapshot,
} from '../offline/scan-offline.model';
import {ScanSyncService} from '../offline/scan-sync.service';
import {ScanWorkflowService} from '../offline/scan-workflow.service';
import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerHandle, CameraScannerService} from './camera-scanner.service';
import {normalizeIsbn} from './isbn.util';

export type ScanScreen =
  | 'home'
  | 'session-mode'
  | 'tri'
  | 'manual'
  | 'session-end'
  | 'cash'
  | 'consultation';

type ScanDestination = 'tri' | 'cash' | 'consultation';
type ManualKey = '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '0' | 'clear' | 'backspace';

interface CashScanItem {
  id: string;
  isbn13: string;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  isRare: boolean;
  quantityAvailable: number;
  quantityAnnounced: number;
}

@Component({
  selector: 'app-scanner',
  templateUrl: './scanner.component.html',
  styleUrl: './scanner.component.scss',
  standalone: false,
})
export class ScannerComponent implements OnInit, OnDestroy {
  private static readonly openLibraryCoverUrlTemplate =
    'https://covers.openlibrary.org/b/isbn/{isbn13}-L.jpg?default=false';

  @ViewChild('cameraContainer', {static: true})
  private readonly cameraContainer!: ElementRef<HTMLElement>;

  readonly manualKeys: readonly ManualKey[] = [
    '1', '2', '3',
    '4', '5', '6',
    '7', '8', '9',
    'clear', '0', 'backspace',
  ];

  isbnInput = '';
  manualIsbn = '';
  manualError: string | null = null;
  metadata: BookMetadata | null = null;
  coverUrl: string | null = null;
  errorMessage: string | null = null;
  cameraError: string | null = null;
  storageError: string | null = null;
  syncError: string | null = null;
  cashMessage: string | null = null;
  isLoading = false;
  cameraActive = false;
  localScan: LocalScanResult | null = null;
  consultationResult: LocalCatalogResult | null = null;
  cashItems: CashScanItem[] = [];
  pendingCount = 0;
  isOnline = typeof navigator === 'undefined' || navigator.onLine;
  persistenceStatus: PersistentStorageStatus | null = null;
  session: ScanSessionSnapshot | null = null;
  completedSession: ScanSessionSnapshot | null = null;
  sessionDurationLabel = '0 min de tri';
  authAvailable = false;
  isAuthenticated = false;
  accountName: string | null = null;
  syncStatus: 'idle' | 'syncing' | 'success' | 'error' = 'idle';
  sessionCloseError: string | null = null;
  screen: ScanScreen = 'tri';
  selectedMode: LocalScanMode = 'AvailableNow';
  manualReturnScreen: ScanDestination = 'tri';

  private cameraHandle: CameraScannerHandle | null = null;
  private lookupVersion = 0;
  private scannerBuffer = '';
  private lastScannerKeyAt = 0;
  private localModeReady = false;
  private syncInProgress = false;
  private syncPromise: Promise<void> | null = null;
  private sessionEnding = false;
  private sessionEnded = false;
  private sessionCloseCompleted = false;
  private syncTimer: number | null = null;
  private cameraStartToken = 0;

  constructor(
    private readonly metadataService: BookMetadataService,
    private readonly cameraScanner: CameraScannerService,
    private readonly changeDetector: ChangeDetectorRef,
    private readonly destroyRef: DestroyRef,
    @Optional() private readonly scanWorkflow: ScanWorkflowService | null,
    @Optional() private readonly scanAuth: ScanAuthService | null,
    @Optional() private readonly scanSync: ScanSyncService | null,
  ) {
    // The isolated component tests do not provide the local workflow. Keeping
    // them on the scan surface preserves the old direct-lookup test harness;
    // the real PWA opens on the mode-selection home screen.
    this.screen = scanWorkflow === null ? 'tri' : 'home';
  }

  ngOnInit(): void {
    this.authAvailable = this.scanAuth !== null;
    if (this.scanAuth) {
      this.scanAuth.authState$
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(authState => {
          this.isAuthenticated = authState.status === 'authorized';
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

  get displayName(): string {
    return this.accountName ?? 'Bénévole';
  }

  get canSort(): boolean {
    return !this.authAvailable || this.scanAuth === null || this.scanAuth.canSort;
  }

  get canSell(): boolean {
    return !this.authAvailable || this.scanAuth === null || this.scanAuth.canSell;
  }

  get activeMode(): LocalScanMode {
    return this.session?.mode ?? this.selectedMode;
  }

  get activeModeLabel(): string {
    return this.activeMode === 'AvailableNow'
      ? 'Disponibles maintenant'
      : 'Prochaine bourse';
  }

  get activeModeDescription(): string {
    return this.activeMode === 'AvailableNow'
      ? 'Mis en vente tout de suite'
      : `Annoncés en ligne pour ${this.nextFairLongLabel}`;
  }

  get nextFairShortLabel(): string {
    return 'date à préciser';
  }

  get nextFairLongLabel(): string {
    return 'la prochaine bourse';
  }

  get sessionScannedCount(): number {
    return this.session?.scannedCount ?? this.completedSession?.scannedCount ?? 0;
  }

  get sessionKeptCount(): number {
    return this.session?.keptCount ?? this.completedSession?.keptCount ?? 0;
  }

  get sessionRejectedCount(): number {
    return this.session?.rejectedCount ?? this.completedSession?.rejectedCount ?? 0;
  }

  get manualDigitCount(): number {
    return this.manualIsbn.replace(/[^0-9Xx]/g, '').length;
  }

  get activeBook(): ScanCatalogBook | null {
    return this.localScan?.catalogBook ?? this.consultationResult?.catalogBook ?? null;
  }

  get activeVerdict(): LocalScanResult['verdict'] | LocalCatalogResult['verdict'] | null {
    return this.localScan?.verdict ?? this.consultationResult?.verdict ?? null;
  }

  get isImmediateRepeatScan(): boolean {
    return this.localScan?.isImmediateRepeat === true;
  }

  get activeTitle(): string {
    return this.metadata?.title
      ?? this.activeBook?.title
      ?? (this.isLoading ? 'Notice en attente…' : 'Titre non renseigné');
  }

  get activeAuthors(): string | null {
    return this.metadata?.authors ?? this.activeBook?.authors ?? null;
  }

  get activePublisher(): string | null {
    return this.metadata?.publisher ?? null;
  }

  get activePublicationYear(): number | null {
    return this.metadata?.publicationYear ?? null;
  }

  get activeIsbn(): string {
    return this.metadata?.isbn13 ?? this.activeBook?.isbn13 ?? this.isbnInput;
  }

  get activeVerdictKey(): 'TooMany' | 'Wanted' | 'Selling' | 'FirstCopy' | 'Rare' {
    if (this.activeVerdict?.isRare) {
      return 'Rare';
    }

    switch (this.activeVerdict?.verdict) {
      case 'Wanted':
        return 'Wanted';
      case 'Selling':
        return 'Selling';
      case 'TooMany':
        return 'TooMany';
      default:
        return 'FirstCopy';
    }
  }

  get verdictTitle(): string {
    if (this.isImmediateRepeatScan) {
      return 'Déjà scanné à l’instant';
    }

    switch (this.activeVerdictKey) {
      case 'Rare':
        return 'Bac « livres rares »';
      case 'Wanted':
        return 'À garder';
      case 'Selling':
        return 'À garder';
      case 'TooMany':
        return 'Inutile d’en garder';
      default:
        return this.activeVerdict?.isKnown && this.activeVerdict.totalKnownQuantity > 0
          ? 'Exemplaire supplémentaire'
          : 'Premier exemplaire';
    }
  }

  get verdictSummary(): string {
    const verdict = this.activeVerdict;
    if (!verdict) {
      return '';
    }

    if (this.isImmediateRepeatScan) {
      return 'Ce livre a déjà été scanné à l’instant. Vérifiez le doublon avant de continuer.';
    }

    switch (this.activeVerdictKey) {
      case 'Rare':
        return 'Signalé par un responsable — ne pas mettre en rayon';
      case 'Wanted':
        return `${verdict.activeRequesterCount} personne${verdict.activeRequesterCount > 1 ? 's' : ''} le recherche${verdict.activeRequesterCount > 1 ? 'nt' : ''}`;
      case 'Selling':
        return `${verdict.salesCount} vente${verdict.salesCount > 1 ? 's' : ''} déjà enregistrée${verdict.salesCount > 1 ? 's' : ''}`;
      case 'TooMany':
        return `Déjà ${verdict.totalKnownQuantity} exemplaire${verdict.totalKnownQuantity > 1 ? 's' : ''} · ${this.availableQuantity} disponibles + ${this.announcedQuantity} annoncés`;
      default:
        return verdict.isKnown
          ? verdict.totalKnownQuantity > 0
            ? `Déjà ${verdict.totalKnownQuantity} exemplaire${verdict.totalKnownQuantity > 1 ? 's' : ''} dans la copie locale`
            : 'Aucun exemplaire disponible dans la copie locale'
          : 'Ce titre n’est pas encore au catalogue';
    }
  }

  get availableQuantity(): number {
    return this.activeBook?.qtyAvailable ?? 0;
  }

  get announcedQuantity(): number {
    return this.activeBook?.qtyAnnounced ?? 0;
  }

  get salesQuantity(): number {
    return this.activeBook?.salesCount ?? this.activeVerdict?.salesCount ?? 0;
  }

  get requesterQuantity(): number {
    return this.activeVerdict?.activeRequesterCount ?? 0;
  }

  get offlineFreshnessLabel(): string {
    if (!this.session?.lastSyncAt) {
      return 'd’après les données locales non synchronisées';
    }

    const date = new Date(this.session.lastSyncAt);
    if (Number.isNaN(date.getTime())) {
      return 'd’après les données locales';
    }

    return `d’après les données locales du ${new Intl.DateTimeFormat('fr-FR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date)}`;
  }

  get publicEffectLabel(): string {
    return this.activeMode === 'NextFair'
      ? `Annoncés en ligne pour ${this.nextFairLongLabel}`
      : 'Disponibles à la vente immédiatement';
  }

  async submit(): Promise<void> {
    await this.lookup(this.isbnInput, 'tri');
  }

  async lookup(rawInput: string, destination: ScanDestination = this.destinationForScreen()): Promise<void> {
    const normalizedIsbn = normalizeIsbn(rawInput);
    const lookupVersion = ++this.lookupVersion;
    this.resetLookupState();

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
    const localPromise = destination === 'tri' && this.scanWorkflow
      ? this.scanWorkflow.recordScan(normalizedIsbn)
      : (destination === 'cash' || destination === 'consultation') && this.scanWorkflow
        ? this.scanWorkflow.lookupCatalog(normalizedIsbn)
        : Promise.resolve(null);

    const [localOutcome, metadataOutcome] = await Promise.allSettled([
      localPromise,
      metadataPromise,
    ]);

    if (lookupVersion !== this.lookupVersion) {
      return;
    }

    let localResult: LocalScanResult | null = null;
    if (localOutcome.status === 'fulfilled') {
      if (destination === 'tri') {
        localResult = localOutcome.value as LocalScanResult | null;
        this.localScan = localResult;
        if (localResult) {
          await this.refreshLocalState();
          this.trySync();
        }
      } else if (destination === 'cash' || destination === 'consultation') {
        this.consultationResult = localOutcome.value as LocalCatalogResult | null;
      }
    } else if (destination === 'tri') {
      this.storageError = 'Le geste n’a pas pu être conservé localement. Vérifiez le stockage du navigateur.';
    }

    let metadata: BookMetadata | null = null;
    if (metadataOutcome.status === 'fulfilled') {
      metadata = metadataOutcome.value;
      this.metadata = metadata;
      this.coverUrl = metadata.coverUrl;
      if (this.scanWorkflow) {
        try {
          await this.scanWorkflow.cacheMetadata(metadata);
        } catch {
          // Bibliographic metadata is best-effort; the durable scan remains in the outbox.
        }
      }
    } else {
      const error = metadataOutcome.reason;
      this.errorMessage = error instanceof HttpErrorResponse && error.status === 404
        ? 'Aucune notice bibliographique trouvée pour cet ISBN.'
        : 'La notice ne peut pas être chargée pour le moment.';
    }

    if (destination === 'cash') {
      this.cashItems = [...this.cashItems, this.createCashItem(normalizedIsbn, metadata)];
      this.cashMessage = null;
    }

    this.isLoading = false;
    this.refreshView();
  }

  startSorting(): void {
    if (this.authAvailable && !this.canSort) {
      return;
    }

    this.stopCamera();
    if (this.sessionEnded) {
      if (this.scanWorkflow && !this.sessionCloseCompleted) {
        this.sessionCloseError = 'La session précédente doit être synchronisée avant d’en ouvrir une nouvelle.';
        this.screen = 'session-end';
        this.refreshView();
        return;
      }

      this.session = null;
      this.completedSession = null;
      this.sessionEnded = false;
      this.sessionCloseCompleted = false;
    }

    if (this.session && this.session.scannedCount > 0) {
      this.screen = 'tri';
    } else {
      this.screen = 'session-mode';
    }
    this.refreshView();
    this.startCameraIfNeeded();
  }

  async chooseSessionMode(mode: LocalScanMode): Promise<void> {
    this.selectedMode = mode;
    this.stopCamera();

    if (this.scanWorkflow) {
      try {
        this.session = await this.scanWorkflow.setSessionMode(mode);
      } catch {
        this.storageError = 'Le mode de session n’a pas pu être conservé localement.';
      }
    } else if (this.session) {
      this.session = {...this.session, mode};
    }

    this.screen = 'tri';
    this.refreshView();
    this.startCameraIfNeeded();
  }

  openCash(): void {
    if (this.authAvailable && !this.canSell) {
      return;
    }

    this.stopCamera();
    this.screen = 'cash';
    this.resetLookupState();
    this.cashMessage = null;
    this.refreshView();
    this.startCameraIfNeeded(true);
  }

  openConsultation(): void {
    this.stopCamera();
    this.screen = 'consultation';
    this.resetLookupState();
    this.refreshView();
    this.startCameraIfNeeded(true);
  }

  openManualInput(): void {
    this.stopCamera();
    this.manualReturnScreen = this.destinationForScreen();
    this.manualIsbn = this.isbnInput;
    this.manualError = null;
    this.screen = 'manual';
    this.refreshView();
  }

  appendManualKey(key: ManualKey): void {
    this.manualError = null;
    if (key === 'clear') {
      this.manualIsbn = '';
    } else if (key === 'backspace') {
      this.manualIsbn = this.manualIsbn.slice(0, -1);
    } else if (this.manualIsbn.replace(/[^0-9Xx]/g, '').length < 13) {
      this.manualIsbn += key;
    }
    this.refreshView();
  }

  async validateManualIsbn(): Promise<void> {
    const normalizedIsbn = normalizeIsbn(this.manualIsbn);
    if (!normalizedIsbn) {
      this.manualError = 'Ce code n’est pas un ISBN valide.';
      this.refreshView();
      return;
    }

    const destination = this.manualReturnScreen;
    this.screen = destination;
    try {
      await this.lookup(normalizedIsbn, destination);
    } finally {
      this.restartContinuousCamera(destination);
    }
  }

  returnToScan(): void {
    this.screen = this.manualReturnScreen;
    this.manualError = null;
    this.refreshView();
    this.startCameraIfNeeded();
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
    this.screen = 'home';
    this.refreshView();
  }

  async syncNow(): Promise<void> {
    if (
      !this.scanSync ||
      !this.isAuthenticated ||
      !this.isOnline ||
      !this.localModeReady
    ) {
      return;
    }

    if (this.syncPromise) {
      await this.syncPromise;
      return;
    }

    const syncPromise = this.performSync(this.scanSync);
    this.syncPromise = syncPromise;
    try {
      await syncPromise;
    } finally {
      if (this.syncPromise === syncPromise) {
        this.syncPromise = null;
      }
    }
  }

  private async performSync(scanSync: ScanSyncService): Promise<void> {
    this.syncInProgress = true;
    this.syncStatus = 'syncing';
    this.syncError = null;
    this.refreshView();

    try {
      const summary = await scanSync.syncAll();
      const closeRequested = this.session?.closeRequested === true;
      if (summary.closed) {
        this.sessionCloseCompleted = true;
        this.sessionCloseError = null;
      }
      if (!summary.catalog) {
        this.syncStatus = 'error';
        this.syncError = 'Le compte est connecté, mais le catalogue n’a pas pu être synchronisé (droits ou réseau).';
      } else if (summary.outbox.stoppedOnError) {
        this.syncStatus = 'error';
        this.syncError = 'La file locale reste conservée et sera réessayée automatiquement.';
      } else if (closeRequested && !summary.closed) {
        this.syncStatus = 'error';
        this.syncError = 'La session est prête, mais sa fermeture serveur sera réessayée automatiquement.';
        this.sessionCloseError = 'La session reste enregistrée localement et sera clôturée dès que la synchronisation aboutira.';
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
    return this.verdictTitle;
  }

  getVerdictDescription(): string {
    return this.verdictSummary;
  }

  async endSession(): Promise<void> {
    if (!this.session || this.sessionEnding) {
      return;
    }

    if (this.localScan?.entry.status === 'Pending') {
      this.syncError = 'Choisissez « Garder » ou « Écarter » pour le dernier livre avant de terminer.';
      this.screen = 'tri';
      this.refreshView();
      return;
    }

    this.sessionEnding = true;
    this.sessionCloseError = null;

    try {
      const completedSession = {...this.session};
      this.completedSession = completedSession;
      this.sessionDurationLabel = this.formatSessionDuration(completedSession);
      this.sessionEnded = true;
      this.sessionCloseCompleted = false;
      this.stopCamera();

      if (this.scanWorkflow) {
        try {
          this.session = await this.scanWorkflow.requestClose('Manual');
          await this.refreshLocalState();
        } catch {
          this.sessionCloseError = 'La demande de fin n’a pas pu être conservée localement.';
          this.screen = 'tri';
          return;
        }
      }

      this.resetLookupState();
      this.screen = 'session-end';
      this.refreshView();

      if (!this.scanWorkflow) {
        this.sessionCloseCompleted = true;
        return;
      }

      if (!this.scanSync) {
        this.sessionCloseError = 'Session enregistrée localement ; la synchronisation est indisponible.';
        return;
      }

      if (!this.isAuthenticated || !this.isOnline || !this.localModeReady) {
        this.sessionCloseError = 'Session enregistrée localement ; reconnectez-vous pour publier les livres.';
        return;
      }

      await this.syncNow();
      if (this.session?.closeRequested && !this.sessionCloseCompleted) {
        // A sync already in progress may have started before requestClose().
        await this.syncNow();
      }
      if (this.session?.closeRequested && !this.sessionCloseCompleted) {
        this.sessionCloseError = 'La session reste enregistrée localement et sera clôturée dès que la synchronisation aboutira.';
      }
    } catch {
      this.sessionCloseError = 'La session n’a pas pu être clôturée. Les scans restent conservés localement.';
    } finally {
      this.sessionEnding = false;
      this.refreshView();
    }
  }

  returnHome(): void {
    this.stopCamera();
    this.resetLookupState();
    this.cashItems = [];
    this.cashMessage = null;
    this.screen = 'home';
    this.refreshView();
  }

  removeCashItem(id: string): void {
    const remainingItems = this.cashItems.filter(item => item.id !== id);
    if (remainingItems.length === this.cashItems.length) {
      return;
    }

    this.cashItems = remainingItems;
    this.cashMessage = null;
    this.refreshView();
  }

  undoLastCashItem(): void {
    const lastItem = this.cashItems.at(-1);
    if (!lastItem) {
      return;
    }

    this.removeCashItem(lastItem.id);
  }

  async validateCash(): Promise<void> {
    if (this.cashItems.length === 0) {
      return;
    }

    const items = [...this.cashItems];
    const count = items.length;

    if (!this.scanWorkflow) {
      this.cashMessage = `${count} livre${count > 1 ? 's' : ''} dans la vente.`;
      this.cashItems = [];
      this.refreshView();
      return;
    }

    try {
      await this.scanWorkflow.recordCashSales(items.map(item => item.isbn13));
      this.cashItems = [];
      this.cashMessage = `${count} livre${count > 1 ? 's' : ''} enregistré${count > 1 ? 's' : ''} localement. Synchronisation automatique en cours.`;
      await this.refreshLocalState();
      this.trySync();
    } catch {
      this.cashMessage = 'La vente n’a pas pu être conservée localement. Réessayez sans quitter cet écran.';
    }

    this.refreshView();
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
    if (this.cameraHandle || this.cameraActive) {
      this.stopCamera();
      return;
    }

    await this.startCamera();
  }

  retryCamera(): void {
    this.cameraError = null;
    this.refreshView();
    this.startCameraIfNeeded(true);
  }

  private async startCamera(): Promise<void> {
    if (this.cameraHandle || this.cameraActive) {
      return;
    }

    this.cameraError = null;
    this.cameraActive = true;
    const startToken = ++this.cameraStartToken;
    this.refreshView();

    try {
      const cameraHandle = await this.cameraScanner.start(
        this.cameraContainer.nativeElement,
        rawValue => this.handleCameraDetection(rawValue),
      );
      if (startToken !== this.cameraStartToken) {
        await cameraHandle.stop();
        return;
      }

      this.cameraHandle = cameraHandle;
    } catch (error: unknown) {
      if (startToken !== this.cameraStartToken) {
        return;
      }

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

    const destination = this.destinationForScreen();
    this.stopCamera();
    this.cameraError = null;
    this.refreshView();

    try {
      const rawValue = await this.cameraScanner.scanFile(imageFile);
      await this.lookup(rawValue, destination);
    } catch {
      this.cameraError = 'Aucun code-barres lisible n’a été trouvé dans cette photo.';
      this.refreshView();
    } finally {
      this.restartContinuousCamera(destination);
    }
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (!this.isScanDestinationActive() || this.isEditableTarget(event.target)) {
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
        void this.lookup(scannedValue, this.destinationForScreen());
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
      this.selectedMode = this.session?.mode ?? 'AvailableNow';
      await this.refreshLocalState();
      this.localModeReady = true;

      if (!this.persistenceStatus.available) {
        this.storageError = 'Ce navigateur ne fournit pas IndexedDB : le tri hors ligne est indisponible.';
      } else if (!this.persistenceStatus.persisted) {
        this.storageError = 'Le navigateur n’a pas garanti la conservation des données hors ligne. Gardez l’application régulièrement connectée.';
      }

      if (this.localScan || (this.session?.scannedCount ?? 0) > 0) {
        this.screen = 'tri';
      }
      this.trySync();
      this.refreshView();
      this.startCameraIfNeeded();
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
      this.resetLookupState();
      this.refreshView();
      this.startCameraIfNeeded();
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
    this.selectedMode = this.session?.mode ?? this.selectedMode;
  }

  private stopCamera(): void {
    this.cameraStartToken += 1;
    this.cameraHandle?.stop();
    this.cameraHandle = null;
    this.cameraActive = false;
  }

  private startCameraIfNeeded(force = false): void {
    if (
      !this.isCameraDestinationActive() ||
      !this.authAvailable ||
      !this.isAuthenticated ||
      (!force && this.screen === 'tri' && (this.localScan !== null || this.metadata !== null)) ||
      this.cameraActive ||
      this.cameraHandle
    ) {
      return;
    }

    void this.startCamera();
  }

  private restartContinuousCamera(destination: ScanDestination): void {
    if (destination === 'tri' || this.screen !== destination) {
      return;
    }

    this.startCameraIfNeeded(true);
  }

  private handleCameraDetection(rawValue: string): void {
    const destination = this.destinationForScreen();
    this.stopCamera();
    void this.lookup(rawValue, destination)
      .finally(() => this.restartContinuousCamera(destination));
  }

  private isCameraDestinationActive(): boolean {
    return this.screen === 'tri' || this.screen === 'cash' || this.screen === 'consultation';
  }

  private refreshView(): void {
    this.changeDetector.markForCheck();
  }

  private trySync(): void {
    void this.syncNow();
  }

  private destinationForScreen(): ScanDestination {
    switch (this.screen) {
      case 'cash':
        return 'cash';
      case 'consultation':
        return 'consultation';
      default:
        return 'tri';
    }
  }

  private isScanDestinationActive(): boolean {
    return this.screen === 'tri' || this.screen === 'cash' || this.screen === 'consultation';
  }

  private resetLookupState(): void {
    this.metadata = null;
    this.coverUrl = null;
    this.localScan = null;
    this.consultationResult = null;
    this.errorMessage = null;
    this.cameraError = null;
    this.isLoading = false;
  }

  private createCashItem(isbn13: string, metadata: BookMetadata | null): CashScanItem {
    const book = this.activeBook;
    return {
      id: `${isbn13}-${Date.now()}-${this.cashItems.length}`,
      isbn13,
      title: metadata?.title ?? book?.title ?? null,
      authors: metadata?.authors ?? book?.authors ?? null,
      publisher: metadata?.publisher ?? null,
      publicationYear: metadata?.publicationYear ?? null,
      isRare: this.activeVerdict?.isRare ?? book?.isRare ?? false,
      quantityAvailable: book?.qtyAvailable ?? 0,
      quantityAnnounced: book?.qtyAnnounced ?? 0,
    };
  }

  private formatSessionDuration(session: ScanSessionSnapshot): string {
    const startedAt = Date.parse(session.startedAt);
    const lastScanAt = Date.parse(session.lastScanAt);
    if (Number.isNaN(startedAt) || Number.isNaN(lastScanAt) || lastScanAt <= startedAt) {
      return '0 min de tri';
    }

    const totalMinutes = Math.round((lastScanAt - startedAt) / 60_000);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return hours > 0
      ? `${hours} h ${minutes.toString().padStart(2, '0')} de tri`
      : `${minutes} min de tri`;
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
