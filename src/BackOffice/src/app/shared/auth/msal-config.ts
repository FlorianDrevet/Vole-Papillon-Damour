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

/**
 * Page d'atterrissage après une connexion réussie. MSAL revient toujours sur
 * `redirectUri` (la racine), puis renvoie vers la page d'où la connexion est
 * partie : depuis l'écran de connexion, cette page serait l'écran de connexion
 * lui-même, d'où ce point de chute explicite.
 */
export const HOME_ROUTE = '/actualites';

export const LOGIN_ROUTE = '/login';

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
  loginFailedRoute: LOGIN_ROUTE,
};

// Axios is the BackOffice HTTP client, so MsalInterceptor is intentionally not
// registered. API token acquisition is centralized in ApiAccessTokenService.
export const msalInterceptorConfig: MsalInterceptorConfiguration = {
  interactionType: InteractionType.Redirect,
  protectedResourceMap: new Map(),
};
