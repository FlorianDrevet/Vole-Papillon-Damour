import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  PLATFORM_ID,
  Signal,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {
  CatalogAdminAccount,
  CatalogAdminAccountFilters,
  CatalogAdminAccountPage,
  CatalogAdminAccountRole,
  CatalogAdminAlert,
  CatalogAdminAlertFilters,
  CatalogAdminAlertPage,
  CatalogAdminBook,
  CatalogAdminBookFilters,
  CatalogAdminBookPage,
  CatalogAdminFair,
  CatalogAdminFairPage,
  CatalogAdminFairStats,
  CatalogAdminMemberDetail,
  CatalogAdminMemberFilters,
  CatalogAdminMemberPage,
  CatalogAdminOverview,
  CatalogAdminCreateAccountRequest,
  CatalogAdminScanSession,
  CatalogAdminScanSessionPage,
  CatalogAdminSessionFilters,
  CatalogAdminSettings,
  CatalogDeadStockBook,
} from '../../core/catalog.models';
import {toDeadStockCsv} from './dead-stock-export';

const DEFAULT_MIN_AGE_MONTHS = 6;
const DEFAULT_MIN_QUANTITY = 3;
const MAX_MIN_AGE_MONTHS = 120_000;

export type CatalogAdminSection =
  | 'overview'
  | 'sessions'
  | 'dead-stock'
  | 'inventory'
  | 'catalogue'
  | 'fairs'
  | 'alerts'
  | 'members'
  | 'accounts'
  | 'settings';

export type CatalogAdminOverviewPeriod = '30-days' | '3-months' | 'year';

type CatalogAdminNavIcon =
  | 'dashboard'
  | 'scan'
  | 'dead-stock'
  | 'catalogue'
  | 'inventory'
  | 'fairs'
  | 'accounts'
  | 'settings';

interface CatalogAdminNavItem {
  id: CatalogAdminSection;
  label: string;
  icon: CatalogAdminNavIcon;
}

interface CatalogAdminNavGroup {
  label: string;
  items: CatalogAdminNavItem[];
}

