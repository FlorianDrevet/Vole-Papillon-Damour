import {HttpErrorResponse} from '@angular/common/http';
import {Component, EventEmitter, Input, Output, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogApiService} from '../../../core/catalog-api.service';
import {
  CatalogBookReference,
  CatalogManualBookRequestWrite,
  CatalogManualRequestScope,
  CatalogManualRequestStock,
} from '../../../core/catalog.models';

@Component({
  selector: 'app-manual-request-add-dialog',
  standalone: false,
  templateUrl: './manual-request-add-dialog.component.html',
  styleUrls: ['./manual-request-add-dialog.component.scss'],
})
export class ManualRequestAddDialogComponent {
  @Input() accessToken = '';
  @Input() requesterId = '';
  @Input() requesterFirstName = '';
  @Input() requesterLastName = '';
  @Output() closed = new EventEmitter<void>();
  @Output() added = new EventEmitter<void>();

  readonly results = signal<CatalogBookReference[]>([]);
  readonly selectedBook = signal<CatalogBookReference | null>(null);
  readonly scope = signal<CatalogManualRequestScope>('Work');
  readonly stock = signal<CatalogManualRequestStock | null>(null);
  readonly searching = signal(false);
  readonly loadingStock = signal(false);
  readonly saving = signal(false);
  readonly searchComplete = signal(false);
  readonly error = signal<string | null>(null);

  query = '';

  constructor(
    private readonly catalogApi: CatalogApiService,
    private readonly adminApi: CatalogAdminApiService,
  ) {}

  get requesterName(): string {
    return `${this.requesterFirstName.trim()} ${this.requesterLastName.trim()}`.trim();
  }

  get requesterDisplayName(): string {
    return `${this.requesterFirstName.trim()} ${this.requesterLastName.trim().toLocaleUpperCase('fr-FR')}`.trim();
  }

  async searchBooks(): Promise<void> {
    const query = this.query.trim();
    if (!query || this.searching()) {
      return;
    }
    this.searching.set(true);
    this.error.set(null);
    this.searchComplete.set(false);
    this.selectedBook.set(null);
    this.stock.set(null);
    try {
      const response = await firstValueFrom(this.catalogApi.searchReferences(query, 1, 20));
      this.results.set(response.items);
      this.searchComplete.set(true);
    } catch {
      this.results.set([]);
      this.error.set('La recherche de livres est momentanément indisponible. Réessayez dans quelques minutes.');
    } finally {
      this.searching.set(false);
    }
  }

  async selectBook(book: CatalogBookReference): Promise<void> {
    this.error.set(null);
    this.selectedBook.set(book);
    this.scope.set(book.workId ? 'Work' : 'Edition');
    await this.loadStock();
  }

  async selectScope(scope: CatalogManualRequestScope): Promise<void> {
    const book = this.selectedBook();
    if (!book || (scope === 'Work' && !book.workId) || (scope === 'Edition' && !book.isbn13)) {
      return;
    }
    this.scope.set(scope);
    await this.loadStock();
  }

  async add(pickFromStock: boolean): Promise<void> {
    const book = this.selectedBook();
    const scope = this.scope();
    if (!this.accessToken || !this.requesterId || !book || this.saving()) {
      return;
    }
    if ((scope === 'Work' && !book.workId) || (scope === 'Edition' && !book.isbn13)) {
      return;
    }

    const request: CatalogManualBookRequestWrite = {
      scope,
      workId: scope === 'Work' ? book.workId : null,
      isbn13: scope === 'Edition' ? book.isbn13 : null,
      title: book.title,
      authors: book.authors,
      publisher: book.publisher,
      publicationYear: book.publicationYear,
      pickFromStock,
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.adminApi.addManualBookRequest(this.accessToken, this.requesterId, request));
      this.added.emit();
    } catch (error: unknown) {
      this.error.set(this.isDuplicateError(error)
        ? `${this.requesterName} cherche déjà ce livre.`
        : 'La demande n’a pas pu être ajoutée. Vérifiez la connexion puis réessayez.');
    } finally {
      this.saving.set(false);
    }
  }

  stockSummary(value: CatalogManualRequestStock): string {
    const available = value.quantityAvailable;
    const announced = value.quantityAnnounced;
    const availableText = `${available} exemplaire${available === 1 ? '' : 's'} disponible${available === 1 ? '' : 's'}`;
    if (announced <= 0) {
      return availableText;
    }
    const announcedText = `${announced} annoncé${announced === 1 ? '' : 's'}`;
    return `${availableText} · ${announcedText}${value.nextFairStart ? ` pour le ${this.formatDate(value.nextFairStart)}` : ''}`;
  }

  private async loadStock(): Promise<void> {
    const book = this.selectedBook();
    const scope = this.scope();
    if (!this.accessToken || !book || (scope === 'Work' && !book.workId) || (scope === 'Edition' && !book.isbn13)) {
      this.stock.set(null);
      return;
    }

    const target = scope === 'Work'
      ? {scope: 'Work' as const, workId: book.workId!}
      : {scope: 'Edition' as const, isbn13: book.isbn13!};
    this.loadingStock.set(true);
    this.stock.set(null);
    try {
      this.stock.set(await firstValueFrom(this.adminApi.getManualRequestStock(this.accessToken, target)));
    } catch {
      this.error.set('Le stock n’a pas pu être vérifié. Vous pouvez toujours ajouter la demande.');
    } finally {
      this.loadingStock.set(false);
    }
  }

  private isDuplicateError(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse)) {
      return false;
    }
    const body = error.error as Record<string, unknown> | null;
    const code = body?.['code'] ?? (body?.['error'] as Record<string, unknown> | undefined)?.['code'];
    return code === 'ManualRequest.DuplicateRequest' || JSON.stringify(error.error ?? '').includes('ManualRequest.DuplicateRequest');
  }

  private formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? 'date inconnue'
      : new Intl.DateTimeFormat('fr-FR', {day: 'numeric', month: 'long'}).format(date);
  }
}
