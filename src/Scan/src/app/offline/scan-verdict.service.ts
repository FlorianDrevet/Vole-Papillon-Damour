import {Injectable} from '@angular/core';

import {
  LocalVerdict,
  ScanAssociationSettings,
  ScanCatalogBook,
  ScanOtherEdition,
} from './scan-offline.model';
import type {BookMetadata} from '../scanner/book-metadata.model';

const defaultSettings: Pick<
  ScanAssociationSettings,
  'duplicateThreshold' | 'demandSalesThreshold'
> = {
  duplicateThreshold: 5,
  demandSalesThreshold: 1,
};

@Injectable({providedIn: 'root'})
export class ScanVerdictService {
  calculate(
    book: ScanCatalogBook | null,
    settings: ScanAssociationSettings | null,
    catalog: readonly ScanCatalogBook[] = [],
  ): LocalVerdict {
    const quantityAvailable = book?.qtyAvailable ?? 0;
    const quantityAnnounced = book?.qtyAnnounced ?? 0;
    const salesCount = book?.salesCount ?? 0;
    const activeRequesterCount = book?.isWanted ? 1 : 0;
    const otherEditions = book
      ? this.siblings(book, catalog).filter(edition => edition.quantity > 0)
      : [];
    const totalKnownQuantity = quantityAvailable + quantityAnnounced + otherEditions
      .filter(edition => edition.strong)
      .reduce((total, edition) => total + edition.quantity, 0);
    const duplicateThreshold = settings?.duplicateThreshold ?? defaultSettings.duplicateThreshold;
    const demandSalesThreshold = settings?.demandSalesThreshold ?? defaultSettings.demandSalesThreshold;

    const verdict = book?.isManuallyRequested === true
      ? 'ManualRequest'
      : activeRequesterCount > 0
        ? 'Wanted'
      : salesCount >= demandSalesThreshold
        ? 'Selling'
        : totalKnownQuantity >= duplicateThreshold
          ? 'TooMany'
          : 'FirstCopy';

    return {
      verdict,
      totalKnownQuantity,
      salesCount,
      activeRequesterCount,
      isRare: book?.isRare ?? false,
      isKnown: book !== null,
      otherEditions,
    };
  }

  siblings(
    book: ScanCatalogBook | BookMetadata | null,
    catalog: readonly ScanCatalogBook[],
  ): ScanOtherEdition[] {
    if (!book) {
      return [];
    }

    return catalog
      .filter(candidate => candidate.isbn13 !== book.isbn13)
      .flatMap(candidate => {
        const strong = hasStrongWorkLink(book, candidate);
        const weak = !strong && Boolean(book.workKey && candidate.workKey &&
          book.workKey.toLocaleUpperCase() === candidate.workKey.toLocaleUpperCase());
        if (!strong && !weak) {
          return [];
        }

        return [{
          isbn13: candidate.isbn13,
          publisher: candidate.publisher ?? null,
          publicationYear: candidate.publicationYear ?? null,
          quantity: candidate.qtyAvailable + candidate.qtyAnnounced,
          strong,
        }];
      });
  }
}

function hasStrongWorkLink(
  book: ScanCatalogBook | BookMetadata,
  candidate: ScanCatalogBook,
): boolean {
  const sameWorkId = Boolean(book.workId && candidate.workId && book.workId === candidate.workId);
  if (sameWorkId) {
    return true;
  }

  const isbn = normalizeIsbn(book.isbn13);
  const candidateIsbn = normalizeIsbn(candidate.isbn13);
  return (book as ScanCatalogBook).otherIsbns?.some(other => normalizeIsbn(other) === candidateIsbn) === true ||
    candidate.otherIsbns?.some(other => normalizeIsbn(other) === isbn) === true;
}

function normalizeIsbn(value: string): string {
  return value.replace(/[\s-]/g, '').toUpperCase();
}
