import {ComponentFixture, TestBed} from '@angular/core/testing';
import {LOCALE_ID, signal} from '@angular/core';
import {registerLocaleData} from '@angular/common';
import localeFr from '@angular/common/locales/fr';
import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {RouterModule} from '@angular/router';
import {By} from '@angular/platform-browser';
import {of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogSelectionService} from '../../core/selection/catalog-selection.service';
import {CatalogRareBook, CatalogRareBookDetail} from '../../core/catalog.models';
import {CatalogRareBookCardComponent} from '../../shared/rare-book-card/rare-book-card.component';
import {CatalogAuthPromptComponent} from '../../shared/components/auth-prompt/catalog-auth-prompt.component';
import {CatalogRareBookDetailPageComponent} from './catalog-rare-book-detail-page.component';
import {SelectionButtonComponent} from '../../shared/components/selection-button/selection-button.component';
import {DesignSystemModule} from '@vpd/ui';

registerLocaleData(localeFr);

describe('CatalogRareBookDetailPageComponent', () => {
  let fixture: ComponentFixture<CatalogRareBookDetailPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let auth: jasmine.SpyObj<CatalogAuthService>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;

  const book: CatalogRareBook = {
    id: 'e56118db-233a-4bb2-931d-4d2c50d98901',
    slug: 'les-fables',
    isbn13: null,
    title: 'Les Fables',
    authorMention: 'Jean de La Fontaine',
    publisher: 'Imprimerie royale',
    publicationYear: 1770,
    price: 60,
    condition: 'GoodWithFlaws',
    publicDescription: 'Exemplaire illustré.',
    status: 'Published',
    isSold: false,
    soldAt: null,
    photos: [{
      id: 'photo-1',
      blobUri: 'https://cdn.example.test/one.jpg',
      blobName: 'one.jpg',
      caption: 'Première de couverture',
      position: 0,
      contentType: 'image/jpeg',
      sizeBytes: 100,
      uploadedAt: '2026-09-17T10:00:00Z',
    }],
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getPublicRareBook']);
    api.getPublicRareBook.and.returnValue(of({rareBook: book, relatedBooks: []} satisfies CatalogRareBookDetail));
    auth = jasmine.createSpyObj<CatalogAuthService>('CatalogAuthService', [
      'isAuthenticated',
      'getApiAccessToken',
      'login',
      'register',
    ]);
    auth.isAuthenticated.and.returnValue(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    auth.login.and.resolveTo();
    auth.register.and.resolveTo();
    selection = jasmine.createSpyObj<CatalogSelectionService>(
      'CatalogSelectionService',
      ['add', 'remove'],
      {keys: signal<ReadonlySet<string>>(new Set()), mode: signal<'local' | 'synced' | 'local-unsynced'>('synced')},
    );
    selection.add.and.resolveTo();
    selection.remove.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [
        CatalogRareBookDetailPageComponent,
        CatalogRareBookCardComponent,
        CatalogAuthPromptComponent,
        SelectionButtonComponent,
      ],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogSelectionService, useValue: selection},
        {provide: ActivatedRoute, useValue: {
          paramMap: of(convertToParamMap({id: book.id})),
        }},
        {provide: LOCALE_ID, useValue: 'fr-FR'},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogRareBookDetailPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads the id and publishes an id-based canonical URL', () => {
    expect(api.getPublicRareBook).toHaveBeenCalledWith(book.id);
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Les Fables');
    const canonicalUrl = `https://livres.volepapillondamour.fr/livres-rares/${book.id}`;
    expect(document.head.querySelector(`link[rel="canonical"][href="${canonicalUrl}"]`)).not.toBeNull();
  });

  it('shows only the concise price label and keeps the amount together', () => {
    const price = fixture.nativeElement.querySelector('.rare-detail-price') as HTMLElement;

    expect(price.textContent).toContain('PRIX');
    expect(fixture.nativeElement.textContent).toContain('60,00 €');
    expect(price.textContent).not.toContain('Prix ferme à lire');
    expect(price.textContent).not.toContain('Prix fixé par l’association');
    expect(price.querySelector('small')).toBeNull();
    expect(getComputedStyle(price.querySelector('strong') as HTMLElement).whiteSpace).toBe('nowrap');
  });

  it('places the copy details before the visit selection and removes the old actions', () => {
    const element = fixture.nativeElement as HTMLElement;
    const about = element.querySelector('.rare-detail-about') as HTMLElement;
    const visit = element.querySelector('.rare-selection-visit') as HTMLElement;

    expect(about).not.toBeNull();
    expect(about.compareDocumentPosition(visit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(element.querySelector('.rare-follow-button')).toBeNull();
    expect(element.querySelector('.rare-question-link')).toBeNull();
    expect(element.querySelector('.rare-detail-note')).toBeNull();
  });

  it('explains the real-copy gallery and keeps an explicit no-isbn fact', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-enlarge-button')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Photos prises par les bénévoles');
    expect(fixture.nativeElement.textContent).toContain('aucun (avant 1970)');
  });

  it('does not show removed rare-book classification or physical details', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).not.toContain('Éditions anciennes');
    expect(text).not.toContain('Reliure');
    expect(text).not.toContain('Dimensions');
    expect(text).not.toContain('Pages');
  });

  it('offers a selection action for this exact rare book', () => {
    const action = fixture.debugElement.query(By.directive(SelectionButtonComponent));

    expect(action).not.toBeNull();
    expect(action.componentInstance.ref()).toEqual({kind: 'rare', rareBookId: book.id});
    expect(action.componentInstance.title()).toBe(book.title);
  });

});
