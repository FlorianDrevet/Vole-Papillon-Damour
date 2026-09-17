import {provideZonelessChangeDetection, signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, ParamMap, Router, RouterModule, convertToParamMap} from '@angular/router';
import {FormsModule} from '@angular/forms';
import type {AccountInfo} from '@azure/msal-browser';
import {BehaviorSubject, Subject, of} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {
  CatalogAddedWatchlistItem,
  CatalogBookReference,
  CatalogReferenceSearchResponse,
  CatalogRareBook,
  CatalogRareBookPage,
  CatalogSearchResponse,
  CatalogWatchlistResponse,
} from '../../core/catalog.models';
import {CatalogSearchPageComponent} from './catalog-search-page.component';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CatalogRareBookCardComponent} from '../../shared/rare-book-card/rare-book-card.component';
import {CatalogAuthPromptComponent} from '../../shared/components/auth-prompt/catalog-auth-prompt.component';

function emptyWatchlist(items: CatalogWatchlistResponse['items'] = []): CatalogWatchlistResponse {
  return {
    generatedAt: '',
    alertStatus: 'Active',
    bounceCount: 0,
    items,
  };
}

describe('CatalogSearchPageComponent', () => {
  let fixture: ComponentFixture<CatalogSearchPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    register: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
    tryGetApiAccessToken: jasmine.Spy;
  };
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;
  let response$: Subject<CatalogSearchResponse>;
  let routeParams: BehaviorSubject<ParamMap>;

  const response: CatalogSearchResponse = {
    generatedAt: '2026-09-05T06:00:00Z',
    books: [{
      isbn13: '9791036377426',
      title: 'Petit Ours brun se promène en forêt',
      authors: 'Aubinais, Marie, Bour, Danièle',
      publisher: 'Bayard jeunesse',
      publicationYear: 2025,
      physicalFormat: null,
      language: 'fr',
      genre: 'Jeunesse',
      workId: null,
      coverUrl: null,
      quantityAvailable: 3,
      quantityAnnounced: 0,
      nextFairAt: null,
      lastAvailableAt: '2026-09-05T06:00:00Z',
      firstSeenAt: '2026-09-05T06:00:00Z',
      updatedAt: '2026-09-05T06:00:00Z',
      isRare: false,
    }],
    totalCount: 1,
    page: 1,
    pageSize: 24,
    genres: ['Jeunesse'],
  };

  const reference: CatalogBookReference = {
    isbn13: '9782070612758',
    workId: 'OL42W',
    title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry',
    publisher: 'Gallimard',
    publicationYear: 1999,
    coverUrl: 'https://covers.example.test/le-petit-prince.jpg',
    source: 'OpenLibrary',
  };

  const secondEditionReference: CatalogBookReference = {
    ...reference,
    isbn13: '9782070612759',
    publisher: 'Folio',
    publicationYear: 2015,
  };

  const otherWorkReference: CatalogBookReference = {
    ...secondEditionReference,
    isbn13: '9782070612760',
    workId: 'OL99W',
    title: 'Un autre titre',
  };

  beforeEach(async () => {
    sessionStorage.removeItem('vpd.catalog.pending-reference-follow');
    response$ = new Subject<CatalogSearchResponse>();
    routeParams = new BehaviorSubject<ParamMap>(convertToParamMap({q: 'saint-exupéry'}));
    api = jasmine.createSpyObj<CatalogApiService>(
      'CatalogApiService',
      ['search', 'getPublicRareBooks', 'searchReferences'],
    );
    api.search.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 24, genres: []}));
    api.getPublicRareBooks.and.returnValue(of({
      generatedAt: '',
      books: [],
      totalCount: 0,
      page: 1,
      pageSize: 24,
      shelves: [],
    }));
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference],
      page: 1,
      pageSize: 20,
    }));

    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      error: signal<string | null>(null),
      initialize: jasmine.createSpy('initialize').and.resolveTo(),
      login: jasmine.createSpy('login'),
      register: jasmine.createSpy('register').and.resolveTo(),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
      tryGetApiAccessToken: jasmine.createSpy('tryGetApiAccessToken').and.resolveTo('member-token'),
    };
    memberApi = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', [
      'addWatchlistItem',
      'getWatchlist',
    ]);
    memberApi.getWatchlist.and.returnValue(of(emptyWatchlist()));

    await TestBed.configureTestingModule({
      declarations: [
        CatalogSearchPageComponent,
        BookCardComponent,
        CatalogRareBookCardComponent,
        CatalogAuthPromptComponent,
      ],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: memberApi},
        {provide: ActivatedRoute, useValue: {snapshot: {data: {}, queryParamMap: routeParams.value}, queryParamMap: routeParams}},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogSearchPageComponent);
  });

  it('uses the dedicated rare-book endpoint and cards when the rare filter is active', async () => {
    const rareBook: CatalogRareBook = {
      id: 'rare-1',
      slug: 'atlas-des-jardins',
      isbn13: '9782070408504',
      title: 'Atlas des jardins',
      authorMention: 'Un auteur',
      publisher: 'Un éditeur',
      publicationYear: 1920,
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
      publicDescription: null,
      binding: null,
      dimensions: null,
      pageCount: null,
      status: 'Published',
      isSold: false,
      soldAt: null,
      photos: [],
    };
    const rarePage: CatalogRareBookPage = {
      generatedAt: '2026-09-17T10:00:00Z',
      books: [rareBook],
      totalCount: 1,
      page: 1,
      pageSize: 24,
      shelves: [{label: 'Éditions anciennes', count: 1}],
    };
    api.getPublicRareBooks.and.returnValue(of(rarePage));
    routeParams.next(convertToParamMap({q: 'atlas', rare: 'true'}));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getPublicRareBooks).toHaveBeenCalledWith(jasmine.objectContaining({
      search: 'atlas',
      includeSold: false,
      sort: 'price-desc',
      page: 1,
      pageSize: 24,
    }));
    expect(fixture.nativeElement.querySelector('.rare-book-card')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.book-card')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Atlas des jardins');
  });

  it('explains the two catalogue scopes without exposing the reference provider', () => {
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalled();
    expect(api.searchReferences).toHaveBeenCalledWith('saint-exupéry', 1, 20);
    expect(fixture.nativeElement.textContent).toContain('Dans la bourse aux livres');
    expect(fixture.nativeElement.textContent).toContain('Pas encore dans la bourse aux livres');
    expect(fixture.nativeElement.textContent).toContain('Pas encore dans la bourse aux livres');
    expect(fixture.nativeElement.textContent).not.toContain('Premier périmètre');
    expect(fixture.nativeElement.textContent).not.toContain('Second périmètre');
    expect(fixture.nativeElement.textContent).not.toContain('Référentiel externe');
    expect(fixture.nativeElement.textContent).not.toContain('Open Library');
    expect(fixture.nativeElement.textContent).not.toContain('OPENLIBRARY');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.querySelector('.reference-follow--work')).not.toBeNull();
    expect((fixture.nativeElement.querySelector('.reference-follow--edition') as HTMLButtonElement).textContent)
      .toContain('Suivre cette édition');
  });

  it('does not render an external edition that is already in the catalogue', () => {
    api.search.and.returnValue(of({
      ...response,
      books: [{
        ...response.books[0],
        isbn13: reference.isbn13!,
        title: reference.title,
        authors: reference.authors,
        publisher: reference.publisher,
        publicationYear: reference.publicationYear,
        workId: reference.workId,
      }],
    }));
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference, secondEditionReference],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    const externalBlock = fixture.nativeElement.querySelector('.external-block') as HTMLElement;
    const cards = externalBlock.querySelectorAll('.reference-card');

    expect(cards).toHaveSize(1);
    expect(cards[0].textContent).toContain('Folio');
    expect(cards[0].textContent).not.toContain('Gallimard');
    expect(externalBlock.querySelector('.section-count')?.textContent).toContain('1 édition');
  });

  it('removes an external duplicate when the local response arrives afterwards', async () => {
    const localResults$ = new Subject<CatalogSearchResponse>();
    const externalResults$ = new Subject<CatalogReferenceSearchResponse>();
    api.search.and.returnValue(localResults$.asObservable());
    api.searchReferences.and.returnValue(externalResults$.asObservable());

    fixture.detectChanges();
    externalResults$.next({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference],
      page: 1,
      pageSize: 20,
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.reference-card')).toHaveSize(1);

    localResults$.next({
      ...response,
      books: [{
        ...response.books[0],
        isbn13: reference.isbn13!,
      }],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.reference-card')).toHaveSize(0);
  });

  it('limits the search tab to available books by default', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalledWith(jasmine.objectContaining({
      availability: 'available',
      includeExhausted: false,
    }));
    expect((fixture.nativeElement.querySelector(
      'input[name="availability"][value="available"]',
    ) as HTMLInputElement).checked).toBeTrue();
    expect((fixture.nativeElement.querySelector(
      'input[name="includeExhausted"]',
    ) as HTMLInputElement).checked).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Afficher les livres épuisés');
  });

  it('restores the available scope when search filters are reset', () => {
    fixture.detectChanges();
    fixture.componentInstance.availability = 'all';
    fixture.componentInstance.includeExhausted = true;
    const applyFilters = spyOn(fixture.componentInstance, 'applyFilters');

    fixture.componentInstance.clearFilters();

    expect(fixture.componentInstance.availability).toBe('available');
    expect(fixture.componentInstance.includeExhausted).toBeFalse();
    expect(applyFilters).toHaveBeenCalledOnceWith();
  });

  it('restores exhausted inclusion from the URL and preserves it when another filter changes', async () => {
    fixture.detectChanges();
    routeParams.next(convertToParamMap({q: 'saint-exupéry', includeExhausted: 'true'}));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.includeExhausted).toBeTrue();
    expect((fixture.nativeElement.querySelector(
      'input[name="includeExhausted"]',
    ) as HTMLInputElement).checked).toBeTrue();
    expect(api.search).toHaveBeenCalledWith(jasmine.objectContaining({includeExhausted: true}));

    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);
    fixture.componentInstance.genre = 'Romans';
    fixture.componentInstance.applyFilters();

    expect(navigate).toHaveBeenCalledWith(['/recherche'], {
      queryParams: jasmine.objectContaining({includeExhausted: true}),
    });
  });

  it('keeps an explicit all scope in the search URL', () => {
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);
    fixture.componentInstance.availability = 'all';

    fixture.componentInstance.applyFilters();

    expect(navigate).toHaveBeenCalledWith(['/recherche'], {
      queryParams: jasmine.objectContaining({availability: 'all'}),
    });
  });

  it('keeps the result heading on the last submitted query while the draft changes', async () => {
    fixture.detectChanges();

    fixture.componentInstance.query = 'petit prince';
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('.results-toolbar h1') as HTMLElement).textContent)
      .toContain('Résultats pour « saint-exupéry »');
  });

  it('places the work follow action above the edition list', () => {
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference, secondEditionReference],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    const callout = fixture.nativeElement.querySelector('.external-follow-callout') as HTMLElement;

    expect(callout).not.toBeNull();
    expect(callout.textContent).toContain('Suivre toutes les éditions');
    expect(callout.textContent).toContain('édition précise');
    expect(fixture.nativeElement.querySelectorAll('.reference-card .reference-follow--work').length).toBe(0);
    expect(fixture.nativeElement.querySelectorAll('.reference-card .reference-follow--edition').length).toBe(2);
  });

  it('enables the title follow action for several editions of the same title', () => {
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [
        reference,
        {...reference, isbn13: '9782070612759', workId: 'OL99W'},
      ],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('.reference-follow--work') as HTMLButtonElement).disabled)
      .toBeFalse();
  });

  it('follows each identified work when the title follow action is used', async () => {
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'work-watchlist-item',
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      addedAt: '2026-09-05T06:00:00Z',
    }));
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [
        reference,
        {...reference, isbn13: '9782070612759', workId: 'OL99W'},
      ],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.reference-follow--work') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(memberApi.addWatchlistItem.calls.count()).toBe(2);
    expect(memberApi.addWatchlistItem.calls.allArgs().map(args => (args[1] as unknown as Record<string, unknown>)['workId']))
      .toEqual(['OL42W', 'OL99W']);
    expect(fixture.nativeElement.textContent).toContain('Les éditions ont été ajoutées');
  });

  it('keeps one disabled title follow action before unrelated reference results', () => {
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference, otherWorkReference],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    const callout = fixture.nativeElement.querySelector('.external-follow-callout') as HTMLElement;
    const referenceList = fixture.nativeElement.querySelector('.reference-list') as HTMLElement;
    const button = callout.querySelector('.reference-follow--work') as HTMLButtonElement;

    expect(callout).not.toBeNull();
    expect(button.disabled).toBeTrue();
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(Array.from(callout.parentElement!.children).indexOf(callout))
      .toBeLessThan(Array.from(callout.parentElement!.children).indexOf(referenceList));
    expect(fixture.nativeElement.querySelectorAll('.reference-card .reference-follow--work').length).toBe(0);
    expect(fixture.nativeElement.querySelectorAll('.reference-card .reference-follow--edition').length).toBe(2);
  });

  it('shows a disabled title follow action when the reference search is empty', () => {
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    const callout = fixture.nativeElement.querySelector('.external-follow-callout') as HTMLElement;
    const button = callout.querySelector('.reference-follow--work') as HTMLButtonElement;

    expect(callout).not.toBeNull();
    expect(callout.textContent).toContain('Suivez toutes les éditions');
    expect(button.disabled).toBeTrue();
    expect(button.getAttribute('aria-disabled')).toBe('true');
    button.click();
    expect(memberApi.addWatchlistItem).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Aucun autre titre ne correspond à cette recherche.');
  });

  it('replaces the edition follow button when the edition is already in the watchlist', async () => {
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.getWatchlist.and.returnValue(of(emptyWatchlist([{
      id: 'watchlist-item',
      scope: 'Edition',
      workId: null,
      isbn13: reference.isbn13,
      title: reference.title,
      authors: reference.authors,
      publisher: reference.publisher,
      publicationYear: reference.publicationYear,
      coverUrl: reference.coverUrl,
      book: null,
      addedAt: '2026-09-05T06:00:00Z',
      lastAlertAt: null,
    }])));

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.reference-card') as HTMLElement;

    expect(card.querySelector('.reference-follow--edition')).toBeNull();
    expect(card.querySelector('.reference-followed')).not.toBeNull();
    expect(card.textContent).toContain('Déjà dans votre liste de recherche');
    await fixture.componentInstance.followReference(reference);
    expect(memberApi.addWatchlistItem).not.toHaveBeenCalled();
  });

  it('follows any edition from the section callout without opening a modal', async () => {
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'work-watchlist-item',
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      addedAt: '2026-09-05T06:00:00Z',
    }));

    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.reference-follow--work') as HTMLButtonElement).click();
    await fixture.whenStable();

    const request = memberApi.addWatchlistItem.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request).toEqual({
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: 'https://covers.example.test/le-petit-prince.jpg',
    });
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Le titre a été ajouté à votre liste de recherche.');
  });

  it('renders an asynchronous catalog response in zoneless mode', async () => {
    api.search.and.returnValue(response$.asObservable());

    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-loader="skeleton"]')).not.toBeNull();

    response$.next(response);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Petit Ours brun se promène en forêt');
    expect(fixture.nativeElement.querySelector('[data-loader="skeleton"]')).toBeNull();
  });

  it('reloads only the external section when its page changes', async () => {
    const initialReferences: CatalogReferenceSearchResponse = {
      generatedAt: '',
      query: 'saint-exupéry',
      items: [reference],
      page: 1,
      pageSize: 20,
    };
    const externalPage$ = new Subject<CatalogReferenceSearchResponse>();
    api.search.and.returnValue(of({...response, totalCount: 48}));
    api.searchReferences.and.returnValues(of(initialReferences), externalPage$.asObservable());

    fixture.detectChanges();
    api.search.calls.reset();
    api.searchReferences.calls.reset();

    routeParams.next(convertToParamMap({q: 'saint-exupéry', referencePage: '2'}));
    fixture.detectChanges();

    expect(api.search).not.toHaveBeenCalled();
    expect(api.searchReferences).toHaveBeenCalledOnceWith('saint-exupéry', 2, 20);
    expect(fixture.componentInstance.loading).toBeFalse();
    expect(fixture.nativeElement.querySelector('.book-list')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.external-loader')).not.toBeNull();

    externalPage$.next({...initialReferences, items: [secondEditionReference], page: 2});
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Folio');
    expect(fixture.nativeElement.querySelector('.external-loader')).toBeNull();
  });

  it('reloads only the catalogue section when its page changes', async () => {
    const localPage$ = new Subject<CatalogSearchResponse>();
    api.search.and.returnValues(of({...response, totalCount: 48}), localPage$.asObservable());

    fixture.detectChanges();
    api.search.calls.reset();
    api.searchReferences.calls.reset();

    routeParams.next(convertToParamMap({q: 'saint-exupéry', page: '2'}));
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalledOnceWith(jasmine.objectContaining({page: 2}));
    expect(api.searchReferences).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.external-block')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');

    localPage$.next({...response, totalCount: 48, page: 2});
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="skeleton"]')).toBeNull();
  });

  it('centers the external reference loader in its section', () => {
    const references$ = new Subject<CatalogReferenceSearchResponse>();
    api.searchReferences.and.returnValue(references$.asObservable());

    fixture.detectChanges();

    const loader = fixture.nativeElement.querySelector('.external-loader') as HTMLElement;
    expect(loader).not.toBeNull();
    if (!loader) {
      return;
    }

    const loaderStyle = getComputedStyle(loader);

    expect(loaderStyle.display).toBe('grid');
    expect(loaderStyle.alignItems).toBe('center');
    expect(loaderStyle.justifyItems).toBe('center');
    expect(loaderStyle.minHeight).toBe('150px');
  });

  it('follows a precise edition with only the edition target', async () => {
    const addResponse$ = new Subject<CatalogAddedWatchlistItem>();
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(addResponse$.asObservable());

    fixture.detectChanges();

    const followPromise = fixture.componentInstance.followReference(reference);
    await Promise.resolve();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.reference-follow--edition [data-loader="ring"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Ajout…');

    const request = memberApi.addWatchlistItem.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request).toEqual({
      scope: 'Edition',
      workId: null,
      isbn13: '9782070612758',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: 'https://covers.example.test/le-petit-prince.jpg',
    });

    addResponse$.next({
      id: 'watchlist-item',
      scope: 'Edition',
      workId: null,
      isbn13: '9782070612758',
      addedAt: '2026-09-05T06:00:00Z',
    });
    addResponse$.complete();
    await followPromise;
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('L’édition a été ajoutée à votre liste de recherche.');
    expect(fixture.nativeElement.querySelector('.reference-follow--edition')).toBeNull();
    expect(fixture.nativeElement.querySelector('.reference-followed')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Déjà dans votre liste de recherche');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('sends the reference cover URL when following an edition', async () => {
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'watchlist-item',
      scope: 'Edition',
      workId: null,
      isbn13: '9782070612758',
      addedAt: '2026-09-05T06:00:00Z',
    }));

    fixture.detectChanges();
    await fixture.componentInstance.followReference(reference);

    const request = memberApi.addWatchlistItem.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request['coverUrl']).toBe(reference.coverUrl);
  });

  it('opens the auth prompt instead of redirecting immediately when signed out', async () => {
    fixture.detectChanges();

    await fixture.componentInstance.followReference(reference);
    fixture.detectChanges();

    expect(auth.login).not.toHaveBeenCalled();
    expect(fixture.componentInstance.authPromptOpen).toBeTrue();
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog).not.toBeNull();
    expect(dialog.textContent).toContain('liste de suivi');
  });

  it('keeps the direct edition target across the sign-in redirect', async () => {
    fixture.detectChanges();

    await fixture.componentInstance.followReference(reference);
    fixture.detectChanges();
    await fixture.componentInstance.confirmAuthPrompt('login');

    expect(auth.login).toHaveBeenCalled();
    expect(fixture.componentInstance.authPromptOpen).toBeFalse();
    expect(sessionStorage.getItem('vpd.catalog.pending-reference-follow'))
      .toContain('"scope":"Edition"');
    sessionStorage.removeItem('vpd.catalog.pending-reference-follow');
  });

  it('starts registration instead of login when creating an account from the prompt', async () => {
    fixture.detectChanges();

    await fixture.componentInstance.followReference(reference);
    fixture.detectChanges();
    await fixture.componentInstance.confirmAuthPrompt('register');

    expect(auth.login).not.toHaveBeenCalled();
    expect(auth.register).toHaveBeenCalled();
    expect(sessionStorage.getItem('vpd.catalog.pending-reference-follow'))
      .toContain('"scope":"Edition"');
    sessionStorage.removeItem('vpd.catalog.pending-reference-follow');
  });

  it('closes the auth prompt without starting a sign-in when dismissed', async () => {
    fixture.detectChanges();

    await fixture.componentInstance.followReference(reference);
    fixture.detectChanges();
    fixture.componentInstance.closeAuthPrompt();
    fixture.detectChanges();

    expect(auth.login).not.toHaveBeenCalled();
    expect(auth.register).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(sessionStorage.getItem('vpd.catalog.pending-reference-follow')).toBeNull();
  });

  it('resumes a pending direct follow after sign-in without opening a modal', async () => {
    sessionStorage.setItem('vpd.catalog.pending-reference-follow', JSON.stringify({
      item: reference,
      scope: 'Work',
    }));
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'work-watchlist-item',
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      addedAt: '2026-09-05T06:00:00Z',
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));

    expect(auth.initialize).toHaveBeenCalledOnceWith();
    const request = memberApi.addWatchlistItem.calls.mostRecent().args[1] as unknown as Record<string, unknown>;
    expect(request).toEqual({
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: 'https://covers.example.test/le-petit-prince.jpg',
    });
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(sessionStorage.getItem('vpd.catalog.pending-reference-follow')).toBeNull();
  });

  it('does not invent genre filters when the API has no genre metadata', () => {
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const options = Array.from(
      element.querySelectorAll<HTMLInputElement>('.filters-panel input[name="genre"]'),
    ).map(input => input.value);

    expect(options).toEqual(['']);
  });

  it('opens a branded sort menu instead of relying on the native select popup', () => {
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.sort-select-trigger') as HTMLButtonElement;

    expect(element.querySelector('select[name="sort"]')).toBeNull();
    expect(trigger).not.toBeNull();
    expect(trigger.getAttribute('aria-label')).toBe('Trier les résultats');
    expect(trigger.getAttribute('aria-haspopup')).toBe('menu');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');

    trigger.click();
    fixture.detectChanges();

    const panel = element.querySelector('.sort-select-panel') as HTMLElement;
    expect(panel.getAttribute('role')).toBe('menu');
    expect(panel.querySelectorAll('.sort-option')).toHaveSize(2);
    expect(panel.querySelector('.sort-option--selected')?.textContent).toContain('Pertinence');
    expect(fixture.nativeElement.querySelector('.sort-select-chevron')).not.toBeNull();
  });

  it('updates the sort and closes the branded menu when an option is chosen', async () => {
    fixture.detectChanges();
    const applyFilters = spyOn(fixture.componentInstance, 'applyFilters');
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.sort-select-trigger') as HTMLButtonElement;

    trigger.click();
    fixture.detectChanges();
    (element.querySelector('[data-sort="recent"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.sort).toBe('recent');
    expect(applyFilters).toHaveBeenCalledOnceWith();
    expect(element.querySelector('.sort-select-panel')).toBeNull();
    expect(trigger.textContent).toContain('Arrivée récente');
  });

  it('supports keyboard opening and selection in the branded sort menu', async () => {
    fixture.detectChanges();
    const applyFilters = spyOn(fixture.componentInstance, 'applyFilters');
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.sort-select-trigger') as HTMLButtonElement;

    trigger.dispatchEvent(new KeyboardEvent('keydown', {key: 'Enter', bubbles: true}));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.querySelector('.sort-select-panel')).not.toBeNull();

    const recentOption = element.querySelector('[data-sort="recent"]') as HTMLButtonElement;
    recentOption.dispatchEvent(new KeyboardEvent('keydown', {key: 'Enter', bubbles: true}));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.sort).toBe('recent');
    expect(applyFilters).toHaveBeenCalledOnceWith();
    expect(element.querySelector('.sort-select-panel')).toBeNull();
  });

  it('closes the branded sort menu when focus moves outside it', () => {
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.sort-select-trigger') as HTMLButtonElement;

    trigger.click();
    fixture.detectChanges();
    document.body.dispatchEvent(new MouseEvent('click', {bubbles: true}));
    fixture.detectChanges();

    expect(element.querySelector('.sort-select-panel')).toBeNull();
  });

  it('gives the availability heading breathing room and uses the brand gradient for both sections', () => {
    fixture.detectChanges();

    const availabilityLegend = fixture.nativeElement.querySelector('.filter-group legend') as HTMLElement;
    const availableRule = fixture.nativeElement.querySelector('.available-rule') as HTMLElement;
    const externalRule = fixture.nativeElement.querySelector('.external-rule') as HTMLElement;

    expect(getComputedStyle(availabilityLegend).marginBottom).toBe('6px');
    expect(getComputedStyle(availableRule).backgroundImage).toBe(getComputedStyle(externalRule).backgroundImage);
    expect(getComputedStyle(availableRule).backgroundImage).toContain('linear-gradient');
  });

  it('loads the filtered results when opened with a genre query parameter', async () => {
    routeParams.next(convertToParamMap({genre: 'Romans'}));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalledWith(jasmine.objectContaining({genre: 'Romans'}));
    expect(fixture.componentInstance.genre).toBe('Romans');
  });
});
