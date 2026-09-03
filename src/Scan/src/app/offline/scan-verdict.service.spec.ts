import {TestBed} from '@angular/core/testing';

import {ScanVerdictService} from './scan-verdict.service';

describe('ScanVerdictService', () => {
  let service: ScanVerdictService;

  beforeEach(() => {
    TestBed.configureTestingModule({providers: [ScanVerdictService]});
    service = TestBed.inject(ScanVerdictService);
  });

  it('treats an ISBN absent from the local catalog as a first copy', () => {
    const result = service.calculate(null, null);

    expect(result.verdict).toBe('FirstCopy');
    expect(result.totalKnownQuantity).toBe(0);
    expect(result.isKnown).toBeFalse();
  });

  it('gives a wanted title priority over sales and duplicate thresholds', () => {
    const result = service.calculate({
      ...createBook(),
      qtyAvailable: 10,
      qtyAnnounced: 10,
      salesCount: 10,
      isWanted: true,
    }, createSettings(5, 1));

    expect(result.verdict).toBe('Wanted');
    expect(result.activeRequesterCount).toBe(1);
  });

  it('gives a selling title priority over the duplicate threshold', () => {
    const result = service.calculate({
      ...createBook(),
      qtyAvailable: 5,
      salesCount: 4,
    }, createSettings(5, 4));

    expect(result.verdict).toBe('Selling');
    expect(result.totalKnownQuantity).toBe(5);
  });

  it('includes announced copies when calculating duplicates', () => {
    const result = service.calculate({
      ...createBook(),
      qtyAvailable: 2,
      qtyAnnounced: 3,
    }, createSettings(5, 10));

    expect(result.verdict).toBe('TooMany');
    expect(result.totalKnownQuantity).toBe(5);
  });

  function createBook() {
    return {
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      workId: null,
      qtyAvailable: 0,
      qtyAnnounced: 0,
      salesCount: 0,
      isWanted: false,
      isRare: true,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }

  function createSettings(duplicateThreshold: number, demandSalesThreshold: number) {
    return {
      duplicateThreshold,
      demandSalesThreshold,
      deadStockMinAgeDays: 30,
      deadStockMinQuantity: 1,
      watchlistMaxItems: 100,
      alertCooldownDays: 30,
      sessionIdleTimeoutMinutes: 120,
      alertDelayMinutes: 120,
      updatedAt: '2026-09-03T08:00:00.000Z',
    };
  }
});
