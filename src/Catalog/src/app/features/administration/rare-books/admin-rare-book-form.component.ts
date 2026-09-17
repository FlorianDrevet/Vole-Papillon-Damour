import {HttpErrorResponse} from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  signal,
} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogBookReference, CatalogAdminRareBook} from '../../../core/catalog.models';
import {
  CatalogAdminRareBookFormValue,
  EMPTY_RARE_BOOK_FORM,
} from './admin-rare-books.facade';

@Component({
  selector: 'app-admin-rare-book-form',
  standalone: false,
  templateUrl: './admin-rare-book-form.component.html',
  styleUrls: ['./admin-rare-book-form.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminRareBookFormComponent implements OnChanges {
  @Input() book: CatalogAdminRareBook | null = null;
  @Input() initialValue: CatalogAdminRareBookFormValue | null = null;
  @Input() disabled = false;
  @Output() submitted = new EventEmitter<{
    value: CatalogAdminRareBookFormValue;
    rowVersion: string | null;
  }>();
  @Output() cancelled = new EventEmitter<void>();

  readonly shelves = [
    'Éditions anciennes',
    'Illustrés',
    'Beaux-arts',
    'Régionalisme',
  ];
  readonly conditions = [
    {value: 'AsNew' as const, label: 'Comme neuf'},
    {value: 'GoodWithFlaws' as const, label: 'Bon, défauts signalés'},
    {value: 'Worn' as const, label: 'Usé'},
    {value: 'Damaged' as const, label: 'Abîmé'},
  ];
  readonly validationError = signal<string | null>(null);
  readonly referenceResults = signal<CatalogBookReference[]>([]);
  readonly referenceLoading = signal(false);
  readonly referenceError = signal<string | null>(null);
  readonly rowVersion = signal<string | null>(null);

  form: CatalogAdminRareBookFormValue = {...EMPTY_RARE_BOOK_FORM};
  referenceQuery = '';
  isbnQuery = '';

  constructor(private readonly catalogApi: CatalogApiService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['book'] || changes['initialValue']) {
      this.hydrateForm();
    }
  }

  isEditing(): boolean {
    return this.book !== null;
  }

  descriptionLength(): number {
    return this.form.publicDescription.length;
  }

  async searchReferences(): Promise<void> {
    const query = (this.isbnQuery.trim() || this.referenceQuery.trim());
    if (query.length < 2) {
      this.referenceError.set('La recherche doit contenir au moins deux caractères.');
      this.referenceResults.set([]);
      return;
    }

    this.referenceLoading.set(true);
    this.referenceError.set(null);
    try {
      const response = await firstValueFrom(this.catalogApi.searchReferences(query, 1, 20));
      this.referenceResults.set(response.items);
      if (response.items.length === 0) {
        this.referenceError.set('Aucune notice bibliographique ne correspond à cette recherche.');
      }
    } catch (error: unknown) {
      this.referenceError.set(this.describeReferenceError(error));
      this.referenceResults.set([]);
    } finally {
      this.referenceLoading.set(false);
    }
  }

  useReference(reference: CatalogBookReference): void {
    this.form = {
      ...this.form,
      title: reference.title ?? this.form.title,
      authorMention: reference.authors ?? this.form.authorMention,
      publisher: reference.publisher ?? this.form.publisher,
      publicationYear: reference.publicationYear ?? this.form.publicationYear,
      isbn13: reference.isbn13 ?? this.form.isbn13,
    };
    this.referenceError.set(null);
  }

  submit(): void {
    const title = this.form.title.trim();
    const shelf = this.form.shelf.trim();
    const price = Number(this.form.price ?? 0);
    if (!title) {
      this.validationError.set('Le titre est obligatoire.');
      return;
    }
    if (!shelf) {
      this.validationError.set('Le rayon d’affichage est obligatoire.');
      return;
    }
    if (!Number.isFinite(price) || price < 0) {
      this.validationError.set('Le prix doit être un nombre positif ou nul.');
      return;
    }

    this.form = {
      ...this.form,
      title,
      shelf,
      authorMention: this.form.authorMention.trim(),
      publisher: this.form.publisher.trim(),
      publicDescription: this.form.publicDescription.trim(),
      binding: this.form.binding.trim(),
      dimensions: this.form.dimensions.trim(),
      shelfLocation: this.form.shelfLocation.trim(),
      priceSetBy: this.form.priceSetBy.trim(),
      isbn13: this.form.isbn13.trim(),
      price,
    };
    this.validationError.set(null);
    this.submitted.emit({value: this.form, rowVersion: this.rowVersion()});
  }

  setCondition(condition: CatalogAdminRareBookFormValue['condition']): void {
    this.form = {...this.form, condition};
  }

  cancel(): void {
    this.cancelled.emit();
  }

  private hydrateForm(): void {
    this.validationError.set(null);
    this.referenceError.set(null);
    this.referenceResults.set([]);
    if (this.book) {
      this.form = {
        title: this.book.title,
        authorMention: this.book.authorMention ?? '',
        publisher: this.book.publisher ?? '',
        publicationYear: this.book.publicationYear,
        shelf: this.book.shelf,
        price: this.book.price,
        condition: this.book.condition,
        publicDescription: this.book.publicDescription ?? '',
        binding: this.book.binding ?? '',
        dimensions: this.book.dimensions ?? '',
        pageCount: this.book.pageCount,
        shelfLocation: this.book.shelfLocation ?? '',
        priceSetBy: this.book.priceSetBy ?? '',
        isbn13: this.book.isbn13 ?? '',
      };
      this.rowVersion.set(this.book.rowVersion);
      return;
    }

    this.form = {...EMPTY_RARE_BOOK_FORM, ...(this.initialValue ?? {})};
    this.rowVersion.set(null);
  }

  private describeReferenceError(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
      return 'Le référentiel externe est momentanément indisponible. Réessayez dans un instant.';
    }
    return 'La recherche bibliographique n’a pas pu aboutir. Réessayez dans un instant.';
  }
}
