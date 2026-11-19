import {HttpErrorResponse} from '@angular/common/http';
import {Component, OnInit, computed, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogAdminBookWithoutIsbnItem, CatalogAdminBookWithoutIsbnPage} from '../../../core/catalog.models';
import {EAN13_MODULE_COUNT, Ean13BarRect, ean13BarRects, isEan13} from '../../../shared/barcode/ean13';
import {
  MAX_BOOK_PHOTOS,
  OrdinaryBookForm,
  emptyOrdinaryBookForm,
  toOrdinaryBookRequest,
  validateBookPhoto,
  validateOrdinaryBookForm,
} from './books-without-isbn.rules';

export const BOOKS_WITHOUT_ISBN_PAGE_SIZE = 25;

// E9 (liste) et E10 (formulaire) de la spec. Aucun bouton d'impression sur le site (Q6, RG-176).
@Component({
  selector: 'app-admin-books-without-isbn',
  templateUrl: './admin-books-without-isbn.component.html',
  styleUrl: './admin-books-without-isbn.component.scss',
  standalone: false,
})
export class AdminBooksWithoutIsbnComponent implements OnInit {
  readonly moduleCount = EAN13_MODULE_COUNT;
  readonly maxPhotos = MAX_BOOK_PHOTOS;
  readonly page = signal<CatalogAdminBookWithoutIsbnPage | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly formOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly photoError = signal<string | null>(null);
  readonly created = signal<{code: string} | null>(null);
  readonly totalPages = computed(() => {
    const current = this.page();
    return current ? Math.max(1, Math.ceil(current.totalCount / current.pageSize)) : 1;
  });

  search = '';
  form: OrdinaryBookForm = emptyOrdinaryBookForm();
  photos: File[] = [];

  constructor(
    private readonly api: CatalogAdminApiService,
    private readonly auth: CatalogAuthService,
  ) {}

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      this.page.set(await firstValueFrom(this.api.getBooksWithoutIsbn(token, {
        page: this.pageNumber(),
        pageSize: BOOKS_WITHOUT_ISBN_PAGE_SIZE,
        search: this.search,
      })));
    } catch (error) {
      this.error.set(describeError(error));
    } finally {
      this.loading.set(false);
    }
  }

  submitSearch(): void {
    this.pageNumber.set(1);
    void this.load();
  }

  goToPage(target: number): void {
    if (target < 1 || target > this.totalPages()) {
      return;
    }

    this.pageNumber.set(target);
    void this.load();
  }

  openForm(): void {
    this.created.set(null);
    this.formError.set(null);
    this.photoError.set(null);
    this.form = emptyOrdinaryBookForm();
    this.photos = [];
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  addPhotos(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.photoError.set(null);
    for (const file of files) {
      if (this.photos.length >= MAX_BOOK_PHOTOS) {
        this.photoError.set(`Un livre accepte au plus ${MAX_BOOK_PHOTOS} photos.`);
        break;
      }

      const problem = validateBookPhoto(file);
      if (problem !== null) {
        this.photoError.set(problem);
        continue;
      }

      this.photos = [...this.photos, file];
    }
  }

  removePhoto(index: number): void {
    this.photos = this.photos.filter((_, position) => position !== index);
  }

  async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }

    const problem = validateOrdinaryBookForm(this.form);
    if (problem !== null) {
      this.formError.set(problem);
      return;
    }

    this.saving.set(true);
    this.formError.set(null);
    try {
      const token = await this.auth.getApiAccessToken();
      const result = await firstValueFrom(
        this.api.createBookWithoutIsbn(token, toOrdinaryBookRequest(this.form)),
      );
      this.created.set({code: result.code});
      await this.uploadPhotos(token, result.code);
      await this.load();
    } catch (error) {
      this.formError.set(describeError(error));
    } finally {
      this.saving.set(false);
    }
  }

  barsFor(code: string): Ean13BarRect[] {
    return isEan13(code) ? ean13BarRects(code) : [];
  }

  firstPhotoUrl(item: CatalogAdminBookWithoutIsbnItem): string | null {
    return item.photos[0]?.url ?? null;
  }

  // La fiche existe déjà : un échec de photo n'annule pas la création, il est signalé.
  private async uploadPhotos(token: string, code: string): Promise<void> {
    for (const file of this.photos) {
      try {
        await firstValueFrom(this.api.addBookPhoto(token, code, file));
      } catch {
        this.photoError.set('La fiche est créée, mais une photo n’a pas pu être envoyée.');
        return;
      }
    }
  }
}

function describeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Le serveur ne répond pas. Rien n’a été enregistré.';
    }

    const body = error.error as {error?: string; description?: string} | null;
    return body?.description ?? body?.error ?? `Action impossible (${error.status}).`;
  }

  return error instanceof Error ? error.message : 'Action impossible.';
}