import { Component, signal } from '@angular/core';

import { CookieConsentService, CookiePreferences } from '../../services/cookie-consent.service';

@Component({
  selector: 'app-cookie-banner',
  templateUrl: './cookie-banner.component.html',
  standalone: false,
})
export class CookieBannerComponent {
  readonly analyticsChecked = signal(false);

  constructor(public readonly consent: CookieConsentService) {}

  openPanel(): void {
    this.analyticsChecked.set(this.consent.preferences.analytics);
    this.consent.openPanel();
  }

  savePreferences(): void {
    const preferences: CookiePreferences = { analytics: this.analyticsChecked() };
    this.consent.savePreferences(preferences);
  }
}
