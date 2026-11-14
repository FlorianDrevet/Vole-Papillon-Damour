import {isPlatformBrowser} from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  PLATFORM_ID,
  SimpleChanges,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogFavoritesService} from '../../../core/favorites/catalog-favorites.service';
import {
  CatalogBook,
  CatalogBookReference,
  CatalogIsbndbEditionSearchItem,
} from '../../../core/catalog.models';
import {LocalFavoriteEntry} from '../../../core/favorites/local-favorites.store';

interface FavoritePanelResult {
  isbn13: string;
  title: string;
  authors: string | null;
  coverUrl: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  quantityAvailable: number;
  quantityAnnounced: number;
}

@Component({
  selector: 'app-add-favorite-panel',
  standalone: false,
  templateUrl: './add-favorite-panel.component.html',
  styleUrls: ['./add-favorite-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddFavoritePanelComponent implements OnChanges, OnDestroy {
  @Input() open = false;
  @Input() initialQuery = '';
  @Output() readonly closed = new EventEmitter<void>();

  @ViewChild('dialog') private dialog?: ElementRef<HTMLElement>;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  readonly catalogResults = signal<FavoritePanelResult[]>([]);
  readonly referenceResults = signal<FavoritePanelResult[]>([]);
  readonly isbndbResults = signal<FavoritePanelResult[]>([]);
  readonly addedIsbns = signal<ReadonlySet<string>>(new Set());
  readonly removedIsbns = signal<ReadonlySet<string>>(new Set());
  readonly allReferenceResults = computed(() => [
    ...this.referenceResults(),
    ...this.isbndbResults(),
  ]);
  readonly searching = signal(false);
  readonly searchPerformed = signal(false);
  readonly isbndbLoading = signal(false);
  readonly isbndbUnavailable = signal(false);
  readonly listLimitReached = signal(false);
  readonly additionUnavailable = signal(false);
  readonly searchError = signal(false);

  query = '';

  private readonly platformId = inject(PLATFORM_ID);
  private priorFocus: HTMLElement | null = null;
  private addedBodyScrollLock = false;
  private addedHtmlScrollLock = false;
  private searchVersion = 0;

  constructor(
    private readonly api: CatalogApiService,
    readonly favorites: CatalogFavoritesService,
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['open'] || !isPlatformBrowser(this.platformId)) {
      return;
    }

    if (this.open) {
      this.priorFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      this.addedBodyScrollLock = !document.body.classList.contains('no-scroll');
      this.addedHtmlScrollLock = !document.documentElement.classList.contains('no-scroll');
      document.body.classList.add('no-scroll');
      document.documentElement.classList.add('no-scroll');
      this.query = this.initialQuery;
      queueMicrotask(() => this.searchInput?.nativeElement.focus());
    } else {
      this.releaseScrollLock();
      this.priorFocus?.focus();
      this.priorFocus = null;
    }
  }

  ngOnDestroy(): void {
    this.releaseScrollLock();
  }

  onQueryInput(event: Event): void {
    this.query = (event.target as HTMLInputElement).value;
  }

  onSearchSubmit(event: SubmitEvent): void {
    event.preventDefault();
    void this.search();
  }

  async search(): Promise<void> {
    const query = this.query.trim();
    if (query.length < 2 || this.searching()) {
      return;
    }

    const version = ++this.searchVersion;
    this.searching.set(true);
    this.searchPerformed.set(false);
    this.searchError.set(false);
    this.listLimitReached.set(false);
    this.additionUnavailable.set(false);
    this.isbndbResults.set([]);
    this.isbndbUnavailable.set(false);

    try {
      const [catalog, references] = await Promise.all([
        firstValueFrom(this.api.search({
          query,
          availability: 'all',
          includeExhausted: true,
          pageSize: 20,
          sort: 'relevance',
        })),
        firstValueFrom(this.api.searchReferences(query, 1, 20)),
      ]);
      if (version !== this.searchVersion) {
        return;
      }

      const frenchCatalogBooks = catalog.books.filter(book => isFrenchLanguage(book.language));
      const catalogIsbns = new Set(frenchCatalogBooks.map(book => book.isbn13));
      this.catalogResults.set(frenchCatalogBooks.map(mapCatalogBook));
      this.referenceResults.set(references.items
        .filter((reference): reference is CatalogBookReference & {isbn13: string} =>
          Boolean(reference.isbn13) &&
          !catalogIsbns.has(reference.isbn13!) &&
          isFrenchLanguage(reference.language),
        )
        .map(mapReference));
      this.searchPerformed.set(true);
    } catch {
      if (version === this.searchVersion) {
        this.catalogResults.set([]);
        this.referenceResults.set([]);
        this.searchError.set(true);
        this.searchPerformed.set(true);
      }
    } finally {
      if (version === this.searchVersion) {
        this.searching.set(false);
      }
    }
  }

  async searchOtherEditions(): Promise<void> {
    const query = this.query.trim();
    if (query.length < 3 || this.isbndbLoading()) {
      return;
    }

    this.isbndbLoading.set(true);
    this.isbndbUnavailable.set(false);
    try {
      const response = await firstValueFrom(this.api.searchIsbndbEditions(query));
      const existingIsbns = new Set([
        ...this.catalogResults().map(result => result.isbn13),
        ...this.referenceResults().map(result => result.isbn13),
      ]);
      this.isbndbResults.set(response.items
        .filter(item => !existingIsbns.has(item.isbn13))
        .map(mapIsbndbEdition));
      this.isbndbUnavailable.set(Boolean(response.unavailable));
    } catch {
      this.isbndbUnavailable.set(true);
      this.isbndbResults.set([]);
    } finally {
      this.isbndbLoading.set(false);
    }
  }

  isAdded(isbn13: string): boolean {
    if (this.removedIsbns().has(isbn13)) {
      return false;
    }
    return this.addedIsbns().has(isbn13) || this.favorites.favoriteIsbns().has(isbn13);
  }

  favoriteActionLabel(book: FavoritePanelResult): string {
    return this.isAdded(book.isbn13)
      ? `Retirer ${book.title} de mes coups de cœur`
      : `Ajouter ${book.title} à mes coups de cœur`;
  }

  async toggleFavorite(book: FavoritePanelResult): Promise<void> {
    this.listLimitReached.set(false);
    this.additionUnavailable.set(false);
    if (this.isAdded(book.isbn13)) {
      try {
        await this.favorites.remove(book.isbn13);
        this.removedIsbns.update(isbns => new Set(isbns).add(book.isbn13));
        this.addedIsbns.update(isbns => {
          const next = new Set(isbns);
          next.delete(book.isbn13);
          return next;
        });
      } catch {
        this.additionUnavailable.set(true);
      }
      return;
    }

    const limit = this.favorites.mode() === 'remote' ? 200 : 50;
    if (this.favorites.favorites().length >= limit) {
      this.listLimitReached.set(true);
      return;
    }

    try {
      const added = await this.favorites.add({
        isbn13: book.isbn13,
        title: book.title,
        authors: book.authors,
        coverUrl: book.coverUrl,
      } satisfies Omit<LocalFavoriteEntry, 'addedAt'>);
      if (!added) {
        this.listLimitReached.set(this.favorites.favorites().length >= limit);
        this.additionUnavailable.set(!this.listLimitReached());
        return;
      }
      this.addedIsbns.update(isbns => new Set(isbns).add(book.isbn13));
      this.removedIsbns.update(isbns => {
        const next = new Set(isbns);
        next.delete(book.isbn13);
        return next;
      });
    } catch {
      this.additionUnavailable.set(true);
    }
  }

  availabilityLabel(book: FavoritePanelResult): string {
    if (book.quantityAvailable > 0) {
      return `${book.quantityAvailable} dispo.`;
    }
    if (book.quantityAnnounced > 0) {
      return `${book.quantityAnnounced} annoncés`;
    }
    return '';
  }

  close(): void {
    this.closed.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close();
    }
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (!this.open || !this.dialog) {
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key !== 'Tab') {
      return;
    }

    const focusable = this.dialog.nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]), input:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])',
    );
    if (focusable.length === 0) {
      event.preventDefault();
      this.dialog.nativeElement.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;
    if (event.shiftKey && (active === first || !this.dialog.nativeElement.contains(active))) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !this.dialog.nativeElement.contains(active))) {
      event.preventDefault();
      first.focus();
    }
  }

  private releaseScrollLock(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    if (this.addedBodyScrollLock) {
      document.body.classList.remove('no-scroll');
    }
    if (this.addedHtmlScrollLock) {
      document.documentElement.classList.remove('no-scroll');
    }
    this.addedBodyScrollLock = false;
    this.addedHtmlScrollLock = false;
  }
}

