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

  it('gives a manual request priority over an existing wish, sales and duplicate thresholds', () => {
    const result = service.calculate({
      ...createBook(),
      qtyAvailable: 10,
      qtyAnnounced: 10,
      salesCount: 10,
      isWanted: true,
      isManuallyRequested: true,
    }, createSettings(5, 1));

    expect(result.verdict).toBe('ManualRequest');
    expect(result.activeRequesterCount).toBe(1);
  });

  it('keeps existing rules for catalog records without the manual request flag', () => {
    const legacyBook = {
      ...createBook(),
      salesCount: 1,
    };

    expect(service.calculate(legacyBook, createSettings(5, 1)).verdict).toBe('Selling');
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

  it('counts strongly linked editions of a work in the duplicate verdict', () => {
    const scannedBook = {
      ...createBook(),
      qtyAvailable: 1,
      workId: 'openlibrary-work-1',
    };
    const sibling = {
      ...createBook(),
      isbn13: '9782070612758',
      qtyAvailable: 2,
      workId: 'openlibrary-work-1',
      publisher: 'Gallimard',
      publicationYear: 1998,
    };

    const result = calculateWithCatalog(scannedBook, [scannedBook, sibling], createSettings(3, 10));

    expect(result.verdict).toBe('TooMany');
    expect(result.totalKnownQuantity).toBe(3);
    expect(result.otherEditions).toEqual([{
      isbn13: '9782070612758',
      publisher: 'Gallimard',
      publicationYear: 1998,
      quantity: 2,
      strong: true,
    }]);
  });

  it('shows work-key matches as weak siblings without changing the ISBN verdict', () => {
    const scannedBook = {
      ...createBook(),
      qtyAvailable: 1,
      workKey: 'MISERABLES|HUGO',
    };
    const sibling = {
      ...createBook(),
      isbn13: '9782070612758',
      qtyAvailable: 9,
      workKey: 'MISERABLES|HUGO',
    };

    const result = calculateWithCatalog(scannedBook, [scannedBook, sibling], createSettings(5, 10));

    expect(result.verdict).toBe('FirstCopy');
    expect(result.totalKnownQuantity).toBe(1);
    expect(result.otherEditions).toEqual([jasmine.objectContaining({
      isbn13: '9782070612758',
      quantity: 9,
      strong: false,
    })]);
  });

  it('uses an ISBN listed by the sibling as a strong link even without a work ID', () => {
    const scannedBook = createBook();
    const sibling = {
      ...createBook(),
      isbn13: '9782070612758',
      qtyAnnounced: 2,
      otherIsbns: ['9782070363735'],
    };

    const result = calculateWithCatalog(scannedBook, [scannedBook, sibling], createSettings(2, 10));

    expect(result.verdict).toBe('TooMany');
    expect(result.totalKnownQuantity).toBe(2);
    expect(result.otherEditions?.[0].strong).toBeTrue();
  });

  it('accepts metadata as the scanned work candidate and ignores old records without work keys', () => {
    const scannedMetadata = {
      isbn13: '9782070409228',
      workId: 'openlibrary-work-1',
      workKey: 'MISERABLES|HUGO',
    };
    const sibling = {
      ...createBook(),
      isbn13: '9782070612758',
      qtyAvailable: 1,
      workId: 'openlibrary-work-1',
    };
    const serviceWithSiblings = service as unknown as {
      siblings: (book: unknown, catalog: readonly unknown[]) => unknown[];
    };

    expect(serviceWithSiblings.siblings(scannedMetadata, [sibling])).toEqual([{
      isbn13: '9782070612758',
      publisher: null,
      publicationYear: null,
      quantity: 1,
      strong: true,
    }]);
    expect(calculateWithCatalog(createBook(), [createBook()]).otherEditions).toEqual([]);
  });

  function calculateWithCatalog(
    book: unknown,
    catalog: readonly unknown[],
    settings = createSettings(5, 1),
  ) {
    return (service.calculate as unknown as (
      book: unknown,
      settings: unknown,
      catalog: readonly unknown[],
    ) => {
      verdict: string;
      totalKnownQuantity: number;
      otherEditions?: Array<{
        isbn13: string;
        publisher: string | null | undefined;
        publicationYear: number | null | undefined;
        quantity: number;
        strong: boolean;
      }>;
    })(book, settings, catalog);
  }

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
