import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogAdminApiService} from './catalog-admin-api.service';
import {CatalogDeadStockResponse} from './catalog.models';

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
});
