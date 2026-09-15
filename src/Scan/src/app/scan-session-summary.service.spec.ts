import {TestBed} from '@angular/core/testing';

import {ScanSessionSummaryService} from './scan-session-summary.service';
import {ScanSessionCounts, ScanSessionSnapshot} from './offline/scan-offline.model';

describe('ScanSessionSummaryService', () => {
  let service: ScanSessionSummaryService;

  beforeEach(() => {
    TestBed.configureTestingModule({providers: [ScanSessionSummaryService]});
    service = TestBed.inject(ScanSessionSummaryService);
  });

  it('stores an immutable summary until the summary screen is left', () => {
    const session = createSession();
    const counts: ScanSessionCounts = {scannedCount: 2, keptCount: 1, rejectedCount: 1};

    service.setSummary(session, counts, '3 min de tri');
    session.lastScanAt = 'changed';
    counts.keptCount = 99;

    expect(service.summary()).toEqual({
      session: {...createSession()},
      counts: {scannedCount: 2, keptCount: 1, rejectedCount: 1},
      durationLabel: '3 min de tri',
    });

    service.clear();
    expect(service.summary()).toBeNull();
  });

  function createSession(): ScanSessionSnapshot {
    return {
      key: 'active-session',
      clientSessionId: 'client-session-1',
      remoteSessionId: null,
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-15T08:00:00.000Z',
      lastScanAt: '2026-09-15T08:03:00.000Z',
      lastSyncAt: '2026-09-15T08:03:00.000Z',
      closeRequested: false,
      closeReason: null,
    };
  }
});
