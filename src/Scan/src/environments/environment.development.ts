import type {ScanEnvironment} from './environment.model';
import {localApiUrlForHost} from './local-api-url';

const localHost = typeof window === 'undefined' ? 'localhost' : window.location.hostname;

export const environment: ScanEnvironment = {
  production: false,
  apiUrl: localApiUrlForHost(localHost),
  appInsightsConnectionString: '',
  entra: {
    tenantId: 'b23c80b3-9776-4840-8255-fcbf3b3500fd',
    clientId: 'cabcb17b-537f-4d87-956b-60477103e0ec',
    authority: 'https://volepapillondamour.ciamlogin.com/b23c80b3-9776-4840-8255-fcbf3b3500fd/',
    redirectUri: typeof window === 'undefined' ? 'http://localhost:4300' : window.location.origin,
    postLogoutRedirectUri: typeof window === 'undefined' ? 'http://localhost:4300' : window.location.origin,
    apiScope: 'api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user',
  },
};
