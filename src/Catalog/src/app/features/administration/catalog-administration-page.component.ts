import {ChangeDetectionStrategy, Component, computed, OnDestroy, OnInit, Signal, signal} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta} from '@angular/platform-browser';
import type {AccountInfo} from '@azure/msal-browser';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogDeadStockBook} from '../../core/catalog.models';
import {toDeadStockCsv} from './dead-stock-export';

const DEFAULT_MIN_AGE_MONTHS = 6;
const DEFAULT_MIN_QUANTITY = 3;
const MAX_MIN_AGE_MONTHS = 120_000;

@Component({
  selector: 'app-catalog-administration-page',
  standalone: false,
  templateUrl: './catalog-administration-page.component.html',
  styleUrls: ['./catalog-administration-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAdministrationPageComponent implements OnInit, OnDestroy {
  readonly account: Signal<AccountInfo | null>;
  readonly initialized: Signal<boolean>;
  readonly isAuthenticated: Signal<boolean>;
  readonly authError: Signal<string | null>;
  readonly books = signal<CatalogDeadStockBook[]>([]);
  readonly generatedAt = signal<string | null>(null);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly accountLabel: Signal<string>;

  minAgeMonths = DEFAULT_MIN_AGE_MONTHS;
  minQuantity = DEFAULT_MIN_QUANTITY;

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

  ngOnDestroy(): void {
    this.meta.updateTag({name: 'robots', content: 'index, follow'});
  }

  async initialize(): Promise<void> {
    await this.auth.initialize();
    if (this.auth.isAuthenticated()) {
      await this.loadDeadStock();
    }
  }

  async login(): Promise<void> {
    this.errorMessage.set(null);

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

  async loadDeadStock(): Promise<void> {
    const filters = this.validatedFilters();
    if (!filters || !this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const token = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(
        this.api.getDeadStock(token, filters.minAgeMonths, filters.minQuantity),
      );
      this.books.set(response.books);
      this.generatedAt.set(response.generatedAt);
    } catch (error: unknown) {
      this.books.set([]);
      this.generatedAt.set(null);
      this.errorMessage.set(this.describeError(error));
    } finally {
      this.loading.set(false);
    }
  }

  exportCsv(): void {
    const books = this.books();
    if (books.length === 0) {
      return;
    }

    const csv = toDeadStockCsv({
      generatedAt: this.generatedAt() ?? new Date().toISOString(),
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

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace('.', '');
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

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 403) {
      return 'Le compte connecté ne possède pas les droits d’administration.';
    }

    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'La session d’administration a expiré. Reconnectez-vous pour continuer.';
    }

    return 'La liste de désengorgement n’a pas pu être chargée. Réessayez dans un instant.';
  }

  private displayAccount(account: AccountInfo | null): string {
    if (!account) {
      return '';
    }

    return account.name?.trim() || account.username;
  }
}
