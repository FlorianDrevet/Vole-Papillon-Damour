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
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
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
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;

  const book: CatalogRareBook = {
    id: 'rare-1',
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
    memberApi = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', ['addWatchlistItem']);
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'watch-1',
      scope: 'RareBook',
      workId: null,
      isbn13: null,
      rareBookId: book.id,
      addedAt: '2026-09-17T10:00:00Z',
    }));
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
        {provide: CatalogMemberApiService, useValue: memberApi},
        {provide: CatalogSelectionService, useValue: selection},
        {provide: ActivatedRoute, useValue: {
          paramMap: of(convertToParamMap({slug: 'les-fables'})),
        }},
        {provide: LOCALE_ID, useValue: 'fr-FR'},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogRareBookDetailPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads the slug, publishes SEO metadata and leaves the price outside any total', () => {
    expect(api.getPublicRareBook).toHaveBeenCalledWith('les-fables');
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('Les Fables');
    expect(fixture.nativeElement.textContent).toContain('60,00 €');
    expect(fixture.nativeElement.textContent).toContain('Prix ferme à lire');
  });

  it('offers a direct email question about this exact rare book', () => {
    const link = fixture.nativeElement.querySelector('.rare-question-link') as HTMLAnchorElement;

    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toContain('mailto:volepapillondamour@sfr.fr');
    expect(link.getAttribute('href')).toContain(encodeURIComponent('Les Fables'));
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

  it('lets an authenticated member follow the exact rare copy without creating an ordinary edition target', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const followButton = fixture.nativeElement.querySelector('.rare-follow-button') as HTMLButtonElement;
    expect(followButton).not.toBeNull();
    followButton.click();
    await fixture.whenStable();

    const request = memberApi.addWatchlistItem.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request).toEqual({
      scope: 'RareBook',
      workId: null,
      isbn13: null,
      rareBookId: 'rare-1',
      title: 'Les Fables',
      authors: 'Jean de La Fontaine',
      publisher: 'Imprimerie royale',
      publicationYear: 1770,
      coverUrl: null,
    });
    expect(fixture.nativeElement.textContent).toContain('maintenant suivi');
  });

  it('offers a selection action for this exact rare book', () => {
    const action = fixture.debugElement.query(By.directive(SelectionButtonComponent));

    expect(action).not.toBeNull();
    expect(action.componentInstance.ref()).toEqual({kind: 'rare', rareBookId: book.id});
    expect(action.componentInstance.title()).toBe(book.title);
  });

  it('opens the member prompt before following a rare copy anonymously', async () => {
    auth.isAuthenticated.and.returnValue(false);

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.rare-follow-button') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(memberApi.addWatchlistItem).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
  });
});
