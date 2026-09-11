import {provideZonelessChangeDetection, signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, ParamMap, RouterModule, convertToParamMap} from '@angular/router';
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
  CatalogSearchResponse,
} from '../../core/catalog.models';
import {CatalogSearchPageComponent} from './catalog-search-page.component';
import {BookCardComponent} from '../../shared/book-card/book-card.component';

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
    getApiAccessToken: jasmine.Spy;
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
    coverUrl: null,
    source: 'OpenLibrary',
  };

  const secondEditionReference: CatalogBookReference = {
    ...reference,
    isbn13: '9782070612759',
    publisher: 'Folio',
    publicationYear: 2015,
  };

  beforeEach(async () => {
    sessionStorage.removeItem('vpd.catalog.pending-reference-follow');
    response$ = new Subject<CatalogSearchResponse>();
    routeParams = new BehaviorSubject<ParamMap>(convertToParamMap({q: 'saint-exupéry'}));
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search', 'searchReferences']);
    api.search.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 24, genres: []}));
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
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    memberApi = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', ['addWatchlistItem']);

    await TestBed.configureTestingModule({
      declarations: [CatalogSearchPageComponent, BookCardComponent],
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

  it('does not offer a work follow action when the reference search is empty', () => {
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.external-follow-callout')).toBeNull();
    expect(fixture.nativeElement.querySelector('.reference-follow--work')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Aucun autre titre ne correspond à cette recherche.');
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

    expect(memberApi.addWatchlistItem).toHaveBeenCalledWith('member-token', {
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
    });
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Le titre a été ajouté à votre liste de recherche.');
  });

  it('renders an asynchronous catalog response in zoneless mode', async () => {
    api.search.and.returnValue(response$.asObservable());

    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Le catalogue arrive…');

    response$.next(response);
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Petit Ours brun se promène en forêt');
    expect(fixture.nativeElement.textContent).not.toContain('Le catalogue arrive…');
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
    expect(fixture.nativeElement.textContent).toContain('Ajout…');

    expect(memberApi.addWatchlistItem).toHaveBeenCalledWith('member-token', {
      scope: 'Edition',
      workId: null,
      isbn13: '9782070612758',
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
    expect(fixture.nativeElement.textContent).toContain('Suivre cette édition');
    expect(fixture.nativeElement.textContent).not.toContain('Ajout…');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('keeps the direct edition target across the sign-in redirect', async () => {
    fixture.detectChanges();

    await fixture.componentInstance.followReference(reference);

    expect(auth.login).toHaveBeenCalled();
    expect(sessionStorage.getItem('vpd.catalog.pending-reference-follow'))
      .toContain('"scope":"Edition"');
    sessionStorage.removeItem('vpd.catalog.pending-reference-follow');
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
    expect(memberApi.addWatchlistItem).toHaveBeenCalledWith('member-token', {
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
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
