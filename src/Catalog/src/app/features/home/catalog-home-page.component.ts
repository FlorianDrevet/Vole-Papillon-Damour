import {ChangeDetectionStrategy, Component, OnInit, signal} from '@angular/core';
import {Router} from '@angular/router';
import {catchError, forkJoin, of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook, CatalogFair, CatalogSearchResponse} from '../../core/catalog.models';
import {calendarDataUri, calendarFilename} from '../../core/layouts/catalog-calendar';

const EMPTY_SEARCH: CatalogSearchResponse = {
  generatedAt: '',
  books: [],
  totalCount: 0,
  page: 1,
  pageSize: 4,
  genres: [],
};

@Component({
  selector: 'app-catalog-home-page',
  standalone: false,
  templateUrl: './catalog-home-page.component.html',
  styleUrls: ['./catalog-home-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogHomePageComponent implements OnInit {
  search = '';
  heroGenre = '';
  loading = signal(true);
  hasLoadError = signal(false);
  recent = signal<CatalogBook[]>([]);
  recentTotal = signal(0);
  rare = signal<CatalogBook[]>([]);
  genres = signal<string[]>([]);
  nextFair = signal<CatalogFair | null>(null);

  constructor(
    private readonly api: CatalogApiService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    forkJoin({
      recent: this.api.search({sort: 'recent', pageSize: 4}).pipe(catchError(() => {
        this.hasLoadError.set(true);
        return of(EMPTY_SEARCH);
      })),
      rare: this.api.search({rareOnly: true, sort: 'recent', pageSize: 4}).pipe(catchError(() => {
        this.hasLoadError.set(true);
        return of(EMPTY_SEARCH);
      })),
      fair: this.api.getNextFair().pipe(catchError(() => of(null))),
    }).subscribe(({recent, rare, fair}) => {
      this.recent.set(recent.books);
      this.recentTotal.set(recent.totalCount);
      this.rare.set(rare.books);
      this.genres.set(recent.genres);
      this.nextFair.set(fair);
      this.loading.set(false);
    });
  }

  submitSearch(): void {
    const query = this.search.trim();
    const genre = this.heroGenre.trim();
    void this.router.navigate(['/recherche'], {
      queryParams: {
        ...(query ? {q: query} : {}),
        ...(genre ? {genre} : {}),
      },
    });
  }

  showAll(): void {
    void this.router.navigate(['/catalogue']);
  }

  showRare(): void {
    void this.router.navigate(['/recherche'], {queryParams: {rare: true}});
  }

  trackBook(_index: number, book: CatalogBook): string {
    return book.isbn13;
  }

  formatDate(value: string, withYear = true): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
      ...(withYear ? {year: 'numeric'} : {}),
    }).format(new Date(value));
  }

  formatFairDateRange(fair: CatalogFair): string {
    const start = this.formatDate(fair.dateStart);
    if (!fair.dateEnd) {
      return start;
    }

    const end = this.formatDate(fair.dateEnd);
    return start === end ? start : `Du ${start} au ${end}`;
  }

  formatTime(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      hour: 'numeric',
      minute: '2-digit',
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace(':', ' h ');
  }

  address(fair: CatalogFair): string {
    const street = [fair.roadNumber, fair.road].filter(Boolean).join(' ');
    return [street, fair.city].filter(Boolean).join(', ');
  }

  calendarLink(fair: CatalogFair): string {
    return calendarDataUri({...fair, location: this.address(fair)});
  }

  calendarFileName(fair: CatalogFair): string {
    return calendarFilename(fair.name);
  }
}
