import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {Meta} from '@angular/platform-browser';
import {RouterModule} from '@angular/router';
import type {AccountInfo} from '@azure/msal-browser';
import {of} from 'rxjs';

import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogWatchlistResponse} from '../../core/catalog.models';
import {CatalogAccountPageComponent} from './catalog-account-page.component';

describe('CatalogAccountPageComponent', () => {
  let fixture: ComponentFixture<CatalogAccountPageComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    isAdministrator: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    logout: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
  };
  let api: jasmine.SpyObj<CatalogMemberApiService>;

  const account = (name: string): AccountInfo => ({
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: `${name.toLowerCase()}@example.test`,
    localAccountId: 'local-account-id',
    name,
  });

  const watchlist: CatalogWatchlistResponse = {
    generatedAt: '2026-09-04T20:00:00Z',
    alertStatus: 'Active',
    bounceCount: 0,
    items: [{
      id: 'item-1',
      scope: 'Edition',
      workId: null,
      isbn13: '9782070363735',
      book: {
        isbn13: '9782070363735',
        title: 'Le livre suivi',
        authors: 'Une autrice',
        publisher: 'Un éditeur',
        publicationYear: 2020,
        physicalFormat: 'Poche',
        language: 'fr',
        genre: 'Roman',
        workId: null,
        coverUrl: null,
        quantityAvailable: 0,
        quantityAnnounced: 1,
        nextFairAt: null,
        lastAvailableAt: null,
        firstSeenAt: '2026-09-01T10:00:00Z',
        updatedAt: '2026-09-04T10:00:00Z',
        isRare: false,
      },
      addedAt: '2026-09-04T19:00:00Z',
      lastAlertAt: null,
    }],
  };

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      isAdministrator: signal(false),
      error: signal<string | null>(null),
      initialize: jasmine.createSpy('initialize'),
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout'),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    auth.initialize.and.resolveTo();
    auth.login.and.resolveTo();
    auth.logout.and.resolveTo();
    auth.getApiAccessToken.and.resolveTo('member-token');

    api = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['getWatchlist', 'addWatchlistItem', 'removeWatchlistItem', 'setAlertStatus', 'deleteAccount'],
    );
    api.getWatchlist.and.returnValue(of(watchlist));
    api.removeWatchlistItem.and.returnValue(of(void 0));
    api.setAlertStatus.and.returnValue(of({alertStatus: 'Suspended', bounceCount: 0, changed: true}));
    api.deleteAccount.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      declarations: [CatalogAccountPageComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogAccountPageComponent);
  });

  it('offers a non-blocking login entry when signed out', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Mon compte');
    expect(fixture.nativeElement.textContent).toContain('Se connecter avec Microsoft');
    expect(fixture.nativeElement.querySelector('[data-testid="member-login"]')).not.toBeNull();
    expect(api.getWatchlist).not.toHaveBeenCalled();
  });

  it('marks the account route as not indexable', () => {
    const meta = TestBed.inject(Meta);
    spyOn(meta, 'updateTag').and.callThrough();

    fixture.detectChanges();

    expect(meta.updateTag).toHaveBeenCalledWith({name: 'robots', content: 'noindex, nofollow'});
  });

  it('loads the private watchlist and removes only the selected item', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(api.getWatchlist).toHaveBeenCalledWith('member-token');
    expect(fixture.nativeElement.textContent).toContain('Le livre suivi');
    expect(fixture.nativeElement.textContent).toContain('Ma liste de recherche.');

    await fixture.componentInstance.removeItem(watchlist.items[0]);
    fixture.detectChanges();

    expect(api.removeWatchlistItem).toHaveBeenCalledWith('member-token', 'item-1');
    expect(fixture.nativeElement.textContent).toContain('Aucun titre dans votre liste de recherche');
  });

  it('exposes the administration workspace to an administrator', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    auth.isAdministrator.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const administrationLink = fixture.nativeElement.querySelector(
      '[data-testid="administration-entry"]',
    ) as HTMLAnchorElement | null;

    expect(administrationLink).not.toBeNull();
    expect(administrationLink?.getAttribute('href')).toBe('/administration');
    expect(fixture.nativeElement.textContent).toContain('Espace administration');
  });

  it('does not replace an interactive token redirect with a generic error', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.rejectWith(new CatalogAuthenticationRedirectStartedError());
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Redirection vers Microsoft');
    expect(fixture.nativeElement.textContent).not.toContain('Une erreur est survenue');
  });

  it('requires a second explicit action before deleting the account', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const deleteButton = fixture.nativeElement.querySelector('[data-testid="delete-account"]') as HTMLButtonElement;
    deleteButton.click();
    fixture.detectChanges();
    expect(api.deleteAccount).not.toHaveBeenCalled();

    const confirmButton = fixture.nativeElement.querySelector('[data-testid="confirm-delete-account"]') as HTMLButtonElement;
    confirmButton.click();
    await fixture.whenStable();

    expect(api.deleteAccount).toHaveBeenCalledWith('member-token');
    expect(auth.logout).toHaveBeenCalled();
  });

  it('can suspend and reactivate member alerts without changing the watchlist', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    await fixture.componentInstance.setAlertsEnabled(false);
    fixture.detectChanges();

    expect(api.setAlertStatus).toHaveBeenCalledWith('member-token', false);
    expect(fixture.componentInstance.watchlist()?.alertStatus).toBe('Suspended');
    expect(fixture.nativeElement.textContent).toContain('Alertes suspendues');
  });
});
