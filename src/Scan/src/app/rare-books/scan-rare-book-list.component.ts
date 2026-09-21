import {Component, OnInit} from '@angular/core';
import {Router} from '@angular/router';

import {ScanRareBook} from '../offline/scan-offline.model';
import {ScanRareBookService} from './scan-rare-book.service';

@Component({
  selector: 'app-scan-rare-book-list',
  templateUrl: './scan-rare-book-list.component.html',
  styleUrls: ['./scan-rare-book-list.component.scss'],
  standalone: false,
})
export class ScanRareBookListComponent implements OnInit {
  books: ScanRareBook[] = [];
  search = '';
  pendingPhotoCount = 0;
  loading = true;
  error: string | null = null;

  constructor(
    private readonly rareBooks: ScanRareBookService,
    private readonly router: Router,
  ) {}

  get filteredBooks(): ScanRareBook[] {
    const query = this.search.trim().toLocaleLowerCase('fr-FR');
    if (!query) {
      return this.books;
    }

    return this.books.filter(book => [
      book.title,
      book.authorMention ?? '',
      book.isbn13 ?? '',
      book.shelf,
    ].some(value => value.toLocaleLowerCase('fr-FR').includes(query)));
  }

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  async reload(): Promise<void> {
    this.loading = true;
    this.error = null;
    try {
      this.books = await this.rareBooks.list();
      if (typeof navigator === 'undefined' || navigator.onLine) {
        this.books = await this.rareBooks.refreshFromServer();
      }
      this.pendingPhotoCount = await this.rareBooks.pendingPhotoCount();
    } catch {
      this.error = 'Impossible de charger les fiches rares. Vérifiez votre connexion et réessayez.';
    } finally {
      this.loading = false;
    }
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
    return book.status === 'Published' ? 'Publié' : 'Brouillon';
  }
}
