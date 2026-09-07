import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {CatalogFooterComponent} from './catalog-footer.component';

describe('CatalogFooterComponent', () => {
  let fixture: ComponentFixture<CatalogFooterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CatalogFooterComponent],
      imports: [RouterModule.forRoot([])],
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
    ]);
  });

  it('keeps catalogue legal links local to the SSR application', () => {
    const links = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];

    expect(links.some(link => link.getAttribute('href') === '/mentions-legales')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/confidentialite')).toBeTrue();
  });
});
