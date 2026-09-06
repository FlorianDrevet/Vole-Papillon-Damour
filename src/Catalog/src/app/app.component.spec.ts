import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal} from '@angular/core';
import {Meta} from '@angular/platform-browser';
import {RouterModule} from '@angular/router';

import {AppComponent} from './app.component';
import {CatalogAuthService} from './core/catalog-auth.service';
import {CatalogNavigationComponent} from './core/layouts/navigation/catalog-navigation.component';
import {CatalogFooterComponent} from './core/layouts/footer/catalog-footer.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AppComponent, CatalogNavigationComponent, CatalogFooterComponent],
      imports: [RouterModule.forRoot([])],
      providers: [{
        provide: CatalogAuthService,
        useValue: {
          account: signal(null),
          isAuthenticated: signal(false),
          isAdministrator: signal(false),
        },
      }],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('links the catalogue shell to the member account entry point', () => {
    const accountLink = fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement | null;

    expect(accountLink).not.toBeNull();
    expect(accountLink?.textContent).toContain('Mon compte');
    expect(accountLink?.getAttribute('href')).toBe('/compte');
  });

  it('keeps the mobile menu button label synchronized with its state', () => {
    const menuButton = fixture.nativeElement.querySelector('.menu-toggle') as HTMLButtonElement;

    expect(menuButton.getAttribute('aria-label')).toBe('Ouvrir le menu');

    menuButton.click();
    fixture.detectChanges();

    expect(menuButton.getAttribute('aria-expanded')).toBe('true');
    expect(menuButton.getAttribute('aria-label')).toBe('Fermer le menu');
  });

  it('marks the public catalog as indexable on shell bootstrap', () => {
    const meta = TestBed.inject(Meta);
    spyOn(meta, 'updateTag').and.callThrough();
    const freshFixture = TestBed.createComponent(AppComponent);

    freshFixture.detectChanges();

    expect(meta.updateTag).toHaveBeenCalledWith({name: 'robots', content: 'index, follow'});
    freshFixture.destroy();
  });
});
