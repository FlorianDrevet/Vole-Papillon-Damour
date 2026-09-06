import {HttpErrorResponse} from '@angular/common/http';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {ChangeDetectorRef, DestroyRef} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of, Subject, throwError} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerService} from './camera-scanner.service';
import {ScannerComponent} from './scanner.component';
import {ScanAuthService} from '../auth/scan-auth.service';
import {LocalScanResult, ScanSessionSnapshot} from '../offline/scan-offline.model';
import {ScanSyncService} from '../offline/scan-sync.service';
import {ScanWorkflowService} from '../offline/scan-workflow.service';

describe('ScannerComponent', () => {
  let fixture: ComponentFixture<ScannerComponent>;
  let component: ScannerComponent;
  let metadataService: jasmine.SpyObj<BookMetadataService>;
  let cameraService: jasmine.SpyObj<CameraScannerService>;

  beforeEach(async () => {
    metadataService = jasmine.createSpyObj<BookMetadataService>('BookMetadataService', ['getMetadata']);
    cameraService = jasmine.createSpyObj<CameraScannerService>('CameraScannerService', ['start', 'scanFile']);

    await TestBed.configureTestingModule({
      declarations: [ScannerComponent],
      imports: [CommonModule, FormsModule, DesignSystemModule],
      providers: [
        {provide: BookMetadataService, useValue: metadataService},
        {provide: CameraScannerService, useValue: cameraService},
        {provide: ScanAuthService, useValue: null},
        {provide: ScanSyncService, useValue: null},
        {provide: ScanWorkflowService, useValue: null},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ScannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('normalizes a valid ISBN before requesting metadata', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    component.isbnInput = '0-306-40615-2';

    await component.submit();

    expect(metadataService.getMetadata).toHaveBeenCalledOnceWith('9780306406157');
    expect(component.metadata).toEqual(metadata);
    expect(component.errorMessage).toBeNull();
  });

  it('refreshes the rendered result when a manual lookup completes', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    component.isbnInput = '978-2-07-036373-5';

    await component.submit();
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('#book-title')?.textContent).toContain(metadata.title);
  });

  it('notifies Angular when a manual lookup completes outside a template event', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    const componentChangeDetector = (component as unknown as {
      changeDetector: ChangeDetectorRef;
    }).changeDetector;
    const markForCheck = spyOn(componentChangeDetector, 'markForCheck');

    await component.lookup('9782070363735');

    expect(markForCheck).toHaveBeenCalled();
  });

  it('rejects an invalid ISBN without making a request', async () => {
    component.isbnInput = '4006381333931';

    await component.submit();

    expect(metadataService.getMetadata).not.toHaveBeenCalled();
    expect(component.metadata).toBeNull();
    expect(component.errorMessage).toContain('ISBN');
  });

  it('ignores metadata that belongs to an older scan', async () => {
    const firstResponse = new Subject<BookMetadata>();
    const firstMetadata = createMetadata('Premier livre');
    const secondMetadata = createMetadata('Livre courant');
    metadataService.getMetadata.and.returnValues(firstResponse.asObservable(), of(secondMetadata));

    const firstLookup = component.lookup('9780306406157');
    await component.lookup('9782070363735');
    firstResponse.next(firstMetadata);
    firstResponse.complete();
    await firstLookup;

    expect(component.metadata).toEqual(secondMetadata);
  });

  it('looks up an ISBN decoded from a camera photo', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    cameraService.scanFile.and.returnValue(Promise.resolve('9782070363735'));
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    const imageFile = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});
    Object.defineProperty(input, 'files', {value: [imageFile]});

    await component.scanImage({target: input} as unknown as Event);

    expect(cameraService.scanFile).toHaveBeenCalledOnceWith(imageFile);
    expect(metadataService.getMetadata).toHaveBeenCalledOnceWith('9782070363735');
    expect(component.metadata).toEqual(metadata);
    expect(component.cameraError).toBeNull();
  });

  it('keeps a metadata error separate from a successfully decoded camera photo', async () => {
    cameraService.scanFile.and.returnValue(Promise.resolve('9782070363735'));
    metadataService.getMetadata.and.returnValue(throwError(
      () => new HttpErrorResponse({status: 404}),
    ));
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    const imageFile = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});
    Object.defineProperty(input, 'files', {value: [imageFile]});

    await component.scanImage({target: input} as unknown as Event);

    expect(component.cameraError).toBeNull();
    expect(component.errorMessage).toContain('Aucune notice bibliographique');
  });

  it('falls back to the ISBN cover when the metadata cover cannot be loaded', async () => {
    const metadata = createMetadata();
    metadata.coverUrl = 'https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782070363735&couverture=1';
    metadataService.getMetadata.and.returnValue(of(metadata));

    await component.lookup(metadata.isbn13);
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('.cover-frame img') as HTMLImageElement;

    image.dispatchEvent(new Event('error'));
    await fixture.whenStable();

    expect(image.getAttribute('src'))
      .toBe('https://covers.openlibrary.org/b/isbn/9782070363735-L.jpg?default=false');
  });

  it('shows a placeholder when neither cover source can be loaded', async () => {
    const metadata = createMetadata();
    metadata.coverUrl = 'https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782070363735&couverture=1';
    metadataService.getMetadata.and.returnValue(of(metadata));

    await component.lookup(metadata.isbn13);
    fixture.detectChanges();
    const originalImage = fixture.nativeElement.querySelector('.cover-frame img') as HTMLImageElement;
    originalImage.dispatchEvent(new Event('error'));
    await fixture.whenStable();

    const fallbackImage = fixture.nativeElement.querySelector('.cover-frame img') as HTMLImageElement;
    fallbackImage.dispatchEvent(new Event('error'));
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.cover-placeholder')?.textContent)
      .toContain('Couverture indisponible');
  });

  it('refreshes the rendered error when a camera photo cannot be decoded', async () => {
    cameraService.scanFile.and.returnValue(Promise.reject(new Error('not found')));
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    const imageFile = new File(['barcode'], 'book.jpg', {type: 'image/jpeg'});
    Object.defineProperty(input, 'files', {value: [imageFile]});

    await component.scanImage({target: input} as unknown as Event);
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('.message-error')?.textContent)
      .toContain('Aucun code-barres lisible');
  });

  it('renders the mode selection surface when the component enters the home screen', () => {
    component.returnHome();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.home-screen')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Que faites-vous maintenant ?');
  });

  it('centers the home actions between the header and footer', () => {
    component.returnHome();
    fixture.detectChanges();

    const header = fixture.nativeElement.querySelector('.home-header') as HTMLElement;
    const actions = fixture.nativeElement.querySelector('.home-mode-actions') as HTMLElement;
    const footer = fixture.nativeElement.querySelector('.home-footer') as HTMLElement;
    const actionsRect = actions.getBoundingClientRect();
    const availableCenter = (header.getBoundingClientRect().bottom + footer.getBoundingClientRect().top) / 2;
    const actionsCenter = (actionsRect.top + actionsRect.bottom) / 2;

    expect(actionsCenter).toBeCloseTo(availableCenter, 0);
  });

  it('renders the manual ISBN keypad with an accessible return action', () => {
    component.openManualInput();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.manual-keypad')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Revenir au scan"]')).not.toBeNull();
  });

  it('refreshes the rendered result when the live camera detects an ISBN', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    let onDetected: ((rawValue: string) => void) | undefined;
    cameraService.start.and.callFake(async (_container, callback) => {
      onDetected = callback;
      return {stop: async () => undefined};
    });

    await component.toggleCamera();
    onDetected?.('9782070363735');
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('#book-title')?.textContent).toContain(metadata.title);
  });

  it('starts the live camera when the scan screen opens without a scanner button', async () => {
    cameraService.start.and.returnValue(Promise.resolve({stop: async () => undefined}));
    component.authAvailable = true;
    component.isAuthenticated = true;

    await component.chooseSessionMode('AvailableNow');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(cameraService.start).toHaveBeenCalledOnceWith(
      jasmine.any(HTMLElement),
      jasmine.any(Function),
    );
    expect(component.cameraActive).toBeTrue();
    expect(fixture.nativeElement.querySelector('.scan-dock .dock-primary')).toBeNull();
  });

  it('starts the live camera as soon as the cash screen opens', async () => {
    cameraService.start.and.returnValue(Promise.resolve({stop: async () => undefined}));
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();

    expect(cameraService.start).toHaveBeenCalledOnceWith(
      jasmine.any(HTMLElement),
      jasmine.any(Function),
    );
    expect(component.cameraActive).toBeTrue();
  });

  it('starts the live camera as soon as consultation opens', async () => {
    cameraService.start.and.returnValue(Promise.resolve({stop: async () => undefined}));
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openConsultation();
    await fixture.whenStable();

    expect(cameraService.start).toHaveBeenCalledOnceWith(
      jasmine.any(HTMLElement),
      jasmine.any(Function),
    );
    expect(component.cameraActive).toBeTrue();
  });

  it('restarts the live camera after a cash scan so the next book can be read immediately', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    const detected: Array<(rawValue: string) => void> = [];
    cameraService.start.and.callFake(async (_container, callback) => {
      detected.push(callback);
      return {stop: async () => undefined};
    });
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();
    detected[0]('9782070363735');
    await fixture.whenStable();

    expect(component.cashItems).toHaveSize(1);
    expect(cameraService.start).toHaveBeenCalledTimes(2);
    expect(component.cameraActive).toBeTrue();
  });

  it('labels a repeated local scan instead of showing the first-copy verdict again', () => {
    component.localScan = Object.assign(createLocalScanResult(), {isImmediateRepeat: true});

    expect(component.verdictTitle).toBe('Déjà scanné à l’instant');
    expect(component.verdictSummary).toContain('déjà été scanné');
  });

  it('removes the selected cash item rather than only the last one', () => {
    component.cashItems = [
      createCashItem('first', 'Premier livre'),
      createCashItem('second', 'Second livre'),
    ];
    component.removeCashItem('first');

    expect(component.cashItems.map(item => item.id)).toEqual(['second']);
  });

  it('persists the cash batch before clearing the visible list', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>(
      'ScanWorkflowService',
      ['recordCashSales', 'getPendingCount', 'getSession'],
    );
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    workflow.recordCashSales.and.resolveTo([]);
    workflow.getPendingCount.and.resolveTo(1);
    workflow.getSession.and.resolveTo(null);
    sync.syncAll.and.resolveTo({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 0, remaining: 1, stoppedOnError: false},
      closed: false,
    });

    const internals = component as unknown as {
      changeDetector: ChangeDetectorRef;
      destroyRef: DestroyRef;
    };
    const localComponent = new ScannerComponent(
      metadataService,
      cameraService,
      internals.changeDetector,
      internals.destroyRef,
      workflow,
      null,
      sync,
    );
    localComponent.authAvailable = true;
    localComponent.isAuthenticated = true;
    localComponent.isOnline = true;
    (localComponent as unknown as {localModeReady: boolean}).localModeReady = true;
    localComponent.cashItems = [createCashItem('sale-1', 'Livre vendu')];

    await localComponent.validateCash();

    expect(workflow.recordCashSales).toHaveBeenCalledOnceWith(['9782070363735']);
    expect(localComponent.cashItems).toEqual([]);
    expect(localComponent.cashMessage).toContain('enregistré localement');
    expect(sync.syncAll).toHaveBeenCalledOnceWith();
  });

  it('opens a new session mode screen after a session has been ended', async () => {
    component.session = createSession({scannedCount: 2, keptCount: 2});

    await component.endSession();
    component.returnHome();
    await component.startSorting();

    expect(component.screen).toBe('session-mode');
  });

  it('clears the durable session only after the sync service confirms close', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'requestClose',
      'getPendingCount',
      'getSession',
    ]);
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    const session = createSession({scannedCount: 1, keptCount: 1});
    const completedScan = createLocalScanResult();
    completedScan.entry = {...completedScan.entry, status: 'Kept', kept: true};
    const requestedSession = {...session, closeRequested: true, closeReason: 'Manual' as const};

    workflow.requestClose.and.resolveTo(requestedSession);
    workflow.getPendingCount.and.resolveTo(0);
    workflow.getSession.and.returnValues(
      Promise.resolve(requestedSession),
      Promise.resolve(null),
    );
    sync.syncAll.and.resolveTo({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 1, remaining: 0, stoppedOnError: false},
      closed: true,
    });

    (component as unknown as {scanWorkflow: ScanWorkflowService}).scanWorkflow = workflow;
    (component as unknown as {scanSync: ScanSyncService}).scanSync = sync;
    (component as unknown as {localModeReady: boolean}).localModeReady = true;
    component.authAvailable = true;
    component.isAuthenticated = true;
    component.isOnline = true;
    component.session = session;
    component.localScan = completedScan;

    await component.endSession();

    expect(workflow.requestClose).toHaveBeenCalledOnceWith('Manual');
    expect(sync.syncAll).toHaveBeenCalledOnceWith();
    expect(component.session).toBeNull();
    expect(component.screen).toBe('session-end');
  });
  it('starts synchronizing after a scan is stored locally', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>(
      'ScanWorkflowService',
      ['recordScan', 'cacheMetadata', 'getPendingCount', 'getSession'],
    );
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    workflow.recordScan.and.returnValue(Promise.resolve(createLocalScanResult()));
    workflow.cacheMetadata.and.returnValue(Promise.resolve());
    workflow.getPendingCount.and.returnValue(Promise.resolve(1));
    workflow.getSession.and.returnValue(Promise.resolve(createSession()));
    sync.syncAll.and.returnValue(Promise.resolve({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 0, remaining: 1, stoppedOnError: false},
      closed: false,
    }));
    metadataService.getMetadata.and.returnValue(of(createMetadata()));

    const internals = component as unknown as {
      changeDetector: ChangeDetectorRef;
      destroyRef: DestroyRef;
    };
    const localComponent = new ScannerComponent(
      metadataService,
      cameraService,
      internals.changeDetector,
      internals.destroyRef,
      workflow,
      null,
      sync,
    );
    localComponent.authAvailable = true;
    localComponent.isAuthenticated = true;
    localComponent.isOnline = true;
    (localComponent as unknown as {localModeReady: boolean}).localModeReady = true;

    await localComponent.lookup('9782070363735', 'tri');

    expect(sync.syncAll).toHaveBeenCalled();
  });

  it('does not close a session while its last scan is still pending', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>(
      'ScanWorkflowService',
      ['requestClose'],
    );
    const internals = component as unknown as {
      changeDetector: ChangeDetectorRef;
      destroyRef: DestroyRef;
    };
    const localComponent = new ScannerComponent(
      metadataService,
      cameraService,
      internals.changeDetector,
      internals.destroyRef,
      workflow,
      null,
      null,
    );
    localComponent.session = createSession();
    localComponent.localScan = createLocalScanResult();

    await localComponent.endSession();

    expect(workflow.requestClose).not.toHaveBeenCalled();
    expect(localComponent.screen).toBe('tri');
    expect(localComponent.syncError).toContain('dernier livre');
  });

  function createMetadata(title = 'Le Petit Prince'): BookMetadata {
    return {
      isbn13: '9782070363735',
      title,
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1946,
      coverUrl: null,
      source: 'BnF',
      workId: null,
      retrievedAt: '2026-09-03T08:00:00Z',
    };
  }

  function createLocalScanResult(): LocalScanResult {
    return {
      entry: {
        clientGestureId: 'gesture-1',
        scanSessionId: 'session-1',
        isbn13: '9782070363735',
        occurredAt: '2026-09-03T08:00:00Z',
        createdAt: '2026-09-03T08:00:00Z',
        status: 'Pending',
        kept: null,
        catalogApplied: false,
        verdict: 'FirstCopy',
        quantityAvailable: 0,
        quantityAnnounced: 0,
        salesCount: 0,
        isRare: false,
        attemptCount: 0,
        lastAttemptAt: null,
        lastError: null,
      },
      verdict: {
        verdict: 'FirstCopy',
        totalKnownQuantity: 0,
        salesCount: 0,
        activeRequesterCount: 0,
        isRare: false,
        isKnown: false,
      },
      catalogBook: null,
      isImmediateRepeat: false,
    };
  }

  function createCashItem(id: string, title: string) {
    return {
      id,
      isbn13: '9782070363735',
      title,
      authors: null,
      publisher: null,
      publicationYear: null,
      isRare: false,
      quantityAvailable: 0,
      quantityAnnounced: 0,
    };
  }

  function createSession(overrides: Partial<ScanSessionSnapshot> = {}): ScanSessionSnapshot {
    return {
      key: 'active-session',
      scanSessionId: 'session-1',
      volunteerId: null,
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00Z',
      lastScanAt: '2026-09-03T08:02:00Z',
      lastSyncAt: '2026-09-03T08:00:00Z',
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
      ...overrides,
    };
  }
});
