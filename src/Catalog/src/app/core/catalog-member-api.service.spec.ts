import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogMemberApiService} from './catalog-member-api.service';

describe('CatalogMemberApiService', () => {
  let service: CatalogMemberApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CatalogMemberApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(CatalogMemberApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the member token only to the protected watchlist endpoint', () => {
    service.getWatchlist('member-token').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/watchlist`);

    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({generatedAt: '2026-09-04T20:00:00Z', alertStatus: 'Active', bounceCount: 0, items: []});
  });

  it('posts a typed target and can remove it with the same bearer token', () => {
    service.addWatchlistItem('member-token', {
      scope: 'Edition',
      workId: null,
      isbn13: '9782070363735',
    }).subscribe();
    const addRequest = http.expectOne(`${environment.apiUrl}/catalog/me/watchlist`);

    expect(addRequest.request.method).toBe('POST');
    expect(addRequest.request.body).toEqual({
      scope: 'Edition',
      workId: null,
      isbn13: '9782070363735',
    });
    expect(addRequest.request.headers.get('Authorization')).toBe('Bearer member-token');
    addRequest.flush({id: 'item-id'});

    service.removeWatchlistItem('member-token', 'item-id').subscribe();
    const removeRequest = http.expectOne(`${environment.apiUrl}/catalog/me/watchlist/item-id`);

    expect(removeRequest.request.method).toBe('DELETE');
    expect(removeRequest.request.headers.get('Authorization')).toBe('Bearer member-token');
    removeRequest.flush(null);
  });

  it('deletes the account through the protected account endpoint', () => {
    service.deleteAccount('member-token').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me`);

    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(null);
  });

  it('updates the member alert status through the protected endpoint', () => {
    service.setAlertStatus('member-token', false).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/alerts`);

    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({enabled: false});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({alertStatus: 'Suspended', bounceCount: 0, changed: true});
  });

  it('rejects an empty token before creating an HTTP request', () => {
    expect(() => service.getWatchlist('  ')).toThrowError('A member access token is required.');
    http.expectNone(() => true);
  });
});
