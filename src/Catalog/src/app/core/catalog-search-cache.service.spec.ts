import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {CatalogApiService} from './catalog-api.service';
import {CatalogSearchCacheService} from './catalog-search-cache.service';
import {CatalogSearchResponse} from './catalog.models';

describe('CatalogSearchCacheService', () => {
  let service: CatalogSearchCacheService;
  let http: HttpTestingController;

  function response(page: number): CatalogSearchResponse {
    return {
      generatedAt: '2026-11-15T00:00:00Z',
      books: [],
      totalCount: null,
      hasMore: page < 21,
      page,
      pageSize: 1,
      genres: [],
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        CatalogApiService,
        CatalogSearchCacheService,
      ],
    });
    service = TestBed.inject(CatalogSearchCacheService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify({ignoreCancelled: true}));

  it('reuses a response for equivalent trimmed search parameters', () => {
    let firstResult: CatalogSearchResponse | undefined;
    service.search({query: '  Camus  ', page: 1, pageSize: 24}).subscribe(value => firstResult = value);
    const request = http.expectOne(request => request.url.endsWith('/catalog/search'));
    request.flush(response(1));

    let cachedResult: CatalogSearchResponse | undefined;
    service.search({query: 'Camus'}).subscribe(value => cachedResult = value);

    expect(cachedResult).toEqual(firstResult);
    http.expectNone(request => request.url.endsWith('/catalog/search'));
  });

  it('expires responses after sixty seconds and retains at most twenty entries', () => {
    let now = 1_000_000;
    spyOn(Date, 'now').and.callFake(() => now);

    let refreshedResponse: CatalogSearchResponse | undefined;
    service.search({query: 'expired'}).subscribe(value => refreshedResponse = value);
    http.expectOne(request => request.params.get('q') === 'expired').flush(response(1));
    now += 60_001;
    service.search({query: 'expired'}).subscribe(value => refreshedResponse = value);
    http.expectOne(request => request.params.get('q') === 'expired').flush(response(1));
    expect(refreshedResponse).toEqual(response(1));

    for (let page = 1; page <= 21; page++) {
      service.search({query: 'bounded', page, pageSize: 1}).subscribe();
      http.expectOne(request =>
        request.params.get('q') === 'bounded' && request.params.get('page') === String(page))
        .flush(response(page));
    }

    service.search({query: 'bounded', page: 1, pageSize: 1}).subscribe();
    const evicted = http.expectOne(request =>
      request.params.get('q') === 'bounded' && request.params.get('page') === '1');
    expect(evicted.request.method).toBe('GET');
    evicted.flush(response(1));
    http.expectNone(request => request.params.get('q') === 'bounded' && request.params.get('page') === '2');
  });
});
