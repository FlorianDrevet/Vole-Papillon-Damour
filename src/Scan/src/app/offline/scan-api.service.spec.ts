import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {ScanApiService} from './scan-api.service';
import {ScanCatalogDeltaResponse, ScanBookResponse, ScanSessionResponse} from './scan-offline.model';

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

  it('opens a session with the local client id for idempotent replay', () => {
    const response = createSessionResponse();
    const openRequest = {
      mode: 'AvailableNow' as const,
      targetAssoEventsId: null,
      clientSessionId: 'session-1',
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
      books: [],
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
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
    };
  }
});
