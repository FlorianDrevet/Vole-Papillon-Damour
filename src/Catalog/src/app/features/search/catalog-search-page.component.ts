import {ChangeDetectionStrategy, Component, OnDestroy, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, catchError, of, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {
  CatalogAvailability,
  CatalogSearchParams,
  CatalogSearchResponse,
  CatalogSort,
} from '../../core/catalog.models';

@Component({
  selector: 'app-catalog-search-page',
  standalone: false,
  templateUrl: './catalog-search-page.component.html',
  styleUrls: ['./catalog-search-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogSearchPageComponent implements OnInit, OnDestroy {
  query = '';
  genre = '';
  availability: CatalogAvailability = 'all';
  rareOnly = false;
  sort: CatalogSort = 'relevance';
  browseMode = false;
  loading = true;
  error = false;
  response: CatalogSearchResponse | null = null;

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: CatalogApiService,
  ) {}

  ngOnInit(): void {
    this.browseMode = this.route.snapshot.data['browse'] === true;
    this.route.queryParamMap
      .pipe(takeUntil(this.destroyed))
      .subscribe(params => {
        this.query = params.get('q') || '';
        this.genre = params.get('genre') || '';
        this.availability = this.readAvailability(params.get('availability'));
        this.rareOnly = params.get('rare') === 'true';
        this.sort = params.get('sort') === 'recent' ? 'recent' : 'relevance';
        this.load();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  submitSearch(): void {
    void this.router.navigate(['/recherche'], {queryParams: this.queryParams()});
  }

  applyFilters(): void {
    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: this.queryParams(),
    });
  }

  clearFilters(): void {
    this.query = '';
    this.genre = '';
    this.availability = 'all';
    this.rareOnly = false;
    this.sort = 'relevance';
    this.applyFilters();
  }

  goToPage(page: number): void {
    if (page < 1 || !this.response || page > this.totalPages()) {
      return;
    }

    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: {...this.queryParams(), page},
    });
  }

  totalPages(): number {
    if (!this.response || this.response.totalCount === 0) {
      return 1;
    }
    return Math.ceil(this.response.totalCount / this.response.pageSize);
  }

  pageNumbers(): number[] {
    const total = this.totalPages();
    const current = this.response?.page || 1;
    const first = Math.max(1, Math.min(current - 2, total - 4));
    const last = Math.min(total, first + 4);
    return Array.from({length: last - first + 1}, (_, index) => first + index);
  }

  resultSummary(): string {
    const total = this.response?.totalCount || 0;
    if (total === 0) {
      return 'Aucun titre trouvé';
    }
    return `${total} ${total === 1 ? 'titre trouvé' : 'titres trouvés'}`;
  }

  trackBook(_index: number, isbn13: string): string {
    return isbn13;
  }

  private load(): void {
    this.loading = true;
    this.error = false;
    this.api.search(this.searchParams())
      .pipe(
        catchError(() => {
          this.error = true;
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        this.response = response;
        this.loading = false;
      });
  }

  private searchParams(): CatalogSearchParams {
    return {
      query: this.query,
      genre: this.genre,
      availability: this.availability,
      rareOnly: this.rareOnly,
      sort: this.sort,
      page: this.readPage(),
      pageSize: 24,
    };
  }

  private queryParams(): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {};
    if (this.query.trim()) params['q'] = this.query.trim();
    if (this.genre.trim()) params['genre'] = this.genre.trim();
    if (this.availability !== 'all') params['availability'] = this.availability;
    if (this.rareOnly) params['rare'] = true;
    if (this.sort !== 'relevance') params['sort'] = this.sort;
    return params;
  }

  private readPage(): number {
    const page = Number(this.route.snapshot.queryParamMap.get('page') || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private readAvailability(value: string | null): CatalogAvailability {
    return value === 'available' || value === 'next' ? value : 'all';
  }
}
