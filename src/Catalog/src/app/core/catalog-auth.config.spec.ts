import {BrowserCacheLocation} from '@azure/msal-browser';

import {environment} from '../../environments/environment';
import {
  catalogLoginRequest,
  catalogMsalConfig,
  catalogRegistrationRequest,
} from './catalog-auth.config';

describe('catalogMsalConfig', () => {
  it('uses the catalog application and API scope without exposing secrets', () => {
    expect(catalogMsalConfig.auth.clientId).toBe(environment.entra.clientId);
    expect(catalogMsalConfig.auth.authority).toBe(environment.entra.authority);
    expect(catalogMsalConfig.auth.redirectUri).toBe(environment.entra.redirectUri);
    expect(catalogMsalConfig.auth.postLogoutRedirectUri).toBe(environment.entra.postLogoutRedirectUri);
    expect(catalogMsalConfig.cache?.cacheLocation).toBe(BrowserCacheLocation.LocalStorage);
  });

  it('requests the French External ID experience for sign-in and sign-up', () => {
    expect(catalogLoginRequest.extraQueryParameters).toEqual({
      ui_locales: 'fr-FR',
      mkt: 'fr-FR',
    });
    expect(catalogRegistrationRequest.extraQueryParameters).toEqual({
      ui_locales: 'fr-FR',
      mkt: 'fr-FR',
    });
  });
});
