import type {ApplicationInsights} from '@microsoft/applicationinsights-web';

import {environment} from '../environments/environment';

let applicationInsightsClient: ApplicationInsights | null = null;

export function getApplicationInsightsClient(): ApplicationInsights | null {
  return applicationInsightsClient;
}

/**
 * Starts browser telemetry only for an image built by the deployment
 * pipeline. Local and placeholder builds keep telemetry disabled.
 */
export async function initApplicationInsights(): Promise<ApplicationInsights | null> {
  const connectionString = environment.appInsightsConnectionString;

  if (!connectionString || connectionString.startsWith('__')) {
    applicationInsightsClient = null;
    return null;
  }

  const {ApplicationInsights} = await import('@microsoft/applicationinsights-web');
  const applicationInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
      // Keep API dependency timing and W3C trace headers explicit for the
      // cross-origin Scan -> API call. Ajax/Fetch auto-collection is enabled by default;
      // the API CORS policy allows the correlation headers.
      enableCorsCorrelation: true,
    },
  });

  applicationInsights.loadAppInsights();
  applicationInsights.trackPageView();
  applicationInsightsClient = applicationInsights;

  return applicationInsights;
}
