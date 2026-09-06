import {ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {ActivatedRoute, Router} from '@angular/router';
import {Subject, catchError, firstValueFrom, of, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {
  CatalogAvailability,
  CatalogBookReference,
  CatalogSearchParams,
  CatalogSearchResponse,
  CatalogSort,
  CatalogReferenceSearchResponse,
} from '../../core/catalog.models';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';

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
  externalLoading = false;
  externalError = false;
  externalResponse: CatalogReferenceSearchResponse | null = null;
  referenceFollowPending: string | null = null;
  referenceFollowMessage: string | null = null;
  referenceFollowError: string | null = null;

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: CatalogApiService,
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
    private readonly changeDetector: ChangeDetectorRef,
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

  goToExternalPage(page: number): void {
    if (page < 1 || !this.externalResponse || page < 1) {
      return;
    }

    void this.router.navigate([this.browseMode ? '/catalogue' : '/recherche'], {
      queryParams: {...this.queryParams(), referencePage: page},
    });
  }

  async followReference(item: CatalogBookReference): Promise<void> {
    const key = item.workId || item.isbn13;
    if (!key || this.referenceFollowPending) {
      return;
    }

    this.referenceFollowMessage = null;
    this.referenceFollowError = null;

    if (!this.auth.isAuthenticated()) {
      try {
        await this.auth.login(this.referenceReturnUrl());
      } catch {
        this.referenceFollowError = 'La connexion n’a pas pu être démarrée. Réessayez.';
      }
      return;
    }

    this.referenceFollowPending = key;
    try {
      const token = await this.auth.getApiAccessToken();
      const request = item.workId
        ? {scope: 'Work' as const, workId: item.workId, isbn13: null}
        : {scope: 'Edition' as const, workId: null, isbn13: item.isbn13};
      await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
      this.referenceFollowMessage = 'Le titre a été ajouté à votre liste de suivi.';
    } catch (error: unknown) {
      this.referenceFollowError = error instanceof HttpErrorResponse && error.status === 409
        ? 'Ce titre est déjà présent dans votre liste, ou votre liste est pleine.'
        : 'Le titre n’a pas pu être ajouté. Réessayez dans un instant.';
    } finally {
      this.referenceFollowPending = null;
      this.changeDetector.markForCheck();
    }
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
        this.changeDetector.markForCheck();
      });

    const referenceQuery = this.query.trim();
    if (referenceQuery.length < 2) {
      this.externalResponse = null;
      this.externalLoading = false;
      this.externalError = false;
      return;
    }

    this.externalLoading = true;
    this.externalError = false;
    this.api.searchReferences(referenceQuery, this.readReferencePage(), 20)
      .pipe(
        catchError(() => {
          this.externalError = true;
          return of(null);
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(response => {
        this.externalResponse = response;
        this.externalLoading = false;
        this.changeDetector.markForCheck();
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
    if (this.readReferencePage() > 1) params['referencePage'] = this.readReferencePage();
    return params;
  }

  private readPage(): number {
    const page = Number(this.route.snapshot.queryParamMap.get('page') || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private readAvailability(value: string | null): CatalogAvailability {
    return value === 'available' || value === 'next' ? value : 'all';
  }

  private readReferencePage(): number {
    const page = Number(this.route.snapshot.queryParamMap.get('referencePage') || '1');
    return Number.isInteger(page) && page > 0 ? page : 1;
  }

  private referenceReturnUrl(): string {
    const query = this.queryParams();
    const search = new URLSearchParams();
    for (const [key, value] of Object.entries(query)) {
      search.set(key, String(value));
    }
    return `/recherche${search.toString() ? `?${search.toString()}` : ''}`;
  }
}
