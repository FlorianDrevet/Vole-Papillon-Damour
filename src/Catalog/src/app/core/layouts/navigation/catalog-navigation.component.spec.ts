import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';

import {CatalogAuthService} from '../../catalog-auth.service';
import {CatalogNavigationComponent} from './catalog-navigation.component';

describe('CatalogNavigationComponent', () => {
  let fixture: ComponentFixture<CatalogNavigationComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    isAuthenticated: WritableSignal<boolean>;
    isAdministrator: WritableSignal<boolean>;
  };

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      isAuthenticated: signal(false),
      isAdministrator: signal(false),
    };

    await TestBed.configureTestingModule({
      declarations: [CatalogNavigationComponent],
      imports: [RouterModule.forRoot([])],
      providers: [{provide: CatalogAuthService, useValue: auth}],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogNavigationComponent);
    fixture.detectChanges();
  });

  it('uses the association mark and keeps the catalogue account entry secondary', () => {
    const logo = fixture.nativeElement.querySelector('.brand-logo') as HTMLImageElement | null;
    const accountLink = fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement | null;

    expect(logo?.getAttribute('src')).toBe('images/papillon_without_back.png');
    expect(accountLink?.getAttribute('href')).toBe('/compte');
    expect(accountLink?.textContent).toContain('Mon compte');
    expect(fixture.nativeElement.textContent).toContain('Le site de l’association');
  });

  it('opens the account menu from the trigger without changing the current page', () => {
    const accountLink = fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement;

    accountLink.click();
    fixture.detectChanges();

    expect(accountLink.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.account-popover')).not.toBeNull();
  });

  it('shows the administration workspace to an authenticated administrator', () => {
    auth.account.set({
      homeAccountId: 'home-account-id',
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'tenant-id',
      username: 'administrator@example.test',
      localAccountId: 'local-account-id',
      name: 'Administrator',
    });
    auth.isAuthenticated.set(true);
    auth.isAdministrator.set(true);

    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain("Ouvrir l'administration");
  });
});
