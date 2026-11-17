import {DOCUMENT, isPlatformBrowser} from '@angular/common';
import {ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, PLATFORM_ID, SimpleChanges, inject, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogAdminIsbnNotFoundBook,
  CatalogAdminIsbnNotFoundPage,
} from '../../../core/catalog.models';

@Component({
  selector: 'app-isbn-not-found-view',
  standalone: false,
  templateUrl: './isbn-not-found-view.component.html',
  styleUrls: ['./isbn-not-found-view.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IsbnNotFoundViewComponent implements OnChanges {
  @Input() accessToken = '';
  @Input() refreshVersion = 0;
  @Input() busy = false;
  @Output() bookSelected = new EventEmitter<string>();
  @Output() retryRequested = new EventEmitter<{isbn13: string}>();
  @Output() deleteRequested = new EventEmitter<{isbn13: string}>();
  @Output() importRequested = new EventEmitter<void>();

  readonly page = signal<CatalogAdminIsbnNotFoundPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly exportError = signal<string | null>(null);
  readonly exporting = signal(false);
  readonly formatHelpExpanded = signal(false);
  readonly pageSize = 5;

  private requestVersion = 0;
  private readonly api = inject(CatalogAdminApiService);
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.accessToken) return;

    if (changes['accessToken'] || (changes['refreshVersion'] && !changes['refreshVersion'].firstChange)) {
      this.currentPage = 1;
      void this.loadPage();
    }
  }

  currentPage = 1;

  async refresh(): Promise<void> {
    this.currentPage = 1;
    await this.loadPage();
  }

  async goToPage(page: number): Promise<void> {
    if (!this.page() || page < 1 || page > this.pageCount() || this.loading()) return;
    this.currentPage = page;
    await this.loadPage();
  }

  pageCount(): number {
    const current = this.page();
    return current && current.pageSize > 0
      ? Math.max(1, Math.ceil(current.totalCount / current.pageSize))
      : 1;
  }

  canExport(): boolean {
    return !!this.page() && this.page()!.totalCount > 0 && !this.busy && !this.exporting();
  }

  openBook(item: CatalogAdminIsbnNotFoundBook): void {
    this.bookSelected.emit(item.isbn13);
  }

  retry(item: CatalogAdminIsbnNotFoundBook): void {
    this.retryRequested.emit({isbn13: item.isbn13});
  }

  delete(item: CatalogAdminIsbnNotFoundBook): void {
    this.deleteRequested.emit({isbn13: item.isbn13});
  }

  toggleFormatHelp(): void {
    this.formatHelpExpanded.update(expanded => !expanded);
  }

  formatLastAttempt(value: string | null): string | null {
    if (!value) return null;
    const date = new Date(value);
    if (!Number.isFinite(date.getTime())) return null;
    const day = new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Europe/Paris',
    }).format(date);
    const time = new Intl.DateTimeFormat('fr-FR', {
      hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Paris',
    }).format(date);
    return `${day} à ${time}`;
  }

  async exportCsv(): Promise<void> {
    if (!this.canExport() || !this.accessToken) return;

    this.exporting.set(true);
    this.exportError.set(null);
    let downloadUrl: string | null = null;
    let link: HTMLAnchorElement | null = null;
    const browserUrl = this.document.defaultView?.URL;

    try {
      if (!isPlatformBrowser(this.platformId) || !browserUrl) {
        throw new Error('The browser download API is unavailable.');
      }

      const blob = await firstValueFrom(this.api.exportIsbnNotFoundCsv(this.accessToken));
      downloadUrl = browserUrl.createObjectURL(blob);
      link = this.document.createElement('a');
      link.href = downloadUrl;
      link.download = `isbn-non-trouves-${this.currentDateStamp()}.csv`;
      link.style.display = 'none';
      this.document.body.append(link);
      link.click();
    } catch {
      this.exportError.set('Impossible de préparer le fichier CSV. Réessayez dans un instant.');
    } finally {
      link?.remove();
      if (downloadUrl) browserUrl?.revokeObjectURL(downloadUrl);
      this.exporting.set(false);
    }
  }

  async loadPage(): Promise<void> {
    if (!this.accessToken) return;

    const version = ++this.requestVersion;
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(this.api.getIsbnNotFoundBooks(this.accessToken, {
        page: this.currentPage,
        pageSize: this.pageSize,
      }));
      if (version === this.requestVersion) {
        this.page.set(result);
        this.currentPage = result.page;
      }
    } catch {
      if (version === this.requestVersion) {
        this.error.set('Impossible de charger les ISBN non trouvés. Réessayez dans un instant.');
      }
    } finally {
      if (version === this.requestVersion) this.loading.set(false);
    }
  }

  private currentDateStamp(): string {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${now.getFullYear()}-${month}-${day}`;
  }
}
