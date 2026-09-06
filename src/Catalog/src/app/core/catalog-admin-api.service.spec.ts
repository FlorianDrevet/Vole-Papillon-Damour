import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogAdminApiService} from './catalog-admin-api.service';
import {CatalogAdminBookPage, CatalogAdminOverview, CatalogDeadStockResponse} from './catalog.models';

describe('CatalogAdminApiService', () => {
  let service: CatalogAdminApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CatalogAdminApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(CatalogAdminApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the admin token and the typed dead-stock filters', () => {
    const response: CatalogDeadStockResponse = {
      generatedAt: '2026-09-04T12:00:00Z',
      minAgeMonths: 9,
      minQuantity: 4,
      books: [],
    };

    service.getDeadStock('access-token', 9, 4).subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/dead-stock`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('minAgeMonths')).toBe('9');
    expect(request.request.params.get('minQuantity')).toBe('4');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('does not send an empty bearer token', () => {
    expect(() => service.getDeadStock('  ', 6, 3)).toThrowError(
      'An administrator access token is required.',
    );
  });

  it('loads the dashboard and forwards its optional period', () => {
    const response = {generatedAt: '2026-09-04T12:00:00Z'} as CatalogAdminOverview;

    service.getOverview('access-token', '2026-03-01T00:00:00Z', '2026-03-15T00:00:00Z').subscribe(result => {
      expect(result).toBe(response);
    });

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/overview`);
    expect(request.request.params.get('from')).toBe('2026-03-01T00:00:00Z');
    expect(request.request.params.get('to')).toBe('2026-03-15T00:00:00Z');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads catalogue pages with all administrative filters', () => {
    const response = {generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminBookPage;

    service.getBooks('access-token', {
      search: 'prince',
      metadataStatus: 'Missing',
      rare: true,
      hidden: false,
      undated: true,
      page: 2,
      pageSize: 25,
    }).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/books`);
    expect(request.request.params.get('search')).toBe('prince');
    expect(request.request.params.get('metadataStatus')).toBe('Missing');
    expect(request.request.params.get('rare')).toBe('true');
    expect(request.request.params.get('hidden')).toBe('false');
    expect(request.request.params.get('undated')).toBe('true');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    request.flush(response);
  });

  it('uses the typed mutation endpoints and keeps the bearer token on every action', () => {
    service.setRare('access-token', '9782070363735', true).subscribe();
    const rareRequest = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/books/9782070363735/rare`);
    expect(rareRequest.request.method).toBe('POST');
    expect(rareRequest.request.params.get('isRare')).toBe('true');
    expect(rareRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    rareRequest.flush({});

    service.setAlertStatus('access-token', 'member-id', false).subscribe();
    const memberRequest = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/members/member-id/block`);
    expect(memberRequest.request.method).toBe('POST');
    expect(memberRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    memberRequest.flush({});
  });
});
