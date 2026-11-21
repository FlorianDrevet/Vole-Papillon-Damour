import {Component, OnDestroy, OnInit, ChangeDetectionStrategy} from '@angular/core';
import {Router} from '@angular/router';

import {ScanRareBook} from '../offline/scan-offline.model';
import {ScanRareBookService} from './scan-rare-book.service';
import {rareBookLabelCode} from './rare-book-label';

type RareBookListFilter = 'all' | 'drafts';

@Component({
  selector: 'app-scan-rare-book-list',
  templateUrl: './scan-rare-book-list.component.html',
  styleUrls: ['./scan-rare-book-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ScanRareBookListComponent implements OnInit, OnDestroy {
  static readonly pageSize = 24;

  books: ScanRareBook[] = [];
  search = '';
  listFilter: RareBookListFilter = 'all';
  pendingPhotoCount = 0;
  totalCount = 0;
  page = 1;
  offline = false;
  loading = true;
  error: string | null = null;
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;
  private loadToken = 0;

  constructor(
    private readonly rareBooks: ScanRareBookService,
    private readonly router: Router,
  ) {}

  get filteredBooks(): ScanRareBook[] {
    return this.books;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / ScanRareBookListComponent.pageSize));
  }

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  ngOnDestroy(): void {
    if (this.searchDebounce !== null) {
      clearTimeout(this.searchDebounce);
    }
  }

  onSearchChange(value: string): void {
    this.search = value;
    this.page = 1;
    if (this.searchDebounce !== null) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      this.searchDebounce = null;
      void this.reload();
    }, 250);
  }

  setFilter(filter: RareBookListFilter): void {
    if (this.listFilter === filter) {
      return;
    }
    this.listFilter = filter;
    this.page = 1;
    void this.reload();
  }

  changePage(page: number): void {
    if (page < 1 || page > this.pageCount || page === this.page) {
      return;
    }
    this.page = page;
    void this.reload();
  }

  async reload(): Promise<void> {
    const requestToken = ++this.loadToken;
    this.loading = true;
    this.error = null;
    this.offline = typeof navigator !== 'undefined' && !navigator.onLine;
    try {
      const localBooks = await this.rareBooks.list();
      if (requestToken !== this.loadToken) {
        return;
      }

      if (!this.offline) {
        try {
          const result = await this.rareBooks.refreshFromServer({
            search: this.search.trim() || undefined,
            status: this.listFilter === 'drafts' ? 'Draft' : undefined,
            page: this.page,
            pageSize: ScanRareBookListComponent.pageSize,
          });
          if (requestToken !== this.loadToken) {
            return;
          }

          const pendingBooks = localBooks.filter(book =>
            book.syncStatus !== 'Synced' && this.matchesCurrentFilter(book));
          this.books = this.page === 1
            ? this.withPendingBooks(result.books, pendingBooks)
            : result.books;
          this.totalCount = result.totalCount + pendingBooks.filter(book => !book.serverId).length;

          if (this.page > this.pageCount) {
            this.page = this.pageCount;
            void this.reload();
            return;
          }
        } catch {
          if (requestToken !== this.loadToken) {
            return;
          }
          this.error = 'La liste locale reste disponible. La synchronisation a échoué ; réessayez quand la connexion sera rétablie.';
          this.showCachedPage(localBooks);
        }
      } else {
        this.showCachedPage(localBooks);
      }
      this.pendingPhotoCount = await this.rareBooks.pendingPhotoCount();
    } catch {
      if (requestToken === this.loadToken) {
        this.error = 'Impossible de lire les fiches rares sur cet appareil. Vérifiez le stockage local et réessayez.';
        this.books = [];
        this.totalCount = 0;
      }
    } finally {
      if (requestToken === this.loadToken) {
        this.loading = false;
      }
    }
  }

  private showCachedPage(localBooks: ScanRareBook[]): void {
    const matchingBooks = localBooks.filter(book => this.matchesCurrentFilter(book));
    this.totalCount = matchingBooks.length;
    const start = (this.page - 1) * ScanRareBookListComponent.pageSize;
    this.books = matchingBooks.slice(start, start + ScanRareBookListComponent.pageSize);
    if (this.page > this.pageCount) {
      this.page = this.pageCount;
      this.showCachedPage(localBooks);
    }
  }

  private matchesCurrentFilter(book: ScanRareBook): boolean {
    if (this.listFilter === 'drafts' && book.status !== 'Draft') {
      return false;
    }
    const query = this.search.trim().toLocaleLowerCase('fr-FR');
    return !query || [
      book.title,
      book.authorMention ?? '',
      book.publisher ?? '',
      book.isbn13 ?? '',
      book.internalCode ?? '',
    ].some(value => value.toLocaleLowerCase('fr-FR').includes(query));
  }

  private withPendingBooks(serverBooks: ScanRareBook[], pendingBooks: ScanRareBook[]): ScanRareBook[] {
    const serverIds = new Set(serverBooks.map(book => book.serverId));
    return [
      ...pendingBooks.filter(book => !book.serverId || !serverIds.has(book.serverId)),
      ...serverBooks,
    ];
  }

  openNew(): void {
    void this.router.navigate(['/livres-rares/nouveau']);
  }

  open(book: ScanRareBook): void {
    void this.router.navigate(['/livres-rares', book.clientId]);
  }

  statusLabel(book: ScanRareBook): string {
    if (book.isSold) {
      return 'Parti';
    }
    return book.status === 'Published' ? 'Photo publiée' : 'Photo brouillon';
  }

  // E5 : impression de l'étiquette d'une fiche rare, avec son prix ferme (RG-179).
  printLabel(book: ScanRareBook): void {
    const code = rareBookLabelCode(book);
    if (!code) {
      return;
    }

    const queryParams = book.price > 0 ? {code, price: `${book.price.toFixed(2)} €`} : {code};
    void this.router.navigate(['/etiquette'], {queryParams});
  }

  priceLabel(book: ScanRareBook): string {
    return book.price > 0 ? `${book.price.toLocaleString('fr-FR')} €` : 'prix à fixer';
  }

  photoLabel(book: ScanRareBook): string {
    return book.thumbnail ? 'photo ajoutée' : 'photo à faire';
  }
}
