import {Injectable} from '@angular/core';
import type {ApplicationInsights} from '@microsoft/applicationinsights-web';

import {getApplicationInsightsClient} from '../application-insights';

export interface ScanTelemetryProperties {
  [key: string]: string;
}

@Injectable({providedIn: 'root'})
export class ScanTelemetryService {
  private client: ApplicationInsights | null = null;

  setClient(client: ApplicationInsights | null): void {
    this.client = client;
  }

  trackEvent(name: string, properties: ScanTelemetryProperties): void {
    (this.client ?? getApplicationInsightsClient())?.trackEvent({name}, properties);
  }
}
