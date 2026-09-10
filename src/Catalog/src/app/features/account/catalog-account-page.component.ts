import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  Signal,
  computed,
  signal,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import type {AccountInfo} from '@azure/msal-browser';
import {firstValueFrom} from 'rxjs';

import {
  CatalogAuthenticationRedirectStartedError,
  CatalogAuthService,
} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogWatchlistItem, CatalogWatchlistResponse} from '../../core/catalog.models';

type CatalogAccountTab = 'watchlist' | 'preferences';

@Component({
  selector: 'app-catalog-account-page',
  standalone: false,
  templateUrl: './catalog-account-page.component.html',
  styleUrls: ['./catalog-account-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAccountPageComponent implements OnInit {
  readonly account: Signal<AccountInfo | null>;
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly isAdministrator: Signal<boolean>;
  readonly authError: Signal<string | null>;
  readonly watchlist = signal<CatalogWatchlistResponse | null>(null);
  readonly loading = signal(false);
  readonly removingItemId = signal<string | null>(null);
  readonly alertPending = signal(false);
  readonly deleting = signal(false);
  readonly deletionRequested = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly accountLabel: Signal<string>;
  readonly activeTab = signal<CatalogAccountTab>('watchlist');
  readonly watchlistIntro = computed(() => {
    const count = this.watchlist()?.items.length;
    if (count === undefined) {
      return 'Votre liste de recherche se prépare. Vous recevrez un e-mail dès qu’un exemplaire arrive — au plus un par livre, et jamais sans la date à laquelle il sera disponible.';
    }

    const bookLabel = count === 1 ? 'livre suivi' : 'livres suivis';
    return `${count} ${bookLabel}. Vous recevrez un e-mail dès qu’un exemplaire arrive — au plus un par livre, et jamais sans la date à laquelle il sera disponible.`;
  });

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogMemberApiService,
    private readonly meta: Meta,
  ) {
    this.account = this.auth.account;
    this.initialized = this.auth.initialized;
    this.isAuthenticated = this.auth.isAuthenticated;
    this.isAdministrator = this.auth.isAdministrator;
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
      await this.loadWatchlist();
    }
  }

  async login(): Promise<void> {
    this.errorMessage.set(null);

    try {
      await this.auth.login('/compte');
    } catch {
      this.errorMessage.set('La connexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  async register(): Promise<void> {
    this.errorMessage.set(null);

    try {
      await this.auth.register('/compte');
    } catch {
      this.errorMessage.set('La création du compte n’a pas pu être démarrée. Réessayez.');
    }
  }

  async logout(): Promise<void> {
    try {
      await this.auth.logout();
    } catch {
      this.errorMessage.set('La déconnexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  selectTab(tab: CatalogAccountTab): void {
    this.activeTab.set(tab);
  }

  async loadWatchlist(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(this.api.getWatchlist(token));
      this.watchlist.set(response);
    } catch (error: unknown) {
      this.watchlist.set(null);
      this.errorMessage.set(error instanceof CatalogAuthenticationRedirectStartedError
        ? 'Redirection vers votre fournisseur de connexion pour renouveler votre session…'
        : this.describeError(error));
    } finally {
      this.loading.set(false);
    }
  }

  async removeItem(item: CatalogWatchlistItem): Promise<void> {
    if (!this.auth.isAuthenticated() || this.removingItemId()) {
      return;
    }

    this.removingItemId.set(item.id);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.removeWatchlistItem(token, item.id));
      const current = this.watchlist();
      if (current) {
        this.watchlist.set({
          ...current,
          items: current.items.filter(candidate => candidate.id !== item.id),
        });
      }
      this.successMessage.set('Le titre a été retiré de votre liste.');
    } catch (error: unknown) {
      this.errorMessage.set(this.describeError(error));
    } finally {
      this.removingItemId.set(null);
    }
  }

  async setAlertsEnabled(enabled: boolean): Promise<void> {
    if (!this.auth.isAuthenticated() || this.alertPending()) {
      return;
    }

    this.alertPending.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(this.api.setAlertStatus(token, enabled));
      const current = this.watchlist();
      if (current) {
        this.watchlist.set({
          ...current,
          alertStatus: response.alertStatus,
          bounceCount: response.bounceCount,
        });
      }
      this.successMessage.set(enabled ? 'Les alertes e-mail sont réactivées.' : 'Les alertes e-mail sont suspendues.');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.errorMessage.set('Les alertes sont bloquées par l’association et ne peuvent pas être réactivées ici.');
      } else {
        this.errorMessage.set(this.describeError(error));
      }
    } finally {
      this.alertPending.set(false);
    }
  }

  requestAccountDeletion(): void {
    this.deletionRequested.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
  }

  cancelAccountDeletion(): void {
    if (!this.deleting()) {
      this.deletionRequested.set(false);
    }
  }

  async confirmAccountDeletion(): Promise<void> {
    if (!this.auth.isAuthenticated() || this.deleting()) {
      return;
    }

    this.deleting.set(true);
    this.errorMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.api.deleteAccount(token));
      await this.auth.logout();
    } catch (error: unknown) {
      this.deleting.set(false);
      this.errorMessage.set(this.describeError(error));
    }
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  formatShortDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  itemLabel(item: CatalogWatchlistItem): string {
    return item.book?.title || item.isbn13 || item.workId || 'Titre de la liste de recherche';
  }

  editionLabel(item: CatalogWatchlistItem): string {
    if (item.scope === 'Work') {
      return 'Toutes éditions';
    }

    if (!item.book) {
      return 'Édition recherchée';
    }

    const edition = [item.book.publisher, item.book.publicationYear]
      .filter(value => value !== null && value !== undefined && value !== '')
      .join(' · ');

    return edition || 'Édition suivie';
  }

  availabilityClass(item: CatalogWatchlistItem): 'available' | 'next' | 'pending' {
    if (item.book?.quantityAvailable && item.book.quantityAvailable > 0) {
      return 'available';
    }

    if (item.book?.quantityAnnounced && item.book.quantityAnnounced > 0) {
      return 'next';
    }

    return 'pending';
  }

  availabilityLabel(item: CatalogWatchlistItem): string {
    const book = item.book;
    if (!book) {
      return 'Pas encore reçu par l’association';
    }

    if (book.quantityAvailable > 0) {
      return `${book.quantityAvailable} disponible${book.quantityAvailable > 1 ? 's' : ''}`;
    }

    if (book.quantityAnnounced > 0) {
      if (book.nextFairAt) {
        return `${book.quantityAnnounced} dès le ${this.formatShortDate(book.nextFairAt)}`;
      }

      return `${book.quantityAnnounced} annoncé${book.quantityAnnounced > 1 ? 's' : ''}`;
    }

    return 'Pas encore reçu par l’association';
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'La session a expiré. Reconnectez-vous pour continuer.';
    }

    if (error instanceof HttpErrorResponse && error.status === 409) {
      return 'Cette action ne peut pas être effectuée pour le moment.';
    }

    return 'Une erreur est survenue. Réessayez dans un instant.';
  }

  private displayAccount(account: AccountInfo | null): string {
    if (!account) {
      return '';
    }

    return account.name?.trim() || account.username;
  }
}
