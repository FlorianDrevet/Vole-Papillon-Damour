import type {Configuration, RedirectRequest} from '@azure/msal-browser';

import {environment} from '../../environments/environment';

export const catalogMsalConfig: Configuration = {
  auth: {
    clientId: environment.entra.clientId,
    authority: environment.entra.authority,
    knownAuthorities: ['volepapillondamour.ciamlogin.com'],
    redirectUri: environment.entra.redirectUri,
    postLogoutRedirectUri: environment.entra.postLogoutRedirectUri,
  },
  cache: {
    cacheLocation: 'localStorage',
  },
  system: {
    loggerOptions: {
      loggerCallback: () => undefined,
      piiLoggingEnabled: false,
    },
  },
};

export const catalogLoginRequest: RedirectRequest = {
  scopes: [environment.entra.apiScope],
};
