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
import {CatalogVolunteerStatisticsResponse, CatalogWatchlistResponse} from '../../core/catalog.models';
import {CatalogAccountPageComponent} from './catalog-account-page.component';

describe('CatalogAccountPageComponent', () => {
  let fixture: ComponentFixture<CatalogAccountPageComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    isAdministrator: WritableSignal<boolean>;
    isVolunteer: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    register: jasmine.Spy;
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

  const volunteerStatistics: CatalogVolunteerStatisticsResponse = {
    generatedAt: '2026-09-11T12:00:00Z',
    memberSince: '2024-03-01T12:00:00Z',
    scan: {
      scannedCount: 12,
      keptCount: 9,
      rejectedCount: 3,
      sessionCount: 2,
      durationMinutes: 80,
      firstSessionAt: '2024-03-01T12:00:00Z',
      medianTeamKeepRatePercent: 74,
      monthly: [{periodStart: '2026-09-01T00:00:00Z', kept: 9, rejected: 3}],
      timeSlots: [{dayOfWeek: 6, slot: 'morning', count: 12}],
      impact: {
        foundReaderCount: 5,
        newTitleCount: 2,
        rareCount: 1,
        alertItemCount: 3,
        foundReaderIsEstimated: true,
      },
      topGenres: [{name: 'Romans', quantity: 6}],
      recentSessions: [{
        id: 'session-1',
        startedAt: '2026-09-06T08:00:00Z',
        durationMinutes: 50,
        scannedCount: 8,
        keptRatePercent: 75,
        mode: 'AvailableNow',
      }],
    },
    cash: {
      grossSoldQuantity: 4,
      soldQuantity: 4,
      saleMovementCount: 4,
      voidedSaleQuantity: 0,
      fairCount: 1,
      estimatedDurationMinutes: 45,
      estimatedCadencePerHour: 5,
      medianTeamCadencePerHour: 6,
      fairBreakdown: [{
        id: 'fair-1',
        name: 'Bourse de juin',
        dateStart: '2026-06-01T00:00:00Z',
        netSoldQuantity: 4,
      }],
      peak: {fairDate: '2026-06-01T00:00:00Z', localHour: 11, quantity: 2},
      topBooks: [{isbn13: '9782070363735', title: 'Le livre suivi', quantity: 2}],
      topGenres: [{name: 'Romans', quantity: 4}],
      estimatedTriAndCashOverlap: 1,
      estimatedRevenueShare: 12,
      revenueShare: {
        fairId: 'fair-1',
        fairName: 'Bourse de juin',
        fairDate: '2026-06-01T00:00:00Z',
        fairRevenue: 100,
        volunteerSoldQuantity: 4,
        fairSoldQuantity: 30,
        estimatedShare: 12,
      },
      durationIsEstimated: true,
      cadenceIsEstimated: true,
      revenueShareIsEstimated: true,
      triAndCashOverlapIsEstimated: true,
    },
  };

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      isAdministrator: signal(false),
      isVolunteer: signal(false),
      error: signal<string | null>(null),
      initialize: jasmine.createSpy('initialize'),
      login: jasmine.createSpy('login'),
      register: jasmine.createSpy('register'),
      logout: jasmine.createSpy('logout'),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    auth.initialize.and.resolveTo();
    auth.login.and.resolveTo();
    auth.register.and.resolveTo();
    auth.logout.and.resolveTo();
    auth.getApiAccessToken.and.resolveTo('member-token');

    api = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['getWatchlist', 'getVolunteerStatistics', 'addWatchlistItem', 'removeWatchlistItem', 'setAlertStatus', 'deleteAccount'],
    );
    api.getWatchlist.and.returnValue(of(watchlist));
    api.getVolunteerStatistics.and.returnValue(of(volunteerStatistics));
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

  it('offers login and registration entries without provider branding when signed out', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Mon compte');
    expect(fixture.nativeElement.textContent).toContain('Se connecter');
    expect(fixture.nativeElement.textContent).toContain('Créer un compte');
    expect(fixture.nativeElement.textContent).not.toContain('Microsoft');
    expect(fixture.nativeElement.querySelector('[data-testid="member-login"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="member-register"]')).not.toBeNull();
    expect(api.getWatchlist).not.toHaveBeenCalled();
  });

  it('starts registration from the member-register action', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const registerButton = fixture.nativeElement.querySelector(
      '[data-testid="member-register"]',
    ) as HTMLButtonElement | null;
    expect(registerButton).not.toBeNull();

    registerButton?.click();
    await fixture.whenStable();

    expect(auth.register).toHaveBeenCalledWith('/compte');
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

  it('shows the separate Entra name claims in the account heading', async () => {
    auth.account.set({
      ...account('Member'),
      name: 'unknown',
      idTokenClaims: {
        given_name: 'Camille',
        family_name: 'Dupont',
      },
    });
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('#account-preferences-tab') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#account-profile-title')?.textContent?.trim())
      .toBe('Camille Dupont');
  });

  it('makes the watchlist the default account tab and keeps preferences behind the second tab', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];

    expect(tabs.map(tab => tab.textContent?.trim())).toEqual([
      'Ma liste de recherche',
      'Préférences et compte',
    ]);
    expect(tabs[0]?.getAttribute('aria-selected')).toBe('true');
    expect(tabs[1]?.getAttribute('aria-selected')).toBe('false');
    expect(fixture.nativeElement.querySelector('[data-testid="account-watchlist-panel"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="account-preferences-panel"]')).toBeNull();

    tabs[1]?.click();
    fixture.detectChanges();

    expect(tabs[1]?.getAttribute('aria-selected')).toBe('true');
    expect(fixture.nativeElement.querySelector('[data-testid="account-watchlist-panel"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="account-preferences-panel"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Mes alertes e-mail.');
  });

  it('exposes contribution as a third tab only to a volunteer and loads both roles together', async () => {
    auth.account.set(account('Volunteer'));
    auth.isAuthenticated.set(true);
    auth.isVolunteer.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];
    expect(tabs.map(tab => tab.textContent?.trim())).toEqual([
      'Ma liste de recherche',
      'Ma contribution',
      'Préférences et compte',
    ]);
    expect(api.getVolunteerStatistics).toHaveBeenCalledWith('member-token');

    tabs[1]?.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="account-contribution-panel"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Ce que vous avez trié et encaissé');
    expect(fixture.nativeElement.textContent).toContain('Livres encaissés');
  });

  it('renders a watchlist card with its availability and alert history', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    const current = structuredClone(watchlist);
    current.items[0].book!.quantityAvailable = 3;
    current.items[0].lastAlertAt = '2026-09-05T10:00:00Z';
    api.getWatchlist.and.returnValue(of(current));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.watchlist-cover')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('3 disponibles');
    expect(fixture.nativeElement.textContent).toContain('Dernière alerte');
    expect(fixture.nativeElement.textContent).toContain('Retirer de ma liste');
  });

  it('renders saved reference metadata when an edition is not yet in the catalogue', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    const current = structuredClone(watchlist);
    current.items[0].book = null;
    Object.assign(current.items[0], {
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
    });
    api.getWatchlist.and.returnValue(of(current));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.watchlist-item') as HTMLElement;

    expect(card.querySelector('.watchlist-title')?.textContent?.trim()).toBe('Le Petit Prince');
    expect(card.querySelector('.watchlist-isbn')?.textContent).toContain('9782070363735');
    expect(card.querySelector('.watchlist-author')?.textContent).toContain('Antoine de Saint-Exupéry');
    expect(card.querySelector('.watchlist-publisher')?.textContent).toContain('Gallimard');
    expect(card.textContent).not.toContain('Nous vous préviendrons dès qu’une édition correspondante sera au catalogue.');
  });

  it('uses a smaller account heading for the watchlist page', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.account-heading-title')).not.toBeNull();
  });

  it('exposes the administration workspace to an administrator', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    auth.isAdministrator.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const preferencesTab = fixture.nativeElement.querySelector('#account-preferences-tab') as HTMLButtonElement;
    preferencesTab.click();
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

    expect(fixture.nativeElement.textContent).toContain('Redirection vers votre fournisseur de connexion');
    expect(fixture.nativeElement.textContent).not.toContain('Microsoft');
    expect(fixture.nativeElement.textContent).not.toContain('Une erreur est survenue');
  });

  it('requires a second explicit action before deleting the account', async () => {
    auth.account.set(account('Member'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    const preferencesTab = fixture.nativeElement.querySelector('#account-preferences-tab') as HTMLButtonElement;
    preferencesTab.click();
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

    const preferencesTab = fixture.nativeElement.querySelector('#account-preferences-tab') as HTMLButtonElement;
    preferencesTab.click();
    fixture.detectChanges();

    expect(api.setAlertStatus).toHaveBeenCalledWith('member-token', false);
    expect(fixture.componentInstance.watchlist()?.alertStatus).toBe('Suspended');
    expect(fixture.nativeElement.textContent).toContain('Alertes suspendues');
  });
});
