import {PLATFORM_ID} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {HalloweenPreferences} from './halloween-preferences';

const CALM_STORAGE_KEY = 'vpd.tableau.calm';

function mockMatchMedia(reducedMotion = false): void {
  spyOn(window, 'matchMedia').and.callFake((media: string) => ({
    matches: media.includes('prefers-reduced-motion') && reducedMotion
      || media.includes('max-width: 767.98px'),
    media,
    onchange: null,
    addEventListener: jasmine.createSpy('addEventListener'),
    removeEventListener: jasmine.createSpy('removeEventListener'),
    addListener: jasmine.createSpy('addListener'),
    removeListener: jasmine.createSpy('removeListener'),
    dispatchEvent: jasmine.createSpy('dispatchEvent'),
  }) as unknown as MediaQueryList);
}

function configureBrowserPreferences(): HalloweenPreferences {
  TestBed.configureTestingModule({
    providers: [HalloweenPreferences, {provide: PLATFORM_ID, useValue: 'browser'}],
  });
  return TestBed.inject(HalloweenPreferences);
}

describe('HalloweenPreferences', () => {
  beforeEach(() => {
    localStorage.removeItem(CALM_STORAGE_KEY);
  });

  it('tracks phone width, reduced motion, visibility, and the return time', () => {
    mockMatchMedia(true);
    spyOn(Date, 'now').and.returnValue(12345);
    const preferences = configureBrowserPreferences();

    expect(preferences.phone()).toBeTrue();
    expect(preferences.reducedMotion()).toBeTrue();
    expect(preferences.calm()).toBeTrue();
    const isVisible = document.visibilityState !== 'hidden';
    expect(preferences.visible()).toBe(isVisible);
    expect(preferences.visibleSince()).toBe(isVisible ? 12345 : 0);
  });

  it('persists the calm choice on the device', () => {
    mockMatchMedia();
    const preferences = configureBrowserPreferences();

    expect(preferences.calm()).toBeFalse();
    preferences.toggleCalm();

    expect(preferences.calm()).toBeTrue();
    expect(localStorage.getItem(CALM_STORAGE_KEY)).toBe('true');

    TestBed.resetTestingModule();
    const restoredPreferences = configureBrowserPreferences();
    expect(restoredPreferences.calm()).toBeTrue();
  });

  it('continues working if local storage cannot be read or written', () => {
    mockMatchMedia();
    spyOn(localStorage, 'getItem').and.throwError('Storage unavailable');
    spyOn(localStorage, 'setItem').and.throwError('Storage unavailable');
    const preferences = configureBrowserPreferences();

    expect(() => preferences.toggleCalm()).not.toThrow();
    expect(preferences.calm()).toBeTrue();
  });

  it('does not access browser APIs on the server', () => {
    spyOn(window, 'matchMedia');
    TestBed.configureTestingModule({
      providers: [HalloweenPreferences, {provide: PLATFORM_ID, useValue: 'server'}],
    });

    const preferences = TestBed.inject(HalloweenPreferences);

    expect(preferences.phone()).toBeFalse();
    expect(preferences.visible()).toBeFalse();
    expect(window.matchMedia).not.toHaveBeenCalled();
  });
});
