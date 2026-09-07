import {ComponentFixture, TestBed} from '@angular/core/testing';
import {CommonModule} from '@angular/common';
import {BehaviorSubject} from 'rxjs';

import {CookieConsentService, CookiePreferences} from '../../services/cookie-consent.service';
import {CatalogCookieBannerComponent} from './catalog-cookie-banner.component';

describe('CatalogCookieBannerComponent', () => {
  let fixture: ComponentFixture<CatalogCookieBannerComponent>;
  let consent: {
    bannerVisible$: BehaviorSubject<boolean>;
    panelOpen$: BehaviorSubject<boolean>;
    preferences: CookiePreferences;
    acceptAll: jasmine.Spy;
    rejectAll: jasmine.Spy;
    openPanel: jasmine.Spy;
    savePreferences: jasmine.Spy;
  };

  beforeEach(async () => {
    consent = {
      bannerVisible$: new BehaviorSubject(true),
      panelOpen$: new BehaviorSubject(false),
      preferences: {analytics: false},
      acceptAll: jasmine.createSpy('acceptAll'),
      rejectAll: jasmine.createSpy('rejectAll'),
      openPanel: jasmine.createSpy('openPanel').and.callFake(() => consent.panelOpen$.next(true)),
      savePreferences: jasmine.createSpy('savePreferences'),
    };

    await TestBed.configureTestingModule({
      declarations: [CatalogCookieBannerComponent],
      imports: [CommonModule],
      providers: [{provide: CookieConsentService, useValue: consent}],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogCookieBannerComponent);
    fixture.detectChanges();
  });

  it('shows explicit accept, reject and customization actions', () => {
    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(content).toContain('Microsoft Clarity');
    expect(content).toContain('Google Analytics 4');
    expect(content).toContain('Tout accepter');
    expect(content).toContain('Tout refuser');
    expect(content).toContain('Personnaliser');
  });

  it('forwards the primary consent actions to the service', () => {
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];

    buttons.find(button => button.textContent?.includes('Tout accepter'))?.click();
    buttons.find(button => button.textContent?.includes('Tout refuser'))?.click();

    expect(consent.acceptAll).toHaveBeenCalled();
    expect(consent.rejectAll).toHaveBeenCalled();
  });

  it('opens a keyboard-accessible panel with a necessary category and analytics toggle', () => {
    const customize = (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[])
      .find(button => button.textContent?.includes('Personnaliser')) as HTMLButtonElement;

    customize.click();
    fixture.detectChanges();

    expect(consent.openPanel).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Cookies nécessaires');
    expect(fixture.nativeElement.querySelector('input[type="checkbox"]')).not.toBeNull();
  });
});
