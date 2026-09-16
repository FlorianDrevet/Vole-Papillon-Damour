import {HttpErrorResponse, provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting} from '@angular/common/http/testing';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {Meta} from '@angular/platform-browser';
import {ActivatedRoute, ParamMap, Router, RouterModule, convertToParamMap} from '@angular/router';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {DesignSystemModule} from '@vpd/ui';
import {BehaviorSubject, of, Subject, throwError} from 'rxjs';

import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {CatalogApiService} from '../../core/catalog-api.service';
import {
  CatalogAdminAlertPage,
  CatalogAdminAccount,
  CatalogAdminAccountPage,
  CatalogAdminBook,
  CatalogAdminBookMovement,
  CatalogAdminBookPage,
  CatalogAdminFairPage,
  CatalogAdminFairsEvolution,
  CatalogAdminCatalogueFlowStats,
  CatalogAdminMemberDetail,
  CatalogAdminMemberPage,
  CatalogAdminMemberOperation,
  CatalogAdminOverview,
  CatalogAdminRareBookPage,
  CatalogAdminRareBook,
  CatalogAdminRareBookPublishResult,
  CatalogAdminScanSessionPage,
  CatalogAdminScanSession,
  CatalogAdminSettings,
  CatalogAdminVolunteerStatistics,
  CatalogDeadStockResponse,
} from '../../core/catalog.models';
import {CatalogAdministrationPageComponent} from './catalog-administration-page.component';
import {toDeadStockCsv} from './dead-stock-export';
import {AdminRareBookFormComponent} from './rare-books/admin-rare-book-form.component';
import {AdminRareBookPhotosComponent} from './rare-books/admin-rare-book-photos.component';
import {AdminRareBooksComponent} from './rare-books/admin-rare-books.component';

