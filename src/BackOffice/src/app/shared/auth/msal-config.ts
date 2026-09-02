import {
  BrowserCacheLocation,
  Configuration,
  InteractionType,
  PublicClientApplication,
} from '@azure/msal-browser';
import {
  MsalGuardConfiguration,
  MsalInterceptorConfiguration,
} from '@azure/msal-angular';

import {environment} from '../../../environments/environment';

export const loginRequest = {
  scopes: [environment.entra.apiScope],
};

export const msalConfig: Configuration = {
  auth: {
    clientId: environment.entra.clientId,
    authority: environment.entra.authority,
    knownAuthorities: ['volepapillondamour.ciamlogin.com'],
    redirectUri: environment.entra.redirectUri,
    postLogoutRedirectUri: environment.entra.postLogoutRedirectUri,
  },
  cache: {
    cacheLocation: BrowserCacheLocation.LocalStorage,
  },
  system: {
    loggerOptions: {
      loggerCallback: () => undefined,
      piiLoggingEnabled: false,
    },
  },
};

export function msalInstanceFactory(): PublicClientApplication {
  return new PublicClientApplication(msalConfig);
}

export const msalGuardConfig: MsalGuardConfiguration = {
  interactionType: InteractionType.Redirect,
  authRequest: loginRequest,
  loginFailedRoute: '/login',
};

// Axios is the BackOffice HTTP client, so MsalInterceptor is intentionally not
// registered. API token acquisition is centralized in ApiAccessTokenService.
export const msalInterceptorConfig: MsalInterceptorConfiguration = {
  interactionType: InteractionType.Redirect,
  protectedResourceMap: new Map(),
};
