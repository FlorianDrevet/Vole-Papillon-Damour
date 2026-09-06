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

  it('keeps the four-column association footer used by the Website shell', () => {
    expect(fixture.nativeElement.querySelectorAll('.footer-column').length).toBe(4);
    expect(fixture.nativeElement.textContent).toContain("L'association");
    expect(fixture.nativeElement.textContent).toContain('Maxence');
    expect(fixture.nativeElement.textContent).toContain('Contact');
  });

  it('keeps catalogue legal links local to the SSR application', () => {
    const links = Array.from(fixture.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];

    expect(links.some(link => link.getAttribute('href') === '/mentions-legales')).toBeTrue();
    expect(links.some(link => link.getAttribute('href') === '/confidentialite')).toBeTrue();
  });
});
