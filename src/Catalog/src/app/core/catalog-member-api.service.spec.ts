import {provideHttpClient, withXhr} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogMemberApiService} from './catalog-member-api.service';
import {
  CatalogMemberCard,
  CatalogMemberFavoritesResponse,
  CatalogRecommendationsResponse,
} from './catalog.models';

describe('CatalogMemberApiService', () => {
  let service: CatalogMemberApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CatalogMemberApiService,
        provideHttpClient(withXhr()),
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

  it('loads personal recommendations with a bearer token and a limit of four', () => {
    const response = {status: 'Enabled', items: []} satisfies CatalogRecommendationsResponse;
    service.getRecommendations('member-token').subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(
      request => request.url === `${environment.apiUrl}/catalog/me/recommendations`,
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('limit')).toBe('4');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(response);
  });

  it('requests member recommendations at the selected heart threshold', () => {
    service.getRecommendations('member-token', 8, 4).subscribe();

    const request = http.expectOne(
      request => request.url === `${environment.apiUrl}/catalog/me/recommendations`,
    );
    expect(request.request.params.get('limit')).toBe('8');
    expect(request.request.params.get('minimumHearts')).toBe('4');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({status: 'Enabled', minimumHearts: 4, items: []});
  });

  it('reads personal recommendation preferences with a bearer token', () => {
    const response = {
      enabled: true,
      minimumHearts: 4,
      fairDigestConsentedAt: '2026-10-01T08:00:00Z',
    };
    service.getRecommendationPreference('member-token')
      .subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(
      `${environment.apiUrl}/catalog/me/recommendations/preference`,
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(response);
  });

  it('updates personal recommendation preferences with a bearer token', () => {
    service.setRecommendationPreference('member-token', false).subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/catalog/me/recommendations/preference`,
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({enabled: false});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(null);
  });

  it('updates recommendation consent while retaining the selected threshold', () => {
    service.setRecommendationPreference('member-token', true, 4, false).subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/catalog/me/recommendations/preference`,
    );
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      enabled: true,
      minimumHearts: 4,
      fairDigestConsent: false,
    });
    request.flush(null);
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

  it('posts a typed not-found report with comment and bearer token', () => {
    const response = {
      reportId: 'report-id',
      reportedAt: '2026-09-24T10:00:00Z',
      alreadyOpen: false,
    };

    service.reportNotFound('member-token', 'item-id', {comment: 'Rayon B'})
      .subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/item-id/not-found-report`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({comment: 'Rayon B'});
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

  it('merges local selection entries with the access token', () => {
    const entries = [{isbn13: '9782070612758', addedAt: '2026-09-20T10:00:00.000Z'}];
    service.mergeSelection('member-token', entries).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/selection/merge`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({entries});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({added: 1, alreadyPresent: 0, rejected: []});
  });

  it('loads member favorites with a bearer token', () => {
    const response: CatalogMemberFavoritesResponse = {
      generatedAt: '2026-11-14T10:00:00Z',
      total: 0,
      maximum: 200,
      works: [],
    };
    service.getFavorites('member-token').subscribe(result => expect(result).toEqual(response));

    const request = http.expectOne(`${environment.apiUrl}/catalog/me/favorites`);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush(response);
  });

  it('adds and removes a member favorite by ISBN with the same bearer token', () => {
    const isbn13 = '9782070612758';
    service.addFavorite('member-token', isbn13).subscribe(result => {
      expect(result).toEqual({isbn13, alreadyPresent: false, total: 1});
    });
    const add = http.expectOne(`${environment.apiUrl}/catalog/me/favorites/${isbn13}`);
    expect(add.request.method).toBe('PUT');
    expect(add.request.body).toBeNull();
    expect(add.request.headers.get('Authorization')).toBe('Bearer member-token');
    add.flush({isbn13, alreadyPresent: false, total: 1});

    service.removeFavorite('member-token', isbn13).subscribe();
    const remove = http.expectOne(`${environment.apiUrl}/catalog/me/favorites/${isbn13}`);
    expect(remove.request.method).toBe('DELETE');
    expect(remove.request.headers.get('Authorization')).toBe('Bearer member-token');
    remove.flush(null);
  });

  it('merges local favorite ISBNs and timestamps with the bearer token', () => {
    const entries = [{isbn13: '9782070612758', addedAt: '2026-11-14T10:00:00.000Z'}];
    service.mergeFavorites('member-token', entries).subscribe(result => {
      expect(result).toEqual({added: 1, skipped: 0, total: 1});
    });
    const request = http.expectOne(`${environment.apiUrl}/catalog/me/favorites/merge`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({entries});
    expect(request.request.headers.get('Authorization')).toBe('Bearer member-token');
    request.flush({added: 1, skipped: 0, total: 1});
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
