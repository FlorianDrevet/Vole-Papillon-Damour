import type {ScanEnvironment} from './environment.model';

export const environment: ScanEnvironment = {
  production: true,
  apiUrl: 'https://vole-papillon-damour-backend.onrender.com',
  appInsightsConnectionString: '__APPINSIGHTS_CONNECTION_STRING__',
  entra: {
    clientId: 'cabcb17b-537f-4d87-956b-60477103e0ec',
    authority: 'https://volepapillondamour.ciamlogin.com/',
    redirectUri: 'https://vpd-scan-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io',
    postLogoutRedirectUri: 'https://vpd-scan-ca-dev.mangoground-a76d7dbc.westeurope.azurecontainerapps.io',
    apiScope: 'api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user',
  },
};