@Component({
  selector: 'app-catalog-administration-page',
  standalone: false,
  templateUrl: './catalog-administration-page.component.html',
  styleUrls: ['./catalog-administration-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAdministrationPageComponent implements OnInit {
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly isAdministrator: Signal<boolean>;
  readonly authError: Signal<string | null>;
  readonly activeSection = signal<CatalogAdminSection>('overview');
  readonly loading = signal(false);
  readonly actionPending = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly navGroups: CatalogAdminNavGroup[] = [
    {
      label: 'Pendant la bourse',
      items: [
        {id: 'overview', label: 'Tableau de bord', icon: 'dashboard'},
        {id: 'sessions', label: 'Sessions de scan', icon: 'scan'},
        {id: 'dead-stock', label: 'Désengorgement', icon: 'dead-stock'},
      ],
    },
    {
      label: 'Le fonds de livres',
      items: [
        {id: 'catalogue', label: 'Catalogue', icon: 'catalogue'},
        {id: 'inventory', label: 'Inventaire', icon: 'inventory'},
        {id: 'fairs', label: 'Statistiques par bourse', icon: 'fairs'},
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
  readonly navItems: CatalogAdminNavItem[] = this.navGroups.flatMap(group => group.items);

  navBadge(section: CatalogAdminSection): string | null {
    const totalCount = section === 'sessions'
      ? this.sessionsPage() ? this.correctableSessionCount() : undefined
      : section === 'catalogue'
        ? this.booksPage()?.totalCount
        : undefined;

    return totalCount === undefined ? null : this.formatNumber(totalCount);
  }

  readonly overview = signal<CatalogAdminOverview | null>(null);
  readonly booksPage = signal<CatalogAdminBookPage | null>(null);
  readonly selectedBook = signal<CatalogAdminBook | null>(null);
  readonly fairsPage = signal<CatalogAdminFairPage | null>(null);
  readonly selectedFairStats = signal<CatalogAdminFairStats | null>(null);
  readonly sessionsPage = signal<CatalogAdminScanSessionPage | null>(null);
  readonly selectedSession = signal<CatalogAdminScanSession | null>(null);
  readonly alertsPage = signal<CatalogAdminAlertPage | null>(null);
  readonly membersPage = signal<CatalogAdminMemberPage | null>(null);
  readonly selectedMember = signal<CatalogAdminMemberDetail | null>(null);
  readonly settings = signal<CatalogAdminSettings | null>(null);
  readonly accountsPage = signal<CatalogAdminAccountPage | null>(null);
  readonly accountErrorMessage = signal<string | null>(null);
  readonly editingAccountId = signal<string | null>(null);
  readonly editingAccountRoles = signal<CatalogAdminAccountRole[]>([]);
  readonly showCreateAccount = signal(false);

  readonly deadStockBooks = signal<CatalogDeadStockBook[]>([]);
  readonly deadStockGeneratedAt = signal<string | null>(null);

  minAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  minQuantity = DEFAULT_MIN_QUANTITY;

  bookSearch = '';
  bookMetadataStatus = 'Missing';
  bookRareOnly = false;
  bookHiddenOnly = false;
  bookUndatedOnly = false;
  bookPage = 1;
  readonly bookPageSize = 25;

  sessionStatus = '';
  sessionFrom = '';
  sessionTo = '';
  sessionPage = 1;
  readonly sessionPageSize = 25;
  sessionMode = 'AvailableNow';
  sessionFairId = '';
  sessionPreset: 'correctable' | 'all' | 'open' | 'alerts' = 'correctable';
  overviewPeriod: CatalogAdminOverviewPeriod = '30-days';

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
  readonly accountRoleOptions: {value: CatalogAdminAccountRole; label: string}[] = [
    {value: 'Tri', label: 'Tri'},
    {value: 'Caisse', label: 'Caisse'},
    {value: 'Administration', label: 'Administration'},
  ];
  readonly createAccountForm: CatalogAdminCreateAccountRequest = {
    email: '',
    displayName: '',
    temporaryPassword: '',
    roles: [],
  };

  readonly addBookForm = {
    isbn13: '',
    quantityAvailable: 1,
    note: 'Ajout manuel depuis le catalogue',
    title: '',
    authors: '',
    publisher: '',
    publicationYear: null as number | null,
    physicalFormat: '',
    language: '',
    genre: '',
    workId: '',
  };
  readonly metadataForm = {
    title: '',
    authors: '',
    publisher: '',
    publicationYear: null as number | null,
    physicalFormat: '',
    language: '',
    genre: '',
    workId: '',
  };
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
    updatedAt: '',
    updatedBy: '',
  };
  revenueInput: number | null = null;
  deadStockAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  sessionIdleHours = 2;
  alertDelayHours = 2;

  private readonly platformId = inject(PLATFORM_ID);

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogAdminApiService,
    private readonly meta: Meta,
  ) {
    this.initialized = this.auth.initialized;
    this.isAuthenticated = this.auth.isAuthenticated;
    this.isAdministrator = this.auth.isAdministrator;
    this.authError = this.auth.error;
  }

  ngOnInit(): void {
    this.meta.updateTag({name: 'robots', content: 'noindex, nofollow'});
    void this.initialize();
  }

  async initialize(): Promise<void> {
    await this.auth.initialize();
    if (this.auth.isAuthenticated()) {
      await this.loadOverview();
      await this.loadSessions();
      await this.loadDeadStock();
    }
  }

  async login(): Promise<void> {
    this.clearFeedback();
    try {
      await this.auth.login('/administration');
    } catch {
      this.errorMessage.set('La connexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  async selectSection(section: CatalogAdminSection): Promise<void> {
    this.activeSection.set(section);
    if (!this.auth.isAuthenticated()) {
      return;
    }

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
      case 'inventory':
        await this.loadOverview();
        break;
      case 'catalogue':
        await this.loadBooks();
        break;
      case 'fairs':
        await this.loadFairs();
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

  async loadOverview(): Promise<void> {
    await this.run('overview', async token => {
      const overview = this.overviewPeriod === '30-days'
        ? this.api.getOverview(token)
        : this.api.getOverview(token, ...this.overviewPeriodBounds(this.overviewPeriod));
      this.overview.set(await firstValueFrom(overview));
    });
  }

  setOverviewPeriod(period: CatalogAdminOverviewPeriod): void {
    if (this.overviewPeriod === period) {
      return;
    }

    this.overviewPeriod = period;
    if (this.auth.isAuthenticated()) {
      void this.loadOverview();
    }
  }

  async loadBooks(): Promise<void> {
    const filters: CatalogAdminBookFilters = {
      search: this.bookSearch.trim() || undefined,
      metadataStatus: this.bookMetadataStatus || undefined,
      rare: this.bookRareOnly ? true : undefined,
      hidden: this.bookHiddenOnly ? true : undefined,
      undated: this.bookUndatedOnly ? true : undefined,
      page: this.bookPage,
      pageSize: this.bookPageSize,
    };

    await this.run('books', async token => {
      this.booksPage.set(await firstValueFrom(this.api.getBooks(token, filters)));
    });
  }

  async openBook(isbn13: string): Promise<void> {
    this.activeSection.set('catalogue');
    await this.run('book-detail', async token => {
      const book = await firstValueFrom(this.api.getBook(token, isbn13));
      this.selectedBook.set(book);
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
        physicalFormat: book.physicalFormat || '',
        language: book.language || '',
        genre: book.genre || '',
        workId: book.workId || '',
      });
    });
  }

  async addBook(): Promise<void> {
    if (!this.addBookForm.isbn13.trim() || !this.addBookForm.note.trim()) {
      this.errorMessage.set('ISBN et note d’ajout sont obligatoires.');
      return;
    }

    await this.run('add-book', async token => {
      await firstValueFrom(this.api.addBook(token, {
        isbn13: this.addBookForm.isbn13.trim(),
        quantityAvailable: Number(this.addBookForm.quantityAvailable),
        note: this.addBookForm.note.trim(),
        title: this.optional(this.addBookForm.title),
        authors: this.optional(this.addBookForm.authors),
        publisher: this.optional(this.addBookForm.publisher),
        publicationYear: this.addBookForm.publicationYear,
        physicalFormat: this.optional(this.addBookForm.physicalFormat),
        language: this.optional(this.addBookForm.language),
        genre: this.optional(this.addBookForm.genre),
        workId: this.optional(this.addBookForm.workId),
      }));
      this.successMessage.set('La fiche a été ajoutée au catalogue.');
      await this.loadBooks();
    });
  }

  async updateMetadata(book: CatalogAdminBook): Promise<void> {
    await this.run('metadata', async token => {
      await firstValueFrom(this.api.updateMetadata(token, book.isbn13, {
        title: this.optional(this.metadataForm.title),
        authors: this.optional(this.metadataForm.authors),
        publisher: this.optional(this.metadataForm.publisher),
        publicationYear: this.metadataForm.publicationYear,
        physicalFormat: this.optional(this.metadataForm.physicalFormat),
        language: this.optional(this.metadataForm.language),
        genre: this.optional(this.metadataForm.genre),
        coverBlobRef: null,
        workId: this.optional(this.metadataForm.workId),
        fields: ['Title', 'Authors', 'Publisher', 'PublicationYear', 'PhysicalFormat', 'Language', 'Genre', 'WorkId'],
      }));
      this.successMessage.set('Les métadonnées ont été enregistrées.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  async correctBookQuantity(book: CatalogAdminBook): Promise<void> {
    const quantity = Number(this.quantityCorrection);
    if (!Number.isInteger(quantity) || quantity < 0 || !this.quantityNote.trim()) {
      this.errorMessage.set('La quantité doit être un entier positif et la note est obligatoire.');
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
      this.successMessage.set('La correction de stock a été journalisée.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  async withdrawBook(book: CatalogAdminBook): Promise<void> {
    const quantity = Number(this.withdrawalQuantity);
    if (!Number.isInteger(quantity) || quantity <= 0 || !this.withdrawalNote.trim()) {
      this.errorMessage.set('La quantité retirée doit être positive et la note est obligatoire.');
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
      this.successMessage.set('Le retrait a été journalisé.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  async correctAnnouncement(book: CatalogAdminBook, announcementId: string): Promise<void> {
    const quantity = Number(this.announcementQuantities[announcementId]);
    if (!Number.isInteger(quantity) || quantity < 0 || !this.announcementNote.trim()) {
      this.errorMessage.set('La quantité annoncée doit être positive ou nulle et la note est obligatoire.');
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
      this.successMessage.set('La correction de l’annonce a été journalisée.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  setAnnouncementQuantity(announcementId: string, value: number | string): void {
    this.announcementQuantities[announcementId] = Number(value);
  }

  async toggleRare(book: CatalogAdminBook): Promise<void> {
    await this.run('rare', async token => {
      await firstValueFrom(this.api.setRare(token, book.isbn13, !book.isRare));
      this.successMessage.set(book.isRare ? 'Le signal rare a été retiré.' : 'Le livre est marqué comme rare.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  async toggleVisibility(book: CatalogAdminBook): Promise<void> {
    await this.run('visibility', async token => {
      await firstValueFrom(this.api.setVisibility(token, book.isbn13, !book.isHidden));
      this.successMessage.set(book.isHidden ? 'La fiche est à nouveau visible.' : 'La fiche est masquée du catalogue public.');
      await this.openBook(book.isbn13);
      await this.loadBooks();
    });
  }

  async mergeBook(book: CatalogAdminBook): Promise<void> {
    const target = this.mergeTargetIsbn13.trim();
    if (!target || !this.mergeNote.trim()) {
      this.errorMessage.set('ISBN cible et note de fusion sont obligatoires.');
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
      this.successMessage.set('La fiche source est redirigée vers la fiche canonique.');
      this.selectedBook.set(null);
      await this.loadBooks();
    });
  }

  async deleteBook(book: CatalogAdminBook): Promise<void> {
    if (!this.confirmAction(`Supprimer définitivement la fiche ${book.isbn13} ?`)) {
      return;
    }

    await this.run('delete-book', async token => {
      await firstValueFrom(this.api.deleteBook(token, book.isbn13));
      this.successMessage.set('La fiche a été supprimée.');
      this.selectedBook.set(null);
      await this.loadBooks();
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
      this.errorMessage.set('La recette doit être positive ou vide.');
      return;
    }

    await this.run('revenue', async token => {
      await firstValueFrom(this.api.setFairRevenue(token, stats.fair.id, revenue));
      this.successMessage.set('La recette de la bourse aux livres a été enregistrée.');
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

  private overviewPeriodBounds(period: CatalogAdminOverviewPeriod): [string, string] {
    const to = new Date();
    const from = new Date(to);
    if (period === '30-days') {
      from.setUTCDate(from.getUTCDate() - 30);
    } else if (period === '3-months') {
      this.subtractCalendarMonths(from, 3);
    } else {
      this.subtractCalendarYears(from, 1);
    }

    return [from.toISOString(), to.toISOString()];
  }

  private subtractCalendarMonths(date: Date, months: number): void {
    const day = date.getUTCDate();
    date.setUTCDate(1);
    date.setUTCMonth(date.getUTCMonth() - months);
    date.setUTCDate(Math.min(day, this.daysInUtcMonth(date)));
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
        return sessions.filter(session => session.status === 'Open');
      case 'alerts':
        return sessions.filter(session => session.pendingAlertCount === 0 && session.alertCount > 0);
      default:
        return sessions;
    }
  }

  correctableSessionCount(): number {
    return (this.sessionsPage()?.sessions ?? []).filter(session => this.sessionNeedsCorrection(session)).length;
  }

  alertSessionCount(): number {
    return (this.sessionsPage()?.sessions ?? []).filter(session => session.pendingAlertCount === 0 && session.alertCount > 0).length;
  }

  pendingAlertSessions(): CatalogAdminScanSession[] {
    return (this.sessionsPage()?.sessions ?? [])
      .filter(session => session.pendingAlertCount > 0)
      .slice(0, 2);
  }

  async openSession(sessionId: string): Promise<void> {
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
    if (!session || !this.confirmAction('Retirer ce mouvement du stock ? Une correction sera tracée.')) {
      return;
    }

    await this.run('remove-movement', async token => {
      await firstValueFrom(this.api.removeMovement(token, session.id, movementId));
      this.successMessage.set('Le mouvement a été renversé et reste visible dans le ledger.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  async reassignSession(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.confirmAction('Rejouer cette session avec une autre destination ?')) {
      return;
    }

    await this.run('reassign-session', async token => {
      await firstValueFrom(this.api.reassignSession(token, session.id, {
        mode: this.sessionMode,
        targetAssoEventsId: this.sessionFairId || null,
      }));
      this.successMessage.set('La session a été corrigée et son historique est conservé.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  async cancelSession(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.confirmAction('Annuler cette session et renverser ses mouvements ?')) {
      return;
    }

    await this.run('cancel-session', async token => {
      await firstValueFrom(this.api.cancelSession(token, session.id));
      this.successMessage.set('La session a été annulée avec une correction tracée.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  async cancelSessionAlerts(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.confirmAction('Annuler les alertes encore en attente de cette session ?')) {
      return;
    }

    await this.run('cancel-session-alerts', async token => {
      await firstValueFrom(this.api.cancelSessionAlerts(token, session.id));
      this.successMessage.set('Les alertes non envoyées ont été annulées.');
      await this.openSession(session.id);
      await this.loadSessions();
    });
  }

  async forceSessionAlerts(): Promise<void> {
    const session = this.selectedSession();
    if (!session || !this.confirmAction('Forcer l’envoi des alertes en attente de cette session ?')) {
      return;
    }

    await this.run('force-session-alerts', async token => {
      await firstValueFrom(this.api.forceSessionAlerts(token, session.id));
      this.successMessage.set('Les alertes en attente ont été forcées.');
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
      this.successMessage.set('L’alerte a été annulée.');
      await this.loadAlerts();
    });
  }

  async forceAlert(alert: CatalogAdminAlert): Promise<void> {
    if (!this.confirmAction('Forcer l’envoi de cette alerte maintenant ?')) {
      return;
    }
    await this.run('force-alert', async token => {
      await firstValueFrom(this.api.forceAlert(token, alert.id));
      this.successMessage.set('L’alerte a été forcée.');
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

  async createAccount(): Promise<void> {
    const form = this.createAccountForm;
    if (!form.email.trim() || !form.displayName.trim() || form.temporaryPassword.length < 8 || form.roles.length === 0) {
      this.errorMessage.set('E-mail, nom, mot de passe temporaire et au moins un droit sont obligatoires.');
      return;
    }

    await this.run('create-account', async token => {
      await firstValueFrom(this.api.createAdminAccount(token, {
        email: form.email.trim(),
        displayName: form.displayName.trim(),
        temporaryPassword: form.temporaryPassword,
        roles: [...form.roles],
      }));
      this.successMessage.set('Le compte bénévole a été créé.');
      form.email = '';
      form.displayName = '';
      form.temporaryPassword = '';
      form.roles = [];
      this.showCreateAccount.set(false);
      await this.loadAccounts();
    });
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
      this.errorMessage.set('Un compte doit conserver au moins un droit.');
      return;
    }

    await this.run('account-roles', async token => {
      await firstValueFrom(this.api.updateAdminAccountRoles(
        token,
        account.externalId,
        this.editingAccountRoles(),
      ));
      this.successMessage.set('Les droits du compte ont été mis à jour.');
      this.cancelAccountRoleEdit();
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

  async toggleMemberBlocked(member: CatalogAdminMemberDetail): Promise<void> {
    const blocked = member.member.alertStatus === 'Blocked';
    const action = blocked ? 'réactiver' : 'bloquer';
    if (!this.confirmAction(`Voulez-vous ${action} les alertes de ce membre ?`)) {
      return;
    }

    await this.run('member-alert-status', async token => {
      await firstValueFrom(this.api.setAlertStatus(token, member.member.id, blocked));
      this.successMessage.set(blocked ? 'Les alertes du membre sont réactivées.' : 'Les alertes du membre sont bloquées.');
      await this.openMember(member.member.id);
      await this.loadMembers();
    });
  }

  async deleteMember(member: CatalogAdminMemberDetail): Promise<void> {
    if (!this.confirmAction(`Supprimer le compte de ${member.member.displayName || member.member.email || 'ce membre'} ?`)) {
      return;
    }

    await this.run('delete-member', async token => {
      await firstValueFrom(this.api.deleteMember(token, member.member.id));
      this.successMessage.set('La demande de suppression du membre a été enregistrée.');
      this.selectedMember.set(null);
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
      this.deadStockAgeMonths,
    ];
    const hourFields = [this.sessionIdleHours, this.alertDelayHours];
    if (integerFields.some(value => !Number.isInteger(Number(value)) || Number(value) < 0)
      || hourFields.some(value => !Number.isFinite(Number(value)) || Number(value) < 0)) {
      this.errorMessage.set('Les seuils doivent être des entiers positifs ou nuls ; les durées peuvent être exprimées par demi-heure.');
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
      }));
      this.settings.set(settings);
      this.settingsForm = {...settings};
      this.successMessage.set('Les paramètres ont été enregistrés.');
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

  goToBooksPage(page: number): void {
    if (this.validPage(page, this.booksPage())) {
      this.bookPage = page;
      void this.loadBooks();
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
    return session.pendingAlertCount > 0 && session.status !== 'Cancelled';
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
      this.errorMessage.set(`L’ancienneté doit être un nombre entier entre 1 et ${MAX_MIN_AGE_MONTHS} mois.`);
      return null;
    }

    if (!Number.isInteger(minQuantity) || minQuantity < 0) {
      this.errorMessage.set('Le nombre d’exemplaires doit être un entier positif ou nul.');
      return null;
    }

    return {minAgeMonths, minQuantity};
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
      errorTarget.set(error instanceof CatalogAuthenticationRedirectStartedError
        ? 'Redirection vers Microsoft pour renouveler votre session…'
        : describeError(error));
    } finally {
      this.loading.set(false);
      this.actionPending.set(null);
    }
  }

  private validPage(page: number, response: {totalCount: number; pageSize: number} | null): boolean {
    return Number.isInteger(page) && page >= 1 && (!response || page <= this.pageCount(response));
  }

  private confirmAction(message: string): boolean {
    return !isPlatformBrowser(this.platformId) || window.confirm(message);
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

  private describeAccountError(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
      return 'Le répertoire des comptes Entra est temporairement indisponible. Vérifiez sa configuration côté API ou réessayez plus tard.';
    }

    return this.describeError(error);
  }

}
