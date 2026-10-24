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
  CatalogAdminCheckoutPassageLookup,
  CatalogDeadStockResponse,
  CatalogNotFoundQueue,
  CatalogClosedNotFoundReports,
  CatalogNotFoundSummary,
  CatalogNotFoundClosure,
  CatalogManualRequestQueue,
  CatalogManualRequestSummary,
  CatalogManualRequestStock,
  CatalogManualRequester,
  CatalogManualRequesterCreated,
  CatalogManualBookRequestCreated,
  CatalogManualRequestRelease,
  CatalogManualRequesterDeletion,
  CatalogAdminIsbndbStatus,
  CatalogAdminIsbndbAbsentBooksPage,
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

  it('loads the ISBNdb status with the administrator bearer token', () => {
    const response = {
      dailyLimit: 5000,
      dailyRemaining: 4812,
      observedAt: '2026-10-24T09:00:00Z',
      resetsAt: '2026-10-25T00:00:00Z',
      status: 'Operational',
      exhaustedUntil: null,
      usageToday: [],
      pendingCatchUp: {total: 12, inStock: 5, beforeMigration: 10},
      absentToComplete: 3,
      lastRun: null,
      unmatchedSubjects: [],
    } as CatalogAdminIsbndbStatus;

    service.getIsbndbStatus('access-token').subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(`${environment.apiUrl}/books/admin/isbndb/status`);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads ISBNdb-absent books with status, search, paging and the administrator token', () => {
    const response = {items: [], totalCount: 4, page: 2, pageSize: 5} as CatalogAdminIsbndbAbsentBooksPage;

    service.getIsbndbAbsentBooks('access-token', {
      status: 'completed', search: 'Camus', page: 2, pageSize: 5,
    }).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(`${environment.apiUrl}/books/admin/books/isbndb-absent?status=completed&search=Camus&page=2&pageSize=5`);
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('posts a completion state to the ISBNdb absent-book endpoint', () => {
    service.setIsbndbCompletion('access-token', '9782070363735', true).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/books/admin/books/9782070363735/isbndb-completion`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({completed: true});
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({isbn13: '9782070363735', isCompleted: true, completedAt: null, completedBy: null, changed: true});
  });

  it('posts an on-demand ISBNdb recheck to the administrator endpoint', () => {
    service.recheckIsbndbBook('access-token', '9782070363735').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/books/admin/books/9782070363735/isbndb-recheck`);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush({isbn13: '9782070363735', outcome: 'NotFound', resetsAt: null});
  });

  it('permanently deletes an admin catalogue book through the dedicated endpoint', () => {
    service.permanentlyDeleteBook('access-token', '9782070363735').subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/books/admin/books/9782070363735/permanent`,
    );
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(null);
  });

  it('loads a typed not-found queue with filters and the administrator bearer token', () => {
    const response = {
      generatedAt: '2026-09-24T10:00:00Z',
      openTargetCount: 1,
      openReportCount: 2,
      overdueTargetCount: 0,
      page: 2,
      pageSize: 20,
      totalCount: 1,
      items: [],
    } as CatalogNotFoundQueue;

    service.getNotFoundQueue('access-token', {
      kind: 'edition', sort: 'oldest', page: 2, pageSize: 20,
    }).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/not-found-reports`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('kind')).toBe('edition');
    expect(request.request.params.get('sort')).toBe('oldest');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads closed not-found reports with page filters and the administrator bearer token', () => {
    const response = {
      generatedAt: '2026-09-24T10:00:00Z', page: 1, pageSize: 25, totalCount: 0, items: [],
    } as CatalogClosedNotFoundReports;

    service.getClosedNotFoundReports('access-token', 1, 25).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/not-found-reports/closed`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads a target not-found summary with the administrator bearer token', () => {
    const response = {
      openTargetCount: 0,
      overdueTargetCount: 0,
      openReportCount: 3,
      firstReportedAt: '2026-09-20T10:00:00Z',
      latestComment: null,
      withdrawalMovementIds: [],
    } as CatalogNotFoundSummary;

    service.getNotFoundSummary('access-token', {rareBookId: 'rare-id'})
      .subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/not-found-reports/summary`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('rareBookId')).toBe('rare-id');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('posts a typed not-found closure to the edition action endpoint', () => {
    const response = {
      closedReportCount: 2, withdrawnQuantity: 1, quantityAvailable: 2, movementId: 'movement-id',
    } as CatalogNotFoundClosure;
    const body = {quantityFound: 1, reason: 'Damaged' as const, note: 'Retiré après vérification'};

    service.closeNotFound(
      'access-token', {kind: 'edition', reference: '9782070612758'}, 'withdrawal', body,
    ).subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(
      `${environment.apiUrl}/books/admin/not-found-reports/editions/9782070612758/withdrawal`,
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads the manual request queue with tab, search, paging and bearer token', () => {
    const response = {items: [], totalCount: 0, foundCount: 0, searchingCount: 0, closedCount: 0,
      foundOverdueCount: 0, page: 2, pageSize: 25} as CatalogManualRequestQueue;
    service.getManualRequests('access-token', {tab: 'found', q: 'Jeanne 06 12', page: 2, pageSize: 25})
      .subscribe(result => expect(result).toBe(response));

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/books/admin/manual-requests`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('tab')).toBe('found');
    expect(request.request.params.get('q')).toBe('Jeanne 06 12');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('loads manual request summary and stock by target', () => {
    const summary = {foundCount: 3, foundOverdueCount: 1} as CatalogManualRequestSummary;
    const stock = {quantityAvailable: 2, quantityAnnounced: 4, nextFairStart: '2026-10-20T10:00:00+02:00'} as CatalogManualRequestStock;
    service.getManualRequestSummary('access-token').subscribe(result => expect(result).toBe(summary));
    service.getManualRequestStock('access-token', {scope: 'Work', workId: 'work/1'})
      .subscribe(result => expect(result).toBe(stock));

    const summaryRequest = http.expectOne(`${environment.apiUrl}/books/admin/manual-requests/summary`);
    expect(summaryRequest.request.method).toBe('GET');
    expect(summaryRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    summaryRequest.flush(summary);
    const stockRequest = http.expectOne(request =>
      request.url === `${environment.apiUrl}/books/admin/manual-requests/stock` &&
      request.params.get('scope') === 'Work' && request.params.get('workId') === 'work/1');
    expect(stockRequest.request.method).toBe('GET');
    expect(stockRequest.request.headers.get('Authorization')).toBe('Bearer access-token');
    stockRequest.flush(stock);
  });

  it('gets, creates, updates and deletes a manual requester', () => {
    const requester = {requesterId: 'requester-id', lastName: 'Martin', firstName: 'Jeanne', phone: '0612345678',
      erased: false, consentRecordedAt: '2026-10-12T10:00:00Z', consentRecordedByName: 'Admin', requests: []} as CatalogManualRequester;
    const created = {requesterId: 'new-requester'} as CatalogManualRequesterCreated;
    const deletion = {cancelledRequestCount: 2, notifiedMemberCount: 1} as CatalogManualRequesterDeletion;

    service.getManualRequester('access-token', 'requester-id').subscribe(result => expect(result).toBe(requester));
    service.createManualRequester('access-token', {
      lastName: 'Martin', firstName: 'Jeanne', phone: '06 12 34 56 78', consentGiven: true,
    }).subscribe(result => expect(result).toBe(created));
    service.updateManualRequester('access-token', 'requester-id', {
      lastName: 'Durand', firstName: 'Alice', phone: '0611111111',
    }).subscribe();
    service.deleteManualRequester('access-token', 'requester-id').subscribe(result => expect(result).toBe(deletion));

    const get = http.expectOne(request =>
      request.url === `${environment.apiUrl}/books/admin/manual-requesters/requester-id` && request.method === 'GET');
    expect(get.request.method).toBe('GET');
    expect(get.request.headers.get('Authorization')).toBe('Bearer access-token');
    get.flush(requester);
    const create = http.expectOne(request =>
      request.url === `${environment.apiUrl}/books/admin/manual-requesters` && request.method === 'POST');
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(create.request.body).toEqual({
      lastName: 'Martin', firstName: 'Jeanne', phone: '06 12 34 56 78', consentGiven: true,
    });
    create.flush(created);
    const update = http.expectOne(request =>
      request.url === `${environment.apiUrl}/books/admin/manual-requesters/requester-id` && request.method === 'PUT');
    expect(update.request.method).toBe('PUT');
    expect(update.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(update.request.body).toEqual({lastName: 'Durand', firstName: 'Alice', phone: '0611111111'});
    update.flush(null);
    const remove = http.expectOne(request =>
      request.url === `${environment.apiUrl}/books/admin/manual-requesters/requester-id` && request.method === 'DELETE');
    expect(remove.request.method).toBe('DELETE');
    expect(remove.request.headers.get('Authorization')).toBe('Bearer access-token');
    remove.flush(deletion);
  });

  it('creates, picks, gives, refuses and cancels manual book requests', () => {
    const created = {requestId: 'request-id', status: 'Searching'} as CatalogManualBookRequestCreated;
    const release = {notifiedMemberCount: 2} as CatalogManualRequestRelease;
    const addBody = {
      scope: 'Edition', isbn13: '9782070612758', title: 'Le Petit Prince', authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard', publicationYear: 1943, pickFromStock: false,
    } as const;

    service.addManualBookRequest('access-token', 'requester-id', addBody).subscribe(result => expect(result).toBe(created));
    service.pickManualRequest('access-token', 'request-id').subscribe();
    service.markManualRequestGiven('access-token', 'request-id').subscribe();
    service.refuseManualRequest('access-token', 'request-id', {keepSearching: true})
      .subscribe(result => expect(result).toBe(release));
    service.cancelManualRequest('access-token', 'request-id').subscribe(result => expect(result).toBe(release));

    const add = http.expectOne(`${environment.apiUrl}/books/admin/manual-requesters/requester-id/requests`);
    expect(add.request.method).toBe('POST');
    expect(add.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(add.request.body).toEqual(addBody);
    add.flush(created);
    const pick = http.expectOne(`${environment.apiUrl}/books/admin/manual-requests/request-id/pick`);
    expect(pick.request.method).toBe('POST');
    expect(pick.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(pick.request.body).toBeNull();
    pick.flush(null);
    const given = http.expectOne(`${environment.apiUrl}/books/admin/manual-requests/request-id/given`);
    expect(given.request.method).toBe('POST');
    expect(given.request.headers.get('Authorization')).toBe('Bearer access-token');
    given.flush(null);
    const refuse = http.expectOne(`${environment.apiUrl}/books/admin/manual-requests/request-id/refuse`);
    expect(refuse.request.method).toBe('POST');
    expect(refuse.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(refuse.request.body).toEqual({keepSearching: true});
    refuse.flush(release);
    const cancel = http.expectOne(`${environment.apiUrl}/books/admin/manual-requests/request-id/cancel`);
    expect(cancel.request.method).toBe('POST');
    expect(cancel.request.headers.get('Authorization')).toBe('Bearer access-token');
    cancel.flush(release);
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
      price: 40,
      condition: 'GoodWithFlaws',
      publicDescription: 'Quelques rousseurs.',
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

  it('uploads a book cover as multipart form data with the administrator token', () => {
    const file = new File(['cover'], 'cover.jpg', {type: 'image/jpeg'});

    service.uploadBookCover('access-token', '9782070363735', file).subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/books/admin/books/9782070363735/cover`,
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(request.request.headers.has('Content-Type')).toBeFalse();
    expect(request.request.body).toEqual(jasmine.any(FormData));
    expect((request.request.body as FormData).get('file'))
      .toEqual(jasmine.objectContaining({name: 'cover.jpg', type: 'image/jpeg'}));
    request.flush({});
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

  it('looks up a checkout passage by the member reference', () => {
    const response: CatalogAdminCheckoutPassageLookup = {
      id: '3f2a9c1b-0000-0000-0000-000000000001',
      occurredAt: '2026-09-14T15:00:00Z',
      lineCount: 2,
      displayLabel: 'Camille',
    };

    service.lookupCheckoutPassage('access-token', '3F2A9C1B').subscribe(result => {
      expect(result).toEqual(response);
    });

    const request = http.expectOne(request => request.url === `${environment.apiUrl}/administration/checkout-passages/lookup`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('reference')).toBe('3F2A9C1B');
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(response);
  });

  it('posts an administrator reason when dissociating a checkout passage', () => {
    service.dissociateCheckoutPassage(
      'access-token',
      '3f2a9c1b-0000-0000-0000-000000000001',
      'Carte associée par erreur',
    ).subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/administration/checkout-passages/3f2a9c1b-0000-0000-0000-000000000001/dissociate`,
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({reason: 'Carte associée par erreur'});
    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    request.flush(null);
  });
});
