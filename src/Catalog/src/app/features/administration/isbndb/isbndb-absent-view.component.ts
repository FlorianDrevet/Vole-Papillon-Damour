import {ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogAdminIsbndbAbsentBook,
  CatalogAdminIsbndbAbsentBooksPage,
} from '../../../core/catalog.models';

type IsbndbAbsentFilter = 'to-complete' | 'completed';

@Component({
  selector: 'app-isbndb-absent-view',
  standalone: false,
  templateUrl: './isbndb-absent-view.component.html',
  styleUrls: ['./isbndb-absent-view.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IsbndbAbsentViewComponent implements OnChanges {
  @Input() accessToken = '';
  @Output() bookSelected = new EventEmitter<string>();

  readonly page = signal<CatalogAdminIsbndbAbsentBooksPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly filter = signal<IsbndbAbsentFilter>('to-complete');
  readonly pendingCount = signal(0);
  readonly completedCount = signal(0);
  search = '';
  appliedSearch = '';
  currentPage = 1;
  readonly pageSize = 5;

  private requestVersion = 0;

  constructor(private readonly api: CatalogAdminApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['accessToken'] && this.accessToken) {
      void this.loadCounts();
      void this.loadPage();
    }
  }

  async selectFilter(filter: IsbndbAbsentFilter): Promise<void> {
    if (filter === this.filter()) return;
    this.filter.set(filter);
    this.currentPage = 1;
    await this.loadPage();
  }

  async submitSearch(): Promise<void> {
    this.appliedSearch = this.search.trim();
    this.currentPage = 1;
    await this.loadPage();
  }

  async goToPage(page: number): Promise<void> {
    if (!this.page() || page < 1 || page > this.pageCount()) return;
    this.currentPage = page;
    await this.loadPage();
  }

  openBook(item: CatalogAdminIsbndbAbsentBook): void {
    this.bookSelected.emit(item.isbn13);
  }

  pageCount(): number {
    const page = this.page();
    return page && page.pageSize > 0 ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  }

  missingFieldLabel(field: string): string {
    const labels: Record<string, string> = {
      Summary: 'Résumé',
      Cover: 'Couverture',
      Genre: 'Genre',
      Publisher: 'Éditeur',
      PublicationYear: 'Année',
      Pages: 'Pages',
      Title: 'Titre',
      Authors: 'Auteur',
    };
    return labels[field] ?? field;
  }

  fallbackSourceLabel(source: string | null): string {
    const labels: Record<string, string> = {
      Bnf: 'BnF',
      OpenLibrary: 'Open Library',
      GoogleBooks: 'Google Books',
    };
    return source ? labels[source] ?? source : 'Source inconnue';
  }

  stockLabel(item: CatalogAdminIsbndbAbsentBook): string {
    return item.quantityAvailable > 0 ? `${item.quantityAvailable} disponible${item.quantityAvailable > 1 ? 's' : ''}` : 'Hors stock';
  }

  async loadPage(): Promise<void> {
    if (!this.accessToken) return;
    const version = ++this.requestVersion;
    this.loading.set(true);
    this.error.set(null);
    try {
      const page = await firstValueFrom(this.api.getIsbndbAbsentBooks(this.accessToken, {
        status: this.filter(),
        search: this.appliedSearch || undefined,
        page: this.currentPage,
        pageSize: this.pageSize,
      }));
      if (version === this.requestVersion) {
        this.page.set(page);
        this.currentPage = page.page;
      }
    } catch {
      if (version === this.requestVersion) {
        this.error.set('Impossible de charger les livres hors ISBNdb. Réessayez dans un instant.');
      }
    } finally {
      if (version === this.requestVersion) this.loading.set(false);
    }
  }

  private async loadCounts(): Promise<void> {
    const [pending, completed] = await Promise.allSettled([
      firstValueFrom(this.api.getIsbndbAbsentBooks(this.accessToken, {
        status: 'to-complete', page: 1, pageSize: 1,
      })),
      firstValueFrom(this.api.getIsbndbAbsentBooks(this.accessToken, {
        status: 'completed', page: 1, pageSize: 1,
      })),
    ]);
    if (pending.status === 'fulfilled') this.pendingCount.set(pending.value.totalCount);
    if (completed.status === 'fulfilled') this.completedCount.set(completed.value.totalCount);
  }
}
