import {HttpErrorResponse, provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting} from '@angular/common/http/testing';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {Meta} from '@angular/platform-browser';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {of, throwError} from 'rxjs';

import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {
  CatalogAdminAlertPage,
  CatalogAdminAccountPage,
  CatalogAdminBookPage,
  CatalogAdminFairPage,
  CatalogAdminMemberPage,
  CatalogAdminOverview,
  CatalogAdminScanSessionPage,
  CatalogAdminScanSession,
  CatalogAdminSettings,
  CatalogDeadStockResponse,
} from '../../core/catalog.models';
import {CatalogAdministrationPageComponent} from './catalog-administration-page.component';
import {toDeadStockCsv} from './dead-stock-export';

describe('CatalogAdministrationPageComponent', () => {
  let fixture: ComponentFixture<CatalogAdministrationPageComponent>;
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
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const response: CatalogDeadStockResponse = {
    generatedAt: '2026-09-04T12:00:00Z',
    minAgeMonths: 6,
    minQuantity: 3,
    books: [{
      isbn13: '9782070408504',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      genre: 'Jeunesse',
      quantityAvailable: 7,
      firstAvailableAt: '2025-01-12T10:00:00Z',
    }],
  };

  const account = (name: string): AccountInfo => ({
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: `${name.toLowerCase()}@example.test`,
    localAccountId: 'local-account-id',
    name,
  });

  const scanSession = (id: string, pendingAlertCount: number): CatalogAdminScanSession => ({
    id,
    volunteerId: 'volunteer-id',
    volunteerName: 'Ada Lovelace',
    mode: 'AvailableNow',
    fairId: null,
    fairName: null,
    startedAt: '2026-09-11T08:00:00Z',
    lastScanAt: '2026-09-11T08:01:00Z',
    lastSyncAt: '2026-09-11T08:01:00Z',
    endedAt: '2026-09-11T08:02:00Z',
    closeReason: 'Manual',
    status: 'Closed',
    scannedCount: 2,
    keptCount: 1,
    rejectedCount: 1,
    alertCount: pendingAlertCount,
    pendingAlertCount,
    nextAlertDueAt: pendingAlertCount > 0 ? '2026-09-11T09:00:00Z' : null,
    movements: [],
  });

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
    auth.getApiAccessToken.and.resolveTo('access-token');

    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getOverview', 'getBooks', 'getBook', 'addBook', 'updateMetadata', 'correctQuantity',
      'withdraw', 'correctAnnouncement', 'setRare', 'setVisibility', 'merge', 'deleteBook',
      'getFairs', 'getFairStats', 'setFairRevenue', 'getSessions', 'getSession',
      'removeMovement', 'reassignSession', 'cancelSession', 'cancelSessionAlerts',
      'forceSessionAlerts', 'getAlerts', 'cancelAlert', 'forceAlert', 'getMembers',
      'getMember', 'setAlertStatus', 'deleteMember', 'getSettings', 'updateSettings',
      'getDeadStock', 'getAdminAccounts', 'createAdminAccount', 'updateAdminAccountRoles',
    ]);
    api.getOverview.and.returnValue(of({
      generatedAt: '',
      currentPeriod: {from: '', to: '', scannedCount: 0, keptCount: 0, rejectedCount: 0, soldQuantity: 0, soldTitles: 0},
      previousPeriod: {from: '', to: '', scannedCount: 0, keptCount: 0, rejectedCount: 0, soldQuantity: 0, soldTitles: 0},
      stock: {availableQuantity: 0, availableTitles: 0, announcedQuantity: 0, announcedTitles: 0},
      lastFair: null,
      deadStockCount: 0,
      rareQueueCount: 0,
      metadataMissingCount: 0,
      undatedAnnouncementCount: 0,
      inventoryDriftTitleCount: 0,
      inventoryDriftQuantity: 0,
      pendingAlerts: {pendingCount: 0, oldestDueAt: null, nextDueAt: null},
    } as CatalogAdminOverview));
    api.getBooks.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminBookPage));
    api.getFairs.and.returnValue(of({generatedAt: '', fairs: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminFairPage));
    api.getSessions.and.returnValue(of({generatedAt: '', sessions: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminScanSessionPage));
    api.getAlerts.and.returnValue(of({generatedAt: '', alerts: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminAlertPage));
    api.getMembers.and.returnValue(of({generatedAt: '', members: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminMemberPage));
    api.getAdminAccounts.and.returnValue(of({generatedAt: '', accounts: [], totalCount: 0, page: 1, pageSize: 25} as CatalogAdminAccountPage));
    api.getSettings.and.returnValue(of({} as CatalogAdminSettings));
    api.getDeadStock.and.returnValue(of(response));

    await TestBed.configureTestingModule({
      declarations: [CatalogAdministrationPageComponent],
      imports: [FormsModule],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogAdminApiService, useValue: api},
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogAdministrationPageComponent);
  });

  it('offers a dedicated administrator login when signed out', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Accès administration');
    expect(fixture.nativeElement.textContent).toContain('Se connecter avec Microsoft');
    expect(fixture.nativeElement.querySelector('[data-testid="admin-login"]')).not.toBeNull();
    expect(api.getDeadStock).not.toHaveBeenCalled();
  });

  it('marks the administration route as not indexable', () => {
    const meta = TestBed.inject(Meta);
    spyOn(meta, 'updateTag').and.callThrough();

    fixture.detectChanges();

    expect(meta.updateTag).toHaveBeenCalledWith({name: 'robots', content: 'noindex, nofollow'});
  });

  it('loads and renders the dead-stock list for an authenticated administrator', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('dead-stock');
    fixture.detectChanges();

    expect(api.getDeadStock).toHaveBeenCalledWith('access-token', 6, 3);
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).toContain('7');
    expect(fixture.nativeElement.querySelector('[data-testid="dead-stock-list"]')).not.toBeNull();
  });

  it('uses the AdminSidebar information architecture from the maquette', () => {
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('[data-testid="admin-sidebar"]');
    expect(sidebar).not.toBeNull();
    expect(sidebar.textContent).toContain('Pendant la bourse');
    expect(sidebar.textContent).toContain('Le fonds de livres');
    expect(sidebar.textContent).toContain("Réservé à l'administration");
    expect(sidebar.textContent).toContain('Statistiques par bourse');
    expect(sidebar.textContent).toContain('Comptes & rôles');
    expect(sidebar.textContent).toContain('Paramètres');
    expect(sidebar.textContent).not.toContain('Membres du site');
    expect(sidebar.textContent).not.toContain('Bénévoles');
    expect(getComputedStyle(sidebar).backgroundColor).not.toBe('rgb(7, 43, 69)');
  });

  it('renders the maquette dashboard title and eight data cards', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getOverview.and.returnValue(of({
      generatedAt: '2026-09-04T12:00:00Z',
      currentPeriod: {from: '', to: '', scannedCount: 230, keptCount: 183, rejectedCount: 47, soldQuantity: 812, soldTitles: 100},
      previousPeriod: {from: '', to: '', scannedCount: 0, keptCount: 0, rejectedCount: 0, soldQuantity: 0, soldTitles: 0},
      stock: {availableQuantity: 4_812, availableTitles: 1_240, announcedQuantity: 0, announcedTitles: 0},
      lastFair: {id: 'fair', name: 'Bourse du 8 février', dateStart: '2026-02-08', dateEnd: null, soldQuantity: 812, soldTitles: 100, revenue: 1_140},
      deadStockCount: 287,
      rareQueueCount: 12,
      metadataMissingCount: 38,
      undatedAnnouncementCount: 7,
      inventoryDriftTitleCount: 38,
      inventoryDriftQuantity: 124,
      pendingAlerts: {pendingCount: 14, oldestDueAt: null, nextDueAt: null},
    } as CatalogAdminOverview));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="admin-title"]')?.textContent).toContain('Tableau de bord.');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="admin-stat-card"]').length).toBe(8);
    expect(fixture.nativeElement.textContent).toContain('Disponibles et annoncés');
    expect(fixture.nativeElement.textContent).toContain('Alertes en attente d’envoi');
  });

  it('reloads the dashboard when a period is selected', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-11T10:00:00Z'));
    try {
      auth.account.set(account('Administrator'));
      auth.isAuthenticated.set(true);
      fixture.detectChanges();
      await fixture.componentInstance.initialize();
      api.getOverview.calls.reset();

      const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '.period-chips:not(.session-chips) .period-chip',
      );
      expect(Array.from(buttons).map(button => button.textContent?.trim())).toEqual([
        '30 derniers jours',
        '3 derniers mois',
        'Année',
      ]);
      expect(buttons[0].getAttribute('aria-pressed')).toBe('true');

      buttons[1].click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect((fixture.componentInstance as unknown as {overviewPeriod: string}).overviewPeriod).toBe('3-months');
      expect(buttons[1].getAttribute('aria-pressed')).toBe('true');
      expect(api.getOverview).toHaveBeenCalledTimes(1);
      expect(api.getOverview).toHaveBeenCalledWith(
        'access-token',
        '2026-06-11T10:00:00.000Z',
        '2026-09-11T10:00:00.000Z',
      );

      api.getOverview.calls.reset();
      buttons[2].click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(buttons[2].getAttribute('aria-pressed')).toBe('true');
      expect(api.getOverview).toHaveBeenCalledWith(
        'access-token',
        '2025-09-11T10:00:00.000Z',
        '2026-09-11T10:00:00.000Z',
      );
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('shows the number of correctable sessions in the sidebar badge', () => {
    fixture.componentInstance.sessionsPage.set({
      generatedAt: '2026-09-11T10:00:00Z',
      sessions: [scanSession('correctable', 2), scanSession('already-sent', 0)],
      totalCount: 12,
      page: 1,
      pageSize: 25,
    });
    fixture.detectChanges();

    const sessionButton = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.admin-nav-item'),
    ).find(button => button.textContent?.includes('Sessions de scan'));

    expect(sessionButton).toBeDefined();
    expect(sessionButton?.querySelector('.admin-nav-badge')?.textContent?.trim()).toBe('1');
  });

  it('loads the dashboard first and can switch to each connected workspace', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    expect(api.getOverview).toHaveBeenCalledWith('access-token');

    await fixture.componentInstance.selectSection('catalogue');
    await fixture.componentInstance.selectSection('sessions');
    await fixture.componentInstance.selectSection('fairs');
    await fixture.componentInstance.selectSection('alerts');
    await fixture.componentInstance.selectSection('accounts');
    await fixture.componentInstance.selectSection('settings');

    expect(api.getBooks).toHaveBeenCalled();
    expect(api.getSessions).toHaveBeenCalled();
    expect(api.getFairs).toHaveBeenCalled();
    expect(api.getAlerts).toHaveBeenCalled();
    expect(api.getMembers).toHaveBeenCalled();
    expect(api.getSettings).toHaveBeenCalled();
  });

  it('loads the accounts and roles workspace from the typed Entra accounts endpoint', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getAdminAccounts.and.returnValue(of({
      generatedAt: '2026-09-04T12:00:00Z',
      accounts: [{
        externalId: 'volunteer-id',
        email: 'volunteer@example.test',
        displayName: 'Bénévole Test',
        accountEnabled: true,
        createdAt: '2026-09-01T10:00:00Z',
        roles: ['Tri'],
      }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    } as CatalogAdminAccountPage));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    fixture.detectChanges();

    expect(api.getAdminAccounts).toHaveBeenCalledWith('access-token', {search: undefined, page: 1, pageSize: 25});
    expect(fixture.nativeElement.textContent).toContain('Comptes & rôles');
    expect(fixture.nativeElement.textContent).toContain('Bénévoles.');
    expect(fixture.nativeElement.textContent).toContain('Bénévole Test');
    expect(fixture.nativeElement.textContent).toContain('Tri');
  });

  it('keeps the accounts workspace readable when the Entra directory is unavailable', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getAdminAccounts.and.returnValue(throwError(() => new HttpErrorResponse({status: 503})));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.admin-status-error')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="account-directory-notice"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('répertoire des comptes Entra');
    expect(api.getMembers).toHaveBeenCalled();
  });

  it('uses the scan-authoritative verdict defaults in the administrator form', () => {
    expect(fixture.componentInstance.settingsForm.duplicateThreshold).toBe(5);
    expect(fixture.componentInstance.settingsForm.demandSalesThreshold).toBe(1);
  });

  it('explains when the signed-in account lacks the administration role', async () => {
    auth.account.set(account('Volunteer'));
    auth.isAuthenticated.set(true);
    api.getDeadStock.and.returnValue(throwError(() => new HttpErrorResponse({status: 403})));
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('ne possède pas les droits d’administration');
  });

  it('explains when the token must be renewed interactively', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    auth.getApiAccessToken.and.rejectWith(new CatalogAuthenticationRedirectStartedError());
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Redirection vers Microsoft');
    expect(fixture.nativeElement.textContent).not.toContain('L’opération n’a pas pu être effectuée');
  });

  it('exports a valid CSV with escaped book fields', () => {
    const csv = toDeadStockCsv({
      ...response,
      books: [{
        ...response.books[0],
        title: 'Livre, "rare"',
        authors: 'Autrice\nAuteur',
      }],
    });

    expect(csv).toContain('ISBN;Titre;Auteur;Éditeur;Année;Genre;Exemplaires;Disponible depuis');
    expect(csv).toContain('9782070408504;"Livre, ""rare""";"Autrice\nAuteur";Gallimard;1999;Jeunesse;7;2025-01-12T10:00:00Z');
  });

  it('neutralizes spreadsheet formula prefixes in exported metadata', () => {
    const csv = toDeadStockCsv({
      ...response,
      books: [{...response.books[0], title: '=HYPERLINK("https://example.test")'}],
    });

    expect(csv).toContain(`;"'=HYPERLINK(""https://example.test"")"`);
  });
});
