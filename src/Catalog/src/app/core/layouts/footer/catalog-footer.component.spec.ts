import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {CookieConsentService} from '../../../shared/services/cookie-consent.service';
import {CatalogFooterComponent} from './catalog-footer.component';

describe('CatalogFooterComponent', () => {
  let fixture: ComponentFixture<CatalogFooterComponent>;
  let consent: jasmine.SpyObj<CookieConsentService>;

  beforeEach(async () => {
    consent = jasmine.createSpyObj<CookieConsentService>('CookieConsentService', ['reopen']);

    await TestBed.configureTestingModule({
      declarations: [CatalogFooterComponent],
      imports: [RouterModule.forRoot([])],
      providers: [{provide: CookieConsentService, useValue: consent}],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogFooterComponent);
    fixture.detectChanges();
  });

  it('offers catalogue navigation instead of Website content sections', () => {
    const catalogueLinks = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="catalogue-links"] a'),
    ) as HTMLAnchorElement[];

    expect(catalogueLinks.map(link => ({
      text: link.textContent?.trim(),
      href: link.getAttribute('href'),
    }))).toEqual([
      {text: 'Rechercher un livre', href: '/recherche'},
      {text: 'Catalogue par genre', href: '/catalogue'},
      {text: 'Les prochaines dates', href: '/prochaines-dates'},
      {text: 'Ma liste de recherche', href: '/compte'},
    ]);

    expect(fixture.nativeElement.textContent).not.toContain('Maxence');
    expect(fixture.nativeElement.textContent).not.toContain('Galerie photos');
    expect(fixture.nativeElement.textContent).not.toContain('Contact');
  });

  it('limits association links to the catalogue handoff and local legal pages', () => {
    const associationLinks = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="association-links"] a'),
    ) as HTMLAnchorElement[];

    expect(associationLinks.map(link => ({
      text: link.textContent?.trim(),
      href: link.getAttribute('href'),
    }))).toEqual([
      {text: "Le site de l'association", href: 'https://volepapillondamour.fr'},
      {text: 'Mentions légales', href: '/mentions-legales'},
      {text: 'Confidentialité', href: '/confidentialite'},
      {text: 'Vos données & RGPD', href: '/donnees-personnelles'},
      {text: 'Politique de cookies', href: '/politique-de-cookies'},
      {text: 'Accessibilité', href: '/accessibilite'},
      {text: 'Gérer les cookies', href: '/politique-de-cookies'},
    ]);
  });

  it('keeps catalogue legal links local to the SSR application', () => {
    const links = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];

    expect(links.some(link => link.getAttribute('href') === '/mentions-legales')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/confidentialite')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/donnees-personnelles')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/politique-de-cookies')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/accessibilite')).toBeTrue();
  });

  it('offers a keyboard-accessible action to reopen cookie preferences', () => {
    const manageCookies = fixture.nativeElement.querySelector('[data-testid="manage-cookies"]') as HTMLAnchorElement | null;

    expect(manageCookies?.getAttribute('href')).toBe('/politique-de-cookies');
    expect(manageCookies?.textContent?.trim()).toBe('Gérer les cookies');

    manageCookies?.click();

    expect(consent.reopen).toHaveBeenCalled();
  });

  it('renders the current copyright year', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2031, 0, 1));

    try {
      const freshFixture = TestBed.createComponent(CatalogFooterComponent);
      freshFixture.detectChanges();

      expect(freshFixture.nativeElement.querySelector('.footer-copyright')?.textContent)
        .toContain('© 2031');
      freshFixture.destroy();
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
