import {HttpErrorResponse} from '@angular/common/http';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {ChangeDetectorRef, DestroyRef} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {defer, of, Subject, throwError} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerService} from './camera-scanner.service';
import {ScannerComponent} from './scanner.component';
import {ScanAuthService} from '../auth/scan-auth.service';
import {
  LocalScanResult,
  ScanAssociationSettings,
  ScanLocalStoreError,
  ScanNextBookFair,
  ScanSaleOutboxEntry,
  ScanSessionSnapshot,
} from '../offline/scan-offline.model';
import {ScanSyncService} from '../offline/scan-sync.service';
import {ScanStatusBarComponent} from '../offline/scan-status-bar.component';
import {ScanRecoveryComponent} from '../offline/scan-recovery.component';
import {ScanStatusService} from '../offline/scan-status.service';
import {ScanWorkflowService} from '../offline/scan-workflow.service';
import {ScanConfirmationService} from '../scan-confirmation.service';

describe('ScannerComponent', () => {
  let fixture: ComponentFixture<ScannerComponent>;
  let component: ScannerComponent;
  let metadataService: jasmine.SpyObj<BookMetadataService>;
  let cameraService: jasmine.SpyObj<CameraScannerService>;
  let confirmDialog: jasmine.Spy;

  beforeEach(async () => {
    metadataService = jasmine.createSpyObj<BookMetadataService>('BookMetadataService', ['getMetadata']);
    cameraService = jasmine.createSpyObj<CameraScannerService>('CameraScannerService', ['start', 'scanFile']);
    const confirmation = jasmine.createSpyObj<ScanConfirmationService>(
      'ScanConfirmationService',
      ['confirm'],
    );
    confirmation.confirm.and.resolveTo(true);
    confirmDialog = confirmation.confirm;

    await TestBed.configureTestingModule({
      declarations: [ScannerComponent, ScanStatusBarComponent, ScanRecoveryComponent],
      imports: [CommonModule, FormsModule, DesignSystemModule],
      providers: [
        {provide: BookMetadataService, useValue: metadataService},
        {provide: CameraScannerService, useValue: cameraService},
        {provide: ScanAuthService, useValue: null},
        {provide: ScanSyncService, useValue: null},
        ScanStatusService,
        {provide: ScanWorkflowService, useValue: null},
        {provide: ScanConfirmationService, useValue: confirmation},
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

    await component.lookup(component.isbnInput, 'tri');

    expect(metadataService.getMetadata).toHaveBeenCalledOnceWith('9780306406157');
    expect(component.metadata).toEqual(metadata);
    expect(component.errorMessage).toBeNull();
  });

  it('subscribes to a login request started from the scanner surface', () => {
    let loginStarted = false;
    const auth = jasmine.createSpyObj<ScanAuthService>('ScanAuthService', ['login']);
    auth.login.and.returnValue(defer(() => {
      loginStarted = true;
      return of(undefined);
    }));

    const internals = component as unknown as {
      changeDetector: ChangeDetectorRef;
      destroyRef: DestroyRef;
    };
    const localComponent = new ScannerComponent(
      metadataService,
      cameraService,
      internals.changeDetector,
      internals.destroyRef,
      null,
      auth,
      null,
    );

    localComponent.login();

    expect(auth.login).toHaveBeenCalledOnceWith();
    expect(loginStarted).toBeTrue();
  });

  it('asks before reconciling a local session owned by another account', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'getSession',
      'bindSessionToVolunteer',
    ]);
    workflow.getSession.and.resolveTo(createSession({volunteerId: 'account-a'}));
    const internals = component as unknown as {
      reconcileCurrentAccount: (volunteerId: string) => Promise<void>;
    };
    (component as unknown as {scanWorkflow: ScanWorkflowService}).scanWorkflow = workflow;
    (component as unknown as {localModeReady: boolean}).localModeReady = true;

    await internals.reconcileCurrentAccount('account-b');

    expect(component.accountSwitchPrompt).toBe(
      "Des gestes d'une session précédente sont présents sur cet appareil. Les reprendre sous votre compte, ou les mettre de côté pour un responsable ?",
    );
    expect(workflow.bindSessionToVolunteer).not.toHaveBeenCalled();
  });

  it('rebinds explicitly and returns the set-aside gestures to the active session', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'bindSessionToVolunteer',
      'reattachNeedsReattachToCurrentSession',
      'getSession',
      'getSessionCounts',
      'getOutboxCounts',
      'getSettings',
      'getCatalogSyncState',
    ]);
    const session = createSession({volunteerId: 'account-b'});
    workflow.bindSessionToVolunteer.and.resolveTo();
    workflow.reattachNeedsReattachToCurrentSession.and.resolveTo(2);
    workflow.getSession.and.resolveTo(session);
    workflow.getSessionCounts.and.resolveTo({scannedCount: 0, keptCount: 0, rejectedCount: 0});
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSettings.and.resolveTo(null);
    workflow.getCatalogSyncState.and.resolveTo(null);
    const internals = component as unknown as {
      accountSwitchAccountId: string | null;
    };
    (component as unknown as {scanWorkflow: ScanWorkflowService}).scanWorkflow = workflow;
    internals.accountSwitchAccountId = 'account-b';
    component.accountSwitchPrompt = "Des gestes d'une session précédente sont présents sur cet appareil. Les reprendre sous votre compte, ou les mettre de côté pour un responsable ?";

    await component.resumeAccountSwitch();

    expect(workflow.bindSessionToVolunteer).toHaveBeenCalledOnceWith('account-b', true);
    expect(workflow.reattachNeedsReattachToCurrentSession).toHaveBeenCalledOnceWith();
    expect(component.accountSwitchPrompt).toBeNull();
  });

  it('sets the previous session aside explicitly before allowing a new account session', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'setAsideCurrentSessionForOtherVolunteer',
      'clearSession',
    ]);
    workflow.setAsideCurrentSessionForOtherVolunteer.and.resolveTo(2);
    workflow.clearSession.and.resolveTo();
    const internals = component as unknown as {
      accountSwitchAccountId: string | null;
    };
    (component as unknown as {scanWorkflow: ScanWorkflowService}).scanWorkflow = workflow;
    internals.accountSwitchAccountId = 'account-b';
    component.accountSwitchPrompt = "Des gestes d'une session précédente sont présents sur cet appareil. Les reprendre sous votre compte, ou les mettre de côté pour un responsable ?";

    await component.setAsideAccountSwitch();

    expect(workflow.setAsideCurrentSessionForOtherVolunteer).toHaveBeenCalledOnceWith();
    expect(workflow.clearSession).toHaveBeenCalledOnceWith();
    expect(component.accountSwitchPrompt).toBeNull();
    expect(component.screen).toBe('home');
  });

  it('blocks changing user while local gestures still need attention', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'getOutboxCounts',
      'getSession',
      'getSettings',
      'getCatalogSyncState',
      'clearAccountState',
    ]);
    const auth = jasmine.createSpyObj<ScanAuthService>('ScanAuthService', ['logout']);
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 1, pendingTransmissionCount: 2});
    workflow.getSession.and.resolveTo(createSession());
    workflow.getSettings.and.resolveTo(null);
    workflow.getCatalogSyncState.and.resolveTo(null);

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
      auth,
      null,
    );

    await localComponent.logout();

    expect(auth.logout).not.toHaveBeenCalled();
    expect(workflow.clearAccountState).not.toHaveBeenCalled();
    expect(localComponent.logoutError).toContain('Synchronisez');
  });

  it('purges local account state before logging out when no gesture remains', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'getOutboxCounts',
      'getSession',
      'getSettings',
      'getCatalogSyncState',
      'clearAccountState',
    ]);
    const auth = jasmine.createSpyObj<ScanAuthService>('ScanAuthService', ['logout']);
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSession.and.resolveTo(createSession());
    workflow.getSettings.and.resolveTo(null);
    workflow.getCatalogSyncState.and.resolveTo(null);
    workflow.clearAccountState.and.resolveTo();

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
      auth,
      null,
    );
    localComponent.cashItems = [{
      id: 'cash-1',
      isbn13: '9782070363735',
      title: 'Livre',
      authors: null,
      publisher: null,
      publicationYear: null,
      isRare: false,
      quantityAvailable: 1,
      quantityAnnounced: 0,
    }];
    localComponent.localScan = createLocalScanResult();
    localComponent.cashMessage = 'Vente locale';

    await localComponent.logout();

    expect(workflow.clearAccountState).toHaveBeenCalledOnceWith();
    expect(auth.logout).toHaveBeenCalledOnceWith();
    expect(localComponent.cashItems).toEqual([]);
    expect(localComponent.localScan).toBeNull();
    expect(localComponent.cashMessage).toBeNull();
  });

  it('refreshes the rendered result when a manual lookup completes', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    component.isbnInput = '978-2-07-036373-5';

    await component.lookup(component.isbnInput, 'tri');
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

    await component.lookup(component.isbnInput, 'tri');

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

    expect(fixture.nativeElement.querySelector('vpd-book-cover-placeholder')).not.toBeNull();
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
    expect(fixture.nativeElement.querySelector('[aria-label="Fermer la saisie"]')).not.toBeNull();
  });

  it('gives labeled scan containers an accessible semantic role and lets the ISBN speak its value', () => {
    const cameraHost = fixture.nativeElement.querySelector('.camera-engine-host') as HTMLElement;
    expect(cameraHost.getAttribute('role')).toBe('region');

    const manualFixture = TestBed.createComponent(ScannerComponent);
    const manualComponent = manualFixture.componentInstance;
    manualFixture.detectChanges();
    manualComponent.openManualInput();
    manualFixture.detectChanges();

    const manualValue = manualFixture.nativeElement.querySelector('.manual-value') as HTMLElement;
    expect(manualValue.getAttribute('aria-live')).toBe('polite');
    expect(manualValue.hasAttribute('aria-label')).toBeFalse();
    expect(manualFixture.nativeElement.querySelector('.manual-keypad')?.getAttribute('role')).toBe('group');
    manualFixture.destroy();

    const cashFixture = TestBed.createComponent(ScannerComponent);
    cashFixture.componentInstance.screen = 'cash';
    cashFixture.detectChanges();

    const cashItems = cashFixture.nativeElement.querySelector('.cash-items') as HTMLElement;
    expect(cashItems.getAttribute('role')).toBe('region');
    expect(cashItems.getAttribute('aria-label')).toBe('Livres de la vente');
    cashFixture.destroy();
  });

  it('keeps a valid h1 label target for every operating screen and triage state', () => {
    const screens = [
      ['tri', '.tri-screen'],
      ['manual', '.manual-screen'],
      ['session-end', '.session-end-screen'],
      ['cash', '.cash-screen'],
      ['consultation', '.consultation-screen'],
    ] as const;

    for (const [screen, selector] of screens) {
      const screenFixture = TestBed.createComponent(ScannerComponent);
      const screenComponent = screenFixture.componentInstance;
      screenComponent.screen = screen;
      screenComponent.localScan = null;
      screenComponent.metadata = null;
      screenFixture.detectChanges();

      const section = screenFixture.nativeElement.querySelector(selector) as HTMLElement;
      const headingId = section.getAttribute('aria-labelledby');
      const heading = headingId ? section.querySelector(`#${headingId}`) : null;
      expect(headingId).withContext(screen).toBeTruthy();
      expect(heading?.tagName).withContext(screen).toBe('H1');
      screenFixture.destroy();
    }

    const verdictFixture = TestBed.createComponent(ScannerComponent);
    const verdictComponent = verdictFixture.componentInstance;
    verdictComponent.screen = 'tri';
    verdictComponent.localScan = createLocalScanResult();
    verdictFixture.detectChanges();

    const triSection = verdictFixture.nativeElement.querySelector('.tri-screen') as HTMLElement;
    const triHeadingId = triSection.getAttribute('aria-labelledby');
    expect(triHeadingId).toBeTruthy();
    expect(triHeadingId ? triSection.querySelector(`#${triHeadingId}`)?.tagName : null).toBe('H1');
    verdictFixture.destroy();
  });

  it('refreshes the rendered result when the live camera detects an ISBN', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    let onDetected: ((rawValue: string) => void) | undefined;
    cameraService.start.and.callFake(async (_container, callback) => {
      onDetected = callback;
      return {resume: () => undefined, refocus: async () => true, stop: async () => undefined};
    });

    await component.toggleCamera();
    onDetected?.('9782070363735');
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('#book-title')?.textContent).toContain(metadata.title);
  });

  it('shows that a live camera scan was found while the book lookup is pending', async () => {
    const metadataResponse = new Subject<BookMetadata>();
    metadataService.getMetadata.and.returnValue(metadataResponse.asObservable());
    let onDetected: ((rawValue: string) => void) | undefined;
    cameraService.start.and.callFake(async (_container, callback) => {
      onDetected = callback;
      return {resume: () => undefined, refocus: async () => true, stop: async () => undefined};
    });
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();
    onDetected?.('9782070363735');
    fixture.detectChanges();

    expect(component.isLoading).toBeTrue();
    expect(fixture.nativeElement.querySelector('.scan-progress')?.textContent)
      .toContain('Scan détecté');

    metadataResponse.next(createMetadata());
    metadataResponse.complete();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.scan-progress')).toBeNull();
  });

  it('shows the scan progress in the tri screen while the lookup is pending', async () => {
    const metadataResponse = new Subject<BookMetadata>();
    metadataService.getMetadata.and.returnValue(metadataResponse.asObservable());

    const lookup = component.lookup('9782070363735', 'tri');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.scan-progress-dark')?.textContent)
      .toContain('Recherche du livre');

    metadataResponse.next(createMetadata());
    metadataResponse.complete();
    await lookup;
  });

  it('shows the scan progress in consultation while the lookup is pending', async () => {
    const metadataResponse = new Subject<BookMetadata>();
    metadataService.getMetadata.and.returnValue(metadataResponse.asObservable());
    component.screen = 'consultation';

    const lookup = component.lookup('9782070363735', 'consultation');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.consultation-lookup-progress')?.textContent)
      .toContain('Scan détecté');

    metadataResponse.next(createMetadata());
    metadataResponse.complete();
    await lookup;
  });

  it('requests a refocus when the active camera preview is tapped', async () => {
    const refocus = jasmine.createSpy('refocus').and.returnValue(Promise.resolve(true));
    cameraService.start.and.callFake(async (_container, _callback) => ({
      resume: () => undefined,
      refocus,
      stop: async () => undefined,
    }));
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();
    fixture.detectChanges();

    const preview = fixture.nativeElement.querySelector('.camera-engine-host-active') as HTMLElement;
    expect(preview.getAttribute('role')).toBe('button');
    expect(preview.getAttribute('aria-label')).toContain('mise au point');
    preview.click();
    await fixture.whenStable();

    expect(refocus).toHaveBeenCalledOnceWith();
  });

  it('keeps the native autofocus fallback explicit when refocus is unavailable', async () => {
    const refocus = jasmine.createSpy('refocus').and.returnValue(Promise.resolve(false));
    cameraService.start.and.returnValue(Promise.resolve({
      resume: () => undefined,
      refocus,
      stop: async () => undefined,
    }));
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();
    await component.refocusCamera();
    fixture.detectChanges();

    expect(component.cameraFocusStatus).toBe('unavailable');
    expect(fixture.nativeElement.querySelector('.camera-engine-host-focus-unavailable')).not.toBeNull();
  });

  it('does not add a cash line when the local catalog lookup fails', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'lookupCatalog',
    ]);
    workflow.lookupCatalog.and.rejectWith(new Error('IndexedDB unavailable'));
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
      null,
    );

    await localComponent.lookup('9782070363735', 'cash');

    expect(localComponent.cashItems).toEqual([]);
    expect(localComponent.storageError).toContain('catalogue local');
  });

  it('surfaces a local catalog lookup failure in consultation', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'lookupCatalog',
    ]);
    workflow.lookupCatalog.and.rejectWith(new Error('IndexedDB unavailable'));
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
      null,
    );

    await localComponent.lookup('9782070363735', 'consultation');

    expect(localComponent.storageError).toContain('catalogue local');
  });

  it('distinguishes a local database closed by another instance', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'lookupCatalog',
    ]);
    workflow.lookupCatalog.and.rejectWith(new ScanLocalStoreError(
      'closed-by-other-instance',
      'connection closed',
    ));
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
      null,
    );

    await localComponent.lookup('9782070363735', 'consultation');

    expect(localComponent.storageError).toContain('fermée par une autre instance');
  });

  it('uses a dedicated message when a scan is attempted while the session awaits closure', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'recordScan',
    ]);
    workflow.recordScan.and.rejectWith(
      new Error('The scan session is waiting for synchronization to close.'),
    );
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
      null,
    );

    await localComponent.lookup('9782070363735', 'tri');

    expect(localComponent.storageError).toContain('clôture');
    expect(localComponent.storageError).not.toContain('conservé localement');
  });

  it('starts the live camera when the scan screen opens without a scanner button', async () => {
    cameraService.start.and.returnValue(Promise.resolve({resume: () => undefined, refocus: async () => true, stop: async () => undefined}));
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
    cameraService.start.and.returnValue(Promise.resolve({resume: () => undefined, refocus: async () => true, stop: async () => undefined}));
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
    cameraService.start.and.returnValue(Promise.resolve({resume: () => undefined, refocus: async () => true, stop: async () => undefined}));
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

  it('keeps the live camera session after a cash scan so the next book can be read immediately', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    const detected: Array<(rawValue: string) => void> = [];
    const resume = jasmine.createSpy('resume');
    cameraService.start.and.callFake(async (_container, callback) => {
      detected.push(callback);
      return {resume, refocus: async () => true, stop: async () => undefined};
    });
    component.authAvailable = true;
    component.isAuthenticated = true;

    component.openCash();
    await fixture.whenStable();
    detected[0]('9782070363735');
    await fixture.whenStable();

    expect(component.cashItems).toHaveSize(1);
    expect(cameraService.start).toHaveBeenCalledTimes(1);
    expect(resume).toHaveBeenCalledOnceWith();
    expect(component.cameraActive).toBeTrue();
  });

  it('resumes the existing camera session after keeping a triaged book', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'recordScan',
      'cacheMetadata',
      'getOutboxCounts',
      'getSession',
      'getSettings',
      'getCatalogSyncState',
      'decide',
    ]);
    const localScan = createLocalScanResult();
    const resume = jasmine.createSpy('resume');
    let onDetected: ((rawValue: string) => void) | undefined;

    workflow.recordScan.and.resolveTo(localScan);
    workflow.cacheMetadata.and.resolveTo();
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSession.and.resolveTo(createSession());
    workflow.decide.and.resolveTo({...localScan.entry, status: 'Kept', kept: true});
    metadataService.getMetadata.and.returnValue(of(createMetadata()));
    cameraService.start.and.callFake(async (_container, callback) => {
      onDetected = callback;
      return {resume, refocus: async () => true, stop: async () => undefined};
    });

    (component as unknown as {scanWorkflow: ScanWorkflowService}).scanWorkflow = workflow;
    (component as unknown as {localModeReady: boolean}).localModeReady = true;
    component.authAvailable = true;
    component.isAuthenticated = true;
    component.screen = 'tri';

    await (component as unknown as {startCamera: () => Promise<void>}).startCamera();
    onDetected?.('9782070363735');
    await fixture.whenStable();
    await component.keepCurrentScan();

    expect(cameraService.start).toHaveBeenCalledTimes(1);
    expect(resume).toHaveBeenCalledOnceWith();
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

  it('keeps an unfinished cash sale when leaving is cancelled', () => {
    confirmDialog.and.returnValue(false);
    component.screen = 'cash';
    component.cashItems = [createCashItem('sale-1', 'Livre vendu')];

    component.returnHome();

    expect(confirmDialog).toHaveBeenCalled();
    expect(component.cashItems).toHaveSize(1);
    expect(component.screen).toBe('cash');
  });

  it('asks for confirmation before ending a session', async () => {
    confirmDialog.and.returnValue(false);
    component.session = createSession();
    component.sessionCounts = {scannedCount: 1, keptCount: 1, rejectedCount: 0};

    await component.endSession();

    expect(confirmDialog).toHaveBeenCalled();
    expect(component.screen).not.toBe('session-end');
    expect(component.completedSession).toBeNull();
  });

  it('persists the cash batch before clearing the visible list', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>(
      'ScanWorkflowService',
      ['recordCashSales', 'getOutboxCounts', 'getSession', 'getSettings', 'getCatalogSyncState'],
    );
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    workflow.recordCashSales.and.resolveTo([]);
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 1});
    workflow.getSession.and.resolveTo(null);
    sync.syncAll.and.resolveTo({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 0, remaining: 1, stoppedOnError: false, newlyOrphaned: 0, newlyQuarantined: 0},
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

  it('exposes cancellation of the last validated sale while its outbox entries remain pending', async () => {
    const saleEntry = createSaleOutboxEntry();
    const recordCashSales = jasmine.createSpy('recordCashSales').and.resolveTo([saleEntry]);
    const deleteSaleOutboxEntry = jasmine.createSpy('deleteSaleOutboxEntry').and.resolveTo(true);
    const workflow = {
      recordCashSales,
      deleteSaleOutboxEntry,
      getOutboxCounts: jasmine.createSpy('getOutboxCounts').and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 1}),
      getSession: jasmine.createSpy('getSession').and.resolveTo(null),
      getSettings: jasmine.createSpy('getSettings').and.resolveTo(null),
      getCatalogSyncState: jasmine.createSpy('getCatalogSyncState').and.resolveTo(null),
    } as unknown as ScanWorkflowService;

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
    localComponent.cashItems = [createCashItem('sale-1', 'Livre vendu')];

    await localComponent.validateCash();

    const state = localComponent as unknown as {canCancelLastSale: boolean};
    const cancelLastSale = (localComponent as unknown as {
      cancelLastSale: () => Promise<void>;
    }).cancelLastSale;
    expect(state.canCancelLastSale).toBeTrue();

    await cancelLastSale.call(localComponent);

    expect(deleteSaleOutboxEntry).toHaveBeenCalledOnceWith(saleEntry.clientGestureId);
    expect(state.canCancelLastSale).toBeFalse();
  });

  it('offers the ISBN-10 control key X on the manual keypad', () => {
    expect(component.manualKeys as readonly string[]).toContain('X');

    component.openManualInput();
    fixture.detectChanges();

    const keypadLabels = Array.from(
      fixture.nativeElement.querySelectorAll('.manual-key') as NodeListOf<HTMLButtonElement>,
    ).map(button => button.textContent?.trim());
    expect(keypadLabels).toContain('X');
  });

  it('describes the manual input as 10 or 13 ISBN characters', () => {
    component.openManualInput();
    component.manualIsbn = '123456789X';
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.manual-count')?.textContent)
      .toContain('10 caractères');
    expect(fixture.nativeElement.querySelector('.manual-count')?.textContent)
      .toContain('10 ou 13');
  });

  it('shows the synchronization action and separate queue labels on every operating screen', () => {
    for (const screen of ['tri', 'cash', 'consultation'] as const) {
      const screenFixture = TestBed.createComponent(ScannerComponent);
      const screenComponent = screenFixture.componentInstance;
      screenComponent.screen = screen;
      screenComponent.pendingDecisionCount = 2;
      screenComponent.pendingTransmissionCount = 3;
      screenComponent.syncError = 'Synchronisation à reprendre';
      TestBed.inject(ScanStatusService).updateFromLocalState(
        null,
        {pendingDecisionCount: 2, pendingTransmissionCount: 3},
        {orphaned: 0, quarantined: 0},
      );
      screenFixture.detectChanges();

      const panel = screenFixture.nativeElement.querySelector('.status-bar') as HTMLElement | null;
      expect(panel).not.toBeNull();
      expect(panel?.textContent).toContain('2 livres attendent une décision');
      expect(panel?.textContent).toContain('3 livres en attente d’envoi');
      expect(panel?.textContent).toContain('Synchroniser');
      expect(panel?.textContent).not.toContain('scans en attente');

      screenFixture.destroy();
    }
  });

  it('does not turn an existing set-aside backlog into an error on the next successful sync', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'getOutboxCounts',
      'getSession',
      'getSettings',
      'getCatalogSyncState',
      'getSetAsideCounts',
    ]);
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    const status = TestBed.inject(ScanStatusService);
    const summary = {
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {
        sent: 0,
        remaining: 0,
        stoppedOnError: false,
        orphaned: 2,
        quarantined: 0,
        newlyOrphaned: 0,
        newlyQuarantined: 0,
      },
      closed: false,
    };
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSession.and.resolveTo(null);
    workflow.getSettings.and.resolveTo(null);
    workflow.getCatalogSyncState.and.resolveTo(null);
    workflow.getSetAsideCounts.and.resolveTo({orphaned: 2, quarantined: 0});
    sync.syncAll.and.returnValues(Promise.resolve(summary), Promise.resolve(summary));

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
      status,
    );
    localComponent.authAvailable = true;
    localComponent.isAuthenticated = true;
    localComponent.isOnline = true;
    (localComponent as unknown as {localModeReady: boolean}).localModeReady = true;

    await localComponent.syncNow();
    await localComponent.syncNow();

    expect(localComponent.syncError).toBeNull();
    expect(status.snapshot().setAside).toEqual({total: 2});
    expect(status.message()).toBeNull();
  });

  it('uses the synchronized next fair date and alert delay in the session copy', () => {
    component.nextFair = {
      id: 'fair-1',
      name: 'Bourse de septembre',
      dateStart: '2026-09-14T09:00:00+02:00',
      dateEnd: '2026-09-14T18:00:00+02:00',
      openAt: '2026-09-14T09:00:00+02:00',
      closeAt: '2026-09-14T18:00:00+02:00',
    } satisfies ScanNextBookFair;
    component.associationSettings = {
      duplicateThreshold: 5,
      demandSalesThreshold: 1,
      deadStockMinAgeDays: 30,
      deadStockMinQuantity: 1,
      watchlistMaxItems: 100,
      alertCooldownDays: 30,
      sessionIdleTimeoutMinutes: 120,
      alertDelayMinutes: 90,
      updatedAt: '2026-09-03T08:00:00Z',
    } satisfies ScanAssociationSettings;

    expect(component.nextFairShortLabel).toContain('14 septembre 2026');
    expect(component.nextFairLongLabel).toContain('14 septembre 2026');
    expect(component.correctionWindowLabel).toBe('1 h 30');
  });

  it('opens a new session mode screen after a session has been ended', async () => {
    component.session = createSession();
    component.sessionCounts = {scannedCount: 2, keptCount: 2, rejectedCount: 0};

    await component.endSession();
    component.returnHome();
    await component.startSorting();

    expect(component.screen).toBe('session-mode');
  });

  it('clears the durable session only after the sync service confirms close', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'requestClose',
      'getOutboxCounts',
      'getSession',
      'getSettings',
      'getCatalogSyncState',
    ]);
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    const session = createSession();
    const completedScan = createLocalScanResult();
    completedScan.entry = {...completedScan.entry, status: 'Kept', kept: true};
    const requestedSession = {...session, closeRequested: true, closeReason: 'Manual' as const};

    workflow.requestClose.and.resolveTo(requestedSession);
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSession.and.returnValues(
      Promise.resolve(requestedSession),
      Promise.resolve(null),
    );
    sync.syncAll.and.resolveTo({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 1, remaining: 0, stoppedOnError: false, newlyOrphaned: 0, newlyQuarantined: 0},
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
      ['recordScan', 'cacheMetadata', 'getOutboxCounts', 'getSession', 'getSettings', 'getCatalogSyncState'],
    );
    const sync = jasmine.createSpyObj<ScanSyncService>('ScanSyncService', ['syncAll']);
    workflow.recordScan.and.returnValue(Promise.resolve(createLocalScanResult()));
    workflow.cacheMetadata.and.returnValue(Promise.resolve());
    workflow.getOutboxCounts.and.returnValue(Promise.resolve({pendingDecisionCount: 0, pendingTransmissionCount: 1}));
    workflow.getSession.and.returnValue(Promise.resolve(createSession()));
    sync.syncAll.and.returnValue(Promise.resolve({
      catalog: {booksReceived: 0, booksRemoved: 0, watermark: 'watermark'},
      outbox: {sent: 0, remaining: 1, stoppedOnError: false, newlyOrphaned: 0, newlyQuarantined: 0},
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

  it('closes a session while moving its last pending scan to a decision state', async () => {
    const workflow = jasmine.createSpyObj<ScanWorkflowService>(
      'ScanWorkflowService',
      ['requestClose', 'getSession', 'getOutboxCounts', 'getSettings', 'getCatalogSyncState'],
    );
    const closedSession = createSession();
    closedSession.closeRequested = true;
    workflow.requestClose.and.resolveTo(closedSession);
    workflow.getSession.and.resolveTo(closedSession);
    workflow.getOutboxCounts.and.resolveTo({pendingDecisionCount: 0, pendingTransmissionCount: 0});
    workflow.getSettings.and.resolveTo(null);
    workflow.getCatalogSyncState.and.resolveTo(null);
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

    expect(workflow.requestClose).toHaveBeenCalledOnceWith('Manual');
    expect(localComponent.screen).toBe('session-end');
    expect(localComponent.syncError).toBeNull();
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
        clientSessionId: 'session-1',
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

  function createSaleOutboxEntry(): ScanSaleOutboxEntry {
    return {
      clientGestureId: 'sale-1',
      isbn13: '9782070363735',
      quantity: 1,
      status: 'Pending',
      occurredAt: '2026-09-03T08:01:00.000Z',
      createdAt: '2026-09-03T08:01:00.000Z',
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
    };
  }

  function createSession(overrides: Partial<ScanSessionSnapshot> = {}): ScanSessionSnapshot {
    return {
      key: 'active-session',
      clientSessionId: 'session-1',
      remoteSessionId: null,
      volunteerId: null,
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00Z',
      lastScanAt: '2026-09-03T08:02:00Z',
      lastSyncAt: '2026-09-03T08:00:00Z',
      ...overrides,
    };
  }
});
