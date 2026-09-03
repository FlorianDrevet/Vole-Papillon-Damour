import {Injectable} from '@angular/core';

import {
  LocalVerdict,
  ScanAssociationSettings,
  ScanCatalogBook,
} from './scan-offline.model';

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
  ): LocalVerdict {
    const quantityAvailable = book?.qtyAvailable ?? 0;
    const quantityAnnounced = book?.qtyAnnounced ?? 0;
    const salesCount = book?.salesCount ?? 0;
    const activeRequesterCount = book?.isWanted ? 1 : 0;
    const totalKnownQuantity = quantityAvailable + quantityAnnounced;
    const duplicateThreshold = settings?.duplicateThreshold ?? defaultSettings.duplicateThreshold;
    const demandSalesThreshold = settings?.demandSalesThreshold ?? defaultSettings.demandSalesThreshold;

    const verdict = activeRequesterCount > 0
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
    };
  }
}
