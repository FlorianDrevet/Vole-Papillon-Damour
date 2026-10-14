import {
  formatManualRequesterName,
  formatManualRequesterPhone,
  manualRequesterPhoneHref,
} from './manual-request-format';

describe('manual request display formatting', () => {
  it('formats French mobile numbers in pairs', () => {
    expect(formatManualRequesterPhone('0612345678')).toBe('06 12 34 56 78');
  });

  it('formats international French mobile numbers with the country code', () => {
    expect(formatManualRequesterPhone('+33612345678')).toBe('+33 6 12 34 56 78');
  });

  it('uses a telephone link without display spaces', () => {
    expect(manualRequesterPhoneHref('+33 6 12 34 56 78')).toBe('tel:+33612345678');
  });

  it('formats the first name and uppercase family name', () => {
    expect(formatManualRequesterName('Jeanne', 'Martin', false)).toBe('Jeanne MARTIN');
  });

  it('shows the erased requester label without personal data', () => {
    expect(formatManualRequesterName(null, null, true)).toBe('Demandeur effacé');
  });
});
