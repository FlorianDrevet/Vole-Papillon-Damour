import {HttpErrorResponse} from '@angular/common/http';
import {Injectable, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {
  CatalogAdminBook,
  CatalogAdminRareBook,
  CatalogAdminRareBookFilters,
  CatalogAdminRareBookPage,
  CatalogAdminRareBookRequest,
  CatalogAdminRareBookPublishResult,
  CatalogAdminUpdateRareBookRequest,
} from '../../../core/catalog.models';

export interface CatalogAdminRareBookFormValue {
  title: string;
  authorMention: string;
  publisher: string;
  publicationYear: number | null;
  shelf: string;
  price: number | null;
  condition: 'AsNew' | 'GoodWithFlaws' | 'Worn' | 'Damaged';
  publicDescription: string;
  binding: string;
  dimensions: string;
  pageCount: number | null;
  shelfLocation: string;
  priceSetBy: string;
  isbn13: string;
}

export const EMPTY_RARE_BOOK_FORM: CatalogAdminRareBookFormValue = {
  title: '',
  authorMention: '',
  publisher: '',
  publicationYear: null,
  shelf: 'Éditions anciennes',
  price: 0,
  condition: 'AsNew',
  publicDescription: '',
  binding: '',
  dimensions: '',
  pageCount: null,
  shelfLocation: '',
  priceSetBy: '',
  isbn13: '',
};

@Injectable({providedIn: 'root'})
export class AdminRareBooksFacade {
  readonly page = signal<CatalogAdminRareBookPage | null>(null);
  readonly selectedBook = signal<CatalogAdminRareBook | null>(null);
  readonly draft = signal<CatalogAdminRareBookFormValue | null>(null);
  readonly editorOpen = signal(false);
  readonly publishedCount = signal(0);
  readonly loading = signal(false);
  readonly pendingAction = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly publishWarnings = signal<string[]>([]);
  readonly catalogueRareBook = signal<CatalogAdminRareBook | null | undefined>(undefined);

  private lastFilters: CatalogAdminRareBookFilters = {};

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogAdminApiService,
  ) {}

  async load(filters: CatalogAdminRareBookFilters = this.lastFilters): Promise<void> {
    this.lastFilters = {...filters};
    await this.run('list', async token => {
      const response = await firstValueFrom(this.api.getRareBooks(token, filters));
      this.page.set(response);

      try {
        const published = await firstValueFrom(this.api.getRareBooks(token, {
          status: 'Published',
          page: 1,
          pageSize: 1,
        }));
        this.publishedCount.set(published.totalCount);
      } catch {
        this.publishedCount.set(response.books.filter(book => book.status === 'Published').length);
      }
    });
  }

  async open(id: string): Promise<void> {
    await this.run('detail', async token => {
      this.selectedBook.set(await firstValueFrom(this.api.getRareBook(token, id)));
      this.draft.set(null);
      this.publishWarnings.set([]);
      this.editorOpen.set(true);
    });
  }

  async resolveCatalogRelation(isbn13: string): Promise<void> {
    this.catalogueRareBook.set(undefined);
    await this.run('catalogue-rare-lookup', async token => {
      const response = await firstValueFrom(this.api.getRareBooks(token, {
        search: isbn13,
        page: 1,
        pageSize: 10,
      }));
      this.catalogueRareBook.set(
        response.books.find(book => book.isbn13 === isbn13) ?? null,
      );
    });
  }

  async openOrCreateFromCatalogBook(book: CatalogAdminBook): Promise<boolean> {
    let opened = false;
    await this.run('catalogue-rare', async token => {
      const response = await firstValueFrom(this.api.getRareBooks(token, {
        search: book.isbn13,
        page: 1,
        pageSize: 10,
      }));
      const existing = response.books.find(candidate => candidate.isbn13 === book.isbn13);

      if (existing) {
        const detailedBook = await firstValueFrom(this.api.getRareBook(token, existing.id));
        this.catalogueRareBook.set(detailedBook);
        this.selectedBook.set(detailedBook);
        this.draft.set(null);
        this.publishWarnings.set([]);
        this.editorOpen.set(true);
        opened = true;
        return;
      }

      this.selectedBook.set(null);
      this.catalogueRareBook.set(null);
      this.publishWarnings.set([]);
      this.draft.set({
        ...EMPTY_RARE_BOOK_FORM,
        title: book.title?.trim() ?? '',
        authorMention: book.authors?.trim() ?? '',
        publisher: book.publisher?.trim() ?? '',
        publicationYear: book.publicationYear,
        isbn13: book.isbn13,
      });
      this.editorOpen.set(true);
      opened = true;
    });
    return opened;
  }

  startCreate(prefill: Partial<CatalogAdminRareBookFormValue> = {}): void {
    this.clearFeedback();
    this.selectedBook.set(null);
    this.publishWarnings.set([]);
    this.draft.set({...EMPTY_RARE_BOOK_FORM, ...prefill});
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
    this.selectedBook.set(null);
    this.draft.set(null);
    this.publishWarnings.set([]);
  }

  async save(value: CatalogAdminRareBookFormValue): Promise<void> {
    await this.run('save', async token => {
      const request = toRareBookRequest(value);
      const existing = this.selectedBook();
      const saved = existing
        ? await firstValueFrom(this.api.updateRareBook(token, existing.id, {
          ...request,
          rowVersion: existing.rowVersion,
        } satisfies CatalogAdminUpdateRareBookRequest))
        : await firstValueFrom(this.api.createRareBook(token, request));

      this.setBook(saved);
      this.draft.set(null);
      this.editorOpen.set(true);
      this.success.set('La fiche rare a été enregistrée.');
    });
  }

  async publish(): Promise<void> {
    const existing = this.selectedBook();
    if (!existing) {
      return;
    }

    await this.run('publish', async token => {
      const result = await firstValueFrom(this.api.publishRareBook(token, existing.id));
      this.applyPublishResult(result);
      this.success.set(result.warnings.length > 0
        ? 'La fiche est publiée avec un avertissement à vérifier.'
        : 'La fiche est publiée dans la vitrine.');
    });
  }

  async unpublish(): Promise<void> {
    const existing = this.selectedBook();
    if (!existing) {
      return;
    }

    await this.run('unpublish', async token => {
      this.setBook(await firstValueFrom(this.api.unpublishRareBook(token, existing.id)));
      this.success.set('La fiche est repassée en brouillon et n’est plus visible du public.');
    });
  }

  async deleteSelected(): Promise<void> {
    const existing = this.selectedBook();
    if (!existing) {
      return;
    }

    await this.run('delete', async token => {
      await firstValueFrom(this.api.deleteRareBook(token, existing.id));
      this.closeEditor();
      await this.load(this.lastFilters);
      this.success.set('La fiche rare a été supprimée.');
    });
  }

  async addPhoto(file: File, caption?: string): Promise<void> {
    const existing = this.selectedBook();
    if (!existing) {
      return;
    }

    await this.run('add-photo', async token => {
      this.setBook(await firstValueFrom(this.api.addRareBookPhoto(token, existing.id, file, caption)));
      this.success.set('La photo a été ajoutée.');
    });
  }

  async reorderPhotos(photoIds: string[]): Promise<void> {
    const existing = this.selectedBook();
    if (!existing) {
      return;
    }

    await this.run('reorder-photos', async token => {
      this.setBook(await firstValueFrom(this.api.reorderRareBookPhotos(token, existing.id, photoIds)));
      this.success.set('L’ordre des photos a été enregistré.');
    });
  }

  async updatePhotoCaption(photoId: string, caption: string): Promise<void> {
    await this.run('caption-photo', async token => {
      this.setBook(await firstValueFrom(this.api.updateRareBookPhotoCaption(token, photoId, caption.trim() || null)));
      this.success.set('La légende de la photo a été enregistrée.');
    });
  }

  async deletePhoto(photoId: string): Promise<void> {
    await this.run('delete-photo', async token => {
      await firstValueFrom(this.api.deleteRareBookPhoto(token, photoId));
      const existing = this.selectedBook();
      if (existing) {
        this.setBook({...existing, photos: existing.photos.filter(photo => photo.id !== photoId)});
      }
      this.success.set('La photo a été supprimée.');
    });
  }

  private async run(action: string, operation: (token: string) => Promise<void>): Promise<void> {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.loading.set(true);
    this.pendingAction.set(action);
    this.error.set(null);
    this.success.set(null);

    try {
      await operation(await this.auth.getApiAccessToken());
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    } finally {
      this.loading.set(false);
      this.pendingAction.set(null);
    }
  }

  private setBook(book: CatalogAdminRareBook): void {
    this.selectedBook.set(book);
    this.page.update(page => page
      ? {...page, books: page.books.map(candidate => candidate.id === book.id ? book : candidate)}
      : page);
  }

  private applyPublishResult(result: CatalogAdminRareBookPublishResult): void {
    this.setBook(result.rareBook);
    this.publishWarnings.set(result.warnings);
  }

  private clearFeedback(): void {
    this.error.set(null);
    this.success.set(null);
  }

  private describeError(error: unknown): string {
    const status = error instanceof HttpErrorResponse
      ? error.status
      : error && typeof error === 'object' && 'status' in error && typeof error.status === 'number'
        ? error.status
        : null;

    if (status === 401) {
      return 'La session d’administration a expiré. Reconnectez-vous pour continuer.';
    }
    if (status === 403) {
      return 'Le compte connecté ne possède pas les droits pour gérer les livres rares.';
    }
    if (status === 404) {
      return 'La fiche rare demandée n’existe plus ou a été déplacée.';
    }
    if (status === 409) {
      return 'La fiche a été modifiée par un autre bénévole. Rechargez-la avant de l’enregistrer.';
    }
    if (status === 400) {
      return 'Les données saisies ne sont pas valides. Vérifiez les champs puis réessayez.';
    }

    return 'L’opération sur la fiche rare n’a pas pu aboutir. Réessayez dans un instant.';
  }
}

function toRareBookRequest(value: CatalogAdminRareBookFormValue): CatalogAdminRareBookRequest {
  return {
    title: value.title.trim(),
    authorMention: optional(value.authorMention),
    publisher: optional(value.publisher),
    publicationYear: integerOrNull(value.publicationYear),
    shelf: value.shelf.trim(),
    price: Number(value.price ?? 0),
    condition: value.condition,
    publicDescription: optional(value.publicDescription),
    binding: optional(value.binding),
    dimensions: optional(value.dimensions),
    pageCount: integerOrNull(value.pageCount),
    shelfLocation: optional(value.shelfLocation),
    priceSetBy: optional(value.priceSetBy),
    isbn13: optional(value.isbn13),
  };
}

function optional(value: string): string | null {
  const normalized = value.trim();
  return normalized || null;
}

function integerOrNull(value: number | null): number | null {
  return value === null || value === undefined || value === 0 ? null : Number(value);
}
