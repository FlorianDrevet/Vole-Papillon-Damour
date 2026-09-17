import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {CatalogAdminApiService} from './catalog-admin-api.service';
import {
  CatalogAdminAccount,
  CatalogAdminAccountPage,
  CatalogAdminBookPage,
  CatalogAdminOverview,
  CatalogAdminRareBook,
  CatalogAdminRareBookPage,
  CatalogAdminRareBookRequest,
  CatalogAdminVolunteerStatistics,
  CatalogDeadStockResponse,
} from './catalog.models';

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

  it('loads typed administrator volunteer statistics with period and fair filters', () => {
    const response = {
      generatedAt: '2026-09-12T12:00:00Z',
      from: '2026-08-13T12:00:00Z',
      to: '2026-09-12T12:00:00Z',
      fairId: 'fair-id',
      team: {
        activeVolunteerCount: 0,
        scannedCount: 0,
        keptCount: 0,
        rejectedCount: 0,
        keptRatePercent: null,
        soldQuantity: 0,
        soldOfKeptRatePercent: null,
        sessionCount: 0,
        scanDurationMinutes: 0,
        cashDurationMinutes: 0,
        totalDurationMinutes: 0,
        averageSessionsPerVolunteer: null,
      },
      volunteers: [],
      monthlyActivity: [],
      renewal: {newCount: 0, regularCount: 0, withdrawingCount: 0, windowDays: 90},
      dominantGenres: [],
    } as CatalogAdminVolunteerStatistics;

    service.getVolunteerStatistics(
      'access-token',
      '2026-08-13T12:00:00Z',
      '2026-09-12T12:00:00Z',
      'fair-id',
    ).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/volunteers/stats`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('from')).toBe('2026-08-13T12:00:00Z');
    expect(request.request.params.get('to')).toBe('2026-09-12T12:00:00Z');
    expect(request.request.params.get('fairId')).toBe('fair-id');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('scopes the fairs evolution and the catalogue flow to a single fair', () => {
    service.getFairsEvolution('access-token', undefined, undefined, 'fair-id').subscribe();
    service.getCatalogueFlowStats('access-token', undefined, undefined, 'fair-id').subscribe();

    const evolution = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/fairs/evolution`);
    expect(evolution.request.params.get('fairId')).toBe('fair-id');
    expect(evolution.request.params.has('from')).toBeFalse();
    evolution.flush({});

    const flow = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/catalogue/flow-stats`);
    expect(flow.request.params.get('fairId')).toBe('fair-id');
    flow.flush({});
  });

  it('loads catalogue pages with search and pagination', () => {
    const response = {generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminBookPage;

    service.getBooks('access-token', {
      search: 'prince',
      page: 2,
      pageSize: 25,
    }).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/books`);
    expect(request.request.params.get('search')).toBe('prince');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    request.flush(response);
  });

  it('loads rare-book pages with the complete administration filter set', () => {
    const response = {
      generatedAt: '2026-09-16T12:00:00Z',
      books: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
    } as CatalogAdminRareBookPage;

    service.getRareBooks('access-token', {
      search: 'doré',
      status: 'Published',
      availability: 'available',
      hasIsbn: false,
      minPrice: 12.5,
      maxPrice: 85,
      withoutPhoto: true,
      page: 2,
      pageSize: 25,
    }).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('doré');
    expect(request.request.params.get('status')).toBe('Published');
    expect(request.request.params.get('availability')).toBe('available');
    expect(request.request.params.get('hasIsbn')).toBe('false');
    expect(request.request.params.get('minPrice')).toBe('12.5');
    expect(request.request.params.get('maxPrice')).toBe('85');
    expect(request.request.params.get('withoutPhoto')).toBe('true');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('sends rare-book lifecycle and photo mutations to their typed endpoints', () => {
    const requestBody: CatalogAdminRareBookRequest = {
      title: 'Les fleurs du mal',
      authorMention: 'Charles Baudelaire',
      publisher: 'Poulet-Malassis',
      publicationYear: 1857,
      shelf: 'Éditions anciennes',
      price: 40,
      condition: 'GoodWithFlaws',
      publicDescription: 'Quelques rousseurs.',
      binding: 'Demi-chagrin',
      dimensions: '18 × 12 cm',
      pageCount: 320,
      shelfLocation: 'Table rares',
      priceSetBy: 'Conseil du 5 mars',
      isbn13: '9782070363735',
    };
    const response = {} as CatalogAdminRareBook;

    service.createRareBook('access-token', requestBody).subscribe(result => expect(result).toBe(response));
    const create = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual(requestBody);
    create.flush(response);

    service.updateRareBook('access-token', 'rare-id', {...requestBody, rowVersion: 'version'}).subscribe();
    const update = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/rare-id`);
    expect(update.request.method).toBe('PUT');
    update.flush(response);

    service.publishRareBook('access-token', 'rare-id').subscribe();
    const publish = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/rare-id/publish`);
    expect(publish.request.method).toBe('POST');
    publish.flush({rareBook: response, changed: true, warnings: []});

    const file = new File(['photo'], 'photo.jpg', {type: 'image/jpeg'});
    service.addRareBookPhoto('access-token', 'rare-id', file, 'Couverture').subscribe();
    const photo = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/rare-id/photos`);
    expect(photo.request.method).toBe('POST');
    expect(photo.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(photo.request.body).toEqual(jasmine.any(FormData));
    expect((photo.request.body as FormData).get('file')).toEqual(jasmine.objectContaining({name: 'photo.jpg', type: 'image/jpeg'}));
    expect((photo.request.body as FormData).get('caption')).toBe('Couverture');
    photo.flush(response);

    service.reorderRareBookPhotos('access-token', 'rare-id', ['photo-1', 'photo-2']).subscribe();
    const reorder = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/rare-id/photos/order`);
    expect(reorder.request.method).toBe('PUT');
    expect(reorder.request.body).toEqual({photoIds: ['photo-1', 'photo-2']});
    reorder.flush(response);

    service.deleteRareBookPhoto('access-token', 'photo-1').subscribe();
    const removePhoto = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/photos/photo-1`);
    expect(removePhoto.request.method).toBe('DELETE');
    removePhoto.flush(null);

    service.deleteRareBook('access-token', 'rare-id').subscribe();
    const removeBook = http.expectOne(request => request.url === `${environment.apiUrl}/rare-books/admin/rare-id`);
    expect(removeBook.request.method).toBe('DELETE');
    removeBook.flush(null);
  });

  it('uses the typed mutation endpoints and keeps the bearer token on every action', () => {
    service.setAlertStatus('access-token', 'member-id', false).subscribe();
    const memberRequest = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/members/member-id/block`);
    expect(memberRequest.request.method).toBe('POST');
    expect(memberRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    memberRequest.flush({});
  });

  it('loads Entra volunteer accounts with the administration bearer token', () => {
    const response = {
      generatedAt: '2026-09-04T12:00:00Z',
      accounts: [],
      totalCount: 0,
      page: 1,
      pageSize: 25,
    } as CatalogAdminAccountPage;

    service.getAdminAccounts('access-token', {search: 'Michel', page: 2, pageSize: 25})
      .subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/accounts/admin`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('search')).toBe('Michel');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('creates and updates typed volunteer account roles', () => {
    const account: CatalogAdminAccount = {
      externalId: 'account-id',
      email: 'michel@example.test',
      displayName: 'Michel Bonnet',
      accountEnabled: true,
      createdAt: null,
      roles: ['Tri'],
    };

    service.createAdminAccount('access-token', {
      email: 'michel@example.test',
      firstName: 'Michel',
      lastName: 'Bonnet',
      temporaryPassword: 'temporary-password',
      roles: ['Tri'],
    }).subscribe(result => expect(result).toEqual(account));

    const createRequest = http.expectOne(request => request.url === `${environment.apiUrl}/accounts/admin`);
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual({
      email: 'michel@example.test',
      firstName: 'Michel',
      lastName: 'Bonnet',
      temporaryPassword: 'temporary-password',
      roles: ['Tri'],
    });
    expect(createRequest.request.body.roles).toEqual(['Tri']);
    createRequest.flush(account);

    service.updateAdminAccountRoles('access-token', 'account-id', ['Tri', 'Caisse']).subscribe();

    const updateRequest = http.expectOne(request => request.url === `${environment.apiUrl}/accounts/admin/account-id/roles`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual({roles: ['Tri', 'Caisse']});
    updateRequest.flush(account);
  });

  it('updates the enabled state of a volunteer account in Entra', () => {
    const account: CatalogAdminAccount = {
      externalId: 'account-id',
      email: 'michel@example.test',
      displayName: 'Michel Bonnet',
      accountEnabled: false,
      createdAt: null,
      roles: ['Tri'],
    };

    service.updateAdminAccountStatus('access-token', 'account-id', false)
      .subscribe(result => expect(result).toEqual(account));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/accounts/admin/account-id/status`);
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({accountEnabled: false});
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(account);
  });
});
