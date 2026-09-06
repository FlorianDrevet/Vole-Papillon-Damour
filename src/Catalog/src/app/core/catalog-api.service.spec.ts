import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogApiService} from './catalog-api.service';
import {CatalogSearchResponse} from './catalog.models';

describe('CatalogApiService', () => {
  let service: CatalogApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CatalogApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(CatalogApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('builds a typed search request with the public filters', () => {
    const response: CatalogSearchResponse = {
      generatedAt: '2026-09-04T12:00:00Z',
      books: [],
      totalCount: 0,
      page: 2,
      pageSize: 12,
      genres: ['Romans'],
    };

    service.search({
      query: 'écume',
      genre: 'Romans',
      availability: 'available',
      rareOnly: true,
      sort: 'recent',
      page: 2,
      pageSize: 12,
    }).subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/catalog/search`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('q')).toBe('écume');
    expect(request.request.params.get('genre')).toBe('Romans');
    expect(request.request.params.get('availability')).toBe('available');
    expect(request.request.params.get('rare')).toBe('true');
    expect(request.request.params.get('sort')).toBe('recent');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('12');
    request.flush(response);
  });

  it('does not send empty optional filters', () => {
    service.search().subscribe();

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/catalog/search`);
    expect(request.request.params.keys()).toEqual([]);
    request.flush({
      generatedAt: '2026-09-04T12:00:00Z',
      books: [],
      totalCount: 0,
      page: 1,
      pageSize: 24,
      genres: [],
    } satisfies CatalogSearchResponse);
  });

  it('keeps external bibliographic references separate from the local catalogue', () => {
    service.searchReferences('saint-exupéry', 2, 20).subscribe(result => {
      expect(result.query).toBe('saint-exupéry');
      expect(result.items[0].source).toBe('OpenLibrary');
    });

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/catalog/reference/search`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('q')).toBe('saint-exupéry');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({
      generatedAt: '2026-09-04T12:00:00Z',
      query: 'saint-exupéry',
      items: [{
        isbn13: '9782070612758',
        workId: 'OL42W',
        title: 'Le Petit Prince',
        authors: 'Antoine de Saint-Exupéry',
        publisher: 'Gallimard',
        publicationYear: 1999,
        coverUrl: 'https://covers.openlibrary.org/isbn/9782070612758-M.jpg',
        source: 'OpenLibrary',
      }],
      page: 2,
      pageSize: 20,
    });
  });
});
