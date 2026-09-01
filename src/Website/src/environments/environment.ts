import {EnvironmentInterface} from "../app/shared/interfaces/environment.interface";

export const environment : EnvironmentInterface =
  {
    production: true,
    api_url: "https://vole-papillon-damour-backend.onrender.com",
    appinsights_connection_string: "__APPINSIGHTS_CONNECTION_STRING__",
    google_analytics_measurement_id: "__GOOGLE_ANALYTICS_MEASUREMENT_ID__"
  };
