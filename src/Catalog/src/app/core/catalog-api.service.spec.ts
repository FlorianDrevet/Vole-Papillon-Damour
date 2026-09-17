import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogApiService} from './catalog-api.service';
import {CatalogRareBookDetail, CatalogRareBookPage, CatalogSearchResponse} from './catalog.models';

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
      includeExhausted: true,
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
    expect(request.request.params.get('includeExhausted')).toBe('true');
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

  it('maps the public upcoming event collection and keeps only book fairs', () => {
    service.getUpcomingFairs().subscribe(result => {
      expect(result).toEqual([{
        id: 'fair-1',
        name: 'Bourse de printemps',
        dateStart: '2027-03-14T00:00:00Z',
        dateEnd: '2027-03-15T00:00:00Z',
        openAt: '2027-03-14T09:30:00.000Z',
        closeAt: '2027-03-15T18:00:00.000Z',
        roadNumber: 46,
        city: 'Saint-Just-Saint-Rambert',
        cityCode: 42170,
        road: 'route de Saint-Marcellin',
      }, {
        id: 'fair-2',
        name: 'Bourse d’automne',
        dateStart: '2027-10-09T00:00:00Z',
        dateEnd: '2027-10-10T00:00:00Z',
        openAt: '2027-10-09T09:30:00.000Z',
        closeAt: '2027-10-10T18:00:00.000Z',
        roadNumber: 46,
        city: 'Saint-Just-Saint-Rambert',
        cityCode: 42170,
        road: 'route de Saint-Marcellin',
      }]);
    });

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/asso-events`);
    expect(request.request.method).toBe('GET');
    request.flush([
      {
        id: 'fair-1',
        name: 'Bourse de printemps',
        eventType: 'Books',
        dateStart: '2027-03-14T00:00:00Z',
        dateEnd: '2027-03-15T00:00:00Z',
        hourOpenDoors: '2026-10-05T09:30:00Z',
        hourCloseDoors: '2026-10-05T18:00:00Z',
        roadNumber: 46,
        city: 'Saint-Just-Saint-Rambert',
        cityCode: 42170,
        road: 'route de Saint-Marcellin',
      },
      {
        id: 'bingo-1',
        name: 'Grand loto',
        eventType: 'Bingo',
        dateStart: '2027-05-01T00:00:00Z',
        dateEnd: null,
        hourOpenDoors: '2027-05-01T14:00:00Z',
        hourCloseDoors: '2027-05-01T18:00:00Z',
        roadNumber: 46,
        city: 'Saint-Just-Saint-Rambert',
        cityCode: 42170,
        road: 'route de Saint-Marcellin',
      },
      {
        id: 'fair-2',
        name: 'Bourse d’automne',
        eventType: 'Books',
        dateStart: '2027-10-09T00:00:00Z',
        dateEnd: '2027-10-10T00:00:00Z',
        hourOpenDoors: '2027-10-09T09:30:00Z',
        hourCloseDoors: '2027-10-10T18:00:00Z',
        roadNumber: 46,
        city: 'Saint-Just-Saint-Rambert',
        cityCode: 42170,
        road: 'route de Saint-Marcellin',
      },
    ]);
  });

  it('does not expose the removed legacy next-fair request', () => {
    expect((service as unknown as {getNextFair?: unknown}).getNextFair).toBeUndefined();
  });

  it('builds dedicated public rare-book list and detail requests', () => {
    const page = {
      generatedAt: '2026-09-17T10:00:00Z',
      books: [],
      totalCount: 0,
      page: 1,
      pageSize: 24,
      shelves: [],
    } satisfies CatalogRareBookPage;
    service.getPublicRareBooks({
      search: 'atlas',
      shelf: 'Illustrés',
      includeSold: false,
      sort: 'price-desc',
      page: 2,
      pageSize: 12,
    }).subscribe(result => expect(result).toEqual(page));
    const listRequest = http.expectOne(request => request.url === `${environment.apiUrl}/catalog/rare-books`);
    expect(listRequest.request.params.get('search')).toBe('atlas');
    expect(listRequest.request.params.get('shelf')).toBe('Illustrés');
    expect(listRequest.request.params.get('includeSold')).toBe('false');
    expect(listRequest.request.params.get('sort')).toBe('price-desc');
    expect(listRequest.request.params.get('page')).toBe('2');
    expect(listRequest.request.params.get('pageSize')).toBe('12');
    listRequest.flush(page);

    const detail = {rareBook: {} as unknown as CatalogRareBookDetail['rareBook'], relatedBooks: []} as CatalogRareBookDetail;
    service.getPublicRareBook('les-fables').subscribe(result => expect(result).toEqual(detail));
    const detailRequest = http.expectOne(
      request => request.url === `${environment.apiUrl}/catalog/rare-books/les-fables`,
    );
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush(detail);
  });
});
