export interface CatalogEntraEnvironment {
  tenantId: string;
  clientId: string;
  authority: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
  apiScope: string;
}

export interface CatalogEnvironment {
  production: boolean;
  apiUrl: string;
  publicUrl: string;
  entra: CatalogEntraEnvironment;
}
