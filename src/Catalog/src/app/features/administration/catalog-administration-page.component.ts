import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  PLATFORM_ID,
  Signal,
  computed,
  inject,
  signal,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import type {AccountInfo} from '@azure/msal-browser';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {
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
  | 'catalogue'
  | 'fairs'
  | 'alerts'
  | 'members'
  | 'settings';

interface CatalogAdminNavItem {
  id: CatalogAdminSection;
  label: string;
  icon: string;
  hint?: string;
}

@Component({
  selector: 'app-catalog-administration-page',
  standalone: false,
  templateUrl: './catalog-administration-page.component.html',
  styleUrls: ['./catalog-administration-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAdministrationPageComponent implements OnInit {
  readonly account: Signal<AccountInfo | null>;
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly authError: Signal<string | null>;
  readonly accountLabel: Signal<string>;
  readonly activeSection = signal<CatalogAdminSection>('overview');
  readonly loading = signal(false);
  readonly actionPending = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly navItems: CatalogAdminNavItem[] = [
    {id: 'overview', label: 'Tableau de bord', icon: '⌂'},
    {id: 'sessions', label: 'Sessions de scan', icon: '⌁'},
    {id: 'dead-stock', label: 'Désengorgement', icon: '↘'},
    {id: 'catalogue', label: 'Catalogue & métadonnées', icon: '◎'},
    {id: 'fairs', label: 'Bilan des bourses', icon: '▧'},
    {id: 'alerts', label: 'Files d’alertes', icon: '◇'},
    {id: 'members', label: 'Comptes & rôles', icon: '♙'},
    {id: 'settings', label: 'Paramètres', icon: '⚙'},
  ];

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

  readonly deadStockBooks = signal<CatalogDeadStockBook[]>([]);
  readonly deadStockGeneratedAt = signal<string | null>(null);

  minAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  minQuantity = DEFAULT_MIN_QUANTITY;

  bookSearch = '';
  bookMetadataStatus = '';
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

  alertStatus = '';
  alertPage = 1;
  readonly alertPageSize = 25;

  memberSearch = '';
  memberAlertStatus = '';
  memberPage = 1;
  readonly memberPageSize = 25;

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
    duplicateThreshold: 2,
    demandSalesThreshold: 3,
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

  private readonly platformId = inject(PLATFORM_ID);

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogAdminApiService,
    private readonly meta: Meta,
  ) {
    this.account = this.auth.account;
    this.initialized = this.auth.initialized;
    this.isAuthenticated = this.auth.isAuthenticated;
    this.authError = this.auth.error;
    this.accountLabel = computed(() => this.displayAccount(this.account()));
  }

  ngOnInit(): void {
    this.meta.updateTag({name: 'robots', content: 'noindex, nofollow'});
    void this.initialize();
  }

  async initialize(): Promise<void> {
    await this.auth.initialize();
    if (this.auth.isAuthenticated()) {
      await this.loadOverview();
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

  async logout(): Promise<void> {
    try {
      await this.auth.logout();
    } catch {
      this.errorMessage.set('La déconnexion n’a pas pu être démarrée. Réessayez.');
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
      case 'catalogue':
        await this.loadBooks();
        break;
      case 'fairs':
        await this.loadFairs();
        break;
      case 'alerts':
        await this.loadAlerts();
        break;
      case 'members':
        await this.loadMembers();
        break;
      case 'settings':
        await this.loadSettings();
        break;
    }
  }

  async loadOverview(): Promise<void> {
    await this.run('overview', async token => {
      this.overview.set(await firstValueFrom(this.api.getOverview(token)));
    });
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
      this.fairsPage.set(await firstValueFrom(this.api.getFairs(token)));
    });
  }

  async openFairStats(fair: CatalogAdminFair): Promise<void> {
    await this.run('fair-stats', async token => {
      const stats = await firstValueFrom(this.api.getFairStats(token, fair.id));
      this.selectedFairStats.set(stats);
      this.revenueInput = stats.revenue;
    });
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
      this.successMessage.set('La recette de la bourse a été enregistrée.');
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

  async openSession(sessionId: string): Promise<void> {
    await this.run('session-detail', async token => {
      this.selectedSession.set(await firstValueFrom(this.api.getSession(token, sessionId)));
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
    });
  }

  async saveSettings(): Promise<void> {
    const numericFields = [
      this.settingsForm.duplicateThreshold,
      this.settingsForm.demandSalesThreshold,
      this.settingsForm.deadStockMinAgeDays,
      this.settingsForm.deadStockMinQuantity,
      this.settingsForm.watchlistMaxItems,
      this.settingsForm.alertCooldownDays,
      this.settingsForm.sessionIdleTimeoutMinutes,
      this.settingsForm.alertDelayMinutes,
    ];
    if (numericFields.some(value => !Number.isInteger(Number(value)) || Number(value) < 0)) {
      this.errorMessage.set('Tous les réglages doivent être des nombres entiers positifs ou nuls.');
      return;
    }

    await this.run('save-settings', async token => {
      const settings = await firstValueFrom(this.api.updateSettings(token, {
        duplicateThreshold: Number(this.settingsForm.duplicateThreshold),
        demandSalesThreshold: Number(this.settingsForm.demandSalesThreshold),
        deadStockMinAgeDays: Number(this.settingsForm.deadStockMinAgeDays),
        deadStockMinQuantity: Number(this.settingsForm.deadStockMinQuantity),
        watchlistMaxItems: Number(this.settingsForm.watchlistMaxItems),
        alertCooldownDays: Number(this.settingsForm.alertCooldownDays),
        sessionIdleTimeoutMinutes: Number(this.settingsForm.sessionIdleTimeoutMinutes),
        alertDelayMinutes: Number(this.settingsForm.alertDelayMinutes),
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

  statusLabel(value: string | null | undefined): string {
    const labels: Record<string, string> = {
      Active: 'Active',
      Open: 'Ouverte',
      Closed: 'Clôturée',
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

  private async run(action: string, operation: (token: string) => Promise<void>): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.actionPending.set(action);
    this.errorMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      await operation(token);
    } catch (error: unknown) {
      this.errorMessage.set(error instanceof CatalogAuthenticationRedirectStartedError
        ? 'Redirection vers Microsoft pour renouveler votre session…'
        : this.describeError(error));
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

  private displayAccount(account: AccountInfo | null): string {
    return account?.name?.trim() || account?.username || '';
  }
}
