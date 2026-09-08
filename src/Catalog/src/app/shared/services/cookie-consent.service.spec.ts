import {TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';

import {environment} from '../../../environments/environment';
import {CookieConsentService} from './cookie-consent.service';
import {GoogleAnalyticsService} from './google-analytics.service';

type ClarityFunction = ((...args: unknown[]) => void) & {q?: unknown[][]};
type ClarityTestWindow = Window & {clarity?: ClarityFunction};

describe('CookieConsentService', () => {
  let googleAnalytics: jasmine.SpyObj<GoogleAnalyticsService>;
  let originalClarityProjectId: string;
  let appendScript: jasmine.Spy;

  beforeEach(() => {
    localStorage.removeItem('vpd-catalog-cookie-consent');
    document.querySelectorAll('script[data-catalog-clarity="true"]').forEach(script => script.remove());
    delete (window as ClarityTestWindow).clarity;
    appendScript = spyOn(document.head, 'appendChild').and.callFake(node => node);
    originalClarityProjectId = environment.clarityProjectId;
    environment.clarityProjectId = 'yerabb7gnt';
    googleAnalytics = jasmine.createSpyObj<GoogleAnalyticsService>('GoogleAnalyticsService', [
      'enable',
      'disable',
    ]);

    TestBed.configureTestingModule({
      providers: [
        CookieConsentService,
        {provide: GoogleAnalyticsService, useValue: googleAnalytics},
        provideRouter([]),
      ],
    });
  });

  afterEach(() => {
    environment.clarityProjectId = originalClarityProjectId;
    localStorage.removeItem('vpd-catalog-cookie-consent');
    document.querySelectorAll('script[data-catalog-clarity="true"]').forEach(script => script.remove());
    delete (window as ClarityTestWindow).clarity;
  });

  it('does_not_load_third_party_scripts_before_explicit_consent', () => {
    TestBed.inject(CookieConsentService);

    expect(document.querySelector('script[data-catalog-clarity="true"]')).toBeNull();
    expect(document.getElementById('catalog-google-analytics-script')).toBeNull();
    expect(googleAnalytics.enable).not.toHaveBeenCalled();
  });

  it('acceptAll_loads_clarity_and_enables_google_analytics', () => {
    const service = TestBed.inject(CookieConsentService);

    service.acceptAll();

    const clarityScript = appendScript.calls.mostRecent().args[0] as HTMLScriptElement;
    const clarity = (window as ClarityTestWindow).clarity;

    expect(googleAnalytics.enable).toHaveBeenCalled();
    expect(service.preferences.maps).toBeTrue();
    expect(clarityScript?.src).toContain('/tag/yerabb7gnt');
    expect(clarity?.q).toContain([
      'consentv2',
      {ad_Storage: 'denied', analytics_Storage: 'granted'},
    ]);
  });

  it('rejectAll_disables_both_audience_tools_and_persists_the_choice', () => {
    const service = TestBed.inject(CookieConsentService);

    service.rejectAll();

    expect(googleAnalytics.disable).toHaveBeenCalled();
    expect(service.preferences.analytics).toBeFalse();
    expect(JSON.parse(localStorage.getItem('vpd-catalog-cookie-consent') ?? '{}')).toEqual(jasmine.objectContaining({
      choice: 'rejected',
      preferences: {analytics: false, maps: false},
    }));
  });

  it('keeps an existing consent choice while defaulting the new Maps preference to false', () => {
    localStorage.setItem('vpd-catalog-cookie-consent', JSON.stringify({
      choice: 'rejected',
      preferences: {analytics: false},
      date: '2026-09-01T08:00:00.000Z',
    }));

    const service = TestBed.inject(CookieConsentService);

    expect(service.bannerVisible$.value).toBeFalse();
    expect(service.preferences).toEqual({analytics: false, maps: false});
  });

  it('keeps Google Maps disabled until its consent is explicitly enabled', () => {
    const service = TestBed.inject(CookieConsentService);

    expect(service.preferences.maps).toBeFalse();

    service.enableMaps();

    expect(service.preferences.maps).toBeTrue();
    expect(JSON.parse(localStorage.getItem('vpd-catalog-cookie-consent') ?? '{}')).toEqual(
      jasmine.objectContaining({preferences: jasmine.objectContaining({maps: true})}),
    );
  });

  it('uses_the_current_clarity_consentv2_signal_when_consent_is_withdrawn', () => {
    const service = TestBed.inject(CookieConsentService);

    service.acceptAll();
    service.rejectAll();

    const clarityQueue = (window as ClarityTestWindow).clarity?.q ?? [];

    expect(clarityQueue).toContain([
      'consentv2',
      {ad_Storage: 'denied', analytics_Storage: 'denied'},
    ]);
    expect(clarityQueue).not.toContain(['consent', false]);
  });
});