function mapCatalogBook(book: CatalogBook): FavoritePanelResult {
  return {
    isbn13: book.isbn13,
    title: book.title ?? book.isbn13,
    authors: book.authors,
    coverUrl: book.coverUrl,
    publisher: book.publisher,
    publicationYear: book.publicationYear,
    physicalFormat: book.physicalFormat,
    quantityAvailable: book.quantityAvailable,
    quantityAnnounced: book.quantityAnnounced,
  };
}

function mapReference(reference: CatalogBookReference & {isbn13: string}): FavoritePanelResult {
  return {
    isbn13: reference.isbn13,
    title: reference.title ?? reference.isbn13,
    authors: reference.authors,
    coverUrl: reference.coverUrl,
    publisher: reference.publisher,
    publicationYear: reference.publicationYear,
    physicalFormat: null,
    quantityAvailable: 0,
    quantityAnnounced: 0,
  };
}

function mapIsbndbEdition(edition: CatalogIsbndbEditionSearchItem): FavoritePanelResult {
  return {
    isbn13: edition.isbn13,
    title: edition.title ?? edition.isbn13,
    authors: edition.authors,
    coverUrl: edition.coverUrl,
    publisher: edition.publisher,
    publicationYear: edition.publicationYear,
    physicalFormat: edition.binding,
    quantityAvailable: 0,
    quantityAnnounced: 0,
  };
}

function isFrenchLanguage(language: string | null | undefined): boolean {
  return (language ?? '').split(/[,;|]/).some(value => {
    const normalized = value.trim().toLowerCase().replace(/^\/languages\//, '');
    return normalized === 'fre' ||
      normalized === 'fra' ||
      normalized === 'french' ||
      normalized === 'français' ||
      normalized === 'francais' ||
      /^fr(?:[-_]|$)/.test(normalized);
  });
}
