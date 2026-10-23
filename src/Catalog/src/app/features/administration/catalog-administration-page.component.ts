import {DOCUMENT, isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, combineLatest, firstValueFrom, takeUntil} from 'rxjs';
import {
  getTemporaryPasswordIssue,
  isTemporaryPasswordPolicyProblemDetail,
  temporaryPasswordIssueMessage,
  TEMPORARY_PASSWORD_GUIDANCE,
  type TemporaryPasswordIssue,
} from '@vpd/ui';

import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {CatalogApiService} from '../../core/catalog-api.service';
import {CURATED_CATALOG_GENRES, mergeCatalogGenres} from '../../core/catalog-genres';
import {isCatalogAdministrationRoute} from '../../core/catalog-route';
import {
  catalogAdminSectionFromRoute,
  catalogAdminSectionPath,
  catalogAdminStatisticsTabFromRoute,
  isCatalogAdminSection,
  type CatalogAdminSection,
  type CatalogAdminStatisticsTab,
} from '../../core/catalog-administration-route';
import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {AdminRareBooksFacade} from './rare-books/admin-rare-books.facade';
import {
  CatalogAdminAccount,
  CatalogAdminAccountFilters,
  CatalogAdminAccountPage,
  CatalogAdminAccountRole,
  CatalogAdminCheckoutPassageLookup,
  CatalogAdminAlert,
  CatalogAdminAlertFilters,
  CatalogAdminAlertPage,
  CatalogAdminBook,
  CatalogAdminBookMetadataField,
  CatalogAdminBookMetadataUpdate,
  CatalogAdminBookMetadataQueue,
  CatalogAdminBookMetadataQueueItem,
  CatalogAdminBookImportResponse,
  CatalogAdminBookMovement,
  CatalogAdminBookPage,
  CatalogAdminFair,
  CatalogAdminFairPage,
  CatalogAdminFairStats,
  CatalogAdminFairsEvolution,
  CatalogAdminCatalogueFlowStats,
  CatalogAdminVolunteerStatistics,
  CatalogAdminMemberDetail,
  CatalogAdminMemberFilters,
  CatalogAdminMemberPage,
  CatalogAdminOverview,
  CatalogAdminIsbndbStatus,
  CatalogAdminCreateAccountRequest,
  CatalogAdminScanSession,
  CatalogAdminScanSessionPage,
  CatalogAdminSessionFilters,
  CatalogAdminSettings,
  CatalogBookReference,
  CatalogDeadStockBook,
  CatalogNotFoundCloseAction,
  CatalogNotFoundQueueTarget,
  CatalogNotFoundSummary,
  CatalogManualRequestSummary,
} from '../../core/catalog.models';
import {
  IsbnApplicationCsvFormatError,
  parseIsbnApplicationCsv,
  type IsbnApplicationCsvPreview,
} from './isbn-application-import';
import {toDeadStockCsv} from './dead-stock-export';
import {NotFoundCloseConfirmation} from './not-found-reports/not-found-report-close-dialog.component';
import {buildFairsEvolutionView, toFairsEvolutionCsv} from './fairs-evolution-view';
import {capitalizeFrench} from './statistics-format';
import {
  DEFAULT_VOLUNTEER_SORT,
  VOLUNTEER_COLUMNS,
  describeStatisticsPeriod,
  describeVolunteerCount,
  describeVolunteerLead,
  formatRoundedHours,
  nextVolunteerSort,
  sortVolunteers,
  toVolunteerStatisticsCsv,
  type VolunteerSort,
  type VolunteerSortKey,
} from './volunteer-statistics-view';

const DEFAULT_MIN_AGE_MONTHS = 6;
const DEFAULT_MIN_QUANTITY = 3;
const MAX_MIN_AGE_MONTHS = 120_000;
const MAX_BOOK_QUANTITY = 100_000;
const CATALOGUE_SEARCH_DEBOUNCE_MS = 2_000;
const CATALOGUE_ADJUSTMENT_QUANTITY = 1;
const CATALOGUE_ADJUSTMENT_NOTE = 'Correction depuis le catalogue';
const ISBN_APPLICATION_IMPORT_BATCH_SIZE = 100;
const MAX_BOOK_COVER_SIZE_BYTES = 8 * 1024 * 1024;
const SUPPORTED_BOOK_COVER_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp']);

interface IsbnApplicationImportProgress {
  processedCount: number;
  totalCount: number;
  completedBatches: number;
  totalBatches: number;
  createdCount: number;
  alreadyPresentCount: number;
}

interface IsbnApplicationFilePreview {
  fileName: string;
  preview: IsbnApplicationCsvPreview;
}

type IsbnApplicationInvalidExample = IsbnApplicationCsvPreview['invalidExamples'][number] & {
  fileName: string;
};

interface IsbnApplicationCombinedPreview extends Omit<IsbnApplicationCsvPreview, 'invalidExamples'> {
  fileCount: number;
  invalidExamples: IsbnApplicationInvalidExample[];
}

export type CatalogAdminStatisticsPeriod = '30-days' | 'year' | 'all' | 'fair';
export type CatalogAdminAccountsTab = 'volunteers' | 'members';

type CatalogAdminNavIcon =
  | 'dashboard'
  | 'scan'
  | 'dead-stock'
  | 'flag'
  | 'phone'
  | 'catalogue'
  | 'isbn'
  | 'rare-books'
  | 'statistics'
  | 'accounts'
  | 'settings';

type CatalogAdminNavigationBadgeSection = 'catalogue' | 'isbn-queue' | 'isbndb-absent' | 'rare-books';
type CatalogAdminNavigationBadgeCounts = Partial<Record<CatalogAdminNavigationBadgeSection, number>>;

interface CatalogAdminNavItem {
  id: CatalogAdminSection;
  label: string;
  icon: CatalogAdminNavIcon;
}

interface CatalogAdminNavGroup {
  label: string;
  items: CatalogAdminNavItem[];
}

const IN_PROGRESS_SESSION_STATUS = 'InProgress';

const FULL_NAV_GROUPS: CatalogAdminNavGroup[] = [
  {
    label: 'Pendant la bourse',
    items: [
      {id: 'overview', label: 'Tableau de bord', icon: 'dashboard'},
      {id: 'sessions', label: 'Sessions de scan', icon: 'scan'},
      {id: 'dead-stock', label: 'Désengorgement', icon: 'dead-stock'},
      {id: 'not-found', label: 'Livres introuvables', icon: 'flag'},
      {id: 'manual-requests', label: 'Demandes manuelles', icon: 'phone'},
    ],
  },
  {
    label: 'Le fonds de livres',
    items: [
      {id: 'catalogue', label: 'Catalogue', icon: 'catalogue'},
      {id: 'isbn-queue', label: 'ISBN en cours d’analyse', icon: 'isbn'},
      {id: 'isbndb-absent', label: 'Livres hors ISBNdb', icon: 'isbn'},
      {id: 'rare-books', label: 'Livres rares', icon: 'rare-books'},
      {id: 'statistics', label: 'Statistiques', icon: 'statistics'},
    ],
  },
  {
    label: "Réservé à l'administration",
    items: [
      {id: 'accounts', label: 'Comptes & rôles', icon: 'accounts'},
      {id: 'settings', label: 'Paramètres', icon: 'settings'},
    ],
  },
];

const RARE_ONLY_NAV_GROUPS: CatalogAdminNavGroup[] = [
  {
    label: 'Le fonds de livres',
    items: [
      {id: 'rare-books', label: 'Livres rares', icon: 'rare-books'},
      {id: 'not-found', label: 'Livres introuvables', icon: 'flag'},
    ],
  },
];

interface CatalogBookCandidate {
  isbn13: string;
  workId: string | null;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  genre: string | null;
  coverUrl: string | null;
  source: string;
}

type CatalogBookCandidateStatus =
  | {state: 'loading'}
  | {state: 'found'; quantityAvailable: number}
  | {state: 'not-found'}
  | {state: 'error'};

type CatalogQuantityAdjustmentDirection = 'increase' | 'decrease';

type CatalogAdminSessionAlertState = 'none' | 'pending' | 'dispatching' | 'sent' | 'cancelled' | 'failed';

type CatalogAdminSessionConfirmationAction =
  | {kind: 'remove-movement'; sessionId: string; movementId: string}
  | {kind: 'reassign-session'; sessionId: string}
  | {kind: 'cancel-session'; sessionId: string}
  | {kind: 'cancel-session-alerts'; sessionId: string}
  | {kind: 'force-session-alerts'; sessionId: string};

interface CatalogAdminSessionConfirmation {
  action: CatalogAdminSessionConfirmationAction;
  title: string;
  description: string;
  confirmLabel: string;
  tone: 'accent' | 'danger';
}

interface CatalogQuantityConfirmation {
  book: CatalogAdminBook;
  direction: CatalogQuantityAdjustmentDirection;
  amount: number;
  nextQuantity: number;
  note: string;
}