describe('CatalogAdministrationPageComponent', () => {
  let fixture: ComponentFixture<CatalogAdministrationPageComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    isAdministrator: WritableSignal<boolean>;
    isRareBookManager: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    logout: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
  };
  let api: jasmine.SpyObj<CatalogAdminApiService>;
  let catalogApi: jasmine.SpyObj<CatalogApiService>;
  let routeParams: BehaviorSubject<ParamMap>;
  let routeQueryParams: BehaviorSubject<ParamMap>;

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

  const scanSession = (
    id: string,
    pendingAlertCount: number,
    overrides: Partial<CatalogAdminScanSession> = {},
  ): CatalogAdminScanSession => ({
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
    sentAlertCount: 0,
    cancelledAlertCount: 0,
    failedAlertCount: 0,
    nextAlertDueAt: pendingAlertCount > 0 ? '2099-09-11T09:00:00Z' : null,
    movements: [],
    ...overrides,
  });

  const scanMovement = (overrides: Partial<CatalogAdminBookMovement> = {}): CatalogAdminBookMovement => ({
    id: 'movement-1',
    isbn13: '9782070363735',
    type: 'DirectEntry',
    quantity: 1,
    occurredAt: '2026-09-11T08:01:00Z',
    receivedAt: '2026-09-11T08:01:02Z',
    clockSuspect: false,
    scanSessionId: 'session-journal',
    volunteerId: 'volunteer-id',
    fairId: null,
    note: null,
    clientGestureId: 'gesture-1',
    reversalOfMovementId: null,
    ...overrides,
  });

  const catalogueBook = (overrides: Partial<CatalogAdminBook> = {}): CatalogAdminBook => ({
    isbn13: '9782070363735',
    workId: 'OL42W',
    title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry',
    publisher: 'Gallimard',
    publicationYear: 1999,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Jeunesse',
    metadataStatus: 'Complete',
    metadataSource: 'OpenLibrary',
    manuallyEditedFields: null,
    quantityAvailable: 4,
    quantityAnnounced: 1,
    salesCount: 2,
    rejectionCount: 0,
    isRare: false,
    isHidden: false,
    redirectedToIsbn13: null,
    coverUrl: null,
    firstSeenAt: '2026-01-10T10:00:00Z',
    lastAvailableAt: '2026-09-10T10:00:00Z',
    updatedAt: '2026-09-10T10:00:00Z',
    announcements: [],
    movements: [],
    ...overrides,
  });

  const memberDetail = (): CatalogAdminMemberDetail => ({
    member: {
      id: 'member-id',
      externalId: 'entra-member-id',
      email: 'member@example.test',
      displayName: 'Membre Test',
      createdAt: '2026-09-01T10:00:00Z',
      lastSeenAt: '2026-09-12T10:00:00Z',
      anonymizedAt: null,
      alertStatus: 'Active',
      bounceCount: 0,
      watchlistItemCount: 1,
      alertHistoryCount: 2,
    },
    watchlist: [{
      id: 'watchlist-item-id',
      scope: 'Edition',
      workId: null,
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      quantityAvailable: 3,
      quantityAnnounced: 0,
      addedAt: '2026-09-05T10:00:00Z',
      lastAlertAt: null,
    }],
    alerts: [],
  });

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      isAdministrator: signal(true),
      isRareBookManager: signal(false),
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
    routeParams = new BehaviorSubject(convertToParamMap({}));
    routeQueryParams = new BehaviorSubject(convertToParamMap({}));

    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getOverview', 'getBooks', 'getBook', 'addBook', 'updateMetadata', 'correctQuantity',
      'withdraw', 'correctAnnouncement', 'setRare', 'setVisibility', 'merge', 'deleteBook',
      'getFairs', 'getFairStats', 'setFairRevenue', 'getSessions', 'getSession',
      'getVolunteerStatistics', 'getFairsEvolution', 'getCatalogueFlowStats',
      'removeMovement', 'reassignSession', 'cancelSession', 'cancelSessionAlerts',
      'forceSessionAlerts', 'getAlerts', 'cancelAlert', 'forceAlert', 'getMembers',
      'getMember', 'setAlertStatus', 'deleteMember', 'getSettings', 'updateSettings',
      'getDeadStock', 'getAdminAccounts', 'createAdminAccount', 'updateAdminAccountRoles',
      'updateAdminAccountStatus', 'getRareBooks', 'getRareBook', 'createRareBook',
      'updateRareBook', 'publishRareBook', 'unpublishRareBook', 'deleteRareBook',
      'addRareBookPhoto', 'reorderRareBookPhotos', 'updateRareBookPhotoCaption',
      'deleteRareBookPhoto',
    ]);
    catalogApi = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['searchReferences']);
    catalogApi.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: '',
      items: [],
      page: 1,
      pageSize: 20,
    }));
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
    api.getVolunteerStatistics.and.returnValue(of({
      generatedAt: '',
      from: null,
      to: null,
      fairId: null,
      team: {
        activeVolunteerCount: 0,
        scannedCount: 0,
        keptCount: 0,
        rejectedCount: 0,
        keptRatePercent: null,
        soldQuantity: 0,
        soldOfKeptRatePercent: null,
        sessionCount: 0,
        scanDurationMinutes: 0,
        cashDurationMinutes: 0,
        totalDurationMinutes: 0,
        averageSessionsPerVolunteer: null,
      },
      volunteers: [],
      monthlyActivity: [],
      renewal: {newCount: 0, regularCount: 0, withdrawingCount: 0, windowDays: 90},
      dominantGenres: [],
    } as CatalogAdminVolunteerStatistics));
    api.getFairsEvolution.and.returnValue(of({
      generatedAt: '',
      from: null,
      to: null,
      fairCount: 0,
      totalSoldQuantity: 0,
      totalRevenue: null,
      averageBasket: null,
      growthSinceFirstPercent: null,
      seasons: [],
      fairs: [],
    } as CatalogAdminFairsEvolution));
    api.getCatalogueFlowStats.and.returnValue(of({
      generatedAt: '',
      from: null,
      to: null,
      funnel: {
        scannedCount: 0,
        keptCount: 0,
        keptRatePercent: null,
        soldCount: 0,
        soldRatePercent: null,
        dormantCount: 0,
        dormantOverYearCount: 0,
      },
      flowRateByGenre: [],
      timeToSellDistribution: [],
    } as CatalogAdminCatalogueFlowStats));
    api.getSessions.and.returnValue(of({generatedAt: '', sessions: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminScanSessionPage));
    api.getAlerts.and.returnValue(of({generatedAt: '', alerts: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminAlertPage));
    api.getMembers.and.returnValue(of({generatedAt: '', members: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminMemberPage));
    api.getAdminAccounts.and.returnValue(of({generatedAt: '', accounts: [], totalCount: 0, page: 1, pageSize: 25} as CatalogAdminAccountPage));
    api.getSettings.and.returnValue(of({} as CatalogAdminSettings));
    api.getDeadStock.and.returnValue(of(response));
    api.getRareBooks.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 50} as CatalogAdminRareBookPage));
    api.getRareBook.and.returnValue(of({} as CatalogAdminRareBook));
    api.createRareBook.and.returnValue(of({} as CatalogAdminRareBook));
    api.updateRareBook.and.returnValue(of({} as CatalogAdminRareBook));
    api.publishRareBook.and.returnValue(of({} as CatalogAdminRareBookPublishResult));
    api.unpublishRareBook.and.returnValue(of({} as CatalogAdminRareBook));
    api.deleteRareBook.and.returnValue(of(undefined));
    api.addRareBookPhoto.and.returnValue(of({} as CatalogAdminRareBook));
    api.reorderRareBookPhotos.and.returnValue(of({} as CatalogAdminRareBook));
    api.updateRareBookPhotoCaption.and.returnValue(of({} as CatalogAdminRareBook));
    api.deleteRareBookPhoto.and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      declarations: [
        CatalogAdministrationPageComponent,
        AdminRareBooksComponent,
        AdminRareBookFormComponent,
        AdminRareBookPhotosComponent,
      ],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogAdminApiService, useValue: api},
        {provide: CatalogApiService, useValue: catalogApi},
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: routeParams,
            queryParamMap: routeQueryParams,
            snapshot: {
              paramMap: routeParams.value,
              queryParamMap: routeQueryParams.value,
            },
          },
        },
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

  it('renders feedback as an accessible fixed toast that can be dismissed', () => {
    auth.isAuthenticated.set(true);
    fixture.componentInstance.successMessage.set('Les paramètres ont été enregistrés.');
    fixture.detectChanges();

    const toast = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[data-testid="admin-feedback-toast"]',
    );

    expect(toast).not.toBeNull();
    expect(toast?.classList.contains('admin-toast-success')).toBeTrue();
    expect(toast?.getAttribute('role')).toBe('status');
    expect(toast?.querySelector('button[aria-label="Fermer la notification"]')).not.toBeNull();
    expect(getComputedStyle(toast!).position).toBe('fixed');
    expect(fixture.nativeElement.querySelector('.admin-status-success')).toBeNull();

    (toast?.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="admin-feedback-toast"]')).toBeNull();
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

    const sidebar = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[data-testid="admin-sidebar"]')!;
    expect(sidebar).not.toBeNull();
    expect(sidebar.textContent).toContain('Pendant la bourse');
    expect(sidebar.textContent).toContain('Le fonds de livres');
    expect(sidebar.textContent).toContain("Réservé à l'administration");
    expect(sidebar.textContent).toContain('Statistiques');
    expect(sidebar.textContent).not.toContain('Statistiques par bourse');
    expect(sidebar.textContent).toContain('Comptes & rôles');
    expect(sidebar.textContent).toContain('Paramètres');
    expect(sidebar.textContent).not.toContain('Membres du site');
    expect(sidebar.textContent).not.toContain('Bénévoles');
    expect(Array.from(sidebar.querySelectorAll<HTMLElement>('.admin-nav-item-label')).map(label => label.textContent?.trim())).toEqual([
      'Tableau de bord',
      'Sessions de scan',
      'Désengorgement',
      'Catalogue',
      'Livres rares',
      'Statistiques',
      'Comptes & rôles',
      'Paramètres',
    ]);
    expect(getComputedStyle(sidebar).backgroundColor).not.toBe('rgb(7, 43, 69)');
  });

  it('does not expose the removed inventory workspace', () => {
    fixture.detectChanges();

    const sidebar = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[data-testid="admin-sidebar"]')!;

    expect(sidebar.textContent).not.toContain('Inventaire');
    expect(Array.from(sidebar.querySelectorAll<HTMLElement>('.admin-nav-item-label'))
      .map(label => label.textContent?.trim())).not.toContain('Inventaire');
  });

  it('renders administration workspaces as real router links', () => {
    auth.isAuthenticated.set(true);
    fixture.detectChanges();

    const links = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('.admin-nav-item'),
    );

    expect(links.map(link => link.getAttribute('href'))).toEqual([
      '/administration/overview',
      '/administration/sessions',
      '/administration/dead-stock',
      '/administration/catalogue',
      '/administration/rare-books',
      '/administration/statistics',
      '/administration/accounts',
      '/administration/settings',
    ]);
  });

  it('restricts the rare-book role to its single workspace and redirects the bare administration route', async () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    auth.isAdministrator.set(false);
    auth.isRareBookManager.set(true);
    auth.isAuthenticated.set(true);

    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.componentInstance.navItems).toEqual([
      {id: 'rare-books', label: 'Livres rares', icon: 'rare-books'},
    ]);
    expect(router.navigate).toHaveBeenCalledWith(
      ['/administration', 'rare-books'],
      {replaceUrl: true},
    );
    expect(api.getOverview).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Livres rares');
    expect(fixture.nativeElement.textContent).toContain('Accès limité à la gestion des livres rares');
  });

  it('shows the reserved-access state to an authenticated account without an administration role', async () => {
    auth.isAdministrator.set(false);
    auth.isRareBookManager.set(false);
    auth.isAuthenticated.set(true);

    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(api.getOverview).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('ne possède pas les droits d’administration');
    expect(fixture.nativeElement.querySelector('[data-testid="admin-sidebar"]')?.textContent)
      .not.toContain('Tableau de bord');
  });

  it('does not load administrator workspaces for a rare-book manager already on the rare route', async () => {
    auth.isAdministrator.set(false);
    auth.isRareBookManager.set(true);
    auth.isAuthenticated.set(true);
    fixture.componentInstance.activeSection.set('rare-books');

    await fixture.componentInstance.initialize();

    expect(api.getOverview).not.toHaveBeenCalled();
    expect(api.getSessions).not.toHaveBeenCalled();
    expect(api.getDeadStock).not.toHaveBeenCalled();
  });

  it('keeps the catalogue workspace as the only book-management entry', () => {
    fixture.detectChanges();

    expect(fixture.componentInstance.navItems.filter(item => item.id === 'catalogue')).toEqual([
      {id: 'catalogue', label: 'Catalogue', icon: 'catalogue'},
    ]);
  });

  it('renders the catalogue page title for the book-management workspace', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    await fixture.componentInstance.selectSection('catalogue');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#admin-title')?.textContent).toContain('Catalogue.');
  });

  it('redirects the removed inventory route to the administration dashboard', () => {
    fixture.detectChanges();

    routeParams.next(convertToParamMap({section: 'inventory'}));
    fixture.detectChanges();

    expect(fixture.componentInstance.activeSection()).toBe('overview');
  });

  it('restores the selected statistics tab from the current query parameters', () => {
    fixture.detectChanges();

    routeParams.next(convertToParamMap({section: 'statistics'}));
    routeQueryParams.next(convertToParamMap({tab: 'evolution'}));
    fixture.detectChanges();

    expect(fixture.componentInstance.activeSection()).toBe('statistics');
    expect(fixture.componentInstance.statisticsTab()).toBe('evolution');
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
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.admin-nav-item'),
    ).find(button => button.textContent?.includes('Sessions de scan'));

    expect(sessionButton).toBeDefined();
    expect(sessionButton?.querySelector('.admin-nav-badge')?.textContent?.trim()).toBe('1');
  });

  it('confirms session alert actions in the front modal and moves a forced session out of correctable', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-11T08:15:00Z'));
    try {
      auth.account.set(account('Administrator'));
      auth.isAuthenticated.set(true);
      const pending = scanSession('session-force', 2);
      const forced = scanSession('session-force', 2, {
        nextAlertDueAt: '2026-09-11T08:15:00Z',
      });
      api.getSession.and.returnValue(of(forced));
      api.getSessions.and.returnValue(of({
        generatedAt: '',
        sessions: [forced],
        totalCount: 1,
        page: 1,
        pageSize: 25,
      }));
      api.forceSessionAlerts.and.returnValue(of({
        scanSessionId: pending.id,
        affectedMovementCount: 0,
        affectedAlertCount: 2,
        changed: true,
      }));
      const nativeConfirm = spyOn(window, 'confirm').and.returnValue(false);

      fixture.detectChanges();
      fixture.componentInstance.selectedSession.set(pending);
      fixture.detectChanges();

      (fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-force-alerts"]')?.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="session-confirmation"]')).not.toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Lancer l’envoi immédiat');
      expect(nativeConfirm).not.toHaveBeenCalled();

      (fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-confirmation-confirm"]')?.click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(api.forceSessionAlerts).toHaveBeenCalledWith('access-token', pending.id);
      expect(fixture.componentInstance.sessionNeedsCorrection(forced)).toBeFalse();
      expect(fixture.componentInstance.sessionAlertState(forced)).toBe('dispatching');
      fixture.componentInstance.setSessionPreset('alerts');
      expect(fixture.componentInstance.visibleSessions()).toEqual([forced]);
      expect(fixture.nativeElement.textContent).toContain('Envoi immédiat demandé');
      expect((fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-force-alerts"]')?.disabled).toBeTrue();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('shows an explicit cancelled alert state and keeps both alert actions disabled', async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-09-11T08:15:00Z'));
    try {
      auth.account.set(account('Administrator'));
      auth.isAuthenticated.set(true);
      const pending = scanSession('session-cancel-alerts', 2);
      const cancelled = scanSession('session-cancel-alerts', 0, {
        alertCount: 2,
        cancelledAlertCount: 2,
      });
      api.getSession.and.returnValue(of(cancelled));
      api.getSessions.and.returnValue(of({
        generatedAt: '',
        sessions: [cancelled],
        totalCount: 1,
        page: 1,
        pageSize: 25,
      }));
      api.cancelSessionAlerts.and.returnValue(of({
        scanSessionId: pending.id,
        affectedMovementCount: 0,
        affectedAlertCount: 2,
        changed: true,
      }));
      const nativeConfirm = spyOn(window, 'confirm').and.returnValue(false);

      fixture.detectChanges();
      fixture.componentInstance.selectedSession.set(pending);
      fixture.detectChanges();
      (fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-cancel-alerts"]')?.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-testid="session-confirmation"]')).not.toBeNull();
      expect(nativeConfirm).not.toHaveBeenCalled();
      (fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-confirmation-confirm"]')?.click();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(api.cancelSessionAlerts).toHaveBeenCalledWith('access-token', pending.id);
      expect(fixture.componentInstance.sessionAlertState(cancelled)).toBe('cancelled');
      expect(fixture.nativeElement.textContent).toContain('Alertes annulées');
      expect((fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-cancel-alerts"]')?.disabled).toBeTrue();
      expect((fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="session-force-alerts"]')?.disabled).toBeTrue();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('opens the complete scan journal with a correction action for each movement', () => {
    const session = scanSession('session-journal', 0, {
      movements: [scanMovement()],
    });

    fixture.detectChanges();
    fixture.componentInstance.selectedSession.set(session);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="session-journal"]')).toBeNull();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="session-journal-toggle"]')?.click();
    fixture.detectChanges();

    const journal = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[data-testid="session-journal"]');
    expect(journal).not.toBeNull();
    expect(journal?.textContent).toContain('9782070363735');
    expect(journal?.textContent).toContain('Entrée directe');
    expect(journal?.querySelector('[data-testid="session-remove-movement-movement-1"]')).not.toBeNull();
  });

  it('loads the dashboard first and can switch to each connected workspace', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    expect(api.getOverview).toHaveBeenCalledWith('access-token');

    await fixture.componentInstance.selectSection('catalogue');
    await fixture.componentInstance.selectSection('sessions');
    await fixture.componentInstance.selectSection('statistics');
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

  it('loads every book in the catalogue workspace without work-queue filters', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getBooks.and.returnValue(of({
      generatedAt: '2026-09-12T10:00:00Z',
      books: [catalogueBook()],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    }));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    fixture.detectChanges();

    expect(api.getBooks).toHaveBeenCalledWith('access-token', {
      search: undefined,
      page: 1,
      pageSize: 25,
    });
    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-book-list"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Toutes les fiches');
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).not.toContain('Remise à plat');
    const booksZone = fixture.nativeElement.querySelector('.catalogue-books-zone') as HTMLElement;
    const addZone = fixture.nativeElement.querySelector('.catalogue-add-zone') as HTMLElement;
    expect(booksZone.compareDocumentPosition(addZone) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain('Ajuster le stock disponible');
    expect(fixture.nativeElement.textContent).not.toContain('Les boutons − et + créent une correction tracée');
    expect(fixture.nativeElement.textContent).not.toContain('Afficher toutes les fiches');
    expect(fixture.nativeElement.textContent).not.toContain('Chaque ajout et chaque correction reste attribué');
  });

  it('opens a book fiche inside the catalogue workspace', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const book = catalogueBook();
    api.getBooks.and.returnValue(of({
      generatedAt: '',
      books: [book],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    }));
    api.getBook.and.returnValue(of(book));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    fixture.detectChanges();

    const detailButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '.catalogue-book-detail',
    );
    expect(detailButton).not.toBeNull();

    detailButton!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getBook).toHaveBeenCalledWith('access-token', book.isbn13);
    expect(fixture.componentInstance.activeSection()).toBe('catalogue');
    expect(fixture.nativeElement.querySelector('.detail-screen')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#admin-title')?.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).not.toContain('Inventaire.');
  });

  it('shows a loader on the catalogue refresh action while the server request is pending', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const page: CatalogAdminBookPage = {
      generatedAt: '',
      books: [catalogueBook()],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    };
    api.getBooks.and.returnValue(of(page));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');

    const pending = new Subject<CatalogAdminBookPage>();
    api.getBooks.calls.reset();
    api.getBooks.and.returnValue(pending.asObservable());
    const refresh = fixture.componentInstance.loadCatalogue();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-refresh-loader"]')).not.toBeNull();
    expect((fixture.nativeElement.querySelector('[data-testid="catalogue-refresh"]') as HTMLButtonElement).disabled).toBeTrue();

    pending.next(page);
    pending.complete();
    await refresh;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-refresh-loader"]')).toBeNull();
  });

  it('starts a catalogue search after two seconds of input inactivity and requests the first server page', async () => {
    jasmine.clock().install();
    try {
      auth.account.set(account('Administrator'));
      auth.isAuthenticated.set(true);
      api.getBooks.and.returnValue(of({
        generatedAt: '',
        books: [],
        totalCount: 0,
        page: 1,
        pageSize: 25,
      }));

      fixture.detectChanges();
      await fixture.componentInstance.selectSection('catalogue');
      fixture.detectChanges();
      api.getBooks.calls.reset();
      fixture.componentInstance.cataloguePage = 3;

      const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('[data-testid="catalogue-search"]');
      expect(input).not.toBeNull();
      input!.value = 'prince';
      input!.dispatchEvent(new Event('input', {bubbles: true}));
      fixture.detectChanges();

      expect(api.getBooks).not.toHaveBeenCalled();
      jasmine.clock().tick(1_999);
      expect(api.getBooks).not.toHaveBeenCalled();

      jasmine.clock().tick(1);
      await fixture.whenStable();
      fixture.detectChanges();

      expect(fixture.componentInstance.cataloguePage).toBe(1);
      expect(api.getBooks).toHaveBeenCalledWith('access-token', {
        search: 'prince',
        page: 1,
        pageSize: 25,
      });
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('adjusts a fiche quantity by one from the compact catalogue controls', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const book = catalogueBook({quantityAvailable: 4});
    api.getBooks.and.returnValue(of({
      generatedAt: '',
      books: [book],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    }));
    api.correctQuantity.and.returnValue(of({
      isbn13: book.isbn13,
      previousQuantityAvailable: 4,
      quantityAvailable: 5,
      delta: 1,
      changed: true,
      movementId: 'movement-id',
    }));
    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    await fixture.whenStable();
    fixture.detectChanges();

    const increase = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      `[data-testid="catalogue-increase-${book.isbn13}"]`,
    );
    expect(increase).not.toBeNull();

    await fixture.componentInstance.adjustCatalogueQuantity(book, 'increase');
    fixture.detectChanges();

    const confirm = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-confirmation-confirm"]',
    );
    expect(confirm).not.toBeNull();
    confirm!.click();
    await fixture.whenStable();

    expect(api.correctQuantity).toHaveBeenCalledWith('access-token', book.isbn13, {
      quantityAvailable: 5,
      note: 'Correction depuis le catalogue',
    });
  });

  it('opens a styled confirmation modal before changing catalogue quantity', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const book = catalogueBook({quantityAvailable: 4});
    api.getBooks.and.returnValue(of({
      generatedAt: '',
      books: [book],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    }));
    const nativeConfirm = spyOn(window, 'confirm').and.returnValue(false);
    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    await fixture.whenStable();
    fixture.detectChanges();

    const decrease = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      `[data-testid="catalogue-decrease-${book.isbn13}"]`,
    );
    expect(decrease).not.toBeNull();
    decrease!.click();
    fixture.detectChanges();

    const dialog = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[data-testid="catalogue-confirmation"]',
    );
    expect(dialog).not.toBeNull();
    expect(dialog?.getAttribute('role')).toBe('dialog');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(dialog?.textContent).toContain('Retirer 1 exemplaire');
    expect((fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="catalogue-confirmation-cancel"]',
    )).not.toBeNull();
    expect(nativeConfirm).not.toHaveBeenCalled();
    expect(api.correctQuantity).not.toHaveBeenCalled();

    document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape'}));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="catalogue-confirmation"]')).toBeNull();
  });

  it('removes one unit without allowing a negative stock', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const book = catalogueBook({quantityAvailable: 4});
    api.getBooks.and.returnValue(of({
      generatedAt: '',
      books: [book],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    }));
    api.correctQuantity.and.returnValue(of({
      isbn13: book.isbn13,
      previousQuantityAvailable: 4,
      quantityAvailable: 3,
      delta: -1,
      changed: true,
      movementId: 'movement-id',
    }));
    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    await fixture.whenStable();

    await fixture.componentInstance.adjustCatalogueQuantity(book, 'decrease');
    fixture.detectChanges();

    const confirm = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-confirmation-confirm"]',
    );
    expect(confirm).not.toBeNull();
    confirm!.click();
    await fixture.whenStable();

    expect(api.correctQuantity).toHaveBeenCalledWith('access-token', book.isbn13, {
      quantityAvailable: 3,
      note: 'Correction depuis le catalogue',
    });
  });

  it('normalizes an ISBN before preparing a fiche from the external reference', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const reference = {
      isbn13: '9782070612758',
      workId: 'OL42W',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: null,
      source: 'OpenLibrary',
    };
    catalogApi.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: reference.isbn13,
      items: [reference],
      page: 1,
      pageSize: 20,
    }));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    fixture.componentInstance.catalogueIsbn = '978-207-061-2758';
    await fixture.componentInstance.lookupCatalogueIsbn();

    expect(catalogApi.searchReferences).toHaveBeenCalledWith(reference.isbn13, 1, 20);
    expect(fixture.componentInstance.catalogueAddCandidate()?.isbn13).toBe(reference.isbn13);
  });

  it('clears stale external references when the ISBN is invalid', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.componentInstance.catalogueReferenceResults.set([{
      isbn13: '9782070612758',
      workId: null,
      title: 'Ancienne notice',
      authors: null,
      publisher: null,
      publicationYear: null,
      coverUrl: null,
      source: 'OpenLibrary',
    }]);
    fixture.componentInstance.catalogueIsbn = '9782070612759';

    await fixture.componentInstance.lookupCatalogueIsbn();

    expect(catalogApi.searchReferences).not.toHaveBeenCalled();
    expect(fixture.componentInstance.catalogueReferenceResults()).toEqual([]);
    expect(fixture.componentInstance.catalogueLookupError()).toBe('Saisissez un ISBN-10 ou ISBN-13 valide.');
  });

  it('keeps external references separate and lets an administrator add the selected fiche', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const reference = {
      isbn13: '9782070612758',
      workId: 'OL42W',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: 'https://covers.openlibrary.org/isbn/9782070612758-M.jpg',
      source: 'OpenLibrary',
    };
    catalogApi.searchReferences.and.returnValue(of({
      generatedAt: '2026-09-12T10:00:00Z',
      query: 'Le Petit Prince',
      items: [reference],
      page: 1,
      pageSize: 20,
    }));
    api.getBooks.and.returnValue(of({generatedAt: '', books: [], totalCount: 0, page: 1, pageSize: 25}));
    api.getBook.and.returnValue(of(catalogueBook({
      isbn13: reference.isbn13,
      quantityAvailable: 4,
    })));
    api.addBook.and.returnValue(of({changed: true, isbn13: reference.isbn13}));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    fixture.detectChanges();

    const query = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '[data-testid="catalogue-reference-query"]',
    );
    const search = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-reference-search"]',
    );
    expect(query).not.toBeNull();
    expect(search).not.toBeNull();

    fixture.componentInstance.catalogueReferenceQuery = 'Le Petit Prince';
    fixture.detectChanges();
    search!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(catalogApi.searchReferences).toHaveBeenCalledWith('Le Petit Prince', 1, 20);
    expect(fixture.nativeElement.textContent).toContain('Référentiel externe');
    const useReference = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-use-reference"]',
    );
    expect(useReference).not.toBeNull();

    useReference!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    const referenceRow = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '.catalogue-reference-row',
    );
    expect(referenceRow?.nextElementSibling?.getAttribute('data-testid')).toBe('catalogue-candidate-form');
    expect(fixture.nativeElement.textContent).toContain('Cette fiche est déjà dans le fonds');
    expect(fixture.nativeElement.textContent).toContain('4 exemplaires disponibles');
    const quantity = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '[data-testid="catalogue-add-quantity"]',
    );
    const note = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '[data-testid="catalogue-add-note"]',
    );
    const add = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-add-submit"]',
    );
    const decrease = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-add-quantity-decrease"]',
    );
    const increase = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-add-quantity-increase"]',
    );
    expect(quantity).not.toBeNull();
    expect(note).not.toBeNull();
    expect(add).not.toBeNull();
    expect(decrease).not.toBeNull();
    expect(increase).not.toBeNull();

    fixture.componentInstance.catalogueAddQuantity = 2;
    fixture.componentInstance.catalogueAddNote = 'Ajout du don';
    fixture.detectChanges();
    increase!.click();
    decrease!.click();
    expect(fixture.componentInstance.catalogueAddQuantity).toBe(2);
    add!.click();
    await fixture.whenStable();

    expect(api.addBook).toHaveBeenCalledWith('access-token', {
      isbn13: reference.isbn13,
      quantityAvailable: 2,
      note: 'Ajout du don',
      title: reference.title,
      authors: reference.authors,
      publisher: reference.publisher,
      publicationYear: reference.publicationYear,
      physicalFormat: null,
      language: null,
      genre: null,
      coverUrl: reference.coverUrl,
      workId: reference.workId,
    });

  });

  it('shows when the selected bibliographic fiche is not yet in the fonds', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const reference = {
      isbn13: '9782070612758',
      workId: 'OL42W',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      coverUrl: null,
      source: 'OpenLibrary',
    };
    catalogApi.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'Le Petit Prince',
      items: [reference],
      page: 1,
      pageSize: 20,
    }));
    api.getBook.and.returnValue(throwError(() => new HttpErrorResponse({status: 404})));

    fixture.detectChanges();
    await fixture.componentInstance.selectSection('catalogue');
    fixture.componentInstance.catalogueReferenceQuery = 'Le Petit Prince';
    await fixture.componentInstance.searchCatalogueReferences();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      '[data-testid="catalogue-use-reference"]',
    )!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Cette fiche n’est pas encore dans le fonds');
  });

  it('renders the two statistics tabs and omits the quality-data screen', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getVolunteerStatistics.and.returnValue(of({
      generatedAt: '2026-09-12T12:00:00Z',
      from: '2026-08-13T12:00:00Z',
      to: '2026-09-12T12:00:00Z',
      fairId: null,
      team: {
        activeVolunteerCount: 2,
        scannedCount: 5916,
        keptCount: 4619,
        rejectedCount: 1297,
        keptRatePercent: 78,
        soldQuantity: 3697,
        soldOfKeptRatePercent: 80,
        sessionCount: 215,
        scanDurationMinutes: 8340,
        cashDurationMinutes: 3240,
        totalDurationMinutes: 11580,
        averageSessionsPerVolunteer: 14.3,
      },
      volunteers: [{
        volunteerId: 'volunteer-id',
        displayName: 'Michel Bonnet',
        roles: ['Tri', 'Caisse'],
        sessionCount: 31,
        scanDurationMinutes: 1230,
        cashDurationMinutes: 0,
        totalDurationMinutes: 1230,
        scannedCount: 985,
        keptCount: 798,
        keptRatePercent: 81,
        soldQuantity: 662,
        waitingQuantity: 20,
        waitingOverYearQuantity: 4,
        flowRatePercent: 98,
        firstActivityAt: '2026-06-01T08:00:00Z',
        lastActivityAt: '2026-09-09T18:00:00Z',
        dominantGenre: 'Romans',
      }],
      monthlyActivity: [],
      renewal: {newCount: 3, regularCount: 12, withdrawingCount: 3, windowDays: 90},
      dominantGenres: [{name: 'Romans', quantity: 2100}],
    } as CatalogAdminVolunteerStatistics));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('statistics');
    await fixture.componentInstance.selectStatisticsTab('volunteers');
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('Statistiques par bourse');
    expect(page.textContent).toContain('Statistiques des bénévoles');
    expect(page.textContent).toContain('Michel Bonnet');
    expect(page.textContent).not.toContain('Qualité des données');
    expect(page.textContent).not.toContain('4C');
    const summary = page.querySelector('[data-testid="volunteer-summary"]');
    expect(summary?.textContent).toContain('Ensemble, sur trente jours');
    expect(summary?.textContent).toContain('livres passés sous la douchette par deux personnes.');
    expect(summary?.textContent).toContain('78 % du scanné');
    expect(summary?.textContent).toContain('139 h tri · 54 h caisse');
    expect(summary?.textContent).toContain('14 par personne');
    expect(page.querySelector('.volunteer-stat-grid')).toBeNull();
    expect(page.querySelector('[data-testid="statistics-period"]')).not.toBeNull();
    expect(page.querySelectorAll('.admin-nav-item')).not.toHaveSize(0);
    expect(Array.from(page.querySelectorAll('.admin-nav-item')).map(item => item.textContent?.trim()))
      .not.toContain('Bénévoles');
    expect(api.getVolunteerStatistics).toHaveBeenCalled();
  });

  it('renders the fairs evolution tab with real cross-fair data', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getFairsEvolution.and.returnValue(of({
      generatedAt: '2026-09-12T12:00:00Z',
      from: null,
      to: null,
      fairCount: 2,
      totalSoldQuantity: 250,
      totalRevenue: 1500,
      averageBasket: 6,
      growthSinceFirstPercent: 50,
      seasons: [{season: 'Printemps', averageSoldQuantity: 100, fairCount: 1}],
      fairs: [
        {
          fairId: 'fair-1',
          name: 'Bourse de mars',
          dateStart: '2025-03-01T00:00:00Z',
          dateEnd: '2025-03-01T00:00:00Z',
          soldQuantity: 100,
          revenue: 500,
          averageBasket: 5,
          variationPercent: null,
          daysOpen: 1,
        },
        {
          fairId: 'fair-2',
          name: 'Bourse de septembre',
          dateStart: '2025-09-01T00:00:00Z',
          dateEnd: '2025-09-01T00:00:00Z',
          soldQuantity: 150,
          revenue: 1000,
          averageBasket: 6.67,
          variationPercent: 50,
          daysOpen: 1,
        },
      ],
    } as CatalogAdminFairsEvolution));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('statistics');
    await fixture.componentInstance.selectStatisticsTab('evolution');
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('Évolution');
    expect(page.textContent).toContain('Bourse de mars');
    expect(page.textContent).toContain('Bourse de septembre');
    expect(page.textContent).toContain('Printemps');
    expect(page.textContent).toContain('Mars 2025 → septembre 2025');
    expect(page.textContent).toContain('Deux bourses, d’une fois sur l’autre');
    expect(page.querySelectorAll('[data-testid="evolution-chart"] .evolution-bar').length).toBe(2);
    expect(page.querySelector('[data-testid="evolution-export"]')).not.toBeNull();
    expect(api.getFairsEvolution).toHaveBeenCalled();
  });

  it('renders the books (catalogue flow) tab with real funnel data', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getCatalogueFlowStats.and.returnValue(of({
      generatedAt: '2026-09-12T12:00:00Z',
      from: null,
      to: null,
      funnel: {
        scannedCount: 100,
        keptCount: 80,
        keptRatePercent: 80,
        soldCount: 60,
        soldRatePercent: 75,
        dormantCount: 20,
        dormantOverYearCount: 5,
      },
      flowRateByGenre: [{genre: 'Romans', keptQuantity: 40, soldQuantity: 30, flowRatePercent: 75}],
      timeToSellDistribution: [{bucket: '< 1 mois', quantity: 30, sharePercent: 50}],
    } as CatalogAdminCatalogueFlowStats));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('statistics');
    await fixture.componentInstance.selectStatisticsTab('books');
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('Les livres');
    expect(page.textContent).toContain('Romans');
    expect(page.textContent).toContain('< 1 mois');
    expect(api.getCatalogueFlowStats).toHaveBeenCalled();
  });

  it('scopes the evolution tab to a single fair from the period picker', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('statistics');
    await fixture.componentInstance.selectStatisticsTab('evolution');
    fixture.detectChanges();

    expect(api.getFairsEvolution.calls.mostRecent().args[3]).toBeUndefined();
    expect(fixture.nativeElement.querySelector('[data-testid="statistics-period-fair"]')).not.toBeNull();

    await fixture.componentInstance.selectStatisticsFair('fair-2');

    expect(fixture.componentInstance.statisticsPeriod).toBe('fair');
    expect(api.getFairsEvolution.calls.mostRecent().args.slice(1)).toEqual([undefined, undefined, 'fair-2']);

    await fixture.componentInstance.selectStatisticsFair('');

    expect(fixture.componentInstance.statisticsPeriod).toBe('30-days');
    expect(api.getFairsEvolution.calls.mostRecent().args[3]).toBeUndefined();
  });

  it('lists every volunteer with their roles in one sortable "Qui a porté quoi" table', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.getVolunteerStatistics.and.returnValue(of({
      generatedAt: '2026-09-12T12:00:00Z',
      from: null,
      to: null,
      fairId: null,
      team: {
        activeVolunteerCount: 2,
        scannedCount: 10,
        keptCount: 8,
        rejectedCount: 2,
        keptRatePercent: 80,
        soldQuantity: 5,
        soldOfKeptRatePercent: 62.5,
        sessionCount: 10,
        scanDurationMinutes: 100,
        cashDurationMinutes: 50,
        totalDurationMinutes: 150,
        averageSessionsPerVolunteer: 5,
      },
      volunteers: [
        {
          volunteerId: 'sorter-id',
          displayName: 'Sylvie Rousseau',
          roles: ['Tri'],
          sessionCount: 5,
          scanDurationMinutes: 100,
          cashDurationMinutes: 0,
          totalDurationMinutes: 100,
          scannedCount: 10,
          keptCount: 8,
          keptRatePercent: 80,
          soldQuantity: 5,
          waitingQuantity: 3,
          waitingOverYearQuantity: 0,
          flowRatePercent: 62.5,
          firstActivityAt: null,
          lastActivityAt: null,
          dominantGenre: null,
        },
        {
          volunteerId: 'cashier-id',
          displayName: 'Patrick Noël',
          roles: ['Caisse'],
          sessionCount: 4,
          scanDurationMinutes: 0,
          cashDurationMinutes: 50,
          totalDurationMinutes: 50,
          scannedCount: 0,
          keptCount: 0,
          keptRatePercent: null,
          soldQuantity: 0,
          waitingQuantity: 0,
          waitingOverYearQuantity: 0,
          flowRatePercent: null,
          firstActivityAt: null,
          lastActivityAt: null,
          dominantGenre: null,
        },
      ],
      monthlyActivity: [],
      renewal: {newCount: 0, regularCount: 0, withdrawingCount: 0, windowDays: 90},
      dominantGenres: [],
    } as CatalogAdminVolunteerStatistics));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('statistics');
    await fixture.componentInstance.selectStatisticsTab('volunteers');
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    const names = () => Array.from(page.querySelectorAll('.volunteer-contribution-table tbody .volunteer-name'))
      .map(element => element.textContent?.trim());
    expect(names()).toEqual(['Sylvie Rousseau', 'Patrick Noël']);
    expect(page.querySelector('.volunteer-contribution-table')?.textContent).toContain('Caisse');
    expect(page.querySelector('[data-testid="volunteer-contributions"]')?.textContent).toContain('Deux bénévoles actifs');
    expect(page.querySelector('[data-testid="volunteer-export"]')).not.toBeNull();

    fixture.componentInstance.sortVolunteersBy('sessionCount');
    fixture.detectChanges();
    expect(names()).toEqual(['Sylvie Rousseau', 'Patrick Noël']);

    fixture.componentInstance.sortVolunteersBy('sessionCount');
    fixture.detectChanges();
    expect(names()).toEqual(['Patrick Noël', 'Sylvie Rousseau']);
    expect(page.querySelector('th[aria-sort="ascending"]')?.textContent).toContain('Sess.');

    const scatterLabels = Array.from(page.querySelectorAll('.scatter-label')).map(el => el.textContent);
    expect(scatterLabels).toContain('Sylvie Rousseau');
    expect(scatterLabels).toContain('Patrick Noël');
  });

  it('distinguishes fairs with the same name by their dates in the statistics selector', () => {
    fixture.detectChanges();
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.componentInstance.activeSection.set('statistics');
    fixture.componentInstance.fairsPage.set({
      generatedAt: '2026-09-11T10:00:00Z',
      fairs: [
        {
          id: 'fair-october',
          name: 'Bourse aux livres',
          dateStart: '2026-10-10T10:00:00Z',
          dateEnd: '2026-10-10T18:00:00Z',
          isCancelled: false,
          revenue: null,
        },
        {
          id: 'fair-november',
          name: 'Bourse aux livres',
          dateStart: '2026-11-14T10:00:00Z',
          dateEnd: '2026-11-14T18:00:00Z',
          isCancelled: false,
          revenue: null,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 50,
    });
    fixture.detectChanges();

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLOptionElement>('select[name="fairId"] option'),
    ).map(option => option.textContent?.trim() ?? '');

    expect(options[1]).toContain(fixture.componentInstance.formatDate('2026-10-10T10:00:00Z'));
    expect(options[2]).toContain(fixture.componentInstance.formatDate('2026-11-14T10:00:00Z'));
    expect(options[1]).not.toBe(options[2]);
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
        roles: ['Tri', 'LivresRares'],
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
    const rareRoleChip = (fixture.nativeElement as HTMLElement).querySelector('.role-chip-rare');
    expect(rareRoleChip?.textContent).toContain('Livres rares');
  });

  it('offers Livres rares when assigning account roles', () => {
    expect(fixture.componentInstance.accountRoleOptions).toEqual(jasmine.arrayContaining([
      {value: 'LivresRares', label: 'Livres rares'},
    ]));
  });

  it('creates a volunteer account with separate first and last names', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    api.createAdminAccount.and.returnValue(of({
      externalId: 'created-account-id',
      email: 'marie@example.test',
      displayName: 'Marie Tri',
      accountEnabled: true,
      createdAt: '2026-09-16T10:00:00Z',
      roles: ['Tri'],
    }));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    fixture.componentInstance.showCreateAccount.set(true);
    fixture.detectChanges();

    const form = (fixture.nativeElement as HTMLElement).querySelector<HTMLFormElement>('.account-create-card');
    expect(form).not.toBeNull();
    expect(form?.textContent).toContain('Prénom');
    expect(form?.textContent).toContain('Nom');
    expect(form?.textContent).not.toContain('Nom affiché');

    expect(form?.querySelector<HTMLInputElement>('[name="accountFirstName"]')).not.toBeNull();
    expect(form?.querySelector<HTMLInputElement>('[name="accountLastName"]')).not.toBeNull();
    Object.assign(fixture.componentInstance.createAccountForm, {
      firstName: 'Marie',
      lastName: 'Tri',
      email: 'marie@example.test',
      temporaryPassword: 'Temporaire1!',
    });
    fixture.componentInstance.toggleCreateRole('Tri');
    await fixture.whenStable();

    await fixture.componentInstance.createAccount();

    expect(api.createAdminAccount).toHaveBeenCalledWith('access-token', {
      email: 'marie@example.test',
      firstName: 'Marie',
      lastName: 'Tri',
      temporaryPassword: 'Temporaire1!',
      roles: ['Tri'],
    });
    expect(fixture.componentInstance.showCreateAccount()).toBeFalse();
  });

  it('offers volunteer and site-member subtabs in the accounts workspace', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    fixture.detectChanges();

    const tabs = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[data-testid="accounts-subtab"]'),
    );
    expect(tabs.map(tab => tab.textContent?.trim())).toEqual(['Bénévoles', 'Membres du site']);
    expect(tabs[0].classList.contains('active')).toBeTrue();

    tabs[1].click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.accountsTab()).toBe('members');
    expect(fixture.nativeElement.querySelector('[data-testid="members-workspace"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Membres du site.');
  });

  it('opens a member sheet modal with actions and removes the old support notice', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const detail = memberDetail();
    api.getMembers.and.returnValue(of({
      generatedAt: '',
      members: [detail.member],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    } as CatalogAdminMemberPage));
    api.getMember.and.returnValue(of(detail));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    await fixture.componentInstance.selectAccountsTab('members');
    fixture.detectChanges();

    const detailButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="member-detail-member-id"]');
    expect(detailButton).not.toBeNull();
    detailButton?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const dialog = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[data-testid="member-detail-modal"]');
    expect(dialog).not.toBeNull();
    expect(dialog?.getAttribute('role')).toBe('dialog');
    expect(dialog?.textContent).toContain('Membre Test');
    expect(dialog?.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).not.toContain('Consulter la liste de recherche d’un membre sert au support');
  });

  it('deletes the member identity through the admin endpoint after modal confirmation', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const detail = memberDetail();
    api.getMember.and.returnValue(of(detail));
    api.deleteMember.and.returnValue(of({
      memberId: detail.member.id,
      alertStatus: 'Deleted',
      changed: true,
      deletionCompleted: true,
    } as CatalogAdminMemberOperation));

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.openMember(detail.member.id);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="member-delete"]')?.click();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="member-delete-confirmation"]')).not.toBeNull();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="member-delete-confirm"]')?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.deleteMember).toHaveBeenCalledWith('access-token', detail.member.id);
    expect(fixture.componentInstance.successMessage()).toContain('Entra');
    expect(fixture.componentInstance.selectedMember()).toBeNull();
  });

  it('enables and disables a volunteer account through the Entra status endpoint', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    const volunteer: CatalogAdminAccount = {
      externalId: 'volunteer-id',
      email: 'volunteer@example.test',
      displayName: 'Bénévole Test',
      accountEnabled: true,
      createdAt: '2026-09-01T10:00:00Z',
      roles: ['Tri'],
    };
    api.getAdminAccounts.and.returnValue(of({
      generatedAt: '',
      accounts: [volunteer],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    } as CatalogAdminAccountPage));
    api.updateAdminAccountStatus.and.returnValue(of({...volunteer, accountEnabled: false}));
    spyOn(window, 'confirm').and.returnValue(true);

    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.selectSection('accounts');
    fixture.detectChanges();

    const toggle = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="account-status-volunteer-id"]');
    expect(toggle).not.toBeNull();
    expect(toggle?.disabled).toBeFalse();
    toggle?.click();
    await fixture.whenStable();

    expect(api.updateAdminAccountStatus).toHaveBeenCalledWith('access-token', 'volunteer-id', false);
    expect(fixture.componentInstance.successMessage()).toContain('désactivé');
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
    auth.isAdministrator.set(false);
    auth.isRareBookManager.set(false);
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
