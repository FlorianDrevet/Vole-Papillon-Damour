import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

import { GoogleAnalyticsService } from './google-analytics.service';

const STORAGE_KEY = 'vpd-cookie-consent';
const CLARITY_PROJECT_ID = 'nwy66l4uol';

export interface CookiePreferences {
  analytics: boolean;
}

interface StoredConsent {
  choice: 'accepted' | 'rejected' | 'customized';
  preferences: CookiePreferences;
  date: string;
}

declare global {
  interface Window {
    clarity?: (...args: unknown[]) => void;
  }
}

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly googleAnalytics = inject(GoogleAnalyticsService);

  bannerVisible$ = new BehaviorSubject<boolean>(false);
  panelOpen$ = new BehaviorSubject<boolean>(false);
  preferences: CookiePreferences = { analytics: false };

  constructor() {
    if (!this.isBrowser) {
      return;
    }

    const stored = this.readStored();
    if (stored) {
      this.preferences = stored.preferences;
      if (stored.preferences.analytics) {
        this.enableAnalytics();
      }
    } else {
      this.bannerVisible$.next(true);
    }
  }

  acceptAll(): void {
    this.save('accepted', { analytics: true });
  }

  rejectAll(): void {
    this.save('rejected', { analytics: false });
  }

  savePreferences(preferences: CookiePreferences): void {
    this.save('customized', preferences);
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
      return JSON.parse(raw) as StoredConsent;
    } catch {
      return null;
    }
  }

  private save(choice: StoredConsent['choice'], preferences: CookiePreferences): void {
    const stored: StoredConsent = { choice, preferences, date: new Date().toISOString() };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.preferences = preferences;
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
      window.clarity?.('consentv2', {
        ad_Storage: 'denied',
        analytics_Storage: 'granted',
      });
    }
    this.googleAnalytics.enable();
  }

  private disableAnalytics(): void {
    this.googleAnalytics.disable();
    if (!this.isBrowser || !window.clarity) {
      return;
    }

    window.clarity('consentv2', {
      ad_Storage: 'denied',
      analytics_Storage: 'denied',
    });
    // This also clears a Clarity session already created before the visitor
    // changed their choice. The v2 signal above remains the canonical state.
    window.clarity('consent', false);
  }

  private loadClarity(): void {
    if (!this.isBrowser || window.clarity) {
      return;
    }
    (function(c: Window, l: Document, a: 'clarity', r: string, i: string) {
      c[a] = c[a] || function(...args: unknown[]) {
        (((c[a] as any).q = (c[a] as any).q || []) as unknown[][]).push(args);
      };
      const t = l.createElement(r) as HTMLScriptElement;
      t.async = true;
      t.src = 'https://www.clarity.ms/tag/' + i;
      const y = l.getElementsByTagName(r)[0];
      y.parentNode!.insertBefore(t, y);
    })(window, document, 'clarity', 'script', CLARITY_PROJECT_ID);
  }
}
