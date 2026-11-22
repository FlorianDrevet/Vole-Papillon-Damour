import {HttpErrorResponse} from '@angular/common/http';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {Router} from '@angular/router';
import {of, throwError} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanApiService} from '../offline/scan-api.service';
import {ScanPublicCatalogBook} from '../offline/scan-offline.model';
import {CameraScannerHandle, CameraScannerService} from './camera-scanner.service';
import {BookLookupPageComponent} from './book-lookup-page.component';

describe('BookLookupPageComponent', () => {
  let fixture: ComponentFixture<BookLookupPageComponent>;
  let component: BookLookupPageComponent;
  let api: jasmine.SpyObj<ScanApiService>;
  let camera: jasmine.SpyObj<CameraScannerService>;
  let router: jasmine.SpyObj<Router>;
  let auth: {canSort: boolean; canManageRareBooks: boolean};
  let detectedIsbn: ((isbn: string) => void) | null;
  let cameraHandle: CameraScannerHandle;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', ['getPublicCatalogBook']);
    api.getPublicCatalogBook.and.returnValue(of(createBook()));
    camera = jasmine.createSpyObj<CameraScannerService>('CameraScannerService', ['start']);
    router = jasmine.createSpyObj<Router>('Router', ['navigateByUrl']);
    router.navigateByUrl.and.resolveTo(true);
    auth = {canSort: true, canManageRareBooks: false};
    detectedIsbn = null;
    cameraHandle = {
      resume: jasmine.createSpy('resume'),
      refocus: jasmine.createSpy('refocus').and.resolveTo(true),
      stop: jasmine.createSpy('stop').and.resolveTo(),
    };
    camera.start.and.callFake((_container, onDetected) => {
      detectedIsbn = onDetected;
      return Promise.resolve(cameraHandle);
    });

    await TestBed.configureTestingModule({
      imports: [BookLookupPageComponent],
      providers: [
        {provide: ScanApiService, useValue: api},
        {provide: CameraScannerService, useValue: camera},
        {provide: Router, useValue: router},
        {provide: ScanAuthService, useValue: auth},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(BookLookupPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('normalizes a typed ISBN and shows the matching catalog fiche', async () => {
    component.onIsbnChanged('0-306-40615-2');

    await component.search();
    fixture.detectChanges();

    expect(api.getPublicCatalogBook).toHaveBeenCalledOnceWith('9780306406157');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).toContain('1 exemplaire disponible');
  });

  it('opens as a dedicated page with a return action', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('main[aria-labelledby="book-lookup-title"]')).not.toBeNull();
    const returnButton = fixture.nativeElement.querySelector('[data-zone="book-lookup-return"]') as HTMLButtonElement;
    expect(returnButton).not.toBeNull();
    returnButton.click();
    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/accueil');
  });

  it('offers label reprinting for a found ISBN and opens the existing label preview', async () => {
    component.onIsbnChanged('9780306406157');
    await component.search();
    fixture.detectChanges();

    const reprintButton = fixture.nativeElement.querySelector('[data-zone="book-lookup-reprint"]') as HTMLButtonElement;
    expect(reprintButton).not.toBeNull();
    expect(reprintButton.textContent).toContain('Réimprimer l’étiquette');

    reprintButton.click();
    await fixture.whenStable();

    expect(router.navigateByUrl).toHaveBeenCalledOnceWith('/etiquette?isbn=9780306406157');
  });

  it('hides label reprinting when the account cannot print', async () => {
    auth.canSort = false;
    auth.canManageRareBooks = false;
    component.onIsbnChanged('9780306406157');
    await component.search();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-zone="book-lookup-reprint"]')).toBeNull();
  });

  it('submits the ISBN entered in the form', async () => {
    const input = fixture.nativeElement.querySelector('#book-lookup-isbn') as HTMLInputElement;
    input.value = '0-306-40615-2';
    input.dispatchEvent(new Event('input', {bubbles: true}));
    await fixture.whenStable();
    fixture.detectChanges();
    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit', {bubbles: true, cancelable: true}));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getPublicCatalogBook).toHaveBeenCalledOnceWith('9780306406157');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
  });

  it('explains when the ISBN has no fiche in the book fair catalog', async () => {
    api.getPublicCatalogBook.and.returnValue(
      throwError(() => new HttpErrorResponse({status: 404, statusText: 'Not Found'})),
    );
    component.onIsbnChanged('9780306406157');

    await component.search();
    fixture.detectChanges();

    expect(component.lookupState()).toBe('not-found');
    expect(fixture.nativeElement.textContent)
      .toContain('Aucune fiche n’existe pour ce livre dans la bourse aux livres.');
  });

  it('does not request the catalog for an invalid ISBN', async () => {
    component.onIsbnChanged('12345');

    await component.search();
    fixture.detectChanges();

    expect(api.getPublicCatalogBook).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Saisissez un ISBN-10 ou ISBN-13 valide.');
  });

  it('looks up the ISBN detected by the camera and stops the camera', async () => {
    await component.startCamera();
    detectedIsbn?.('9780306406157');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getPublicCatalogBook).toHaveBeenCalledOnceWith('9780306406157');
    expect(cameraHandle.stop).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
  });

  it('keeps catalog service failures distinct from a missing fiche', async () => {
    api.getPublicCatalogBook.and.returnValue(
      throwError(() => new HttpErrorResponse({status: 503, statusText: 'Unavailable'})),
    );
    component.onIsbnChanged('9780306406157');

    await component.search();
    fixture.detectChanges();

    expect(component.lookupState()).toBe('unavailable');
    expect(fixture.nativeElement.textContent).toContain('Impossible de vérifier le catalogue pour le moment.');
  });

  it('keeps the search result within a narrow mobile width', async () => {
    fixture.nativeElement.style.width = '320px';
    component.onIsbnChanged('9780306406157');

    await component.search();
    fixture.detectChanges();

    const page = fixture.nativeElement.querySelector('.book-lookup-page') as HTMLElement;
    const card = fixture.nativeElement.querySelector('.book-lookup') as HTMLElement;
    const actions = fixture.nativeElement.querySelector('.book-lookup-actions') as HTMLElement;

    expect(page.scrollWidth).toBeLessThanOrEqual(page.clientWidth);
    expect(card.scrollWidth).toBeLessThanOrEqual(card.clientWidth);
    expect(actions.scrollWidth).toBeLessThanOrEqual(actions.clientWidth);
  });

  function createBook(): ScanPublicCatalogBook {
    return {
      isbn13: '9780306406157',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1943,
      genre: 'Conte',
      coverUrl: null,
      quantityAvailable: 1,
      quantityAnnounced: 2,
      nextFairAt: null,
      isRare: false,
    };
  }
});
