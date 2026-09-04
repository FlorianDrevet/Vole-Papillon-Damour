import type {CatalogEnvironment} from './environment.model';

export const environment: CatalogEnvironment = {
  production: true,
  apiUrl: 'https://vole-papillon-damour-backend.onrender.com',
  publicUrl: 'https://livres.volepapillondamour.fr',
  entra: {
    tenantId: 'b23c80b3-9776-4840-8255-fcbf3b3500fd',
    clientId: '9ceb5499-d273-4d7c-b0d0-047eff9f0541',
    authority: 'https://volepapillondamour.ciamlogin.com/b23c80b3-9776-4840-8255-fcbf3b3500fd/',
    redirectUri: 'https://livres.volepapillondamour.fr',
    postLogoutRedirectUri: 'https://livres.volepapillondamour.fr',
    apiScope: 'api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user',
  },
};
