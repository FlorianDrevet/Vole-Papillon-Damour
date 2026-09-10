import {TestBed} from '@angular/core/testing';

import {ScanStatusService} from './scan-status.service';

describe('ScanStatusService', () => {
  let service: ScanStatusService;

  beforeEach(() => {
    TestBed.configureTestingModule({providers: [ScanStatusService]});
    service = TestBed.inject(ScanStatusService);
  });

  it('keeps catalog freshness, outbox, decisions and set-aside totals independent', () => {
    service.updateFromLocalState(
      {
        key: 'catalog-sync',
        watermark: 'watermark',
        updatedAt: '2026-09-09T09:00:00.000Z',
        nextFair: null,
      },
      {pendingDecisionCount: 2, pendingTransmissionCount: 3},
      {orphaned: 1, quarantined: 2},
      new Date('2026-09-09T10:00:00.000Z'),
    );

    expect(service.snapshot()).toEqual({
      catalog: {state: 'fresh', syncedAt: '2026-09-09T09:00:00.000Z'},
      outbox: {state: 'retrying', queued: 3},
      decision: {pending: 2},
      setAside: {total: 3},
    });
  });

  it('reports a new set-aside as a message without changing the persistent backlog axis', () => {
    service.updateSetAsideTotals({orphaned: 2, quarantined: 0});
    service.showInfo('2 livres viennent d’être mis de côté.');

    expect(service.snapshot().setAside).toEqual({total: 2});
    expect(service.message()).toEqual({
      level: 'info',
      text: '2 livres viennent d’être mis de côté.',
    });
  });

  it('marks a catalog older than four hours as stale', () => {
    service.updateCatalog(
      {
        key: 'catalog-sync',
        watermark: 'watermark',
        updatedAt: '2026-09-09T05:00:00.000Z',
        nextFair: null,
      },
      new Date('2026-09-09T10:00:00.001Z'),
    );

    expect(service.snapshot().catalog).toEqual({
      state: 'stale',
      syncedAt: '2026-09-09T05:00:00.000Z',
    });
  });
});
