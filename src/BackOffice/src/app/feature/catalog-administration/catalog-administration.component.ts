import {Component, OnInit, inject, signal} from '@angular/core';
import {FormBuilder, Validators} from '@angular/forms';

import {
  AdminAccount,
  AdminAccountFilters,
  AdminAccountRole,
  AdminBook,
  AdminBookFilters,
  AdminFair,
  AdminMemberDetail,
  AdminScanSession,
  AdminSessionFilters,
  CatalogAdminOverview,
} from '../../shared/models/catalog-admin.model';
import {CatalogAdminFacadeService} from '../../shared/facades/catalog-admin.facade.service';

type AdminTab = 'overview' | 'books' | 'fairs' | 'sessions' | 'alerts' | 'members' | 'accounts' | 'settings';

@Component({
  selector: 'app-catalog-administration',
  templateUrl: './catalog-administration.component.html',
  styleUrls: ['./catalog-administration.component.scss'],
  standalone: false,
})
export class CatalogAdministrationComponent implements OnInit {
  private readonly facade = inject(CatalogAdminFacadeService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly activeTab = signal<AdminTab>('overview');
  protected readonly isLoading = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly overview = signal<CatalogAdminOverview | null>(null);
  protected readonly books = signal<AdminBook[]>([]);
  protected readonly selectedBook = signal<AdminBook | null>(null);
  protected readonly bookTotal = signal(0);
  protected readonly bookPage = signal(1);
  protected readonly fairs = signal<AdminFair[]>([]);
  protected readonly selectedFair = signal<AdminFair | null>(null);
  protected readonly fairStats = signal<any | null>(null);
  protected readonly sessions = signal<AdminScanSession[]>([]);
  protected readonly selectedSession = signal<AdminScanSession | null>(null);
  protected readonly sessionTotal = signal(0);
  protected readonly alerts = signal<any[]>([]);
  protected readonly members = signal<any[]>([]);
  protected readonly selectedMember = signal<AdminMemberDetail | null>(null);
  protected readonly memberTotal = signal(0);
  protected readonly accounts = signal<AdminAccount[]>([]);
  protected readonly accountTotal = signal(0);
  protected readonly accountPage = signal(1);
  protected readonly editingAccountId = signal<string | null>(null);
  protected readonly editingRoles = signal<AdminAccountRole[]>([]);
  protected readonly settings = signal<any | null>(null);

  protected bookSearch = '';
  protected metadataStatus = '';
  protected rareFilter = '';
  protected hiddenFilter = '';
  protected undatedFilter = '';
  protected sessionStatus = '';
  protected memberSearch = '';
  protected memberAlertStatus = '';
  protected alertStatus = '';
  protected accountSearch = '';
  protected showAddBook = signal(false);
  protected showCreateAccount = signal(false);
  protected announcementToCorrect = signal<string | null>(null);
  protected revenueInput = '';
  protected reassignMode = 'AvailableNow';
  protected reassignFairId = '';

  protected readonly accountRoleOptions: {value: AdminAccountRole; label: string}[] = [
    {value: 'Tri', label: 'Tri'},
    {value: 'Caisse', label: 'Caisse'},
    {value: 'Administration', label: 'Administrateur'},
  ];

  protected readonly addBookForm = this.formBuilder.nonNullable.group({
    isbn13: ['', [Validators.required, Validators.pattern(/^\d{13}$/)]],
    quantityAvailable: [0, [Validators.required, Validators.min(0), Validators.max(100000)]],
    note: ['Ajout administratif', [Validators.required, Validators.maxLength(500)]],
    title: [''],
    authors: [''],
    publisher: [''],
    publicationYear: [0],
    physicalFormat: [''],
    language: [''],
    genre: [''],
    workId: [''],
  });

  protected readonly metadataForm = this.formBuilder.nonNullable.group({
    title: [''],
    authors: [''],
    publisher: [''],
    publicationYear: [0],
    physicalFormat: [''],
    language: [''],
    genre: [''],
    coverBlobRef: [''],
    workId: [''],
  });

  protected readonly quantityForm = this.formBuilder.nonNullable.group({
    quantityAvailable: [0, [Validators.required, Validators.min(0), Validators.max(100000)]],
    note: ['Correction d’inventaire', [Validators.required, Validators.maxLength(500)]],
  });

  protected readonly withdrawalForm = this.formBuilder.nonNullable.group({
    quantity: [1, [Validators.required, Validators.min(1), Validators.max(100000)]],
    note: ['Retrait administratif', [Validators.required, Validators.maxLength(500)]],
  });

  protected readonly announcementForm = this.formBuilder.nonNullable.group({
    quantity: [1, [Validators.required, Validators.min(1), Validators.max(100000)]],
    note: ['Correction d’annonce', [Validators.required, Validators.maxLength(500)]],
  });

  protected readonly mergeForm = this.formBuilder.nonNullable.group({
    targetIsbn13: ['', [Validators.required, Validators.pattern(/^\d{13}$/)]],
    note: ['Fusion de fiches', [Validators.required, Validators.maxLength(500)]],
  });

  protected readonly settingsForm = this.formBuilder.nonNullable.group({
    duplicateThreshold: [5, [Validators.required, Validators.min(0)]],
    demandSalesThreshold: [1, [Validators.required, Validators.min(0)]],
    deadStockMinAgeDays: [30, [Validators.required, Validators.min(0)]],
    deadStockMinQuantity: [1, [Validators.required, Validators.min(0)]],
    watchlistMaxItems: [100, [Validators.required, Validators.min(1)]],
    alertCooldownDays: [30, [Validators.required, Validators.min(0)]],
    sessionIdleTimeoutMinutes: [120, [Validators.required, Validators.min(1)]],
    alertDelayMinutes: [120, [Validators.required, Validators.min(0)]],
  });

  protected readonly createAccountForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    temporaryPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(256)]],
    roles: this.formBuilder.nonNullable.control<AdminAccountRole[]>([], Validators.required),
  });

  ngOnInit(): void {
    this.loadOverview();
  }

  protected selectTab(tab: AdminTab): void {
    this.activeTab.set(tab);
    this.clearMessages();
    switch (tab) {
      case 'overview': this.loadOverview(); break;
      case 'books': this.loadBooks(); break;
      case 'fairs': this.loadFairs(); break;
      case 'sessions': this.loadSessions(); break;
      case 'alerts': this.loadAlerts(); break;
      case 'members': this.loadMembers(); break;
      case 'accounts': this.loadAccounts(); break;
      case 'settings': this.loadSettings(); break;
    }
  }

  protected loadOverview(): void {
    this.load(() => this.facade.getOverview(), value => this.overview.set(value));
  }

  protected loadBooks(page = 1): void {
    const filters: AdminBookFilters = {
      search: this.bookSearch.trim() || undefined,
      metadataStatus: this.metadataStatus || undefined,
      rare: this.rareFilter === '' ? undefined : this.rareFilter === 'true',
      hidden: this.hiddenFilter === '' ? undefined : this.hiddenFilter === 'true',
      undated: this.undatedFilter === '' ? undefined : this.undatedFilter === 'true',
      page,
      pageSize: 25,
    };
    this.load(() => this.facade.getBooks(filters), value => {
      this.books.set(value.books);
      this.bookTotal.set(value.totalCount);
      this.bookPage.set(value.page);
    });
  }

  protected selectBook(isbn13: string): void {
    this.load(() => this.facade.getBook(isbn13), value => {
      this.selectedBook.set(value);
      this.metadataForm.patchValue({
        title: value.title ?? '', authors: value.authors ?? '', publisher: value.publisher ?? '',
        publicationYear: value.publicationYear ?? 0, physicalFormat: value.physicalFormat ?? '',
        language: value.language ?? '', genre: value.genre ?? '', workId: value.workId ?? '',
      });
      this.quantityForm.patchValue({quantityAvailable: value.quantityAvailable});
    });
  }

  protected addBook(): void {
    if (this.addBookForm.invalid) { this.addBookForm.markAllAsTouched(); return; }
    const value = this.addBookForm.getRawValue();
    this.save(() => this.facade.addBook(this.compact({
      ...value,
      publicationYear: value.publicationYear || null,
    })), 'La fiche a été ajoutée.', () => this.loadBooks());
  }

  protected saveMetadata(): void {
    const book = this.selectedBook();
    if (!book || this.metadataForm.invalid) { return; }
    const value = this.metadataForm.getRawValue();
    this.save(() => this.facade.updateMetadata(book.isbn13, {
      ...value,
      publicationYear: value.publicationYear || null,
      fields: ['Title', 'Authors', 'Publisher', 'PublicationYear', 'PhysicalFormat', 'Language', 'Genre', 'WorkId'],
    }), 'Les métadonnées ont été enregistrées.', () => this.selectBook(book.isbn13));
  }

  protected correctQuantity(): void {
    const book = this.selectedBook();
    if (!book || this.quantityForm.invalid) { return; }
    this.save(() => this.facade.correctQuantity(book.isbn13, this.quantityForm.getRawValue()), 'Le stock disponible a été corrigé.', () => this.selectBook(book.isbn13));
  }

  protected withdrawBook(): void {
    const book = this.selectedBook();
    if (!book || this.withdrawalForm.invalid) { return; }
    this.save(() => this.facade.withdraw(book.isbn13, this.withdrawalForm.getRawValue()), 'Le retrait a été enregistré.', () => this.selectBook(book.isbn13));
  }

  protected startAnnouncementCorrection(announcementId: string, quantity: number): void {
    this.announcementToCorrect.set(announcementId);
    this.announcementForm.patchValue({quantity});
  }

  protected correctAnnouncement(): void {
    const announcementId = this.announcementToCorrect();
    const book = this.selectedBook();
    if (!announcementId || !book || this.announcementForm.invalid) { return; }
    this.save(() => this.facade.correctAnnouncement(announcementId, this.announcementForm.getRawValue()), 'L’annonce a été corrigée.', () => {
      this.announcementToCorrect.set(null);
      this.selectBook(book.isbn13);
    });
  }

  protected toggleRare(): void {
    const book = this.selectedBook();
    if (!book) { return; }
    this.save(() => this.facade.setRare(book.isbn13, !book.isRare), 'Le marquage rare a été mis à jour.', () => this.selectBook(book.isbn13));
  }

  protected toggleVisibility(): void {
    const book = this.selectedBook();
    if (!book) { return; }
    this.save(() => this.facade.setVisibility(book.isbn13, !book.isHidden), 'La visibilité catalogue a été mise à jour.', () => this.selectBook(book.isbn13));
  }

  protected mergeBook(): void {
    const book = this.selectedBook();
    if (!book || this.mergeForm.invalid) { return; }
    const target = this.mergeForm.controls.targetIsbn13.value;
    if (target === book.isbn13 || !window.confirm(`Fusionner ${book.isbn13} vers ${target} ?`)) { return; }
    this.save(() => this.facade.merge(book.isbn13, this.mergeForm.getRawValue()), 'Les fiches ont été fusionnées.', () => {
      this.selectedBook.set(null);
      this.loadBooks();
    });
  }

  protected deleteBook(): void {
    const book = this.selectedBook();
    if (!book || !window.confirm(`Supprimer définitivement la fiche ${book.isbn13} ?`)) { return; }
    this.save(() => this.facade.deleteBook(book.isbn13), 'La fiche a été supprimée.', () => {
      this.selectedBook.set(null);
      this.loadBooks();
    });
  }

  protected loadFairs(): void {
    this.load(() => this.facade.getFairs(true), value => this.fairs.set(value.fairs));
  }

  protected selectFair(fair: AdminFair): void {
    this.selectedFair.set(fair);
    this.revenueInput = fair.revenue?.toString() ?? '';
    this.load(() => this.facade.getFairStats(fair.id), value => this.fairStats.set(value));
  }

  protected saveRevenue(): void {
    const fair = this.selectedFair();
    if (!fair) { return; }
    const revenue = this.revenueInput.trim() === '' ? null : Number(this.revenueInput.replace(',', '.'));
    if (revenue !== null && (!Number.isFinite(revenue) || revenue < 0)) {
      this.errorMessage.set('La recette doit être un nombre positif ou vide.');
      return;
    }
    this.save(() => this.facade.setFairRevenue(fair.id, revenue), 'La recette de la bourse a été enregistrée.', () => this.loadFairs());
  }

  protected loadSessions(page = 1): void {
    const filters: AdminSessionFilters = {status: this.sessionStatus || undefined, page, pageSize: 25};
    this.load(() => this.facade.getSessions(filters), value => {
      this.sessions.set(value.sessions);
      this.sessionTotal.set(value.totalCount);
    });
  }

  protected selectSession(sessionId: string): void {
    this.load(() => this.facade.getSession(sessionId), value => this.selectedSession.set(value));
  }

  protected removeMovement(movementId: string): void {
    const session = this.selectedSession();
    if (!session || !window.confirm('Retirer ce mouvement et recalculer la session ?')) { return; }
    this.save(() => this.facade.removeMovement(session.id, movementId), 'Le mouvement a été retiré.', () => this.selectSession(session.id));
  }

  protected reassignSession(): void {
    const session = this.selectedSession();
    if (!session || !window.confirm('Rejouer cette session dans le nouveau mode ?')) { return; }
    this.save(() => this.facade.reassignSession(session.id, {
      mode: this.reassignMode,
      targetAssoEventsId: this.reassignFairId.trim() || null,
    }), 'La session a été réaffectée.', () => this.selectSession(session.id));
  }

  protected cancelSession(): void {
    const session = this.selectedSession();
    if (!session || !window.confirm('Annuler cette session et inverser ses mouvements ?')) { return; }
    this.save(() => this.facade.cancelSession(session.id), 'La session a été annulée.', () => this.selectSession(session.id));
  }

  protected cancelSessionAlerts(): void {
    const session = this.selectedSession();
    if (!session) { return; }
    this.save(() => this.facade.cancelSessionAlerts(session.id), 'Les alertes en attente ont été annulées.', () => this.selectSession(session.id));
  }

  protected forceSessionAlerts(): void {
    const session = this.selectedSession();
    if (!session || !window.confirm('Forcer la mise en file immédiate des alertes ?')) { return; }
    this.save(() => this.facade.forceSessionAlerts(session.id), 'Les alertes ont été forcées.', () => this.selectSession(session.id));
  }

  protected loadAlerts(): void {
    this.load(() => this.facade.getAlerts({status: this.alertStatus || undefined, page: 1, pageSize: 50}), value => this.alerts.set(value.alerts));
  }

  protected cancelAlert(id: string): void {
    this.save(() => this.facade.cancelAlert(id), 'L’alerte a été annulée.', () => this.loadAlerts());
  }

  protected forceAlert(id: string): void {
    if (!window.confirm('Forcer l’envoi de cette alerte ?')) { return; }
    this.save(() => this.facade.forceAlert(id), 'L’alerte a été remise en file.', () => this.loadAlerts());
  }

  protected loadMembers(): void {
    this.load(() => this.facade.getMembers(this.memberSearch.trim() || undefined, this.memberAlertStatus || undefined), value => {
      this.members.set(value.members);
      this.memberTotal.set(value.totalCount);
    });
  }

  protected selectMember(memberId: string): void {
    this.load(() => this.facade.getMember(memberId), value => this.selectedMember.set(value));
  }

  protected toggleMemberBlock(): void {
    const member = this.selectedMember();
    if (!member) { return; }
    const blocked = member.member.alertStatus !== 'Blocked';
    this.save(() => blocked ? this.facade.blockMember(member.member.id) : this.facade.unblockMember(member.member.id), blocked ? 'Les alertes du membre ont été bloquées.' : 'Les alertes du membre sont de nouveau actives.', () => {
      this.selectMember(member.member.id);
      this.loadMembers();
    });
  }

  protected deleteMember(): void {
    const member = this.selectedMember();
    if (!member || !window.confirm('Demander la suppression de ce compte et de ses données membre ?')) { return; }
    this.save(() => this.facade.deleteMember(member.member.id), 'La demande de suppression a été enregistrée.', () => {
      this.selectedMember.set(null);
      this.loadMembers();
    });
  }

  protected loadAccounts(page = 1): void {
    const filters: AdminAccountFilters = {
      search: this.accountSearch.trim() || undefined,
      page,
      pageSize: 25,
    };
    this.load(() => this.facade.getAccounts(filters), value => {
      this.accounts.set(value.accounts);
      this.accountTotal.set(value.totalCount);
      this.accountPage.set(value.page);
    });
  }

  protected createAccount(): void {
    if (this.createAccountForm.invalid) {
      this.createAccountForm.markAllAsTouched();
      return;
    }

    this.save(
      () => this.facade.createAccount(this.createAccountForm.getRawValue()),
      'Le compte a été créé.',
      () => {
        this.createAccountForm.reset({email: '', displayName: '', temporaryPassword: '', roles: []});
        this.showCreateAccount.set(false);
        this.loadAccounts();
      },
    );
  }

  protected toggleCreateRole(role: AdminAccountRole): void {
    const roles = this.createAccountForm.controls.roles.value;
    this.createAccountForm.controls.roles.setValue(
      roles.includes(role) ? roles.filter(item => item !== role) : [...roles, role],
    );
    this.createAccountForm.controls.roles.markAsTouched();
  }

  protected hasCreateRole(role: AdminAccountRole): boolean {
    return this.createAccountForm.controls.roles.value.includes(role);
  }

  protected startAccountRoleEdit(account: AdminAccount): void {
    this.editingAccountId.set(account.externalId);
    this.editingRoles.set([...account.roles]);
  }

  protected cancelAccountRoleEdit(): void {
    this.editingAccountId.set(null);
    this.editingRoles.set([]);
  }

  protected toggleEditingRole(role: AdminAccountRole): void {
    const roles = this.editingRoles();
    this.editingRoles.set(roles.includes(role) ? roles.filter(item => item !== role) : [...roles, role]);
  }

  protected hasEditingRole(role: AdminAccountRole): boolean {
    return this.editingRoles().includes(role);
  }

  protected saveAccountRoles(account: AdminAccount): void {
    this.save(
      () => this.facade.updateAccountRoles(account.externalId, this.editingRoles()),
      'Les rôles du compte ont été mis à jour.',
      () => {
        this.cancelAccountRoleEdit();
        this.loadAccounts(this.accountPage());
      },
    );
  }

  protected roleLabel(role: AdminAccountRole): string {
    return this.accountRoleOptions.find(option => option.value === role)?.label ?? role;
  }

  protected isLastAccountPage(): boolean {
    return this.accountPage() * 25 >= this.accountTotal();
  }

  protected loadSettings(): void {
    this.load(() => this.facade.getSettings(), value => {
      this.settings.set(value);
      this.settingsForm.patchValue(value);
    });
  }

  protected saveSettings(): void {
    if (this.settingsForm.invalid) { this.settingsForm.markAllAsTouched(); return; }
    this.save(() => this.facade.updateSettings(this.settingsForm.getRawValue()), 'Les paramètres ont été enregistrés.', () => this.loadSettings());
  }

  protected formatDate(value: string | null | undefined): string {
    if (!value) { return '—'; }
    return new Intl.DateTimeFormat('fr-FR', {dateStyle: 'medium', timeStyle: 'short'}).format(new Date(value));
  }

  protected formatNumber(value: number | null | undefined): string {
    return new Intl.NumberFormat('fr-FR').format(value ?? 0);
  }

  protected isLastBookPage(): boolean {
    return this.bookPage() * 25 >= this.bookTotal();
  }

  private load<T>(request: () => Promise<T>, assign: (value: T) => void): void {
    this.isLoading.set(true);
    this.clearMessages();
    request()
      .then(value => assign(value))
      .catch(error => this.errorMessage.set(this.readError(error)))
      .finally(() => this.isLoading.set(false));
  }

  private save<T>(request: () => Promise<T>, message: string, after: () => void): void {
    this.isSaving.set(true);
    this.clearMessages();
    request()
      .then(() => {
        after();
        this.successMessage.set(message);
      })
      .catch(error => this.errorMessage.set(this.readError(error)))
      .finally(() => this.isSaving.set(false));
  }

  private clearMessages(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  private compact<T extends Record<string, unknown>>(value: T): Partial<T> {
    return Object.fromEntries(Object.entries(value).filter(([, item]) => item !== '' && item !== undefined)) as Partial<T>;
  }

  private readError(error: any): string {
    return error?.response?.data?.description ?? error?.response?.data?.error ?? error?.message ?? 'Une erreur est survenue.';
  }
}
