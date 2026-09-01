import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

import { environment } from '../../../environments/environment';

const GOOGLE_ANALYTICS_SCRIPT_ID = 'google-analytics-script';

type GoogleAnalyticsWindow = Window & {
  dataLayer?: unknown[];
  gtag?: (...args: unknown[]) => void;
};

@Injectable({ providedIn: 'root' })
export class GoogleAnalyticsService {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly measurementId = environment.google_analytics_measurement_id;
  private enabled = false;
  private lastTrackedPath: string | null = null;

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        if (this.enabled) {
          this.trackPageView(event.urlAfterRedirects);
        }
      });
  }

  get isConfigured(): boolean {
    return this.isBrowser
      && this.measurementId.length > 0
      && !this.measurementId.startsWith('__');
  }

  enable(): void {
    if (!this.isConfigured || this.enabled) {
      return;
    }

    this.enabled = true;
    const analyticsWindow = this.browserWindow;
    this.setCollectionDisabled(analyticsWindow, false);
    this.ensureTagIsReady(analyticsWindow);
    analyticsWindow.gtag!('consent', 'update', {
      ad_storage: 'denied',
      analytics_storage: 'granted',
      ad_user_data: 'denied',
      ad_personalization: 'denied',
    });
    analyticsWindow.gtag!('js', new Date());
    analyticsWindow.gtag!('config', this.measurementId, {
      send_page_view: false,
      allow_google_signals: false,
      allow_ad_personalization_signals: false,
    });

    if (this.router.navigated) {
      this.trackPageView(this.currentPath());
    }
  }

  disable(): void {
    if (!this.isConfigured) {
      return;
    }

    this.enabled = false;
    this.lastTrackedPath = null;
    this.setCollectionDisabled(this.browserWindow, true);
    this.browserWindow.gtag?.('consent', 'update', {
      ad_storage: 'denied',
      analytics_storage: 'denied',
      ad_user_data: 'denied',
      ad_personalization: 'denied',
    });
  }

  private ensureTagIsReady(analyticsWindow: GoogleAnalyticsWindow): void {
    analyticsWindow.dataLayer ??= [];
    analyticsWindow.gtag ??= (...args: unknown[]) => {
      analyticsWindow.dataLayer!.push(args);
    };

    if (this.document.getElementById(GOOGLE_ANALYTICS_SCRIPT_ID)) {
      return;
    }

    const script = this.document.createElement('script');
    script.id = GOOGLE_ANALYTICS_SCRIPT_ID;
    script.async = true;
    script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(this.measurementId)}`;
    this.document.head.appendChild(script);
  }

  private trackPageView(path: string): void {
    if (!this.enabled) {
      return;
    }

    const pagePath = path || '/';
    if (pagePath === this.lastTrackedPath) {
      return;
    }

    this.lastTrackedPath = pagePath;
    this.browserWindow.gtag?.('event', 'page_view', {
      page_title: this.document.title,
      page_location: `${this.browserWindow.location.origin}${pagePath}`,
      page_path: pagePath,
    });
  }

  private currentPath(): string {
    return `${this.browserWindow.location.pathname}${this.browserWindow.location.search}`;
  }

  private setCollectionDisabled(analyticsWindow: GoogleAnalyticsWindow, disabled: boolean): void {
    (analyticsWindow as GoogleAnalyticsWindow & Record<string, unknown>)[`ga-disable-${this.measurementId}`] = disabled;
  }

  private get browserWindow(): GoogleAnalyticsWindow {
    return this.document.defaultView as GoogleAnalyticsWindow;
  }
}
