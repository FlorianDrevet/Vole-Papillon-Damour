import { ApplicationInsights } from '@microsoft/applicationinsights-web';

import { environment } from '../../../environments/environment';

let applicationInsightsClient: ApplicationInsights | null = null;

export function getApplicationInsightsClient(): ApplicationInsights | null {
  return applicationInsightsClient;
}

/**
 * Starts the browser telemetry. The connection string is baked into the bundle
 * at image build time, so it is empty when running locally and on any build
 * that was not produced by the deployment pipeline: in that case nothing is
 * initialised rather than failing on an unusable connection string.
 */
export function initApplicationInsights(): ApplicationInsights | null {
  const connectionString = environment.appinsights_connection_string;

  if (!connectionString || connectionString.startsWith('__')) {
    return null;
  }

  const apiHost = hostOf(environment.api_url);
  const applicationInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
      // W3C trace headers on API calls link a slow page to its API request.
      // Restricted to the API host, whose CORS policy accepts them.
      enableCorsCorrelation: apiHost !== null,
      correlationHeaderDomains: apiHost === null ? undefined : [apiHost],
    },
  });

  applicationInsights.loadAppInsights();
  applicationInsights.trackPageView();
  applicationInsightsClient = applicationInsights;

  return applicationInsights;
}

function hostOf(url: string): string | null {
  try {
    return new URL(url).host;
  } catch {
    return null;
  }
}
