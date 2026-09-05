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

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogWatchlistItem, CatalogWatchlistResponse} from '../../core/catalog.models';

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
  readonly authError: Signal<string | null>;
  readonly watchlist = signal<CatalogWatchlistResponse | null>(null);
  readonly loading = signal(false);
  readonly removingItemId = signal<string | null>(null);
  readonly deleting = signal(false);
  readonly deletionRequested = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly accountLabel: Signal<string>;

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogMemberApiService,
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

  async logout(): Promise<void> {
    try {
      await this.auth.logout();
    } catch {
      this.errorMessage.set('La déconnexion n’a pas pu être démarrée. Réessayez.');
    }
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
      this.errorMessage.set(this.describeError(error));
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

  itemLabel(item: CatalogWatchlistItem): string {
    return item.book?.title || item.isbn13 || item.workId || 'Titre suivi';
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