@Component({
  selector: 'app-catalog-administration-page',
  standalone: false,
  templateUrl: './catalog-administration-page.component.html',
  styleUrls: ['./catalog-administration-page.component.scss', './fairs-evolution.scss', './statistics-workspace.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAdministrationPageComponent implements OnInit, OnDestroy {
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly isAdministrator: Signal<boolean>;
  readonly isRareBookManager: Signal<boolean>;
  readonly hasAdministrationAccess = computed(() => this.isAdministrator() || this.isRareBookManager());
  readonly authError: Signal<string | null>;
  readonly activeSection = signal<CatalogAdminSection>('overview');
  readonly loading = signal(false);
  readonly actionPending = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly navGroups = computed<CatalogAdminNavGroup[]>(() => {
    if (this.isAdministrator()) {
      return FULL_NAV_GROUPS;
    }
    return this.isRareBookManager() ? RARE_ONLY_NAV_GROUPS : [];
  });
  readonly navigationBadgeCounts = signal<CatalogAdminNavigationBadgeCounts>({});

  get navItems(): CatalogAdminNavItem[] {
    return this.navGroups().flatMap(group => group.items);
  }

  navBadge(section: CatalogAdminSection): string | null {
    if (section === 'not-found') {
      const count = this.notFoundSummary()?.openTargetCount ?? 0;
      return count > 0 ? this.formatNumber(count) : null;
    }
    if (section === 'manual-requests') {
      const count = this.manualRequestSummary()?.foundCount ?? 0;
      return count > 0 ? this.formatNumber(count) : null;
    }
    if (section === 'isbndb-absent') {
      const count = this.navigationBadgeCounts()['isbndb-absent'];
      return count && count > 0 ? this.formatNumber(count) : null;
    }

    const totalCount = section === 'sessions'
      ? this.sessionsPage() ? this.openSessionCount() : undefined
      : section === 'catalogue'
        ? this.catalogueBooksPage()?.totalCount ?? this.navigationBadgeCounts().catalogue
        : section === 'isbn-queue'
          ? this.isbnMetadataQueue()?.totalCount ?? this.navigationBadgeCounts()['isbn-queue']
        : section === 'rare-books'
          ? this.rareBooksFacade.page()
            ? this.rareBooksFacade.publishedCount()
            : this.navigationBadgeCounts()['rare-books']
        : undefined;

    return totalCount === undefined ? null : this.formatNumber(totalCount);
  }

  readonly overview = signal<CatalogAdminOverview | null>(null);
  readonly isbndbStatus = signal<CatalogAdminIsbndbStatus | null>(null);
  readonly isbndbAccessToken = signal('');
  readonly catalogueBooksPage = signal<CatalogAdminBookPage | null>(null);
  readonly isbnMetadataQueue = signal<CatalogAdminBookMetadataQueue | null>(null);
  readonly catalogueReferenceResults = signal<CatalogBookReference[]>([]);
  readonly catalogueAddCandidate = signal<CatalogBookCandidate | null>(null);
  readonly catalogueCandidateStatus = signal<CatalogBookCandidateStatus | null>(null);
  readonly catalogueLookupLoading = signal(false);
  readonly catalogueLookupError = signal<string | null>(null);
  readonly isbnApplicationFiles = signal<IsbnApplicationFilePreview[]>([]);
  readonly isbnApplicationPreview = signal<IsbnApplicationCombinedPreview | null>(null);
  readonly isbnApplicationHelpOpen = signal(false);
  readonly isbnApplicationReading = signal(false);
  readonly isbnApplicationRunning = signal(false);
  readonly isbnApplicationStatus = signal<'idle' | 'interrupted' | 'complete'>('idle');
  readonly isbnApplicationProgress = signal<IsbnApplicationImportProgress | null>(null);
  readonly isbnApplicationError = signal<string | null>(null);
  readonly isbnApplicationRefreshWarning = signal<string | null>(null);
  readonly catalogueConfirmation = signal<CatalogQuantityConfirmation | null>(null);
  readonly sessionConfirmation = signal<CatalogAdminSessionConfirmation | null>(null);
  readonly selectedBook = signal<CatalogAdminBook | null>(null);
  readonly pendingBookCover = signal<{
    isbn13: string;
    file: File;
    previewUrl: string | null;
  } | null>(null);
  readonly bookDetailReturnSection = signal<CatalogAdminSection>('catalogue');
  readonly notFoundSummary = signal<CatalogNotFoundSummary | null>(null);
  readonly manualRequestSummary = signal<CatalogManualRequestSummary | null>(null);
  readonly manualRequesterId = signal<string | null>(null);
  readonly manualRequestAccessToken = signal('');
  readonly selectedBookNotFoundSummary = signal<CatalogNotFoundSummary | null>(null);
  readonly notFoundAccessToken = signal('');
  readonly bookNotFoundDialog = signal<{target: CatalogNotFoundQueueTarget; action: CatalogNotFoundCloseAction} | null>(null);
  readonly fairsPage = signal<CatalogAdminFairPage | null>(null);
  readonly selectedFairStats = signal<CatalogAdminFairStats | null>(null);
  readonly statisticsTab = signal<CatalogAdminStatisticsTab>('fairs');
  readonly fairsEvolution = signal<CatalogAdminFairsEvolution | null>(null);
  readonly fairsEvolutionView = computed(() => {
    const evolution = this.fairsEvolution();
    return evolution ? buildFairsEvolutionView(evolution) : null;
  });
  readonly catalogueFlowStats = signal<CatalogAdminCatalogueFlowStats | null>(null);
  readonly volunteerStatistics = signal<CatalogAdminVolunteerStatistics | null>(null);
  readonly volunteerColumns = VOLUNTEER_COLUMNS;
  readonly volunteerSort = signal<VolunteerSort>(DEFAULT_VOLUNTEER_SORT);
  readonly sortedVolunteers = computed(() => {
    const stats = this.volunteerStatistics();
    return stats ? sortVolunteers(stats.volunteers, this.volunteerSort()) : [];
  });
  readonly maxVolunteerScanned = computed(() =>
    Math.max(0, ...(this.volunteerStatistics()?.volunteers.map(volunteer => volunteer.scannedCount) ?? [])));
  readonly maxVolunteerSold = computed(() =>
    Math.max(0, ...(this.volunteerStatistics()?.volunteers.map(volunteer => volunteer.soldQuantity) ?? [])));
  readonly sessionsPage = signal<CatalogAdminScanSessionPage | null>(null);
  readonly selectedSession = signal<CatalogAdminScanSession | null>(null);
  readonly sessionJournalOpen = signal(false);
  readonly alertsPage = signal<CatalogAdminAlertPage | null>(null);
  readonly membersPage = signal<CatalogAdminMemberPage | null>(null);
  readonly selectedMember = signal<CatalogAdminMemberDetail | null>(null);
  readonly memberDeletionConfirmation = signal<CatalogAdminMemberDetail | null>(null);
  readonly settings = signal<CatalogAdminSettings | null>(null);
  readonly accountsPage = signal<CatalogAdminAccountPage | null>(null);
  readonly accountsTab = signal<CatalogAdminAccountsTab>('volunteers');
  readonly accountErrorMessage = signal<string | null>(null);
  readonly checkoutPassageLookup = signal<CatalogAdminCheckoutPassageLookup | null>(null);
  readonly checkoutPassageError = signal<string | null>(null);
  readonly editingAccountId = signal<string | null>(null);
  readonly editingAccountRoles = signal<CatalogAdminAccountRole[]>([]);
  readonly showCreateAccount = signal(false);
  createAccountSubmitted = false;
  readonly temporaryPasswordGuidance = TEMPORARY_PASSWORD_GUIDANCE;

  readonly deadStockBooks = signal<CatalogDeadStockBook[]>([]);
  readonly deadStockGeneratedAt = signal<string | null>(null);

  minAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  minQuantity = DEFAULT_MIN_QUANTITY;

  catalogueSearch = '';
  catalogueIsbn = '';
  catalogueReferenceQuery = '';
  cataloguePage = 1;
  readonly cataloguePageSize = 5;
  metadataQueuePage = 1;
  readonly metadataQueuePageSize = 50;
  metadataQueueFilter: 'all' | 'pending' | 'failed' | 'not-found' = 'all';
  catalogueAddQuantity = 1;
  catalogueAddNote = 'Ajout depuis le catalogue';

  sessionStatus = '';
  sessionFrom = '';
  sessionTo = '';
  sessionPage = 1;
  readonly sessionPageSize = 25;
  sessionMode = 'AvailableNow';
  sessionFairId = '';
  sessionPreset: 'correctable' | 'all' | 'open' | 'alerts' = 'all';
  statisticsPeriod: CatalogAdminStatisticsPeriod = '30-days';
  statisticsFairId = '';

  alertStatus = '';
  alertPage = 1;
  readonly alertPageSize = 25;

  memberSearch = '';
  memberAlertStatus = '';
  memberPage = 1;
  readonly memberPageSize = 25;

  accountSearch = '';
  accountPage = 1;
  readonly accountPageSize = 25;
  checkoutPassageReference = '';
  checkoutPassageReason = '';
  readonly accountRoleOptions: {value: CatalogAdminAccountRole; label: string}[] = [
    {value: 'Tri', label: 'Tri'},
    {value: 'Caisse', label: 'Caisse'},
    {value: 'Administration', label: 'Administration'},
    {value: 'LivresRares', label: 'Livres rares'},
  ];
  readonly createAccountForm: CatalogAdminCreateAccountRequest = {
    email: '',
    firstName: '',
    lastName: '',
    temporaryPassword: '',
    roles: [],
  };

  readonly metadataForm = {
    title: '',
    authors: '',
    publisher: '',
    publicationYear: null as number | null,
    pages: null as number | null,
    edition: '',
    physicalFormat: '',
    language: '',
    genre: '',
    genres: [] as string[],
    summary: '',
    workId: '',
  };
  readonly catalogGenres = CURATED_CATALOG_GENRES;
  quantityCorrection = 0;
  quantityNote = '';
  withdrawalQuantity = 1;
  withdrawalNote = '';
  mergeTargetIsbn13 = '';
  mergeNote = '';
  announcementQuantities: Record<string, number> = {};
  announcementNote = '';

  settingsForm: CatalogAdminSettings = {
    duplicateThreshold: 5,
    demandSalesThreshold: 1,
    deadStockMinAgeDays: 180,
    deadStockMinQuantity: 3,
    watchlistMaxItems: 100,
    alertCooldownDays: 30,
    sessionIdleTimeoutMinutes: 30,
    alertDelayMinutes: 30,
    notFoundReportDailyLimit: 10,
    updatedAt: '',
    updatedBy: '',
  };
  revenueInput: number | null = null;
  deadStockAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  sessionIdleHours = 2;
  alertDelayHours = 2;

  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyed = new Subject<void>();
  private administrationInitialized = false;
  private catalogueSearchTimer: number | null = null;
  private bookCloseOpener: HTMLElement | null = null;

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogAdminApiService,
    private readonly catalogApi: CatalogApiService,
    private readonly meta: Meta,
    readonly rareBooksFacade: AdminRareBooksFacade,
  ) {
    this.initialized = this.auth.initialized;
    this.isAuthenticated = this.auth.isAuthenticated;
    this.isAdministrator = this.auth.isAdministrator;
    this.isRareBookManager = this.auth.isRareBookManager;
    this.authError = this.auth.error;
  }

  ngOnInit(): void {
    this.meta.updateTag({name: 'robots', content: 'noindex, nofollow'});
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntil(this.destroyed))
      .subscribe(([params, queryParams]) => this.applyRouteState(
        params.get('section'),
        queryParams.get('tab'),
        queryParams.get('demandeur'),
      ));
    void this.initialize();
  }

  ngOnDestroy(): void {
    this.clearPendingBookCover();
    this.destroyed.next();
    this.destroyed.complete();
    this.cancelCatalogueSearch();
  }

  async initialize(): Promise<void> {
    await this.auth.initialize();
    let navigationBadgeCountsTask: Promise<void> | undefined;
    if (this.auth.isAuthenticated() && this.hasAdministrationAccess()) {
      navigationBadgeCountsTask = this.loadNavigationBadgeCounts();
      if (!this.isAdministrator() && this.isRareBookManager()) {
        if (!(['rare-books', 'not-found'] as CatalogAdminSection[]).includes(this.activeSection())) {
          this.activeSection.set('rare-books');
          await this.router.navigate(['/administration', 'rare-books'], {replaceUrl: true});
        }
        await this.loadNotFoundSummary();
        if (this.activeSection() === 'not-found') {
          await this.loadSection('not-found');
        }
      } else {
        await this.loadOverview();
        await this.loadSessions();
        await this.loadDeadStock();
        await this.loadNotFoundSummary();
        await this.loadManualRequestSummary();

        if (!(['overview', 'sessions', 'dead-stock'] as CatalogAdminSection[]).includes(this.activeSection())) {
          await this.loadSection(this.activeSection());
        }
      }
    }
    this.administrationInitialized = true;
    await navigationBadgeCountsTask;
  }

  async login(): Promise<void> {
    this.clearFeedback();
    try {
      await this.auth.login(isCatalogAdministrationRoute(this.router.url) ? this.router.url : '/administration');
    } catch {
      this.showError('La connexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  dismissFeedback(): void {
    this.clearFeedback();
  }

  async selectSection(section: CatalogAdminSection): Promise<void> {
    if (section === 'members') {
      this.accountsTab.set('members');
      section = 'accounts';
    }

    if (!this.isAdministrator() && this.isRareBookManager() && section !== 'rare-books' && section !== 'not-found') {
      section = 'rare-books';
    }

    if (!this.hasAdministrationAccess()) {
      return;
    }

    if (isCatalogAdministrationRoute(this.router.url) && this.activeSection() !== section) {
      await this.router.navigate(['/administration', section]);
      return;
    }

    this.activeSection.set(section);
    if (!this.auth.isAuthenticated()) {
      return;
    }

    await this.loadSection(section);
  }

  selectMobileSection(section: string): void {
    if (isCatalogAdminSection(section)) {
      void this.selectSection(section);
    }
  }

  adminSectionUrl(section: CatalogAdminSection): string {
    return catalogAdminSectionPath(section);
  }

  private async loadSection(section: CatalogAdminSection): Promise<void> {
    switch (section) {
      case 'overview':
        await this.loadOverview();
        break;
      case 'sessions':
        await this.loadSessions();
        break;
      case 'dead-stock':
        await this.loadDeadStock();
        break;
      case 'not-found':
        await this.loadNotFoundWorkspace();
        break;
      case 'manual-requests':
        await this.loadManualRequestWorkspace();
        break;
      case 'catalogue':
        await this.loadCatalogue();
        break;
      case 'isbn-queue':
        await this.loadIsbnMetadataQueue();
        break;
      case 'isbndb-absent':
        await this.loadIsbndbAbsentWorkspace();
        break;
      case 'rare-books':
        break;
      case 'statistics':
        await this.loadStatisticsTab();
        break;
      case 'alerts':
        await this.loadAlerts();
        break;
      case 'accounts':
        await Promise.all([this.loadAccounts(), this.loadMembers()]);
        break;
      case 'settings':
        await this.loadSettings();
        break;
    }
  }

  private applyRouteState(sectionParam: string | null, statisticsTabParam: string | null, requesterParam: string | null): void {
    if (sectionParam && !isCatalogAdminSection(sectionParam)) {
      void this.router.navigate(['/administration', 'overview'], {replaceUrl: true});
      return;
    }

    const section = catalogAdminSectionFromRoute(sectionParam);
    const statisticsTab = catalogAdminStatisticsTabFromRoute(statisticsTabParam);
    const sectionChanged = this.activeSection() !== section;
    const statisticsTabChanged = this.statisticsTab() !== statisticsTab;

    this.activeSection.set(section);
    this.statisticsTab.set(statisticsTab);
    this.manualRequesterId.set(section === 'manual-requests' ? requesterParam : null);

    if (!this.administrationInitialized || !this.auth.isAuthenticated()) {
      return;
    }

    if (!this.hasAdministrationAccess()) {
      return;
    }

    if (!this.isAdministrator() && this.isRareBookManager() && section !== 'rare-books' && section !== 'not-found') {
      void this.router.navigate(['/administration', 'rare-books'], {replaceUrl: true});
      return;
    }

    if (sectionChanged) {
      void this.loadSection(section);
    } else if (statisticsTabChanged && section === 'statistics') {
      void this.loadStatisticsTab();
    }
  }

  async loadOverview(): Promise<void> {
    await this.run('overview', async token => {
      const overview = this.api.getOverview(token, undefined, undefined, true);
      this.overview.set(await firstValueFrom(overview));
      await this.loadIsbndbStatus(token);
    });
  }

  private async loadIsbndbStatus(token: string): Promise<void> {
    try {
      const status = await firstValueFrom(this.api.getIsbndbStatus(token));
      this.isbndbStatus.set(status);
      this.navigationBadgeCounts.update(counts => ({...counts, 'isbndb-absent': status.absentToComplete}));
    } catch {
      this.isbndbStatus.set(null);
    }
  }

  toggleMetadataGenre(genre: string): void {
    const current = this.metadataForm.genres;
    this.metadataForm.genres = current.includes(genre)
      ? current.filter(item => item !== genre)
      : [...current, genre];
    this.metadataForm.genre = this.metadataForm.genres[0] ?? '';
  }

  metadataPlaceholder(book: CatalogAdminBook, value: string | number | null): string {
    return book.isAbsentFromIsbndb && (value === null || value === '') ? '· manquant' : '';
  }

  provenanceSourceLabel(source: string | null): string {
    const labels: Record<string, string> = {
      Bnf: 'BnF',
      OpenLibrary: 'Open Library',
      GoogleBooks: 'Google Books',
    };
    return source ? labels[source] ?? source : 'Aucune';
  }

  provenanceBadgeLabel(book: CatalogAdminBook): string {
    if (book.isIsbndbSourced) return 'ISBNdb';
    if (book.isAbsentFromIsbndb) {
      return book.isbndbCompletedAt ? 'Hors ISBNdb · complétée' : 'Hors ISBNdb · à compléter';
    }
    return 'Secours · en attente du rattrapage';
  }

  isbndbLookupLabel(book: CatalogAdminBook): string {
    return book.isIsbndbSourced ? 'Trouvé' : book.isAbsentFromIsbndb ? 'Livre inconnu' : 'Pas encore interrogé';
  }

  canRecheckIsbndbBook(book: CatalogAdminBook): boolean {
    return !book.isIsbndbSourced && !book.isbndbCompletedAt && !this.isIsbndbQuotaExhausted();
  }

  isIsbndbQuotaExhausted(): boolean {
    return this.isbndbStatus()?.status === 'Exhausted';
  }

  isbndbExhaustedUntil(): string {
    const status = this.isbndbStatus();
    const value = status?.exhaustedUntil ?? status?.resetsAt;
    if (!value) return '—';
    const date = new Date(value);
    return Number.isFinite(date.getTime())
      ? new Intl.DateTimeFormat('fr-FR', {
        timeZone: 'Europe/Paris', hour: '2-digit', minute: '2-digit',
      }).format(date)
      : '—';
  }

  private formatIsbndbParisTime(value: string | null): string {
    const date = value ? new Date(value) : null;
    return date && Number.isFinite(date.getTime())
      ? new Intl.DateTimeFormat('fr-FR', {
        timeZone: 'Europe/Paris', hour: '2-digit', minute: '2-digit',
      }).format(date)
      : '—';
  }

  async loadCatalogue(): Promise<void> {
    this.cancelCatalogueSearch();
    await this.run('catalogue', async token => {
      await this.loadCataloguePage(token);
    });
  }

  async loadIsbnMetadataQueue(): Promise<void> {
    await this.run('isbn-metadata-queue', async token => {
      await this.loadIsbnMetadataQueuePage(token);
    });
  }

  private async loadIsbndbAbsentWorkspace(): Promise<void> {
    await this.run('isbndb-absent-access', async token => {
      this.isbndbAccessToken.set(token);
    });
  }

  private metadataUpdateRequest(
    fields: CatalogAdminBookMetadataField[] = [
      'Title', 'Authors', 'Publisher', 'PublicationYear', 'PhysicalFormat', 'Language',
      'Genre', 'WorkId', 'Summary', 'Pages', 'Edition',
    ],
  ): CatalogAdminBookMetadataUpdate {
    const genres = [...this.metadataForm.genres];
    return {
      title: this.optional(this.metadataForm.title),
      authors: this.optional(this.metadataForm.authors),
      publisher: this.optional(this.metadataForm.publisher),
      publicationYear: this.metadataForm.publicationYear,
      physicalFormat: this.optional(this.metadataForm.physicalFormat),
      language: this.optional(this.metadataForm.language),
      genre: genres[0] ?? null,
      coverUrl: null,
      workId: this.optional(this.metadataForm.workId),
      fields,
      summary: this.optional(this.metadataForm.summary),
      pages: this.metadataForm.pages,
      edition: this.optional(this.metadataForm.edition),
      genres,
    };
  }

  async setIsbnMetadataQueueFilter(filter: 'all' | 'pending' | 'failed' | 'not-found'): Promise<void> {
    if (this.metadataQueueFilter === filter) {
      return;
    }

    this.metadataQueueFilter = filter;
    this.metadataQueuePage = 1;
    await this.loadIsbnMetadataQueue();
  }

  goToIsbnMetadataQueuePage(page: number): void {
    if (!this.validPage(page, this.isbnMetadataQueue())) {
      return;
    }
    this.metadataQueuePage = page;
    void this.loadIsbnMetadataQueue();
  }

  submitCatalogueSearch(): void {
    this.cancelCatalogueSearch();
    this.cataloguePage = 1;
    void this.loadCatalogue();
  }

  scheduleCatalogueSearch(event: Event): void {
    if (event.target instanceof HTMLInputElement) {
      this.catalogueSearch = event.target.value;
    }
    this.cancelCatalogueSearch();
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.catalogueSearchTimer = window.setTimeout(() => {
      this.catalogueSearchTimer = null;
      this.cataloguePage = 1;
      void this.loadCatalogue();
    }, CATALOGUE_SEARCH_DEBOUNCE_MS);
  }

  catalogueLoading(): boolean {
    return this.loading() && this.actionPending() === 'catalogue';
  }

  async lookupCatalogueIsbn(): Promise<void> {
    const isbn = this.normalizeIsbn(this.catalogueIsbn);
    this.catalogueIsbn = isbn;
    if (!this.isValidIsbn(isbn)) {
      this.catalogueLookupError.set('Saisissez un ISBN-10 ou ISBN-13 valide.');
      this.catalogueReferenceResults.set([]);
      this.catalogueAddCandidate.set(null);
      this.catalogueCandidateStatus.set(null);
      return;
    }

    this.catalogueReferenceResults.set([]);
    this.catalogueAddCandidate.set(null);
    this.catalogueCandidateStatus.set(null);
    await this.runCatalogueLookup(async () => {
      const response = await firstValueFrom(this.catalogApi.searchReferences(isbn, 1, 20));
      const isbn13 = this.isbn13Equivalent(isbn);
      const reference = response.items.find(item => this.normalizeIsbn(item.isbn13 || '') === (isbn13 ?? isbn));
      const candidate = reference ? this.toCatalogueCandidate(reference) : null;
      if (!reference || !candidate) {
        this.catalogueLookupError.set('Aucune notice bibliographique ne correspond à cet ISBN.');
        return;
      }
      this.catalogueReferenceResults.set([reference]);
      await this.selectCatalogueCandidate(reference);
    });
  }

  async searchCatalogueReferences(): Promise<void> {
    const query = this.catalogueReferenceQuery.trim();
    if (query.length < 2) {
      this.catalogueLookupError.set('La recherche doit contenir au moins deux caractères.');
      this.catalogueReferenceResults.set([]);
      this.catalogueAddCandidate.set(null);
      this.catalogueCandidateStatus.set(null);
      return;
    }

    this.catalogueReferenceQuery = query;
    this.catalogueReferenceResults.set([]);
    this.catalogueAddCandidate.set(null);
    this.catalogueCandidateStatus.set(null);
    await this.runCatalogueLookup(async () => {
      const response = await firstValueFrom(this.catalogApi.searchReferences(query, 1, 20));
      this.catalogueReferenceResults.set(response.items);
      if (response.items.length === 0) {
        this.catalogueLookupError.set('Aucune référence ne correspond à cette recherche.');
      }
    });
  }

  async selectCatalogueCandidate(reference: CatalogBookReference): Promise<void> {
    const candidate = this.toCatalogueCandidate(reference);
    if (!candidate) {
      return;
    }

    this.catalogueAddCandidate.set(candidate);
    this.catalogueCandidateStatus.set({state: 'loading'});
    this.catalogueAddQuantity = 1;
    this.catalogueAddNote = 'Ajout depuis le catalogue';
    this.catalogueLookupError.set(null);
    await this.loadCatalogueCandidateStatus(candidate.isbn13);
  }

  async readIsbnApplicationFile(event: Event): Promise<void> {
    if (this.isbnApplicationRunning() || this.isbnApplicationReading() || !this.isAdministrator()) {
      return;
    }

    const input = event.currentTarget as HTMLInputElement;
    const selectedFiles = Array.from(input.files ?? []);
    if (selectedFiles.length === 0) {
      return;
    }

    this.isbnApplicationError.set(null);
    this.isbnApplicationReading.set(true);
    let currentFile = selectedFiles[0];
    try {
      const parsedFiles: IsbnApplicationFilePreview[] = [];
      for (const file of selectedFiles) {
        currentFile = file;
        if (!file.name.toLowerCase().endsWith('.csv')) {
          throw new IsbnApplicationCsvFormatError('ce fichier n’a pas l’extension .csv.');
        }

        parsedFiles.push({
          fileName: file.name,
          preview: parseIsbnApplicationCsv(await file.text()),
        });
      }

      const files = [...this.isbnApplicationFiles(), ...parsedFiles];
      this.isbnApplicationFiles.set(files);
      this.isbnApplicationPreview.set(this.combineIsbnApplicationFiles(files));
      this.isbnApplicationStatus.set('idle');
      this.isbnApplicationProgress.set(null);
      this.isbnApplicationRefreshWarning.set(null);
    } catch (error: unknown) {
      const message = error instanceof IsbnApplicationCsvFormatError
        ? error.message
        : 'ce fichier n’a pas pu être lu.';
      this.isbnApplicationError.set(`« ${currentFile.name} » : ${message}`);
    } finally {
      this.isbnApplicationReading.set(false);
      input.value = '';
    }
  }

  toggleIsbnApplicationHelp(): void {
    this.isbnApplicationHelpOpen.update(open => !open);
  }

  removeIsbnApplicationFile(index: number): void {
    if (this.isbnApplicationRunning() || this.isbnApplicationReading()) {
      return;
    }

    const files = this.isbnApplicationFiles().filter((_, fileIndex) => fileIndex !== index);
    this.isbnApplicationFiles.set(files);
    this.isbnApplicationPreview.set(files.length > 0 ? this.combineIsbnApplicationFiles(files) : null);
    this.isbnApplicationStatus.set('idle');
    this.isbnApplicationProgress.set(null);
    this.isbnApplicationError.set(null);
    this.isbnApplicationRefreshWarning.set(null);
  }

  clearIsbnApplicationFiles(): void {
    if (this.isbnApplicationRunning() || this.isbnApplicationReading()) {
      return;
    }

    this.isbnApplicationFiles.set([]);
    this.isbnApplicationPreview.set(null);
    this.isbnApplicationStatus.set('idle');
    this.isbnApplicationProgress.set(null);
    this.isbnApplicationError.set(null);
    this.isbnApplicationRefreshWarning.set(null);
    const input = this.document.getElementById('isbn-application-file') as HTMLInputElement | null;
    if (input) {
      input.value = '';
    }
  }

  private combineIsbnApplicationFiles(files: readonly IsbnApplicationFilePreview[]): IsbnApplicationCombinedPreview {
    const uniqueIsbns = new Set<string>();
    const invalidExamples: IsbnApplicationInvalidExample[] = [];
    let dataRowCount = 0;
    let emptyRowCount = 0;
    let duplicateCount = 0;
    let invalidCount = 0;

    for (const file of files) {
      dataRowCount += file.preview.dataRowCount;
      emptyRowCount += file.preview.emptyRowCount;
      duplicateCount += file.preview.duplicateCount;
      invalidCount += file.preview.invalidCount;

      for (const example of file.preview.invalidExamples) {
        if (invalidExamples.length >= 10) {
          break;
        }
        invalidExamples.push({...example, fileName: file.fileName});
      }

      for (const isbn of file.preview.isbns) {
        if (uniqueIsbns.has(isbn)) {
          duplicateCount += 1;
        } else {
          uniqueIsbns.add(isbn);
        }
      }
    }

    return {
      fileCount: files.length,
      dataRowCount,
      emptyRowCount,
      duplicateCount,
      invalidCount,
      invalidExamples,
      isbns: [...uniqueIsbns],
    };
  }

  async importIsbnApplication(): Promise<void> {
    const preview = this.isbnApplicationPreview();
    if (!preview || preview.isbns.length === 0 || this.isbnApplicationRunning() || !this.isAdministrator()) {
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.isbnApplicationError.set('Reconnectez-vous avec un compte administrateur pour lancer l’import.');
      return;
    }

    const totalBatches = Math.ceil(preview.isbns.length / ISBN_APPLICATION_IMPORT_BATCH_SIZE);
    this.isbnApplicationRunning.set(true);
    this.isbnApplicationStatus.set('idle');
    this.isbnApplicationError.set(null);
    this.isbnApplicationRefreshWarning.set(null);
    this.isbnApplicationProgress.set({
      processedCount: 0,
      totalCount: preview.isbns.length,
      completedBatches: 0,
      totalBatches,
      createdCount: 0,
      alreadyPresentCount: 0,
    });

    try {
      const token = await this.auth.getApiAccessToken();
      for (let offset = 0; offset < preview.isbns.length; offset += ISBN_APPLICATION_IMPORT_BATCH_SIZE) {
        const batch = preview.isbns.slice(offset, offset + ISBN_APPLICATION_IMPORT_BATCH_SIZE);
        const result = await firstValueFrom(this.api.importBooks(token, {isbns: batch}));
        this.validateIsbnImportBatchResponse(result, batch.length);

        this.isbnApplicationProgress.update(progress => progress ? {
          ...progress,
          processedCount: progress.processedCount + batch.length,
          completedBatches: progress.completedBatches + 1,
          createdCount: progress.createdCount + result.createdCount,
          alreadyPresentCount: progress.alreadyPresentCount + result.alreadyPresentCount,
        } : progress);
      }

      this.isbnApplicationStatus.set('complete');
      try {
        await this.loadCataloguePage(token);
      } catch {
        this.isbnApplicationRefreshWarning.set(
          'L’import est terminé, mais la liste du catalogue n’a pas pu être actualisée. Utilisez « Actualiser » pour la recharger.',
        );
      }
    } catch (error: unknown) {
      this.isbnApplicationStatus.set('interrupted');
      const progress = this.isbnApplicationProgress();
      const description = error instanceof CatalogAuthenticationRedirectStartedError
        ? 'Redirection vers Microsoft pour renouveler votre session…'
        : error instanceof HttpErrorResponse
          ? this.describeError(error)
          : error instanceof Error
            ? error.message
            : this.describeError(error);
      const completedBatches = progress?.completedBatches ?? 0;
      this.isbnApplicationError.set(
        `L’import s’est arrêté après ${this.formatNumber(completedBatches)} lot${completedBatches === 1 ? '' : 's'} sur ${this.formatNumber(totalBatches)}. ${description} Relancez l’import complet : les fiches déjà créées seront reconnues comme déjà présentes.`,
      );
    } finally {
      this.isbnApplicationRunning.set(false);
    }
  }

  private validateIsbnImportBatchResponse(result: CatalogAdminBookImportResponse, batchSize: number): void {
    if (result.requestedCount !== batchSize
      || result.createdCount + result.alreadyPresentCount !== batchSize
      || result.results.length !== batchSize) {
      throw new Error('La réponse du serveur ne correspond pas au lot envoyé.');
    }
  }

  adjustCatalogueAddQuantity(delta: number): void {
    const quantity = Number(this.catalogueAddQuantity);
    const current = Number.isFinite(quantity) ? Math.trunc(quantity) : 0;
    this.catalogueAddQuantity = Math.min(
      MAX_BOOK_QUANTITY,
      Math.max(0, current + delta),
    );
  }

  catalogueCandidateQuantity(): number {
    const status = this.catalogueCandidateStatus();
    return status?.state === 'found' ? status.quantityAvailable : 0;
  }

  async addCatalogueCandidate(): Promise<void> {
    const candidate = this.catalogueAddCandidate();
    const quantity = Number(this.catalogueAddQuantity);
    const note = this.catalogueAddNote.trim();
    if (!candidate || !Number.isInteger(quantity) || quantity < 0 || quantity > MAX_BOOK_QUANTITY || !note) {
      this.catalogueLookupError.set('La quantité doit être un entier positif ou nul et la note est obligatoire.');
      return;
    }

    if (note.length > 500) {
      this.catalogueLookupError.set('La note ne peut pas dépasser 500 caractères.');
      return;
    }

    await this.run('add-catalogue-book', async token => {
      await firstValueFrom(this.api.addBook(token, {
        isbn13: candidate.isbn13,
        quantityAvailable: quantity,
        note,
        title: candidate.title,
        authors: candidate.authors,
        publisher: candidate.publisher,
        publicationYear: candidate.publicationYear,
        physicalFormat: null,
        language: null,
        genre: candidate.genre,
        coverUrl: candidate.coverUrl,
        workId: candidate.workId,
      }));
      this.catalogueAddCandidate.set(null);
      this.catalogueCandidateStatus.set(null);
      this.catalogueReferenceResults.set([]);
      this.showSuccess('La fiche a été ajoutée au catalogue.');
      await this.loadCataloguePage(token);
    });
  }

  async adjustCatalogueQuantity(
    book: CatalogAdminBook,
    direction: CatalogQuantityAdjustmentDirection,
  ): Promise<void> {
    const amount = CATALOGUE_ADJUSTMENT_QUANTITY;
    const note = CATALOGUE_ADJUSTMENT_NOTE;

    const nextQuantity = direction === 'increase'
      ? book.quantityAvailable + amount
      : book.quantityAvailable - amount;
    if (nextQuantity < 0) {
      this.showError('Impossible de retirer davantage d’exemplaires que le stock disponible.');
      return;
    }

    if (nextQuantity > MAX_BOOK_QUANTITY) {
      this.showError('Le stock disponible ne peut pas dépasser 100 000 exemplaires.');
      return;
    }

    this.catalogueConfirmation.set({book, direction, amount, nextQuantity, note});
  }

  async confirmCatalogueAdjustment(): Promise<void> {
    const confirmation = this.catalogueConfirmation();
    if (!confirmation || this.loading()) {
      return;
    }

    this.catalogueConfirmation.set(null);
    await this.run('catalogue-quantity', async token => {
      await firstValueFrom(this.api.correctQuantity(token, confirmation.book.isbn13, {
        quantityAvailable: confirmation.nextQuantity,
        note: confirmation.note,
      }));
      this.showSuccess(`Le stock de « ${confirmation.book.title || confirmation.book.isbn13} » est maintenant de ${confirmation.nextQuantity} exemplaire(s).`);
      await this.loadCataloguePage(token);
    });
  }

  cancelCatalogueAdjustment(): void {
    this.catalogueConfirmation.set(null);
  }

  @HostListener('document:keydown.escape')
  closeOpenDialogOnEscape(): void {
    if (this.sessionConfirmation()) {
      this.cancelSessionConfirmation();
      return;
    }

    if (this.catalogueConfirmation()) {
      this.cancelCatalogueAdjustment();
      return;
    }

    if (this.memberDeletionConfirmation()) {
      this.cancelMemberDeletion();
      return;
    }

    if (this.selectedMember()) {
      this.closeMemberDetail();
      return;
    }

    if (this.selectedSession()) {
      this.closeSessionDialog();
    }
  }

  closeSessionDialog(): void {
    this.cancelSessionConfirmation();
    this.sessionJournalOpen.set(false);
    this.selectedSession.set(null);
  }

  async openBook(
    isbn13: string,
    returnSection: CatalogAdminSection = 'catalogue',
  ): Promise<void> {
    this.clearPendingBookCover();
    this.bookDetailReturnSection.set(returnSection);
    if ((returnSection === 'isbn-queue' || returnSection === 'isbndb-absent') && this.activeSection() !== 'catalogue') {
      await this.router.navigate(['/administration', 'catalogue']);
    }
    this.activeSection.set('catalogue');
    await this.run('book-detail', token => this.loadBookDetail(token, isbn13));
  }

  private async loadBookDetail(token: string, isbn13: string): Promise<void> {
    const [book, reportSummary] = await Promise.all([
      firstValueFrom(this.api.getBook(token, isbn13)),
      firstValueFrom(this.api.getNotFoundSummary(token, {isbn13})),
    ]);
    this.selectedBook.set(book);
    this.selectedBookNotFoundSummary.set(reportSummary);
    this.quantityCorrection = book.quantityAvailable;
    this.quantityNote = '';
    this.withdrawalQuantity = 1;
    this.withdrawalNote = '';
    this.mergeTargetIsbn13 = '';
    this.mergeNote = '';
    this.announcementQuantities = Object.fromEntries(
      book.announcements.map(announcement => [announcement.id, announcement.quantity]),
    );
    Object.assign(this.metadataForm, {
      title: book.title || '',
      authors: book.authors || '',
      publisher: book.publisher || '',
      publicationYear: book.publicationYear,
      pages: book.pages,
      edition: book.edition || '',
      physicalFormat: book.physicalFormat || '',
      language: book.language || '',
      genre: book.genre || '',
      genres: mergeCatalogGenres(book.genres, book.genre || ''),
      summary: book.summary || '',
      workId: book.workId || '',
    });
    await this.rareBooksFacade.resolveCatalogRelation(book.isbn13);
  }

  closeBookDetail(): void {
    this.selectedBook.set(null);
    const returnSection = this.bookDetailReturnSection();
    this.bookDetailReturnSection.set('catalogue');
    if (returnSection === 'isbn-queue' || returnSection === 'isbndb-absent') {
      void this.selectSection(returnSection);
    }
  }

  async retryIsbnMetadata(item: CatalogAdminBookMetadataQueueItem): Promise<void> {
    await this.run('isbn-metadata-retry', async token => {
      await firstValueFrom(this.api.retryBookMetadata(token, item.isbn13));
      this.showSuccess(`Un nouvel essai a été lancé pour l’ISBN ${item.isbn13}.`);
      await this.loadIsbnMetadataQueuePage(token);
    });
  }

  async setIsbndbCompletion(book: CatalogAdminBook, completed: boolean): Promise<void> {
    if (!book.isAbsentFromIsbndb ||
        (completed && (!this.metadataForm.title.trim() || !this.metadataForm.authors.trim()))) {
      return;
    }

    const returnSection = this.bookDetailReturnSection();
    await this.run('isbndb-completion', async token => {
      if (completed && (
        this.optional(this.metadataForm.title) !== this.optional(book.title || '') ||
        this.optional(this.metadataForm.authors) !== this.optional(book.authors || '')
      )) {
        await firstValueFrom(this.api.updateMetadata(
          token,
          book.isbn13,
          this.metadataUpdateRequest(['Title', 'Authors']),
        ));
      }
      await firstValueFrom(this.api.setIsbndbCompletion(token, book.isbn13, completed));
      this.showSuccess(completed ? 'La fiche est marquée comme complétée.' : 'La fiche est rouverte.');
      await this.loadBookDetail(token, book.isbn13);
      await this.loadIsbndbStatus(token);
      this.bookDetailReturnSection.set(returnSection);
    });
  }

  async recheckIsbndbBook(book: CatalogAdminBook): Promise<void> {
    if (book.isbndbCompletedAt || this.isIsbndbQuotaExhausted()) return;
    const returnSection = this.bookDetailReturnSection();
    await this.run(
      'isbndb-recheck',
      async token => {
        const result = await firstValueFrom(this.api.recheckIsbndbBook(token, book.isbn13));
        if (result.outcome === 'QuotaExhausted') {
          this.showError(`Quota épuisé jusqu’à ${this.formatIsbndbParisTime(result.resetsAt)}.`);
        } else if (result.outcome === 'Unavailable') {
          this.showError('Le service ISBNdb est momentanément indisponible. Réessayez plus tard.');
        } else {
          this.showSuccess(result.outcome === 'Found'
            ? 'Une notice ISBNdb a été trouvée et la fiche a été actualisée.'
            : 'ISBNdb ne connaît toujours pas ce livre.');
          await this.loadBookDetail(token, book.isbn13);
          this.bookDetailReturnSection.set(returnSection);
        }
        await this.loadIsbndbStatus(token);
      },
      this.errorMessage,
      error => this.describeIsbndbRecheckError(error),
    );
  }

  async deleteIsbnMetadataCandidate(item: CatalogAdminBookMetadataQueueItem): Promise<void> {
    if (!this.confirmAction(
      `Retirer l’ISBN ${item.isbn13} de la file ? Le stock disponible sera annulé et l’historique conservé.`,
    )) {
      return;
    }

    await this.run('isbn-metadata-delete', async token => {
      await firstValueFrom(this.api.deleteBook(token, item.isbn13));
      this.showSuccess(`L’ISBN ${item.isbn13} a été retiré de la file d’analyse.`);
      await this.loadIsbnMetadataQueuePage(token);
    });
  }

  openBookNotFoundDialog(book: CatalogAdminBook, action: CatalogNotFoundCloseAction, event: Event): void {
    const summary = this.selectedBookNotFoundSummary();
    if (!summary || summary.openReportCount <= 0) {
      return;
    }
    const target: CatalogNotFoundQueueTarget = {
      kind: 'edition',
      isbn13: book.isbn13,
      rareBookId: null,
      title: book.title || 'Titre inconnu',
      authors: book.authors,
      publisher: book.publisher,
      publicationYear: book.publicationYear,
      coverUrl: book.coverUrl,
      genre: book.genre,
      quantityAvailable: book.quantityAvailable,
      reportCount: summary.openReportCount,
      memberCount: summary.openReportCount,
      firstReportedAt: summary.firstReportedAt || new Date().toISOString(),
      lastReportedAt: summary.latestComment?.reportedAt || summary.firstReportedAt || new Date().toISOString(),
      overdue: false,
      comments: summary.latestComment ? [summary.latestComment] : [],
    };
    this.bookCloseOpener = event.currentTarget instanceof HTMLElement ? event.currentTarget : null;
    this.bookNotFoundDialog.set({target, action});
  }

  closeBookNotFoundDialog(): void {
    this.bookNotFoundDialog.set(null);
    this.bookCloseOpener?.focus();
    this.bookCloseOpener = null;
  }

  async closeNotFoundFromBook(confirmation: NotFoundCloseConfirmation): Promise<void> {
    const dialog = this.bookNotFoundDialog();
    if (!dialog) {
      return;
    }

    await this.run('book-not-found-close', async token => {
      const reference = dialog.target.isbn13;
      if (!reference) {
        return;
      }
      await firstValueFrom(this.api.closeNotFound(
        token,
        {kind: 'edition', reference},
        confirmation.action,
        confirmation.request,
      ));
      this.bookNotFoundDialog.set(null);
      const [book, summary, overviewSummary] = await Promise.all([
        firstValueFrom(this.api.getBook(token, reference)),
        firstValueFrom(this.api.getNotFoundSummary(token, {isbn13: reference})),
        firstValueFrom(this.api.getNotFoundSummary(token)),
      ]);
      this.selectedBook.set(book);
      this.selectedBookNotFoundSummary.set(summary);
      this.notFoundSummary.set(overviewSummary);
      if (this.bookCloseOpener?.isConnected) {
        this.bookCloseOpener.focus();
      } else {
        this.document.querySelector<HTMLElement>('.detail-heading h1')?.focus();
      }
      this.bookCloseOpener = null;
    });
  }

  async loadNotFoundSummary(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    try {
      const token = await this.auth.getApiAccessToken();
      this.notFoundSummary.set(await firstValueFrom(this.api.getNotFoundSummary(token)));
    } catch {
      this.notFoundSummary.set(null);
    }
  }

  onNotFoundCountsChanged(summary: CatalogNotFoundSummary): void {
    this.notFoundSummary.set(summary);
  }

  onManualRequestCountsChanged(summary: CatalogManualRequestSummary): void {
    this.manualRequestSummary.set(summary);
  }

  openManualRequester(requesterId: string | null): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {demandeur: requesterId ?? 'nouveau'},
      queryParamsHandling: 'merge',
    });
  }

  closeManualRequester(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {demandeur: null},
      queryParamsHandling: 'merge',
    });
  }

  private async loadNotFoundWorkspace(): Promise<void> {
    await this.run('not-found-access', async token => {
      this.notFoundAccessToken.set(token);
      this.notFoundSummary.set(await firstValueFrom(this.api.getNotFoundSummary(token)));
    });
  }

  private async loadManualRequestSummary(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    try {
      const token = await this.auth.getApiAccessToken();
      this.manualRequestSummary.set(await firstValueFrom(this.api.getManualRequestSummary(token)));
    } catch {
      this.manualRequestSummary.set(null);
    }
  }

  private async loadManualRequestWorkspace(): Promise<void> {
    await this.run('manual-requests-access', async token => {
      this.manualRequestAccessToken.set(token);
      this.manualRequestSummary.set(await firstValueFrom(this.api.getManualRequestSummary(token)));
    });
  }

  async openRareBookFromCatalogue(book: CatalogAdminBook): Promise<void> {
    if (await this.rareBooksFacade.openOrCreateFromCatalogBook(book)) {
      await this.selectSection('rare-books');
    }
  }

  bookCoverPreviewUrl(book: CatalogAdminBook): string | null {
    const pending = this.pendingBookCover();
    return pending?.isbn13 === book.isbn13 && pending.previewUrl
      ? pending.previewUrl
      : book.coverUrl;
  }

  hasPendingBookCover(book: CatalogAdminBook): boolean {
    return this.pendingBookCover()?.isbn13 === book.isbn13;
  }

  selectBookCover(event: Event, book: CatalogAdminBook): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];
    if (!input || !file) {
      return;
    }
    input.value = '';

    if (book.redirectedToIsbn13) {
      return;
    }

    if (!SUPPORTED_BOOK_COVER_TYPES.has(file.type)) {
      this.clearPendingBookCover(book);
      this.showError('Choisissez une image JPEG, PNG ou WebP.');
      return;
    }

    if (file.size <= 0 || file.size > MAX_BOOK_COVER_SIZE_BYTES) {
      this.clearPendingBookCover(book);
      this.showError('L’image doit peser 8 Mo maximum.');
      return;
    }

    this.clearPendingBookCover();
    const previewUrl = isPlatformBrowser(this.platformId) ? URL.createObjectURL(file) : null;
    this.pendingBookCover.set({isbn13: book.isbn13, file, previewUrl});
    this.clearFeedback();
  }

  clearPendingBookCover(book?: CatalogAdminBook): void {
    const pending = this.pendingBookCover();
    if (book && pending?.isbn13 !== book.isbn13) {
      return;
    }

    if (pending?.previewUrl && isPlatformBrowser(this.platformId)) {
      URL.revokeObjectURL(pending.previewUrl);
    }
    this.pendingBookCover.set(null);
  }

  async uploadBookCover(book: CatalogAdminBook): Promise<void> {
    const pending = this.pendingBookCover();
    if (!pending || pending.isbn13 !== book.isbn13 || book.redirectedToIsbn13) {
      return;
    }

    const returnSection = this.bookDetailReturnSection();
    await this.run('book-cover-upload', async token => {
      await firstValueFrom(this.api.uploadBookCover(token, book.isbn13, pending.file));
      this.clearPendingBookCover(book);
      this.showSuccess('La couverture a été enregistrée.');
      await this.openBook(book.isbn13, returnSection);
    });
  }

  async updateMetadata(book: CatalogAdminBook): Promise<void> {
    const returnSection = this.bookDetailReturnSection();
    await this.run('metadata', async token => {
      await firstValueFrom(this.api.updateMetadata(token, book.isbn13, this.metadataUpdateRequest()));
      this.showSuccess('Les métadonnées ont été enregistrées.');
      await this.loadBookDetail(token, book.isbn13);
      this.bookDetailReturnSection.set(returnSection);
      if (returnSection === 'isbn-queue') {
        await this.loadIsbnMetadataQueuePage(token);
      } else if (returnSection !== 'isbndb-absent') {
        await this.loadCataloguePage(token);
      }
    });
  }

  async correctBookQuantity(book: CatalogAdminBook): Promise<void> {
    const quantity = Number(this.quantityCorrection);
    if (!Number.isInteger(quantity) || quantity < 0 || !this.quantityNote.trim()) {
      this.showError('La quantité doit être un entier positif et la note est obligatoire.');
      return;
    }

    if (!this.confirmAction(`Corriger la quantité de « ${book.title || book.isbn13} » ?`)) {
      return;
    }

    await this.run('quantity', async token => {
      await firstValueFrom(this.api.correctQuantity(token, book.isbn13, {
        quantityAvailable: quantity,
        note: this.quantityNote.trim(),
      }));
      this.showSuccess('La correction de stock a été journalisée.');
      await this.openBook(book.isbn13);
      await this.loadCatalogue();
    });
  }

  async withdrawBook(book: CatalogAdminBook): Promise<void> {
    const quantity = Number(this.withdrawalQuantity);
    if (!Number.isInteger(quantity) || quantity <= 0 || !this.withdrawalNote.trim()) {
      this.showError('La quantité retirée doit être positive et la note est obligatoire.');
      return;
    }

    if (!this.confirmAction(`Retirer ${quantity} exemplaire(s) de « ${book.title || book.isbn13} » ?`)) {
      return;
    }

    await this.run('withdraw', async token => {
      await firstValueFrom(this.api.withdraw(token, book.isbn13, {
        quantity,
        note: this.withdrawalNote.trim(),
      }));
      this.showSuccess('Le retrait a été journalisé.');
      await this.openBook(book.isbn13);
      await this.loadCatalogue();
    });
  }

  async correctAnnouncement(book: CatalogAdminBook, announcementId: string): Promise<void> {
    const quantity = Number(this.announcementQuantities[announcementId]);
    if (!Number.isInteger(quantity) || quantity < 0 || !this.announcementNote.trim()) {
      this.showError('La quantité annoncée doit être positive ou nulle et la note est obligatoire.');
      return;
    }

    if (!this.confirmAction('Corriger cette annonce ?')) {
      return;
    }

    await this.run('announcement', async token => {
      await firstValueFrom(this.api.correctAnnouncement(token, announcementId, {
        quantity,
        note: this.announcementNote.trim(),
      }));
      this.showSuccess('La correction de l’annonce a été journalisée.');
      await this.openBook(book.isbn13);
      await this.loadCatalogue();
    });
  }

  setAnnouncementQuantity(announcementId: string, value: number | string): void {
    this.announcementQuantities[announcementId] = Number(value);
  }

  async toggleVisibility(book: CatalogAdminBook): Promise<void> {
    await this.run('visibility', async token => {
      await firstValueFrom(this.api.setVisibility(token, book.isbn13, !book.isHidden));
      this.showSuccess(book.isHidden ? 'La fiche est à nouveau visible.' : 'La fiche est masquée du catalogue public.');
      await this.openBook(book.isbn13);
      await this.loadCatalogue();
    });
  }

  async mergeBook(book: CatalogAdminBook): Promise<void> {
    const target = this.mergeTargetIsbn13.trim();
    if (!target || !this.mergeNote.trim()) {
      this.showError('ISBN cible et note de fusion sont obligatoires.');
      return;
    }

    if (!this.confirmAction(`Rediriger ${book.isbn13} vers ${target} ?`)) {
      return;
    }

    await this.run('merge', async token => {
      await firstValueFrom(this.api.merge(token, book.isbn13, {
        targetIsbn13: target,
        note: this.mergeNote.trim(),
      }));
      this.showSuccess('La fiche source est redirigée vers la fiche canonique.');
      this.selectedBook.set(null);
      await this.loadCatalogue();
    });
  }

  canPermanentlyDeleteBook(book: CatalogAdminBook): boolean {
    return book.redirectedToIsbn13 === null &&
      book.salesCount === 0 &&
      book.announcements.length === 0 &&
      !book.movements.some(movement =>
        movement.type === 'Sale' || movement.scanSessionId !== null || movement.fairId !== null);
  }

  async permanentlyDeleteBook(book: CatalogAdminBook): Promise<void> {
    if (!this.canPermanentlyDeleteBook(book) || !this.confirmAction(
      `Supprimer définitivement la fiche ${book.isbn13} ? La fiche, son stock, ses mouvements et ses suivis seront effacés. Cette action est irréversible. Elle est indisponible après une vente, une annonce, un scan ou une fusion.`,
    )) {
      return;
    }

    await this.run('permanent-delete-book', async token => {
      await firstValueFrom(this.api.permanentlyDeleteBook(token, book.isbn13));
      this.showSuccess('La fiche et ses données associées ont été supprimées définitivement.');
      this.selectedBook.set(null);
      await this.loadCatalogue();
    });
  }

  async loadFairs(): Promise<void> {
    await this.run('fairs', async token => {
      const page = await firstValueFrom(this.api.getFairs(token));
      this.fairsPage.set(page);
      if (!this.selectedFairStats() && page.fairs.length > 0) {
        const stats = await firstValueFrom(this.api.getFairStats(token, page.fairs[0].id));
        this.selectedFairStats.set(stats);
        this.revenueInput = stats.revenue;
      }
    });
  }

  async loadStatisticsTab(): Promise<void> {
    if (this.statisticsTab() === 'volunteers') {
      await this.loadVolunteerStatistics();
      return;
    }
    if (this.statisticsTab() === 'evolution') {
      await this.loadFairsEvolution();
      return;
    }
    if (this.statisticsTab() === 'books') {
      await this.loadCatalogueFlowStats();
      return;
    }

    await this.loadFairs();
  }

  async selectStatisticsTab(tab: CatalogAdminStatisticsTab): Promise<void> {
    this.statisticsTab.set(tab);

    if (isCatalogAdministrationRoute(this.router.url)) {
      const routeTab = tab === 'fairs' ? null : tab;
      if (this.route.snapshot.queryParamMap.get('tab') !== routeTab) {
        await this.router.navigate([], {
          relativeTo: this.route,
          queryParams: {tab: routeTab},
          queryParamsHandling: 'merge',
        });
      }
    }

    if (!this.auth.isAuthenticated()) {
      return;
    }

    await this.loadStatisticsTab();
  }

  setStatisticsPeriod(period: CatalogAdminStatisticsPeriod): void {
    this.statisticsPeriod = period;
    if (this.auth.isAuthenticated() && this.statisticsTab() !== 'fairs') {
      void this.loadStatisticsTab();
    }
  }

  async selectStatisticsFair(fairId: string): Promise<void> {
    this.statisticsFairId = fairId;
    if (fairId) {
      this.statisticsPeriod = 'fair';
    } else if (this.statisticsPeriod === 'fair') {
      this.statisticsPeriod = '30-days';
    }
    if (this.auth.isAuthenticated() && this.statisticsTab() !== 'fairs') {
      await this.loadStatisticsTab();
    }
  }

  sortVolunteersBy(key: VolunteerSortKey): void {
    this.volunteerSort.set(nextVolunteerSort(this.volunteerSort(), key));
  }

  volunteerAriaSort(key: VolunteerSortKey | null): 'ascending' | 'descending' | null {
    const sort = this.volunteerSort();
    if (!key || sort.key !== key) {
      return null;
    }
    return sort.direction === 'asc' ? 'ascending' : 'descending';
  }

  async loadVolunteerStatistics(): Promise<void> {
    const [from, to] = this.statisticsPeriodBounds(this.statisticsPeriod);
    await this.run('volunteer-statistics', async token => {
      await this.ensureStatisticsFairOptions(token);
      this.volunteerStatistics.set(await firstValueFrom(this.api.getVolunteerStatistics(
        token,
        from,
        to,
        this.selectedStatisticsFairId(),
      )));
    });
  }

  async loadFairsEvolution(): Promise<void> {
    const [from, to] = this.statisticsPeriodBounds(this.statisticsPeriod);
    await this.run('fairs-evolution', async token => {
      await this.ensureStatisticsFairOptions(token);
      this.fairsEvolution.set(await firstValueFrom(
        this.api.getFairsEvolution(token, from, to, this.selectedStatisticsFairId()),
      ));
    });
  }

  async loadCatalogueFlowStats(): Promise<void> {
    const [from, to] = this.statisticsPeriodBounds(this.statisticsPeriod);
    await this.run('catalogue-flow-stats', async token => {
      await this.ensureStatisticsFairOptions(token);
      this.catalogueFlowStats.set(await firstValueFrom(
        this.api.getCatalogueFlowStats(token, from, to, this.selectedStatisticsFairId()),
      ));
    });
  }

  private selectedStatisticsFairId(): string | undefined {
    return this.statisticsPeriod === 'fair' && this.statisticsFairId ? this.statisticsFairId : undefined;
  }

  private async ensureStatisticsFairOptions(token: string): Promise<void> {
    if (!this.fairsPage()) {
      this.fairsPage.set(await firstValueFrom(this.api.getFairs(token)));
    }
  }

  async openFairStats(fair: CatalogAdminFair): Promise<void> {
    await this.run('fair-stats', async token => {
      const stats = await firstValueFrom(this.api.getFairStats(token, fair.id));
      this.selectedFairStats.set(stats);
      this.revenueInput = stats.revenue;
    });
  }

  async selectFair(fairId: string): Promise<void> {
    const fair = this.fairsPage()?.fairs.find(candidate => candidate.id === fairId);
    if (fair) {
      await this.openFairStats(fair);
    }
  }

  async saveFairRevenue(): Promise<void> {
    const stats = this.selectedFairStats();
    if (!stats) {
      return;
    }

    const revenue = this.revenueInput === null || this.revenueInput === undefined
      ? null
      : Number(this.revenueInput);
    if (revenue !== null && (!Number.isFinite(revenue) || revenue < 0)) {
      this.showError('La recette doit être positive ou vide.');
      return;
    }

    await this.run('revenue', async token => {
      await firstValueFrom(this.api.setFairRevenue(token, stats.fair.id, revenue));
      this.showSuccess('La recette de la bourse aux livres a été enregistrée.');
      await this.openFairStats(stats.fair);
      await this.loadFairs();
    });
  }

  async loadSessions(): Promise<void> {
    const filters: CatalogAdminSessionFilters = {
      status: this.sessionStatus || undefined,
      from: this.sessionFrom ? new Date(`${this.sessionFrom}T00:00:00Z`).toISOString() : undefined,
      to: this.sessionTo ? new Date(`${this.sessionTo}T23:59:59Z`).toISOString() : undefined,
      page: this.sessionPage,
      pageSize: this.sessionPageSize,
    };

    await this.run('sessions', async token => {
      this.sessionsPage.set(await firstValueFrom(this.api.getSessions(token, filters)));
    });
  }

  setSessionPreset(preset: 'correctable' | 'all' | 'open' | 'alerts'): void {
    this.sessionPreset = preset;
  }

  private statisticsPeriodBounds(period: CatalogAdminStatisticsPeriod): [string | undefined, string | undefined] {
    if (period === 'all' || period === 'fair') {
      return [undefined, undefined];
    }

    const to = new Date();
    const from = new Date(to);
    if (period === '30-days') {
      from.setUTCDate(from.getUTCDate() - 30);
    } else {
      this.subtractCalendarYears(from, 1);
    }

    return [from.toISOString(), to.toISOString()];
  }

  private subtractCalendarYears(date: Date, years: number): void {
    const day = date.getUTCDate();
    date.setUTCDate(1);
    date.setUTCFullYear(date.getUTCFullYear() - years);
    date.setUTCDate(Math.min(day, this.daysInUtcMonth(date)));
  }

  private daysInUtcMonth(date: Date): number {
    return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate();
  }

  visibleSessions(): CatalogAdminScanSession[] {
    const sessions = this.sessionsPage()?.sessions ?? [];
    switch (this.sessionPreset) {
      case 'correctable':
        return sessions.filter(session => this.sessionNeedsCorrection(session));
      case 'open':
        return sessions.filter(session => session.status === IN_PROGRESS_SESSION_STATUS);
      case 'alerts':
        return sessions.filter(session => {
          const state = this.sessionAlertState(session);
          return state === 'sent' || state === 'dispatching';
        });
      default:
        return sessions;
    }
  }

  correctableSessionCount(): number {
    return (this.sessionsPage()?.sessions ?? []).filter(session => this.sessionNeedsCorrection(session)).length;
  }

  openSessionCount(): number {
    return (this.sessionsPage()?.sessions ?? [])
      .filter(session => session.status === IN_PROGRESS_SESSION_STATUS).length;
  }

  alertSessionCount(): number {
    return (this.sessionsPage()?.sessions ?? []).filter(session => {
      const state = this.sessionAlertState(session);
      return state === 'sent' || state === 'dispatching';
    }).length;
  }

  pendingAlertSessions(): CatalogAdminScanSession[] {
    return (this.sessionsPage()?.sessions ?? [])
      .filter(session => this.sessionNeedsCorrection(session))
      .slice(0, 2);
  }

  async openSession(sessionId: string, keepJournalOpen = false): Promise<void> {
    this.sessionConfirmation.set(null);
    if (!keepJournalOpen) {
      this.sessionJournalOpen.set(false);
    }
    await this.run('session-detail', async token => {
      const session = await firstValueFrom(this.api.getSession(token, sessionId));
      this.selectedSession.set(session);
      this.sessionMode = session.mode;
      this.sessionFairId = session.fairId || '';
      if (!this.fairsPage()) {
        this.fairsPage.set(await firstValueFrom(this.api.getFairs(token)));
      }
    });
  }

  async removeMovement(movementId: string): Promise<void> {
    const session = this.selectedSession();
    const movement = session?.movements.find(candidate => candidate.id === movementId);
    if (!session || !movement || movement.reversalOfMovementId) {
      return;
    }

    this.sessionConfirmation.set({
      action: {kind: 'remove-movement', sessionId: session.id, movementId},
      title: 'Retirer ce livre du stock ?',
      description: 'Le mouvement sera renversé et restera visible dans le journal de la session pour garder une trace complète de la correction.',
      confirmLabel: 'Retirer le mouvement',
      tone: 'danger',
    });
  }

  async reassignSession(): Promise<void> {
    const session = this.selectedSession();
    if (!session) {
      return;
    }

    this.sessionConfirmation.set({
      action: {kind: 'reassign-session', sessionId: session.id},
      title: 'Appliquer les corrections ?',
      description: 'Les mouvements de cette session seront rejoués avec le mode et la bourse sélectionnés. L’historique initial restera conservé.',
      confirmLabel: 'Appliquer les corrections',
      tone: 'accent',
    });
  }

  async cancelSession(): Promise<void> {
    const session = this.selectedSession();
    if (!session) {
      return;
    }

    this.sessionConfirmation.set({
      action: {kind: 'cancel-session', sessionId: session.id},
      title: 'Annuler cette session ?',
      description: 'Tous les mouvements de la session seront renversés et l’annulation sera inscrite dans l’historique.',
      confirmLabel: 'Annuler la session',
      tone: 'danger',
    });
  }

  async cancelSessionAlerts(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.sessionAlertActionsAvailable(session)) {
      return;
    }

    this.sessionConfirmation.set({
      action: {kind: 'cancel-session-alerts', sessionId: session.id},
      title: 'Annuler les alertes en attente ?',
      description: `Les ${this.formatNumber(session.pendingAlertCount)} alertes ne seront pas envoyées aux membres. Cette décision restera visible dans le journal.`,
      confirmLabel: 'Annuler les alertes',
      tone: 'danger',
    });
  }

  async forceSessionAlerts(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.sessionAlertActionsAvailable(session)) {
      return;
    }

    this.sessionConfirmation.set({
      action: {kind: 'force-session-alerts', sessionId: session.id},
      title: 'Lancer l’envoi immédiat ?',
      description: `Les ${this.formatNumber(session.pendingAlertCount)} alertes seront remises à la file d’envoi maintenant. La session ne sera plus corrigeable pendant leur traitement.`,
      confirmLabel: 'Lancer l’envoi immédiat',
      tone: 'accent',
    });
  }

  cancelSessionConfirmation(): void {
    this.sessionConfirmation.set(null);
  }

  async confirmSessionAction(): Promise<void> {
    const confirmation = this.sessionConfirmation();
    const session = this.selectedSession();
    if (!confirmation || !session || confirmation.action.sessionId !== session.id || this.loading()) {
      return;
    }

    this.sessionConfirmation.set(null);
    switch (confirmation.action.kind) {
      case 'remove-movement':
        await this.executeRemoveMovement(session, confirmation.action.movementId);
        return;
      case 'reassign-session':
        await this.executeReassignSession(session);
        return;
      case 'cancel-session':
        await this.executeCancelSession(session);
        return;
      case 'cancel-session-alerts':
        await this.executeCancelSessionAlerts(session);
        return;
      case 'force-session-alerts':
        await this.executeForceSessionAlerts(session);
        return;
    }
  }

  toggleSessionJournal(): void {
    this.sessionJournalOpen.update(open => !open);
  }

  private async executeRemoveMovement(
    session: CatalogAdminScanSession,
    movementId: string,
  ): Promise<void> {
    await this.run('remove-movement', async token => {
      await firstValueFrom(this.api.removeMovement(token, session.id, movementId));
      this.showSuccess('Le mouvement a été renversé et reste visible dans le journal.');
      await this.openSession(session.id, true);
      await this.loadSessions();
    });
  }

  private async executeReassignSession(session: CatalogAdminScanSession): Promise<void> {
    await this.run('reassign-session', async token => {
      await firstValueFrom(this.api.reassignSession(token, session.id, {
        mode: this.sessionMode,
        targetAssoEventsId: this.sessionFairId || null,
      }));
      this.showSuccess('La session a été corrigée et son historique est conservé.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  private async executeCancelSession(session: CatalogAdminScanSession): Promise<void> {
    await this.run('cancel-session', async token => {
      await firstValueFrom(this.api.cancelSession(token, session.id));
      this.showSuccess('La session a été annulée avec une correction tracée.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  private async executeCancelSessionAlerts(session: CatalogAdminScanSession): Promise<void> {
    await this.run('cancel-session-alerts', async token => {
      const operation = await firstValueFrom(this.api.cancelSessionAlerts(token, session.id));
      this.showSuccess(`${this.formatNumber(operation.affectedAlertCount)} alerte(s) ont été annulée(s) et ne seront pas envoyée(s).`);
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  private async executeForceSessionAlerts(session: CatalogAdminScanSession): Promise<void> {
    await this.run('force-session-alerts', async token => {
      const operation = await firstValueFrom(this.api.forceSessionAlerts(token, session.id));
      this.showSuccess(`${this.formatNumber(operation.affectedAlertCount)} alerte(s) sont maintenant en envoi immédiat et la session n’est plus corrigeable.`);
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  async loadAlerts(): Promise<void> {
    const filters: CatalogAdminAlertFilters = {
      status: this.alertStatus || undefined,
      page: this.alertPage,
      pageSize: this.alertPageSize,
    };
    await this.run('alerts', async token => {
      this.alertsPage.set(await firstValueFrom(this.api.getAlerts(token, filters)));
    });
  }

  async cancelAlert(alert: CatalogAdminAlert): Promise<void> {
    if (!this.confirmAction('Annuler cette alerte avant son envoi ?')) {
      return;
    }
    await this.run('cancel-alert', async token => {
      await firstValueFrom(this.api.cancelAlert(token, alert.id));
      this.showSuccess('L’alerte a été annulée.');
      await this.loadAlerts();
    });
  }

  async forceAlert(alert: CatalogAdminAlert): Promise<void> {
    if (!this.confirmAction('Forcer l’envoi de cette alerte maintenant ?')) {
      return;
    }
    await this.run('force-alert', async token => {
      await firstValueFrom(this.api.forceAlert(token, alert.id));
      this.showSuccess('L’alerte a été forcée.');
      await this.loadAlerts();
    });
  }

  async loadMembers(): Promise<void> {
    const filters: CatalogAdminMemberFilters = {
      search: this.memberSearch.trim() || undefined,
      alertStatus: this.memberAlertStatus || undefined,
      page: this.memberPage,
      pageSize: this.memberPageSize,
    };
    await this.run('members', async token => {
      this.membersPage.set(await firstValueFrom(this.api.getMembers(token, filters)));
    });
  }

  async loadAccounts(): Promise<void> {
    const filters: CatalogAdminAccountFilters = {
      search: this.accountSearch.trim() || undefined,
      page: this.accountPage,
      pageSize: this.accountPageSize,
    };
    await this.run('accounts', async token => {
      this.accountsPage.set(await firstValueFrom(this.api.getAdminAccounts(token, filters)));
    }, this.accountErrorMessage, error => this.describeAccountError(error));
  }

  async lookupCheckoutPassage(): Promise<void> {
    const reference = this.checkoutPassageReference.trim();
    if (!reference) {
      this.checkoutPassageLookup.set(null);
      this.checkoutPassageError.set('Saisissez la référence du passage ou son identifiant complet.');
      return;
    }

    this.checkoutPassageLookup.set(null);
    await this.run('checkout-passage-lookup', async token => {
      this.checkoutPassageLookup.set(await firstValueFrom(
        this.api.lookupCheckoutPassage(token, reference),
      ));
    }, this.checkoutPassageError, error => this.describeCheckoutPassageError(error));
  }

  async dissociateCheckoutPassage(): Promise<void> {
    const passage = this.checkoutPassageLookup();
    const reason = this.checkoutPassageReason.trim();
    if (!passage) {
      return;
    }

    if (reason.length < 3 || reason.length > 500) {
      this.checkoutPassageError.set('La raison doit contenir entre 3 et 500 caractères.');
      return;
    }

    const member = passage.displayLabel || 'ce membre';
    const confirmation = `Dissocier le passage du ${this.formatDate(passage.occurredAt)} (${passage.lineCount} lignes) associé à ${member} ?`;
    if (!this.confirmAction(confirmation)) {
      return;
    }

    await this.run('checkout-passage-dissociate', async token => {
      await firstValueFrom(this.api.dissociateCheckoutPassage(token, passage.id, reason));
      this.checkoutPassageLookup.set(null);
      this.checkoutPassageReference = '';
      this.checkoutPassageReason = '';
      this.showSuccess('L’association du passage a été supprimée. Les lignes de vente et le stock sont inchangés.');
    }, this.checkoutPassageError, error => this.describeCheckoutPassageError(error));
  }

  async selectAccountsTab(tab: CatalogAdminAccountsTab): Promise<void> {
    this.accountsTab.set(tab);
    if (!this.auth.isAuthenticated()) {
      return;
    }

    if (tab === 'members') {
      await this.loadMembers();
      return;
    }

    await this.loadAccounts();
  }

  async createAccount(): Promise<void> {
    const form = this.createAccountForm;
    this.createAccountSubmitted = true;
    if (!form.email.trim() || !form.firstName.trim() || !form.lastName.trim() || form.roles.length === 0) {
      this.showError('E-mail, prénom, nom et au moins un droit sont obligatoires.');
      return;
    }

    const passwordIssue = getTemporaryPasswordIssue(form.temporaryPassword);
    if (passwordIssue) {
      this.showError(temporaryPasswordIssueMessage(passwordIssue));
      return;
    }

    await this.run('create-account', async token => {
      await firstValueFrom(this.api.createAdminAccount(token, {
        email: form.email.trim(),
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        temporaryPassword: form.temporaryPassword,
        roles: [...form.roles],
      }));
      this.showSuccess('Le compte bénévole a été créé.');
      form.email = '';
      form.firstName = '';
      form.lastName = '';
      form.temporaryPassword = '';
      form.roles = [];
      this.createAccountSubmitted = false;
      this.showCreateAccount.set(false);
      await this.loadAccounts();
    }, this.errorMessage, error => this.describeCreateAccountError(error));
  }

  getTemporaryPasswordIssue(password: string | null | undefined): TemporaryPasswordIssue | null {
    return getTemporaryPasswordIssue(password);
  }

  temporaryPasswordMessage(issue: TemporaryPasswordIssue): string {
    return temporaryPasswordIssueMessage(issue);
  }

  toggleCreateRole(role: CatalogAdminAccountRole): void {
    this.createAccountForm.roles = this.createAccountForm.roles.includes(role)
      ? this.createAccountForm.roles.filter(item => item !== role)
      : [...this.createAccountForm.roles, role];
  }

  hasCreateRole(role: CatalogAdminAccountRole): boolean {
    return this.createAccountForm.roles.includes(role);
  }

  startAccountRoleEdit(account: CatalogAdminAccount): void {
    this.editingAccountId.set(account.externalId);
    this.editingAccountRoles.set([...account.roles]);
  }

  cancelAccountRoleEdit(): void {
    this.editingAccountId.set(null);
    this.editingAccountRoles.set([]);
  }

  toggleEditingRole(role: CatalogAdminAccountRole): void {
    const roles = this.editingAccountRoles();
    this.editingAccountRoles.set(roles.includes(role)
      ? roles.filter(item => item !== role)
      : [...roles, role]);
  }

  hasEditingRole(role: CatalogAdminAccountRole): boolean {
    return this.editingAccountRoles().includes(role);
  }

  async saveAccountRoles(account: CatalogAdminAccount): Promise<void> {
    if (this.editingAccountRoles().length === 0) {
      this.showError('Un compte doit conserver au moins un droit.');
      return;
    }

    await this.run('account-roles', async token => {
      await firstValueFrom(this.api.updateAdminAccountRoles(
        token,
        account.externalId,
        this.editingAccountRoles(),
      ));
      this.showSuccess('Les droits du compte ont été mis à jour.');
      this.cancelAccountRoleEdit();
      await this.loadAccounts();
    });
  }

  async toggleAccountEnabled(account: CatalogAdminAccount): Promise<void> {
    const accountEnabled = !account.accountEnabled;
    const action = accountEnabled ? 'réactiver' : 'désactiver';
    if (!this.confirmAction(`Voulez-vous ${action} le compte de ${account.displayName || account.email || 'ce bénévole'} ?`)) {
      return;
    }

    await this.run('account-status', async token => {
      await firstValueFrom(this.api.updateAdminAccountStatus(token, account.externalId, accountEnabled));
      this.showSuccess(accountEnabled ? 'Le compte bénévole a été réactivé.' : 'Le compte bénévole a été désactivé.');
      await this.loadAccounts();
    });
  }

  accountRoleLabel(role: CatalogAdminAccountRole): string {
    return this.accountRoleOptions.find(option => option.value === role)?.label ?? role;
  }

  accountActivity(account: CatalogAdminAccount): string {
    const sessions = this.sessionsPage()?.sessions.filter(session => session.volunteerId === account.externalId);
    if (!sessions?.length) {
      return 'Aucune session de tri';
    }

    const scanCount = sessions.reduce((total, session) => total + session.scannedCount, 0);
    const latestScan = sessions
      .map(session => session.lastScanAt)
      .sort()
      .at(-1);
    return `${sessions.length} session${sessions.length > 1 ? 's' : ''} · ${this.formatNumber(scanCount)} scans · dernier scan ${this.formatDate(latestScan)}`;
  }

  maskedEmail(email: string | null): string {
    if (!email) {
      return 'E-mail non renseigné';
    }
    const [local, domain] = email.split('@');
    if (!local || !domain) {
      return email;
    }
    return `${local.slice(0, 1)}•••••@${domain}`;
  }

  async openMember(memberId: string): Promise<void> {
    await this.run('member-detail', async token => {
      this.selectedMember.set(await firstValueFrom(this.api.getMember(token, memberId)));
    });
  }

  closeMemberDetail(): void {
    this.memberDeletionConfirmation.set(null);
    this.selectedMember.set(null);
  }

  requestMemberDeletion(member: CatalogAdminMemberDetail): void {
    this.memberDeletionConfirmation.set(member);
  }

  cancelMemberDeletion(): void {
    this.memberDeletionConfirmation.set(null);
  }

  async toggleMemberBlocked(member: CatalogAdminMemberDetail): Promise<void> {
    const blocked = member.member.alertStatus === 'Blocked';
    const action = blocked ? 'réactiver' : 'bloquer';
    if (!this.confirmAction(`Voulez-vous ${action} les alertes de ce membre ?`)) {
      return;
    }

    await this.run('member-alert-status', async token => {
      await firstValueFrom(this.api.setAlertStatus(token, member.member.id, blocked));
      this.showSuccess(blocked ? 'Les alertes du membre sont réactivées.' : 'Les alertes du membre sont bloquées.');
      await this.openMember(member.member.id);
      await this.loadMembers();
    });
  }

  async deleteMember(): Promise<void> {
    const member = this.memberDeletionConfirmation();
    if (!member) {
      return;
    }

    this.memberDeletionConfirmation.set(null);
    await this.run('delete-member', async token => {
      const operation = await firstValueFrom(this.api.deleteMember(token, member.member.id));
      this.showSuccess(operation.deletionCompleted
        ? 'Le compte Entra et les données du membre ont été supprimés.'
        : 'La suppression du compte Entra est en attente de reprise par le service sécurisé.');
      this.closeMemberDetail();
      await this.loadMembers();
    });
  }

  async loadSettings(): Promise<void> {
    await this.run('settings', async token => {
      const settings = await firstValueFrom(this.api.getSettings(token));
      this.settings.set(settings);
      this.settingsForm = {...settings};
      this.deadStockAgeMonths = settings.deadStockMinAgeDays > 0
        ? Math.max(1, Math.round(settings.deadStockMinAgeDays / 30))
        : 0;
      this.sessionIdleHours = settings.sessionIdleTimeoutMinutes / 60;
      this.alertDelayHours = settings.alertDelayMinutes / 60;
    });
  }

  async saveSettings(): Promise<void> {
    const integerFields = [
      this.settingsForm.duplicateThreshold,
      this.settingsForm.demandSalesThreshold,
      this.settingsForm.deadStockMinQuantity,
      this.settingsForm.watchlistMaxItems,
      this.settingsForm.alertCooldownDays,
      this.settingsForm.notFoundReportDailyLimit,
      this.deadStockAgeMonths,
    ];
    const hourFields = [this.sessionIdleHours, this.alertDelayHours];
    if (integerFields.some(value => !Number.isInteger(Number(value)) || Number(value) < 0)
      || hourFields.some(value => !Number.isFinite(Number(value)) || Number(value) < 0)) {
      this.showError('Les seuils doivent être des entiers positifs ou nuls ; les durées peuvent être exprimées par demi-heure.');
      return;
    }

    const dailyLimit = Number(this.settingsForm.notFoundReportDailyLimit);
    if (dailyLimit < 1 || dailyLimit > 100) {
      this.showError('Le plafond de signalements doit être compris entre 1 et 100 par 24 h.');
      return;
    }

    await this.run('save-settings', async token => {
      const settings = await firstValueFrom(this.api.updateSettings(token, {
        duplicateThreshold: Number(this.settingsForm.duplicateThreshold),
        demandSalesThreshold: Number(this.settingsForm.demandSalesThreshold),
        deadStockMinAgeDays: Math.round(Number(this.deadStockAgeMonths) * 30),
        deadStockMinQuantity: Number(this.settingsForm.deadStockMinQuantity),
        watchlistMaxItems: Number(this.settingsForm.watchlistMaxItems),
        alertCooldownDays: Number(this.settingsForm.alertCooldownDays),
        sessionIdleTimeoutMinutes: Math.round(Number(this.sessionIdleHours) * 60),
        alertDelayMinutes: Math.round(Number(this.alertDelayHours) * 60),
        notFoundReportDailyLimit: dailyLimit,
      }));
      this.settings.set(settings);
      this.settingsForm = {...settings};
      this.showSuccess('Les paramètres ont été enregistrés.');
    });
  }

  async loadDeadStock(): Promise<void> {
    const filters = this.validatedFilters();
    if (!filters || !this.auth.isAuthenticated()) {
      return;
    }

    await this.run('dead-stock', async token => {
      const response = await firstValueFrom(
        this.api.getDeadStock(token, filters.minAgeMonths, filters.minQuantity),
      );
      this.deadStockBooks.set(response.books);
      this.deadStockGeneratedAt.set(response.generatedAt);
    });
  }

  exportCsv(): void {
    const books = this.deadStockBooks();
    if (books.length === 0 || !isPlatformBrowser(this.platformId)) {
      return;
    }

    const csv = toDeadStockCsv({
      generatedAt: this.deadStockGeneratedAt() ?? new Date().toISOString(),
      minAgeMonths: this.minAgeMonths,
      minQuantity: this.minQuantity,
      books,
    });
    const blob = new Blob([`\uFEFF${csv}`], {type: 'text/csv;charset=utf-8'});
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `livres-a-desengorger-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  exportFairsEvolutionCsv(): void {
    const evolution = this.fairsEvolution();
    if (evolution && evolution.fairs.length > 0) {
      this.downloadCsv(toFairsEvolutionCsv(evolution), 'evolution-des-bourses');
    }
  }

  exportVolunteerStatisticsCsv(): void {
    const volunteers = this.sortedVolunteers();
    if (volunteers.length > 0) {
      this.downloadCsv(toVolunteerStatisticsCsv(volunteers), 'statistiques-benevoles');
    }
  }

  private downloadCsv(csv: string, filePrefix: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const blob = new Blob([`﻿${csv}`], {type: 'text/csv;charset=utf-8'});
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${filePrefix}-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
  }

  goToCataloguePage(page: number): void {
    if (this.validPage(page, this.catalogueBooksPage())) {
      this.cataloguePage = page;
      void this.loadCatalogue();
    }
  }

  goToSessionsPage(page: number): void {
    if (this.validPage(page, this.sessionsPage())) {
      this.sessionPage = page;
      void this.loadSessions();
    }
  }

  goToAlertsPage(page: number): void {
    if (this.validPage(page, this.alertsPage())) {
      this.alertPage = page;
      void this.loadAlerts();
    }
  }

  goToMembersPage(page: number): void {
    if (this.validPage(page, this.membersPage())) {
      this.memberPage = page;
      void this.loadMembers();
    }
  }

  goToAccountsPage(page: number): void {
    if (this.validPage(page, this.accountsPage())) {
      this.accountPage = page;
      void this.loadAccounts();
    }
  }

  formatDate(value: string | null | undefined, withTime = false): string {
    if (!value) {
      return '—';
    }
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      ...(withTime ? {hour: '2-digit', minute: '2-digit'} : {}),
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace('.', '');
  }

  fairOptionLabel(fair: CatalogAdminFair): string {
    const start = this.formatDate(fair.dateStart);
    const end = fair.dateEnd ? this.formatDate(fair.dateEnd) : null;
    const dateLabel = end && end !== start ? `${start} → ${end}` : start;
    return `${dateLabel} · ${fair.name} · ${fair.isCancelled ? 'annulée' : 'conservée'}`;
  }

  formatDay(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(`${value}T12:00:00Z`)).replace('.', '');
  }

  formatMoney(value: number | null | undefined): string {
    return value === null || value === undefined
      ? 'Non saisie'
      : new Intl.NumberFormat('fr-FR', {style: 'currency', currency: 'EUR'}).format(value);
  }

  formatNumber(value: number | null | undefined): string {
    return new Intl.NumberFormat('fr-FR').format(value ?? 0);
  }

  formatDurationHours(value: number | null | undefined): string {
    if (value === null || value === undefined) {
      return '—';
    }
    const hours = Math.floor(value / 60);
    const minutes = value % 60;
    return hours > 0 ? `${hours} h ${String(minutes).padStart(2, '0')}` : `${minutes} min`;
  }

  formatPercent(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : `${this.formatNumber(value)} %`;
  }

  formatGenreFlowPercent(soldQuantity: number, value: number | null): string {
    return soldQuantity > 0 && value === 0 ? '< 0,01 %' : this.formatPercent(value);
  }

  formatRoundedHours(value: number | null | undefined): string {
    return formatRoundedHours(value);
  }

  formatRoundedNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : this.formatNumber(Math.round(value));
  }

  formatRoundedPercent(value: number | null | undefined): string {
    return this.formatPercent(value === null || value === undefined ? value : Math.round(value));
  }

  formatShortDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    return new Intl.DateTimeFormat('fr-FR', {day: 'numeric', month: 'short', timeZone: 'Europe/Paris'})
      .format(new Date(value));
  }

  statisticsPeriodPhrase(): string {
    const fair = this.fairsPage()?.fairs.find(candidate => candidate.id === this.statisticsFairId);
    return describeStatisticsPeriod(this.statisticsPeriod, fair?.name ?? null);
  }

  volunteerLead(activeVolunteerCount: number): string {
    return describeVolunteerLead(activeVolunteerCount);
  }

  volunteerCountLabel(activeVolunteerCount: number): string {
    return capitalizeFrench(describeVolunteerCount(activeVolunteerCount));
  }

  formatDecimal(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toFixed(1).replace('.', ',');
  }

  rolesLabel(roles: string[] | null | undefined): string {
    return roles?.join(' · ') || '—';
  }

  isRecentVolunteer(firstActivityAt: string | null | undefined): boolean {
    return Boolean(firstActivityAt) && Date.now() - new Date(firstActivityAt!).getTime() < 90 * 86_400_000;
  }

  scatterLeft(
    volunteer: CatalogAdminVolunteerStatistics['volunteers'][number],
    volunteers: CatalogAdminVolunteerStatistics['volunteers'],
  ): string {
    const maximum = Math.max(...volunteers.map(item => item.totalDurationMinutes), 1);
    return `${Math.min(96, Math.max(4, Math.round((volunteer.totalDurationMinutes / maximum) * 92) + 4))}%`;
  }

  scatterBottom(
    volunteer: CatalogAdminVolunteerStatistics['volunteers'][number],
    volunteers: CatalogAdminVolunteerStatistics['volunteers'],
  ): string {
    const maximum = Math.max(...volunteers.map(item => item.scannedCount), 1);
    return `${Math.min(92, Math.max(8, Math.round((volunteer.scannedCount / maximum) * 84) + 8))}%`;
  }

  scatterSize(volunteer: CatalogAdminVolunteerStatistics['volunteers'][number]): number {
    return Math.min(30, 12 + volunteer.sessionCount / 3);
  }

  contributionShare(value: number, total: number): string {
    return total > 0 ? `${Math.round((value / total) * 100)} %` : '—';
  }

  topVolunteerShare(stats: CatalogAdminVolunteerStatistics, count: number): string {
    const total = stats.team.scannedCount;
    const volume = stats.volunteers.slice(0, count).reduce((sum, volunteer) => sum + volunteer.scannedCount, 0);
    return this.contributionShare(volume, total);
  }

  maxMonthlySessions(rows: CatalogAdminVolunteerStatistics['monthlyActivity']): number {
    return Math.max(...rows.flatMap(row => row.months.map(month => month.sessionCount)), 1);
  }

  heatmapOpacity(value: number, maximum: number): number {
    return value === 0 ? 0.08 : 0.18 + (value / Math.max(1, maximum)) * 0.82;
  }

  formatCountdown(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const remainingMinutes = Math.ceil((new Date(value).getTime() - Date.now()) / 60_000);
    if (remainingMinutes <= 0) {
      return 'Maintenant';
    }
    const hours = Math.floor(remainingMinutes / 60);
    const minutes = remainingMinutes % 60;
    return hours > 0 ? `${hours} h ${String(minutes).padStart(2, '0')}` : `${minutes} min`;
  }

  sessionAlertState(session: CatalogAdminScanSession): CatalogAdminSessionAlertState {
    if (session.pendingAlertCount > 0) {
      const dueAt = session.nextAlertDueAt ? new Date(session.nextAlertDueAt).getTime() : Number.NaN;
      return Number.isFinite(dueAt) && dueAt <= Date.now() ? 'dispatching' : 'pending';
    }

    if ((session.sentAlertCount ?? 0) > 0 || (
      session.alertCount > 0 &&
      (session.cancelledAlertCount ?? 0) === 0 &&
      (session.failedAlertCount ?? 0) === 0
    )) {
      return 'sent';
    }

    if ((session.cancelledAlertCount ?? 0) > 0) {
      return 'cancelled';
    }

    if ((session.failedAlertCount ?? 0) > 0) {
      return 'failed';
    }

    return 'none';
  }

  sessionAlertActionsAvailable(session: CatalogAdminScanSession): boolean {
    return this.sessionAlertState(session) === 'pending';
  }

  sessionAlertTitle(session: CatalogAdminScanSession): string {
    switch (this.sessionAlertState(session)) {
      case 'pending':
        return 'Alertes encore en attente';
      case 'dispatching':
        return 'Envoi immédiat demandé';
      case 'sent':
        return 'Alertes envoyées';
      case 'cancelled':
        return 'Alertes annulées';
      case 'failed':
        return 'Échec d’envoi';
      default:
        return 'Aucune alerte';
    }
  }

  sessionAlertDescription(session: CatalogAdminScanSession): string {
    switch (this.sessionAlertState(session)) {
      case 'pending':
        return 'Cette session peut encore être corrigée avant que les membres ne soient prévenus.';
      case 'dispatching':
        return 'Les alertes ont été remises à la file d’envoi et la session n’est plus corrigeable.';
      case 'sent':
        return 'Les membres ont été prévenus. Une alerte déjà envoyée ne peut pas être annulée.';
      case 'cancelled':
        return 'Ces alertes n’ont pas été envoyées. Elles ne peuvent plus être relancées depuis cette session.';
      case 'failed':
        return 'L’envoi a échoué après plusieurs tentatives. Consultez le journal des alertes pour intervenir.';
      default:
        return 'Aucun membre n’a été ciblé par une alerte pour cette session.';
    }
  }

  sessionAlertMetric(session: CatalogAdminScanSession): string {
    switch (this.sessionAlertState(session)) {
      case 'pending':
        return this.formatCountdown(session.nextAlertDueAt);
      case 'dispatching':
        return this.formatNumber(session.pendingAlertCount);
      case 'sent':
        return this.formatNumber(session.sentAlertCount || session.alertCount);
      case 'cancelled':
        return this.formatNumber(session.cancelledAlertCount);
      case 'failed':
        return this.formatNumber(session.failedAlertCount);
      default:
        return '—';
    }
  }

  sessionAlertMetricLabel(session: CatalogAdminScanSession): string {
    switch (this.sessionAlertState(session)) {
      case 'pending':
        return 'avant envoi';
      case 'dispatching':
        return 'e-mails en cours';
      case 'sent':
        return 'e-mails envoyés';
      case 'cancelled':
        return 'e-mails annulés';
      case 'failed':
        return 'e-mails en échec';
      default:
        return 'aucune alerte';
    }
  }

  sessionAlertActionHint(session: CatalogAdminScanSession): string {
    switch (this.sessionAlertState(session)) {
      case 'pending':
        return 'Vous pouvez encore corriger la session avant l’envoi.';
      case 'dispatching':
        return 'L’envoi est en cours : les actions d’alerte sont désactivées.';
      case 'sent':
        return 'Les alertes ont déjà été envoyées : aucune action supplémentaire n’est pertinente.';
      case 'cancelled':
        return 'Les alertes ont été annulées : aucune action supplémentaire n’est pertinente.';
      case 'failed':
        return 'Les alertes sont en échec : consultez le journal complet pour le détail.';
      default:
        return 'Aucune alerte à annuler ou à envoyer pour cette session.';
    }
  }

  movementTypeLabel(value: string | null | undefined): string {
    const labels: Record<string, string> = {
      DirectEntry: 'Entrée directe',
      AnnouncementEntry: 'Entrée annoncée',
      Rejection: 'Refus',
      Sale: 'Vente',
      Withdrawal: 'Retrait',
      Correction: 'Correction',
    };
    return value ? labels[value] || value : 'Mouvement';
  }

  sessionModeLabel(value: string | null | undefined): string {
    const labels: Record<string, string> = {
      AvailableNow: 'Disponible maintenant',
      NextFair: 'Prochaine bourse',
    };
    return value ? labels[value] || value : 'Sans destination';
  }

  sessionCloseLabel(value: string | null | undefined): string {
    const labels: Record<string, string> = {
      Manual: 'clôture manuelle',
      Disconnect: 'déconnexion',
      TokenExpired: 'session expirée',
      AdminForced: 'clôture forcée',
      Idle: 'inactivité',
    };
    return value ? labels[value] || value : 'session ouverte';
  }

  sessionNeedsCorrection(session: CatalogAdminScanSession): boolean {
    return this.sessionAlertState(session) === 'pending' && session.status !== 'Cancelled';
  }

  barWidth(value: number | null | undefined, maximum: number): string {
    const amount = Math.max(0, value ?? 0);
    const width = maximum > 0 ? Math.round((amount / maximum) * 100) : 0;
    return `${Math.min(100, width)}%`;
  }

  maxFairSales(): number {
    const stats = this.selectedFairStats();
    return Math.max(
      stats?.soldQuantity ?? 0,
      ...(stats?.previousFairs.map(fair => fair.soldQuantity) ?? [0]),
    );
  }

  maxDailySales(): number {
    return Math.max(...(this.selectedFairStats()?.dailySales.map(item => item.quantity) ?? [0]));
  }

  maxGenreSales(): number {
    return Math.max(...(this.selectedFairStats()?.salesByGenre.map(item => item.quantity) ?? [0]));
  }

  maxTopBookSales(): number {
    return Math.max(...(this.selectedFairStats()?.topBooks.map(item => item.quantity) ?? [0]));
  }

  formatSignedPercent(value: number | null | undefined): string {
    if (value === null || value === undefined) {
      return '—';
    }
    const formatted = this.formatPercent(Math.abs(value));
    return value > 0 ? `+ ${formatted}` : value < 0 ? `− ${formatted}` : formatted;
  }

  maxFunnelCount(): number {
    return Math.max(this.catalogueFlowStats()?.funnel.scannedCount ?? 0, 1);
  }

  maxGenreFlowQuantity(): number {
    return Math.max(...(this.catalogueFlowStats()?.flowRateByGenre.map(item => item.keptQuantity) ?? [0]), 1);
  }

  maxTimeToSellQuantity(): number {
    return Math.max(...(this.catalogueFlowStats()?.timeToSellDistribution.map(item => item.quantity) ?? [0]), 1);
  }

  fairOpenDays(fair: CatalogAdminFair | CatalogAdminFairStats['fair'] | null | undefined): string {
    if (!fair?.dateStart || !fair.dateEnd) {
      return '—';
    }
    const from = new Date(fair.dateStart).getTime();
    const to = new Date(fair.dateEnd).getTime();
    if (!Number.isFinite(from) || !Number.isFinite(to) || to < from) {
      return '—';
    }
    return this.formatNumber(Math.max(1, Math.ceil((to - from) / 86_400_000)) + 1);
  }

  movementLabel(value: string): string {
    const labels: Record<string, string> = {
      AnnouncementEntry: 'Entrée',
      DirectEntry: 'Entrée',
      FairRelease: 'Entrée',
      Sale: 'Vente',
      Rejection: 'Écart',
      Correction: 'Correction',
      Withdrawal: 'Retrait',
    };
    return labels[value] || value;
  }

  isNotFoundMovement(movement: CatalogAdminBookMovement): boolean {
    return this.selectedBookNotFoundSummary()?.withdrawalMovementIds.includes(movement.id) ?? false;
  }

  movementClass(value: string): string {
    return this.statusClass(this.movementLabel(value));
  }

  movementQuantity(value: number): string {
    return value > 0 ? `+${this.formatNumber(value)}` : this.formatNumber(value);
  }

  settingsAgeMonths(): number {
    return this.settingsForm.deadStockMinAgeDays > 0
      ? Math.max(1, Math.round(this.settingsForm.deadStockMinAgeDays / 30))
      : 0;
  }

  statusLabel(value: string | null | undefined): string {
    const labels: Record<string, string> = {
      Active: 'Active',
      Open: 'En cours',
      [IN_PROGRESS_SESSION_STATUS]: 'En cours',
      Closed: 'Terminée',
      Inactive: 'Inactive',
      Pending: 'En attente',
      Sent: 'Envoyée',
      Cancelled: 'Annulée',
      Failed: 'Échec',
      Resumed: 'Reprise',
      Blocked: 'Bloqué',
      Suspended: 'Suspendues',
    };
    return value ? labels[value] || value : '—';
  }

  metadataQueueStatusLabel(value: string): string {
    const labels: Record<string, string> = {
      Pending: 'En attente',
      Failed: 'Échec',
      NotFound: 'Aucune notice trouvée',
      Resolved: 'Titre incomplet',
      Manual: 'À corriger',
    };
    return labels[value] || value;
  }

  statusClass(value: string | null | undefined): string {
    return (value || 'unknown').toLowerCase().replace(/[^a-z0-9]+/g, '-');
  }

  pageCount(page: {totalCount: number; pageSize: number} | null): number {
    return page && page.pageSize > 0 ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  }

  private validatedFilters(): {minAgeMonths: number; minQuantity: number} | null {
    const minAgeMonths = Number(this.minAgeMonths);
    const minQuantity = Number(this.minQuantity);

    if (!Number.isInteger(minAgeMonths) || minAgeMonths < 1 || minAgeMonths > MAX_MIN_AGE_MONTHS) {
      this.showError(`L’ancienneté doit être un nombre entier entre 1 et ${MAX_MIN_AGE_MONTHS} mois.`);
      return null;
    }

    if (!Number.isInteger(minQuantity) || minQuantity < 0) {
      this.showError('Le nombre d’exemplaires doit être un entier positif ou nul.');
      return null;
    }

    return {minAgeMonths, minQuantity};
  }

  private async loadCataloguePage(token: string): Promise<void> {
    this.catalogueBooksPage.set(await firstValueFrom(this.api.getBooks(token, {
      search: this.catalogueSearch.trim() || undefined,
      page: this.cataloguePage,
      pageSize: this.cataloguePageSize,
    })));
  }

  private async loadIsbnMetadataQueuePage(token: string): Promise<void> {
    const metadataStatus: 'Pending' | 'Failed' | 'NotFound' | undefined = this.metadataQueueFilter === 'all'
      ? undefined
      : this.metadataQueueFilter === 'pending' ? 'Pending'
        : this.metadataQueueFilter === 'failed' ? 'Failed' : 'NotFound';

    this.isbnMetadataQueue.set(await firstValueFrom(this.api.getBookMetadataQueue(token, {
      page: this.metadataQueuePage,
      pageSize: this.metadataQueuePageSize,
      ...(metadataStatus ? {metadataStatus} : {}),
    })));
  }

  private cancelCatalogueSearch(): void {
    if (this.catalogueSearchTimer !== null && isPlatformBrowser(this.platformId)) {
      window.clearTimeout(this.catalogueSearchTimer);
    }
    this.catalogueSearchTimer = null;
  }

  private async runCatalogueLookup(operation: () => Promise<void>): Promise<void> {
    this.catalogueLookupLoading.set(true);
    this.catalogueLookupError.set(null);
    try {
      await operation();
    } catch (error: unknown) {
      this.catalogueLookupError.set(this.describeCatalogueLookupError(error));
    } finally {
      this.catalogueLookupLoading.set(false);
    }
  }

  private async loadCatalogueCandidateStatus(isbn13: string): Promise<void> {
    this.catalogueLookupLoading.set(true);
    try {
      const token = await this.auth.getApiAccessToken();
      const book = await firstValueFrom(this.api.getBook(token, isbn13));
      if (this.catalogueAddCandidate()?.isbn13 === isbn13) {
        this.catalogueCandidateStatus.set({
          state: 'found',
          quantityAvailable: book.quantityAvailable,
        });
      }
    } catch (error: unknown) {
      if (this.catalogueAddCandidate()?.isbn13 !== isbn13) {
        return;
      }

      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.catalogueCandidateStatus.set({state: 'not-found'});
        return;
      }

      this.catalogueCandidateStatus.set({state: 'error'});
      this.catalogueLookupError.set(this.describeError(error));
    } finally {
      this.catalogueLookupLoading.set(false);
    }
  }

  private toCatalogueCandidate(reference: CatalogBookReference): CatalogBookCandidate | null {
    const isbn13 = this.normalizeIsbn(reference.isbn13 || '');
    if (!isbn13) {
      return null;
    }

    return {
      isbn13,
      workId: reference.workId,
      title: reference.title,
      authors: reference.authors,
      publisher: reference.publisher,
      publicationYear: reference.publicationYear,
      genre: null,
      coverUrl: reference.coverUrl,
      source: reference.source,
    };
  }

  private normalizeIsbn(value: string): string {
    return value.replace(/[\s-]/g, '').toUpperCase();
  }

  private isValidIsbn(value: string): boolean {
    if (/^\d{13}$/.test(value)) {
      const checksum = [...value].reduce(
        (sum, digit, index) => sum + Number(digit) * (index % 2 === 0 ? 1 : 3),
        0,
      );
      return checksum % 10 === 0;
    }

    if (/^\d{9}[\dX]$/.test(value)) {
      const checksum = [...value].reduce(
        (sum, digit, index) => sum + (digit === 'X' ? 10 : Number(digit)) * (10 - index),
        0,
      );
      return checksum % 11 === 0;
    }

    return false;
  }

  private isbn13Equivalent(value: string): string | null {
    if (/^\d{13}$/.test(value)) {
      return value;
    }
    if (!/^\d{9}[\dX]$/.test(value)) {
      return null;
    }

    const body = `978${value.slice(0, 9)}`;
    const checksum = [...body].reduce(
      (sum, digit, index) => sum + Number(digit) * (index % 2 === 0 ? 1 : 3),
      0,
    );
    return `${body}${(10 - (checksum % 10)) % 10}`;
  }

  private describeCatalogueLookupError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) {
        return 'Aucune notice bibliographique ne correspond à cette recherche.';
      }
      if (error.status === 0 || error.status >= 500) {
        return 'Le référentiel externe est momentanément indisponible. Réessayez dans un instant.';
      }
      if (error.status === 400) {
        return 'La recherche bibliographique n’est pas valide. Vérifiez votre saisie.';
      }
    }

    return 'La recherche bibliographique n’a pas pu aboutir. Réessayez dans un instant.';
  }

  private describeCheckoutPassageError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) {
        return 'Aucun passage associé ne correspond à cette référence.';
      }
      if (error.status === 409) {
        return 'Cette référence correspond à plusieurs passages. Utilisez l’identifiant complet.';
      }
    }

    return 'La correction du passage n’a pas abouti. Réessayez dans un instant.';
  }

  private async run(
    action: string,
    operation: (token: string) => Promise<void>,
    errorTarget: WritableSignal<string | null> = this.errorMessage,
    describeError: (error: unknown) => string = error => this.describeError(error),
  ): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.actionPending.set(action);
    this.errorMessage.set(null);
    errorTarget.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      await operation(token);
    } catch (error: unknown) {
      const message = error instanceof CatalogAuthenticationRedirectStartedError
        ? 'Redirection vers Microsoft pour renouveler votre session…'
        : describeError(error);
      if (errorTarget === this.errorMessage) {
        this.showError(message);
      } else {
        errorTarget.set(message);
      }
    } finally {
      this.loading.set(false);
      this.actionPending.set(null);
    }
  }

  private async loadNavigationBadgeCounts(): Promise<void> {
    let token: string;
    try {
      token = await this.auth.getApiAccessToken();
    } catch {
      return;
    }

    const requests: Promise<void>[] = [];
    if (this.isAdministrator()) {
      requests.push(this.captureNavigationBadgeCount(
        'catalogue',
        () => firstValueFrom(this.api.getBooks(token, {page: 1, pageSize: 1})),
      ));
      requests.push(this.captureNavigationBadgeCount(
        'isbn-queue',
        () => firstValueFrom(this.api.getBookMetadataQueue(token, {page: 1, pageSize: 1})),
      ));
      requests.push(this.captureNavigationBadgeCount(
        'isbndb-absent',
        () => firstValueFrom(this.api.getIsbndbAbsentBooks(token, {
          status: 'to-complete', page: 1, pageSize: 1,
        })),
      ));
    }
    if (this.hasAdministrationAccess()) {
      requests.push(this.captureNavigationBadgeCount(
        'rare-books',
        () => firstValueFrom(this.api.getRareBooks(token, {status: 'Published', page: 1, pageSize: 1})),
      ));
    }

    await Promise.all(requests);
  }

  private async captureNavigationBadgeCount(
    section: CatalogAdminNavigationBadgeSection,
    load: () => Promise<{totalCount: number}>,
  ): Promise<void> {
    try {
      const page = await load();
      this.navigationBadgeCounts.update(counts => ({...counts, [section]: page.totalCount}));
    } catch {
      // The counts are supplemental; opening a workspace still loads its full data.
    }
  }

  private validPage(page: number, response: {totalCount: number; pageSize: number} | null): boolean {
    return Number.isInteger(page) && page >= 1 && (!response || page <= this.pageCount(response));
  }

  private confirmAction(message: string): boolean {
    return !isPlatformBrowser(this.platformId) || window.confirm(message);
  }

  private showError(message: string): void {
    this.successMessage.set(null);
    this.errorMessage.set(message);
  }

  private showSuccess(message: string): void {
    this.errorMessage.set(null);
    this.successMessage.set(message);
  }

  private clearFeedback(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  private optional(value: string): string | null {
    return value.trim() || null;
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        return 'La session d’administration a expiré. Reconnectez-vous pour continuer.';
      }
      if (error.status === 403) {
        return 'Le compte connecté ne possède pas les droits d’administration.';
      }
      if (error.status === 404) {
        return 'La ressource demandée n’existe plus ou a été déplacée.';
      }
      if (error.status === 409) {
        return 'Cette action est refusée car l’état du catalogue a changé. Rechargez la fiche.';
      }
      if (error.status === 400) {
        return 'Les données saisies ne sont pas valides. Vérifiez les champs puis réessayez.';
      }
    }

    return 'L’opération n’a pas pu être effectuée. Réessayez dans un instant.';
  }

  private describeIsbndbRecheckError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 429) {
        return 'Le quota ISBNdb est épuisé. Réessayez après son renouvellement.';
      }
      if (error.status === 409) {
        return 'Cette fiche est déjà marquée comme complétée. Rouvrez-la avant de la revérifier.';
      }
      if (error.status === 0 || error.status >= 500) {
        return 'Le service ISBNdb est momentanément indisponible. Réessayez plus tard.';
      }
    }
    return 'La vérification ISBNdb n’a pas pu aboutir. Réessayez dans un instant.';
  }

  private describeCreateAccountError(error: unknown): string {
    if (error instanceof HttpErrorResponse && isTemporaryPasswordPolicyProblemDetail(error.error?.detail)) {
      return error.error.detail;
    }

    return this.describeError(error);
  }

  private describeAccountError(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
      return 'Le répertoire des comptes Entra est temporairement indisponible. Vérifiez sa configuration côté API ou réessayez plus tard.';
    }

    return this.describeError(error);
  }

}
