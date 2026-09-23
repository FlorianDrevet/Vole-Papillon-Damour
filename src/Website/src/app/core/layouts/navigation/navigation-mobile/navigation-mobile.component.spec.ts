import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { SiteNavItem } from '../nav-items';

import { NavigationMobileComponent } from './navigation-mobile.component';

describe('NavigationMobileComponent', () => {
  let fixture: ComponentFixture<NavigationMobileComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [NavigationMobileComponent],
      imports: [RouterLink],
      providers: [provideRouter([])],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(NavigationMobileComponent);
    fixture.componentRef.setInput('navItems', [{ url: '/accueil', label: 'Accueil' } satisfies SiteNavItem]);
  });

  it('traps keyboard focus, closes on Escape, and returns focus to the menu toggle', async () => {
    fixture.detectChanges();
    const toggle = fixture.nativeElement.querySelector('[data-mobile-menu-toggle]') as HTMLButtonElement;
    toggle.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('[data-mobile-menu-panel]') as HTMLElement;
    const closeButton = panel.querySelector('[data-menu-close]') as HTMLButtonElement;
    const focusable = Array.from(panel.querySelectorAll<HTMLElement>('a[href], button:not(:disabled)'));
    const lastLink = focusable[focusable.length - 1];

    expect(document.activeElement).toBe(closeButton);

    lastLink.focus();
    lastLink.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
    expect(document.activeElement).toBe(closeButton);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-mobile-menu-panel]')).toBeNull();
    expect(document.activeElement).toBe(toggle);
  });

  it('returns focus to the menu toggle after a normal close', async () => {
    fixture.detectChanges();
    const toggle = fixture.nativeElement.querySelector('[data-mobile-menu-toggle]') as HTMLButtonElement;
    toggle.click();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.close();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(document.activeElement).toBe(toggle);
  });
});
