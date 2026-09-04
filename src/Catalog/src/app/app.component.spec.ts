import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {AppComponent} from './app.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      imports: [RouterModule.forRoot([])],
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
});
