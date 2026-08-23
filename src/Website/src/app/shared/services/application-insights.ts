import { ApplicationInsights } from '@microsoft/applicationinsights-web';

import { environment } from '../../../environments/environment';

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
