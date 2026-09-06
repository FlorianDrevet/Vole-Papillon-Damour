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
import {CatalogAddedWatchlistItem, CatalogSearchResponse} from '../../core/catalog.models';
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
    login: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
  };
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;
  let response$: Subject<CatalogSearchResponse>;
  const routeParams = new BehaviorSubject<ParamMap>(convertToParamMap({q: 'saint-exupéry'}));

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

  beforeEach(async () => {
    response$ = new Subject<CatalogSearchResponse>();
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search', 'searchReferences']);
    api.search.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 24, genres: []}));
    api.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'saint-exupéry',
      items: [{
        isbn13: '9782070612758',
        workId: 'OL42W',
        title: 'Le Petit Prince',
        authors: 'Antoine de Saint-Exupéry',
        publisher: 'Gallimard',
        publicationYear: 1999,
        coverUrl: null,
        source: 'OpenLibrary',
      }],
      page: 1,
      pageSize: 20,
    }));

    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      error: signal<string | null>(null),
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
    expect(fixture.nativeElement.textContent).toContain('À ajouter à votre liste de recherche');
    expect(fixture.nativeElement.textContent).not.toContain('Premier périmètre');
    expect(fixture.nativeElement.textContent).not.toContain('Second périmètre');
    expect(fixture.nativeElement.textContent).not.toContain('Référentiel externe');
    expect(fixture.nativeElement.textContent).not.toContain('Open Library');
    expect(fixture.nativeElement.textContent).not.toContain('OPENLIBRARY');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect((fixture.nativeElement.querySelector('.reference-follow') as HTMLButtonElement).textContent)
      .toContain('Ajouter à ma liste de recherche');
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

  it('refreshes the follow button after the asynchronous add completes in zoneless mode', async () => {
    const addResponse$ = new Subject<CatalogAddedWatchlistItem>();
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.resolveTo('member-token');
    memberApi.addWatchlistItem.and.returnValue(addResponse$.asObservable());

    fixture.detectChanges();

    const followPromise = fixture.componentInstance.followReference({
      isbn13: '9782070612758',
      workId: 'OL42W',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: null,
      source: 'OpenLibrary',
    });
    await Promise.resolve();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ajout…');

    addResponse$.next({
      id: 'watchlist-item',
      scope: 'Work',
      workId: 'OL42W',
      isbn13: null,
      addedAt: '2026-09-05T06:00:00Z',
    });
    addResponse$.complete();
    await followPromise;
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Le titre a été ajouté à votre liste de recherche.');
    expect(fixture.nativeElement.textContent).toContain('Ajouter à ma liste de recherche');
    expect(fixture.nativeElement.textContent).not.toContain('Ajout…');
  });
});
