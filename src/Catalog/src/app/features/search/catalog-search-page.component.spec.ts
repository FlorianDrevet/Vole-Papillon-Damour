import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, ParamMap, RouterModule, convertToParamMap} from '@angular/router';
import {FormsModule} from '@angular/forms';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {BehaviorSubject, of} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
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
  const routeParams = new BehaviorSubject<ParamMap>(convertToParamMap({q: 'saint-exupéry'}));

  beforeEach(async () => {
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
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: memberApi},
        {provide: ActivatedRoute, useValue: {snapshot: {data: {}, queryParamMap: routeParams.value}, queryParamMap: routeParams}},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogSearchPageComponent);
  });

  it('loads external references separately for a real search query', () => {
    fixture.detectChanges();

    expect(api.search).toHaveBeenCalled();
    expect(api.searchReferences).toHaveBeenCalledWith('saint-exupéry', 1, 20);
    expect(fixture.nativeElement.textContent).toContain('Référentiel externe');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
  });
});
