import {DOCUMENT, isPlatformBrowser} from '@angular/common';
import {Injectable, PLATFORM_ID, inject} from '@angular/core';
import {BehaviorSubject} from 'rxjs';

import {environment} from '../../../environments/environment';
import {GoogleAnalyticsService} from './google-analytics.service';

const STORAGE_KEY = 'vpd-catalog-cookie-consent';
const CLARITY_CONSENT_VERSION = {
  ad_Storage: 'denied',
  analytics_Storage: 'granted',
} as const;

export interface CookiePreferences {
  analytics: boolean;
  maps: boolean;
}

interface StoredConsent {
  choice: 'accepted' | 'rejected' | 'customized';
  preferences: CookiePreferences;
  date: string;
}

type ClarityFunction = ((...args: unknown[]) => void) & {q?: unknown[][]};
type CatalogBrowserWindow = Window & {clarity?: ClarityFunction};

@Injectable({providedIn: 'root'})
export class CookieConsentService {
  private readonly document = inject(DOCUMENT);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly googleAnalytics = inject(GoogleAnalyticsService);

  readonly bannerVisible$ = new BehaviorSubject<boolean>(false);
  readonly panelOpen$ = new BehaviorSubject<boolean>(false);
  preferences: CookiePreferences = {analytics: false, maps: false};

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    const stored = this.readStored();
    if (!stored) {
      this.bannerVisible$.next(true);
      return;
    }

    this.preferences = stored.preferences;
    if (stored.preferences.analytics) {
      this.enableAnalytics();
    }
  }

  acceptAll(): void {
    this.save('accepted', {analytics: true, maps: true});
  }

  rejectAll(): void {
    this.save('rejected', {analytics: false, maps: false});
  }

  savePreferences(preferences: CookiePreferences): void {
    this.save('customized', preferences);
  }

  enableMaps(): void {
    this.savePreferences({...this.preferences, maps: true});
  }

  openPanel(): void {
    this.panelOpen$.next(true);
  }

  reopen(): void {
    this.panelOpen$.next(false);
    this.bannerVisible$.next(true);
  }

  private readStored(): StoredConsent | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      const parsed: unknown = JSON.parse(raw) as unknown;
      return isStoredConsent(parsed)
        ? {
          ...parsed,
          preferences: {
            analytics: parsed.preferences.analytics,
            maps: parsed.preferences.maps === true,
          },
        }
        : null;
    } catch {
      return null;
    }
  }

  private save(choice: StoredConsent['choice'], preferences: CookiePreferences): void {
    if (!this.isBrowser) {
      return;
    }

    const stored: StoredConsent = {
      choice,
      preferences: {...preferences},
      date: new Date().toISOString(),
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.preferences = stored.preferences;
    this.bannerVisible$.next(false);
    this.panelOpen$.next(false);

    if (preferences.analytics) {
      this.enableAnalytics();
    } else {
      this.disableAnalytics();
    }
  }

  private enableAnalytics(): void {
    this.loadClarity();
    if (this.isBrowser) {
      this.browserWindow.clarity?.('consentv2', CLARITY_CONSENT_VERSION);
    }
    this.googleAnalytics.enable();
  }

  private disableAnalytics(): void {
    this.googleAnalytics.disable();
    if (!this.isBrowser || !this.browserWindow.clarity) {
      return;
    }

    this.browserWindow.clarity('consentv2', {
      ad_Storage: 'denied',
      analytics_Storage: 'denied',
    });
  }

  private loadClarity(): void {
    if (!this.isBrowser || !isConfigured(environment.clarityProjectId) || this.browserWindow.clarity) {
      return;
    }

    const clarity: ClarityFunction = (...args: unknown[]): void => {
      clarity.q ??= [];
      clarity.q.push(args);
    };
    clarity.q = [];
    this.browserWindow.clarity = clarity;

    const script = this.document.createElement('script');
    script.id = 'catalog-clarity-script';
    script.dataset['catalogClarity'] = 'true';
    script.async = true;
    script.src = `https://www.clarity.ms/tag/${encodeURIComponent(environment.clarityProjectId)}`;
    this.document.head.appendChild(script);
  }

  private get browserWindow(): CatalogBrowserWindow {
    return this.document.defaultView as CatalogBrowserWindow;
  }
}

function isConfigured(value: string): boolean {
  return value.length > 0 && !value.startsWith('__');
}

function isStoredConsent(value: unknown): value is StoredConsent {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as {
    choice?: unknown;
    preferences?: {analytics?: unknown; maps?: unknown};
    date?: unknown;
  };

  return (candidate.choice === 'accepted'
      || candidate.choice === 'rejected'
      || candidate.choice === 'customized')
    && typeof candidate.preferences?.analytics === 'boolean'
    && typeof candidate.date === 'string';
}
