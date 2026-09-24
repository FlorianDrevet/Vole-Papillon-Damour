import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogMemberApiService} from './catalog-member-api.service';
import {CatalogMemberCard} from './catalog.models';

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

  it('sends the member token to the private volunteer statistics endpoint', () => {
    service.getVolunteerStatistics('member-token').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/me/statistics`);

    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({});
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

  it('gets member selection with the access token', () => {
    service.getSelection('member-token').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection`);

    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({generatedAt: '2026-09-23T10:00:00Z', nextFair: null, items: []});
  });

  it('adds a member selection target with the access token', () => {
    service.addSelectionItem('member-token', {isbn13: '9782070612758'}).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({isbn13: '9782070612758'});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({id: 'item-id', alreadyPresent: false});
  });

  it('removes a member selection item with the access token', () => {
    service.removeSelectionItem('member-token', 'item-id').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/item-id`);

    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(null);
  });

  it('posts a typed not-found report with location, comment and bearer token', () => {
    const response = {
      reportId: 'report-id',
      reportedAt: '2026-09-24T10:00:00Z',
      alreadyOpen: false,
    };

    service.reportNotFound('member-token', 'item-id', {location: 'Fair', comment: 'Rayon B'})
      .subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/item-id/not-found-report`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({location: 'Fair', comment: 'Rayon B'});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(response);
  });

  it('deletes a not-found report with the member bearer token', () => {
    service.cancelNotFoundReport('member-token', 'report-id').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/not-found-reports/report-id`);
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(null);
  });

  it('sets a member selection status with the access token until the selection migration', () => {
    service.setSelectionStatus('member-token', 'item-id', 'Purchased').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/item-id`);
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({status: 'Purchased'});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(null);
  });

  it('merges local selection entries with the access token', () => {
    const entries = [{isbn13: '9782070612758', addedAt: '2026-09-20T10:00:00.000Z'}];
    service.mergeSelection('member-token', entries).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/merge`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({entries});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({added: 1, alreadyPresent: 0, rejected: []});
  });

  it('loads the member card with a bearer token', () => {
    const expected: CatalogMemberCard = {
      qrPayload: 'VPDC1.AAAA.BBBB',
      recoveryCode: 'LUNE-4271',
      displayLabel: 'Camille',
      issuedAt: '2026-09-23T10:00:00Z',
    };
    let actual: CatalogMemberCard | undefined;

    service.getCard('member-token').subscribe(result => actual = result);

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/card`);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(expected);
    expect(actual).toEqual(expected);
  });

  it('rotates the member card with a bearer token', () => {
    const expected: CatalogMemberCard = {
      qrPayload: 'VPDC1.CCCC.DDDD',
      recoveryCode: 'AUBE-5932',
      displayLabel: 'Camille',
      issuedAt: '2026-09-24T10:00:00Z',
    };
    let actual: CatalogMemberCard | undefined;

    service.rotateCard('member-token').subscribe(result => actual = result);

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/card/rotate`);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(expected);
    expect(actual).toEqual(expected);
  });
});
