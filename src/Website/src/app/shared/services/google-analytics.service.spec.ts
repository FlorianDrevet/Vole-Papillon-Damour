import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { environment } from '../../../environments/environment';
import { GoogleAnalyticsService } from './google-analytics.service';

type GoogleAnalyticsTestWindow = Window & {
  dataLayer?: unknown[];
  gtag?: (...args: unknown[]) => void;
  'ga-disable-G-TEST123'?: boolean;
};

describe('GoogleAnalyticsService', () => {
  const originalMeasurementId = environment.google_analytics_measurement_id;

  afterEach(() => {
    environment.google_analytics_measurement_id = originalMeasurementId;
    document.getElementById('google-analytics-script')?.remove();

    const testWindow = window as GoogleAnalyticsTestWindow;
    delete testWindow.dataLayer;
    delete testWindow.gtag;
    delete testWindow['ga-disable-G-TEST123'];
  });

  it('enable_loads_the_tag_and_queues_a_privacy_safe_configuration', () => {
    environment.google_analytics_measurement_id = 'G-TEST123';
    const service = createService();

    service.enable();

    const script = document.getElementById('google-analytics-script') as HTMLScriptElement;
    const commands = (window as GoogleAnalyticsTestWindow).dataLayer as IArguments[];

    expect(service.isConfigured).toBeTrue();
    expect(script.src).toBe('https://www.googletagmanager.com/gtag/js?id=G-TEST123');
    expect(Array.isArray(commands[0])).toBeFalse();
    expect(Array.from(commands[0])).toEqual([
      'consent',
      'default',
      {
        ad_storage: 'denied',
        analytics_storage: 'denied',
        ad_user_data: 'denied',
        ad_personalization: 'denied',
      },
    ]);
    expect(Array.from(commands[1])).toEqual([
      'consent',
      'update',
      {
        ad_storage: 'denied',
        analytics_storage: 'granted',
        ad_user_data: 'denied',
        ad_personalization: 'denied',
      },
    ]);
    expect(commands[2][0]).toBe('js');
    expect(Array.from(commands[3])).toEqual([
      'config',
      'G-TEST123',
      {
        send_page_view: false,
        allow_google_signals: false,
        allow_ad_personalization_signals: false,
      },
    ]);
  });

  it('disable_stops_future_collection_and_allows_reenable', () => {
    environment.google_analytics_measurement_id = 'G-TEST123';
    const service = createService();

    service.enable();
    service.disable();

    expect((window as GoogleAnalyticsTestWindow)['ga-disable-G-TEST123']).toBeTrue();

    service.enable();

    expect((window as GoogleAnalyticsTestWindow)['ga-disable-G-TEST123']).toBeFalse();
  });

  it('enable_does_not_load_a_tag_when_the_measurement_id_is_not_configured', () => {
    environment.google_analytics_measurement_id = '__GOOGLE_ANALYTICS_MEASUREMENT_ID__';
    const service = createService();

    service.enable();

    expect(service.isConfigured).toBeFalse();
    expect(document.getElementById('google-analytics-script')).toBeNull();
  });

  it('navigation_after_enable_sends_one_page_view_for_the_new_url', async () => {
    environment.google_analytics_measurement_id = 'G-TEST123';
    const service = createService(['accueil']);
    const router = TestBed.inject(Router);

    service.enable();
    await router.navigateByUrl('/accueil');

    const commands = (window as GoogleAnalyticsTestWindow).dataLayer as IArguments[];
    const pageView = commands
      .map((command) => Array.from(command))
      .find((command) => command[0] === 'event');

    expect(pageView).toEqual([
      'event',
      'page_view',
      jasmine.objectContaining({ page_path: '/accueil' }),
    ]);
  });

  function createService(routes: string[] = []): GoogleAnalyticsService {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes.map((path) => ({ path, component: EmptyRouteComponent })))]
    });

    return TestBed.inject(GoogleAnalyticsService);
  }
});

@Component({ template: '' })
class EmptyRouteComponent {}
