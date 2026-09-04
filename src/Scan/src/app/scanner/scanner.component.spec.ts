import {HttpErrorResponse} from '@angular/common/http';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {ChangeDetectorRef} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of, Subject, throwError} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerService} from './camera-scanner.service';
import {ScannerComponent} from './scanner.component';
import {ScanAuthService} from '../auth/scan-auth.service';
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
});
