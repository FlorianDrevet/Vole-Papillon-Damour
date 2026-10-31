export interface EnvironmentInterface {
  production: boolean,
  auth_provider: 'entra' | 'keycloak',
  api_url: string,
  url_vpd_web_site: string,
  time_numero_modal: number,
  appinsights_connection_string: string,
  entra: {
    tenantId: string,
    clientId: string,
    authority: string,
    redirectUri: string,
    postLogoutRedirectUri: string,
    apiScope: string
  },
  keycloak?: {
    url: string,
    realm: string,
    clientId: string
  }
}
