import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { CookieConsentService } from './cookie-consent.service';
import { GoogleAnalyticsService } from './google-analytics.service';

type ClarityTestWindow = Window & {
  clarity?: jasmine.Spy;
};

describe('CookieConsentService', () => {
  let googleAnalytics: jasmine.SpyObj<GoogleAnalyticsService>;
  let clarity: jasmine.Spy;

  beforeEach(() => {
    localStorage.removeItem('vpd-cookie-consent');
    googleAnalytics = jasmine.createSpyObj<GoogleAnalyticsService>('GoogleAnalyticsService', [
      'enable',
      'disable',
    ]);
    clarity = jasmine.createSpy('clarity');
    (window as ClarityTestWindow).clarity = clarity;

    TestBed.configureTestingModule({
      providers: [
        CookieConsentService,
        { provide: GoogleAnalyticsService, useValue: googleAnalytics },
        provideRouter([]),
      ],
    });
  });

  afterEach(() => {
    localStorage.removeItem('vpd-cookie-consent');
    delete (window as ClarityTestWindow).clarity;
  });

  it('acceptAll_enables_analytics_and_grants_only_analytics_storage_to_clarity', () => {
    const service = TestBed.inject(CookieConsentService);

    service.acceptAll();

    expect(googleAnalytics.enable).toHaveBeenCalled();
    expect(clarity).toHaveBeenCalledWith('consentv2', {
      ad_Storage: 'denied',
      analytics_Storage: 'granted',
    });
  });

  it('rejectAll_disables_analytics_and_revokes_clarity_consent', () => {
    const service = TestBed.inject(CookieConsentService);

    service.rejectAll();

    expect(googleAnalytics.disable).toHaveBeenCalled();
    expect(clarity).toHaveBeenCalledWith('consentv2', {
      ad_Storage: 'denied',
      analytics_Storage: 'denied',
    });
    expect(clarity).toHaveBeenCalledWith('consent', false);
  });
});
