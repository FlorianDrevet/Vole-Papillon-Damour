import {Component, signal} from '@angular/core';

import {CookieConsentService, CookiePreferences} from '../../services/cookie-consent.service';

@Component({
  selector: '[app-catalog-cookie-banner]',
  standalone: false,
  templateUrl: './catalog-cookie-banner.component.html',
  styleUrls: ['./catalog-cookie-banner.component.scss'],
})
export class CatalogCookieBannerComponent {
  readonly analyticsChecked = signal(false);
  readonly mapsChecked = signal(false);

  constructor(public readonly consent: CookieConsentService) {}

  openPanel(): void {
    this.analyticsChecked.set(this.consent.preferences.analytics);
    this.mapsChecked.set(this.consent.preferences.maps);
    this.consent.openPanel();
  }

  savePreferences(): void {
    const preferences: CookiePreferences = {
      analytics: this.analyticsChecked(),
      maps: this.mapsChecked(),
    };
    this.consent.savePreferences(preferences);
  }

  onAnalyticsChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.analyticsChecked.set(target.checked);
    }
  }

  onMapsChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.mapsChecked.set(target.checked);
    }
  }
}
