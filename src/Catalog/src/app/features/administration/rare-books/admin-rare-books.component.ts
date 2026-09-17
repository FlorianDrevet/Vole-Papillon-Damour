import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  PLATFORM_ID,
  inject,
} from '@angular/core';

import {
  CatalogAdminRareBook,
  CatalogAdminRareBookFilters,
} from '../../../core/catalog.models';
import {RareBookPhotoAction} from './admin-rare-book-photos.component';
import {
  AdminRareBooksFacade,
  CatalogAdminRareBookFormValue,
} from './admin-rare-books.facade';

type RareBookStatusFilter = 'all' | 'Draft' | 'Published';
type RareBookAvailabilityFilter = 'all' | 'available' | 'sold';
type RareBookIsbnFilter = 'all' | 'with' | 'without';

@Component({
  selector: 'app-admin-rare-books',
  standalone: false,
  templateUrl: './admin-rare-books.component.html',
  styleUrls: ['./admin-rare-books.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminRareBooksComponent implements OnInit {
  readonly facade = inject(AdminRareBooksFacade);
  private readonly platformId = inject(PLATFORM_ID);

  search = '';
  statusFilter: RareBookStatusFilter = 'all';
  availabilityFilter: RareBookAvailabilityFilter = 'all';
  isbnFilter: RareBookIsbnFilter = 'all';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  withoutPhoto = false;
  page = 1;
  readonly pageSize = 50;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    await this.facade.load(this.filters());
  }

  async applyFilters(): Promise<void> {
    this.page = 1;
    await this.load();
  }

  async goToPage(page: number): Promise<void> {
    const totalPages = this.pageCount();
    if (!Number.isInteger(page) || page < 1 || page > totalPages || page === this.page) {
      return;
    }
    this.page = page;
    await this.load();
  }

  startCreate(): void {
    this.facade.startCreate();
  }

  async open(book: CatalogAdminRareBook): Promise<void> {
    await this.facade.open(book.id);
  }

  cancelEditor(): void {
    this.facade.closeEditor();
  }

  async save(event: {value: CatalogAdminRareBookFormValue; rowVersion: string | null}): Promise<void> {
    await this.facade.save(event.value);
  }

  async publish(): Promise<void> {
    await this.facade.publish();
  }

  async unpublish(): Promise<void> {
    await this.facade.unpublish();
  }

  async deleteSelected(): Promise<void> {
    if (!this.confirm('Supprimer définitivement cette fiche rare ?')) {
      return;
    }
    await this.facade.deleteSelected();
  }

  async handlePhotoAction(action: RareBookPhotoAction): Promise<void> {
    switch (action.kind) {
      case 'add':
        await this.facade.addPhoto(action.file, action.caption);
        return;
      case 'delete':
        if (this.confirm('Supprimer cette photo de la fiche rare ?')) {
          await this.facade.deletePhoto(action.photoId);
        }
        return;
      case 'reorder':
        await this.facade.reorderPhotos(action.photoIds);
        return;
      case 'caption':
        await this.facade.updatePhotoCaption(action.photoId, action.caption);
        return;
    }
  }

  draftCount(): number {
    return this.facade.page()?.books.filter(book => book.status === 'Draft').length ?? 0;
  }

  publishedWithoutPhotoCount(): number {
    return this.facade.page()?.books.filter(book => book.status === 'Published' && book.photos.length === 0).length ?? 0;
  }

  pageCount(): number {
    const page = this.facade.page();
    return page && page.pageSize > 0 ? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) : 1;
  }

  statusLabel(status: string): string {
    return status === 'Published' ? 'Publiée' : 'Brouillon';
  }

  availabilityLabel(book: CatalogAdminRareBook): string {
    return book.isSold ? 'Vendue' : 'Disponible';
  }

  conditionLabel(condition: string): string {
    const labels: Record<string, string> = {
      AsNew: 'Comme neuf',
      GoodWithFlaws: 'Bon, défauts signalés',
      Worn: 'Usé',
      Damaged: 'Abîmé',
    };
    return labels[condition] ?? condition;
  }

  formatMoney(value: number): string {
    return `${value.toLocaleString('fr-FR', {minimumFractionDigits: 2, maximumFractionDigits: 2})} €`;
  }

  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? '—'
      : date.toLocaleDateString('fr-FR', {day: 'numeric', month: 'long', year: 'numeric'});
  }

  formatActor(value: string | null | undefined): string {
    if (!value) {
      return 'Non renseigné';
    }
    return `Bénévole · ${value.slice(0, 8)}`;
  }

  private filters(): CatalogAdminRareBookFilters {
    return {
      search: this.search.trim() || undefined,
      status: this.statusFilter === 'all' ? undefined : this.statusFilter,
      availability: this.availabilityFilter === 'all' ? undefined : this.availabilityFilter,
      hasIsbn: this.isbnFilter === 'all' ? undefined : this.isbnFilter === 'with',
      minPrice: this.numberOrUndefined(this.minPrice),
      maxPrice: this.numberOrUndefined(this.maxPrice),
      withoutPhoto: this.withoutPhoto || undefined,
      page: this.page,
      pageSize: this.pageSize,
    };
  }

  private numberOrUndefined(value: number | null): number | undefined {
    if (value === null || value === undefined || String(value).trim() === '') {
      return undefined;
    }
    return Number(value);
  }

  private confirm(message: string): boolean {
    return !isPlatformBrowser(this.platformId) || window.confirm(message);
  }
}
