export interface ScanEnvironment {
  production: boolean;
  apiUrl: string;
  appInsightsConnectionString: string;
  entra: ScanEntraEnvironment;
}

export interface ScanEntraEnvironment {
  tenantId: string;
  clientId: string;
  authority: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
  apiScope: string;
}
