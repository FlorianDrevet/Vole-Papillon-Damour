import type {AccountInfo} from '@azure/msal-browser';

import {getCatalogAccountDisplayName} from './catalog-account-name';

describe('getCatalogAccountDisplayName', () => {
  const account = (overrides: Partial<AccountInfo> = {}): AccountInfo => ({
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: 'camille@example.test',
    localAccountId: 'local-account-id',
    name: 'unknown',
    ...overrides,
  });

  it('combines given and family name claims when the legacy name claim is unknown', () => {
    const result = getCatalogAccountDisplayName(account({
      idTokenClaims: {
        given_name: 'Camille',
        family_name: 'Dupont',
      },
    }));

    expect(result).toBe('Camille Dupont');
  });

  it('accepts the External ID attribute claim names as well as the standard names', () => {
    const result = getCatalogAccountDisplayName(account({
      idTokenClaims: {
        givenName: 'Élodie',
        surname: 'Martin',
      },
    }));

    expect(result).toBe('Élodie Martin');
  });

  it('falls back to a usable account name and then to the username', () => {
    expect(getCatalogAccountDisplayName(account({name: 'Florian DREVET'})))
      .toBe('Florian DREVET');
    expect(getCatalogAccountDisplayName(account({name: 'unknown'})))
      .toBe('camille@example.test');
  });

  it('returns an empty label when no account is available', () => {
    expect(getCatalogAccountDisplayName(null)).toBe('');
  });
});
