import {ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, catchError, of, switchMap, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {
  CatalogFair,
  CatalogRareBook,
  CatalogRareBookPage,
  CatalogRareBookFilters,
} from '../../core/catalog.models';

const EMPTY_PAGE: CatalogRareBookPage = {
  generatedAt: '',
  books: [],
  totalCount: 0,
  page: 1,
  pageSize: 24,
};

@Component({
  selector: 'app-catalog-rare-books-page',
  standalone: false,
  templateUrl: './catalog-rare-books-page.component.html',
  styleUrls: ['./catalog-rare-books-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogRareBooksPageComponent implements OnInit, OnDestroy {
  books: CatalogRareBook[] = [];
  nextFair: CatalogFair | null = null;
  includeSold = true;
  sort: CatalogRareBookFilters['sort'] = 'price-desc';
  page = 1;
  pageSize = 24;
  totalCount = 0;
  loading = true;
  error = false;

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly router: Router,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.api.getUpcomingFairs()
      .pipe(
        catchError(() => of([] as CatalogFair[])),
        takeUntil(this.destroyed),
      )
      .subscribe(fairs => {
        this.nextFair = fairs[0] ?? null;
        this.changeDetector.markForCheck();
      });

    this.route.queryParamMap
      .pipe(
        switchMap(params => {
          this.includeSold = params.get('includeSold') !== 'false';
          this.sort = parseSort(params.get('sort'));
          this.page = parsePage(params.get('page'));
          this.loading = true;
          this.error = false;
          return this.api.getPublicRareBooks(this.filters()).pipe(
            catchError(() => {
              this.error = true;
              return of(EMPTY_PAGE);
            }),
          );
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(page => {
        this.applyPage(page);
        this.changeDetector.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  applyFilters(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        ...(!this.includeSold ? {includeSold: false} : {}),
        ...(this.sort !== 'recent' ? {sort: this.sort} : {}),
        ...(this.page > 1 ? {page: this.page} : {}),
      },
    });
  }

  nextPage(): void {
    if (this.page < this.pageCount()) {
      this.page += 1;
      this.applyFilters();
    }
  }

  previousPage(): void {
    if (this.page > 1) {
      this.page -= 1;
      this.applyFilters();
    }
  }

  pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  trackBook(_index: number, book: CatalogRareBook): string {
    return book.id;
  }

  formatFairDateRange(fair: CatalogFair): string {
    const start = this.formatFairDate(fair.dateStart);
    if (!fair.dateEnd) {
      return start;
    }

    const end = this.formatFairDate(fair.dateEnd);
    return start === end ? start : `Du ${start} au ${end}`;
  }

  formatFairTime(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      hour: 'numeric',
      minute: '2-digit',
      timeZone: 'UTC',
    }).format(new Date(value)).replace(':', ' h ');
  }

  fairAddress(fair: CatalogFair): string {
    const street = [fair.roadNumber, fair.road].filter(Boolean).join(' ');
    return [street, [fair.cityCode, fair.city].filter(Boolean).join(' ')].filter(Boolean).join(', ');
  }

  private filters(): CatalogRareBookFilters {
    return {
      includeSold: this.includeSold,
      sort: this.sort,
      page: this.page,
      pageSize: this.pageSize,
    };
  }

  private applyPage(page: CatalogRareBookPage): void {
    this.books = page.books;
    this.totalCount = page.totalCount;
    this.pageSize = page.pageSize;
    this.loading = false;
  }

  private formatFairDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }
}

function parsePage(value: string | null): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

function parseSort(value: string | null): CatalogRareBookFilters['sort'] {
  return value === 'price-asc' || value === 'price-desc' || value === 'recent'
    ? value
    : 'price-desc';
}
