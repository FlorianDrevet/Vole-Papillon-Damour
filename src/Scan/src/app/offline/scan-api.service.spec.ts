import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {ScanApiService} from './scan-api.service';
import {
  ScanCatalogDeltaResponse,
  ScanBookResponse,
  ScanSessionResponse,
  ScanVolunteerStatisticsResponse,
} from './scan-offline.model';

describe('ScanApiService', () => {
  let service: ScanApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ScanApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(ScanApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the full catalog when no watermark exists', () => {
    const response = createDelta();
    let received: ScanCatalogDeltaResponse | undefined;
    service.getCatalogDelta(null).subscribe(value => received = value);

    const request = http.expectOne(`${environment.apiUrl}/scan/catalog/delta`);
    expect(request.request.method).toBe('GET');
    request.flush(response);
    expect(received).toEqual(response);
  });

  it('sends the scan gesture to the session endpoint', () => {
    const response = createScanResponse();
    const gesture = {
      isbn: '9782070363735',
      kept: true,
      occurredAt: '2026-09-03T08:00:00.000Z',
      clientGestureId: 'gesture-1',
    };
    service.scanBook('session-1', gesture).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/sessions/session-1/scans`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(gesture);
    request.flush(response);
  });

  it('requests the private volunteer statistics snapshot', () => {
    const response = createStatisticsResponse();
    let received: ScanVolunteerStatisticsResponse | undefined;
    service.getVolunteerStatistics().subscribe(value => received = value);

    const request = http.expectOne(`${environment.apiUrl}/scan/me/statistics`);
    expect(request.request.method).toBe('GET');
    request.flush(response);

    expect(received).toEqual(response);
  });

  it('sends a cash sale to the sale endpoint', () => {
    const sale = {
      isbn: '9782070363735',
      quantity: 1,
      occurredAt: '2026-09-03T08:00:00.000Z',
      clientGestureId: 'sale-1',
      checkoutPassageId: 'passage-1',
    };
    service.registerSale(sale).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/sales`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(sale);
    request.flush({});
  });

  it('associates a checkout passage from the member credential', () => {
    const association = {
      credential: 'VPDC1.AAAA.BBBB',
      occurredAt: '2026-09-03T08:00:00.000Z',
    };
    service.associatePassage('passage-1', association).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/passages/passage-1/member`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(association);
    request.flush({
      checkoutPassageId: 'passage-1',
      status: 'Associated',
      displayLabel: 'Camille',
      alreadyProcessed: false,
    });
  });

  it('resolves a member card to a display label for cash-desk confirmation', () => {
    service.resolveMemberCard('VPDC1.AAAA.BBBB').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/member-cards/resolve`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({credential: 'VPDC1.AAAA.BBBB'});
    request.flush({displayLabel: 'Camille'});
  });

  it('sends a rare cash sale without any price or ordinary sale fields', () => {
    const sale = {
      occurredAt: '2026-09-03T08:00:00.000Z',
      scanSessionId: 'session-1',
      assoEventsId: null,
      checkoutPassageId: 'passage-1',
    };
    service.markRareBookSold('rare-1', sale).subscribe();

    const request = http.expectOne(environment.apiUrl + '/rare-books/cash/rare-1/sold');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(sale);
    expect(request.request.body.price).toBeUndefined();
    expect(request.request.body.quantity).toBeUndefined();
    request.flush({});
  });

  it('restores a rare cash sale through the dedicated correction endpoint', () => {
    service.restoreRareBookAvailability('rare-1').subscribe();

    const request = http.expectOne(environment.apiUrl + '/rare-books/cash/rare-1/restore');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });

  it('opens a session with the local client id for idempotent replay', () => {
    const response = createSessionResponse();
    const openRequest = {
      mode: 'AvailableNow' as const,
      targetAssoEventsId: null,
      clientSessionId: 'session-1',
      startedAt: '2026-09-03T08:00:00.000Z',
    };
    service.openSession(openRequest).subscribe();

    const request = http.expectOne(`${environment.apiUrl}/scan/sessions`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(openRequest);
    request.flush(response);
  });

  it('passes the delta watermark as a query parameter', () => {
    service.getCatalogDelta('2026-09-03T08:00:00.000Z').subscribe();

    const request = http.expectOne(
      `${environment.apiUrl}/scan/catalog/delta?since=2026-09-03T08:00:00.000Z`,
    );
    expect(request.request.method).toBe('GET');
    request.flush(createDelta());
  });

  function createDelta(): ScanCatalogDeltaResponse {
    return {
      generatedAt: '2026-09-03T08:00:00.000Z',
      nextWatermark: '2026-09-03T08:00:00.000Z',
      nextFair: null,
      books: [],
      rareBooks: [],
      removedRareBookIds: [],
      settings: {
        duplicateThreshold: 5,
        demandSalesThreshold: 1,
        deadStockMinAgeDays: 30,
        deadStockMinQuantity: 1,
        watchlistMaxItems: 100,
        alertCooldownDays: 30,
        sessionIdleTimeoutMinutes: 120,
        alertDelayMinutes: 120,
        updatedAt: '2026-09-03T08:00:00.000Z',
      },
    };
  }

  function createScanResponse(): ScanBookResponse {
    return {
      isbn13: '9782070363735',
      verdict: 'FirstCopy',
      qtyAvailable: 1,
      qtyAnnounced: 0,
      scanSessionId: 'session-1',
      movementType: 'DirectEntry',
      alreadyProcessed: false,
      clockSuspect: false,
    };
  }

  function createSessionResponse(): ScanSessionResponse {
    return {
      scanSessionId: 'session-1',
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:00:00.000Z',
      lastSyncAt: '2026-09-03T08:00:00.000Z',
      lateArrivals: false,
      endedAt: null,
      closeReason: null,
      status: 'InProgress',
      reusedExistingSession: false,
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
    };
  }

  function createStatisticsResponse(): ScanVolunteerStatisticsResponse {
    return {
      generatedAt: '2026-09-11T12:00:00.000Z',
      memberSince: null,
      scan: {
        scannedCount: 0,
        keptCount: 0,
        rejectedCount: 0,
        sessionCount: 0,
        durationMinutes: 0,
        firstSessionAt: null,
        medianTeamKeepRatePercent: null,
        monthly: [],
        timeSlots: [],
        impact: {
          foundReaderCount: 0,
          newTitleCount: 0,
          rareCount: 0,
          alertItemCount: 0,
          foundReaderIsEstimated: true,
        },
        topGenres: [],
        recentSessions: [],
      },
      cash: {
        grossSoldQuantity: 0,
        soldQuantity: 0,
        saleMovementCount: 0,
        voidedSaleQuantity: 0,
        fairCount: 0,
        estimatedDurationMinutes: 0,
        estimatedCadencePerHour: null,
        medianTeamCadencePerHour: null,
        fairBreakdown: [],
        peak: {fairDate: null, localHour: null, quantity: 0},
        topBooks: [],
        topGenres: [],
        estimatedTriAndCashOverlap: 0,
        estimatedRevenueShare: null,
        revenueShare: null,
        durationIsEstimated: true,
        cadenceIsEstimated: true,
        revenueShareIsEstimated: true,
        triAndCashOverlapIsEstimated: true,
      },
    };
  }
});
