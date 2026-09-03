import {EnvironmentInterface} from "../app/shared/interfaces/environment.interface";

export const environment : EnvironmentInterface =
  {
    production: false,
    api_url: "http://localhost:5257",
    url_vpd_web_site: "https://vole-papillon-damour-website.onrender.com/accueil",
    time_numero_modal: 300,
    appinsights_connection_string: "",
    entra: {
      tenantId: "b23c80b3-9776-4840-8255-fcbf3b3500fd",
      clientId: "b5e7446e-2e87-4eed-8a6a-d40b3c913c9c",
      authority: "https://volepapillondamour.ciamlogin.com/b23c80b3-9776-4840-8255-fcbf3b3500fd/",
      redirectUri: "http://localhost:4200",
      postLogoutRedirectUri: "http://localhost:4200/login",
      apiScope: "api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user"
    }
  };
