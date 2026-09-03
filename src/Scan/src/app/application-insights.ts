import type {ApplicationInsights} from '@microsoft/applicationinsights-web';

import {environment} from '../environments/environment';

/**
 * Starts browser telemetry only for an image built by the deployment
 * pipeline. Local and placeholder builds keep telemetry disabled.
 */
export async function initApplicationInsights(): Promise<ApplicationInsights | null> {
  const connectionString = environment.appInsightsConnectionString;

  if (!connectionString || connectionString.startsWith('__')) {
    return null;
  }

  const {ApplicationInsights} = await import('@microsoft/applicationinsights-web');
  const applicationInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
    },
  });

  applicationInsights.loadAppInsights();
  applicationInsights.trackPageView();

  return applicationInsights;
}
