import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../catalog-auth.service';
import {CatalogApiService} from '../../catalog-api.service';
import {CatalogNavigationComponent} from './catalog-navigation.component';

describe('CatalogNavigationComponent', () => {
  let fixture: ComponentFixture<CatalogNavigationComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    isAuthenticated: WritableSignal<boolean>;
    isAdministrator: WritableSignal<boolean>;
  };
  let api: jasmine.SpyObj<CatalogApiService>;

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      isAuthenticated: signal(false),
      isAdministrator: signal(false),
    };
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search']);
    api.search.and.returnValue(of({
      generatedAt: '',
      books: [],
      totalCount: 0,
      page: 1,
      pageSize: 1,
      genres: ['Essais'],
    }));

    await TestBed.configureTestingModule({
      declarations: [CatalogNavigationComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogNavigationComponent);
    fixture.detectChanges();
    await fixture.whenStable();
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

  it('labels the event tab with the upcoming dates wording', () => {
    const eventLink = (Array.from(fixture.nativeElement.querySelectorAll('a.nav-link')) as HTMLAnchorElement[])
      .find(link => link.textContent?.includes('Les prochaines dates'));

    expect(eventLink?.textContent).toContain('Les prochaines dates');
  });

  it('uses only genres returned by the public API in both navigation variants', () => {
    const desktopGenreLinks = Array.from(
      fixture.nativeElement.querySelectorAll('.dropdown-panel li a'),
    ) as HTMLAnchorElement[];
    const desktopLabels = desktopGenreLinks.map(link => link.querySelector('span')?.textContent?.trim());

    const menuButton = fixture.nativeElement.querySelector('.menu-toggle') as HTMLButtonElement;
    menuButton.click();
    fixture.detectChanges();
    const mobileGenreLinks = Array.from(
      fixture.nativeElement.querySelectorAll('.mobile-nav-item ul a'),
    ) as HTMLAnchorElement[];

    expect(api.search).toHaveBeenCalledWith({pageSize: 1});
    expect(desktopLabels).toEqual([
      'Essais',
      'Voir tous les genres',
    ]);
    expect(mobileGenreLinks.map(link => link.textContent?.trim())).toEqual([
      'Essais',
      'Voir tous les genres',
    ]);
    expect(fixture.nativeElement.textContent).not.toContain('Jeunesse');
  });

  it('does not expose the personal Facebook profile as an association link', () => {
    const menuButton = fixture.nativeElement.querySelector('.menu-toggle') as HTMLButtonElement;
    menuButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a[href="https://www.facebook.com/melvin.drevet.1"]'))
      .toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="https://www.instagram.com/vole_papillon_damour"]'))
      .not.toBeNull();
  });

  it('offers a distinct home tab and routes dates to the dedicated page', () => {
    const links = Array.from(fixture.nativeElement.querySelectorAll('a.nav-link')) as HTMLAnchorElement[];
    const homeLink = links.find(link => link.textContent?.trim() === 'Accueil');
    const eventLink = links.find(link => link.textContent?.includes('Les prochaines dates'));

    expect(homeLink?.getAttribute('href')).toBe('/');
    expect(eventLink?.getAttribute('href')).toBe('/prochaines-dates');
  });

  it('opens the account menu from the trigger without changing the current page', () => {
    const accountLink = fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement;

    accountLink.click();
    fixture.detectChanges();

    expect(accountLink.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.account-popover')).not.toBeNull();
  });

  it('shows the administration workspace with a clear connected label', () => {
    auth.account.set({
      homeAccountId: 'home-account-id',
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'tenant-id',
      username: 'administrator@example.test',
      localAccountId: 'local-account-id',
      name: 'Florian DREVET',
    });
    auth.isAuthenticated.set(true);
    auth.isAdministrator.set(true);

    fixture.componentInstance.url.set('/administration');
    fixture.detectChanges();
    const accountLink = fixture.nativeElement.querySelector('.account-teaser') as HTMLAnchorElement;
    const accountIcon = fixture.nativeElement.querySelector('.person-icon') as HTMLElement;
    accountLink.click();
    fixture.detectChanges();

    expect(accountIcon.textContent?.trim()).toBe('FD');
    expect(accountLink.textContent).toContain('Administration');
    expect(accountLink.textContent).not.toContain('FDFD');
    expect(fixture.nativeElement.textContent).toContain("Ouvrir l'administration");
  });

  it('keeps the account action outside the navigation flex group', () => {
    const headerInner = fixture.nativeElement.querySelector('.header-inner') as HTMLElement;
    const navigation = fixture.nativeElement.querySelector('.main-navigation') as HTMLElement;
    const accountMenu = fixture.nativeElement.querySelector('.account-menu') as HTMLElement;

    expect(navigation.contains(accountMenu)).toBeFalse();
    expect(accountMenu.parentElement).toBe(headerInner);
  });

  it('gives desktop navigation links the same control height as the account action', () => {
    const navigationLink = fixture.nativeElement.querySelector('.nav-link') as HTMLElement;

    expect(getComputedStyle(navigationLink).minHeight).toBe('42px');
  });
});
