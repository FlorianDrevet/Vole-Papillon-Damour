import {EnvironmentInterface} from "../app/shared/interfaces/environment.interface";

export const environment : EnvironmentInterface =
  {
    production: true,
    api_url: "https://vole-papillon-damour-backend.onrender.com",
    url_vpd_web_site: "https://vole-papillon-damour-website.onrender.com/accueil",
    time_numero_modal: 2000,
    appinsights_connection_string: "__APPINSIGHTS_CONNECTION_STRING__",
    entra: {
      tenantId: "b23c80b3-9776-4840-8255-fcbf3b3500fd",
      clientId: "b5e7446e-2e87-4eed-8a6a-d40b3c913c9c",
      authority: "https://volepapillondamour.ciamlogin.com/",
      redirectUri: "https://backoffice.volepapillondamour.fr",
      postLogoutRedirectUri: "https://backoffice.volepapillondamour.fr/login",
      apiScope: "api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user"
    }
  };
