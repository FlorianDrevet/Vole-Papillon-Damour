import {ErrorHandler} from '@angular/core';
import type {ApplicationInsights} from '@microsoft/applicationinsights-web';

import {getApplicationInsightsClient} from './application-insights';

/**
 * Angular catches component, template and change-detection errors before they
 * reach window.onerror, so the Application Insights SDK never sees them on its
 * own. This handler keeps the console output and forwards each error.
 */
export class ApplicationInsightsErrorHandler extends ErrorHandler {
  constructor(
    private readonly getClient: () => ApplicationInsights | null = getApplicationInsightsClient,
  ) {
    super();
  }

  override handleError(error: unknown): void {
    super.handleError(error);
    this.getClient()?.trackException({
      exception: error instanceof Error ? error : new Error(String(error)),
    });
  }
}
