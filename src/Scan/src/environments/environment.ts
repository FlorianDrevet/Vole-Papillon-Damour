import type {ScanEnvironment} from './environment.model';

export const environment: ScanEnvironment = {
  production: true,
  apiUrl: 'https://vole-papillon-damour-backend.onrender.com',
  appInsightsConnectionString: '__APPINSIGHTS_CONNECTION_STRING__',
  entra: {
    tenantId: 'b23c80b3-9776-4840-8255-fcbf3b3500fd',
    clientId: 'cabcb17b-537f-4d87-956b-60477103e0ec',
    authority: 'https://volepapillondamour.ciamlogin.com/b23c80b3-9776-4840-8255-fcbf3b3500fd/',
    redirectUri: 'https://scan.volepapillondamour.fr',
    postLogoutRedirectUri: 'https://scan.volepapillondamour.fr',
    apiScope: 'api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user',
  },
};
